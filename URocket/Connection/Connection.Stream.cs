using System.Runtime.CompilerServices;
using URocket.Utils;
using URocket.Utils.Memory;

namespace URocket.Connection;

public sealed partial class Connection
{
    // -----------------------------
    // Stream read cursor state
    // -----------------------------
    private long _streamSnap;           // current batch boundary (TailSnapshot)
    private int _streamHasSnap;         // 0/1
    private RingItem _streamItem;       // current segment
    private int _streamItemOffset;      // offset into current segment
    private int _streamHasItem;         // 0/1

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    
    public override void SetLength(long value) => throw new NotSupportedException();
    
    public override void Write(byte[] buffer, int offset, int count)
    {
        if (buffer is null) throw new ArgumentNullException(nameof(buffer));
        if ((uint)offset > (uint)buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
        if ((uint)count > (uint)(buffer.Length - offset)) throw new ArgumentOutOfRangeException(nameof(count));
        if (count == 0) return;

        // Append into the unmanaged write slab
        InnerWrite(buffer.AsSpan(offset, count));
    }
    
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Write(buffer.Span);          // no implicit flush
        return ValueTask.CompletedTask;
    }

    // -----------------------------
    // Synchronous Read
    // -----------------------------
    public override void Flush()
    {
        // Stream.Flush is sync; your pipeline is reactor-driven async.
        // Blocking here is the standard bridge.
        InnerFlushAsync().GetAwaiter().GetResult();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (buffer is null) throw new ArgumentNullException(nameof(buffer));
        if ((uint)offset > (uint)buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
        if ((uint)count > (uint)(buffer.Length - offset)) throw new ArgumentOutOfRangeException(nameof(count));
        if (count == 0) return 0;

        return Read(buffer.AsSpan(offset, count));
    }

    public override unsafe int Read(Span<byte> destination)
    {
        if (destination.Length == 0) return 0;

        // Ensure we have at least one segment available (may block waiting for data).
        if (!EnsureReadableSync())
            return 0; // closed

        int written = 0;

        while (written < destination.Length)
        {
            if (!EnsureSegmentSync())
                break; // no more data right now (or closed)

            int available = _streamItem.Length - _streamItemOffset;
            int toCopy = Math.Min(available, destination.Length - written);

            new ReadOnlySpan<byte>(_streamItem.Ptr + _streamItemOffset, toCopy)
                .CopyTo(destination.Slice(written, toCopy));

            _streamItemOffset += toCopy;
            written += toCopy;

            if (_streamItemOffset == _streamItem.Length)
                ReleaseCurrentSegment();
        }

        return written;
    }

    // -----------------------------
    // Async Read
    // -----------------------------
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> destination,
        CancellationToken cancellationToken = default)
    {
        if (destination.Length == 0) 
            return 0;

        var result = await ReadAsync();
        if (result.IsClosed)
            return 0;
        
        var rings = GetAllSnapshotRings(result);
        var len = destination.CopyFromRings(rings);

        foreach (var ring in rings)
        {
            ReturnRing(ring.BufferId);
        }

        return len;
    }
    
    public async ValueTask<int> ReadAsync2(Memory<byte> destination, CancellationToken cancellationToken = default)
    {
        if (destination.Length == 0) return 0;

        // Ensure we have at least one segment available (awaits your ReadAsync()).
        if (!await EnsureReadableAsync(cancellationToken).ConfigureAwait(false))
            return 0; // closed

        int written = 0;

        while (written < destination.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await EnsureSegmentAsync(cancellationToken).ConfigureAwait(false))
                break;

            int available = _streamItem.Length - _streamItemOffset;
            int toCopy = Math.Min(available, destination.Length - written);

            unsafe
            {
                new ReadOnlySpan<byte>(_streamItem.Ptr + _streamItemOffset, toCopy)
                    .CopyTo(destination.Span.Slice(written, toCopy));
            }

            _streamItemOffset += toCopy;
            written += toCopy;

            if (_streamItemOffset == _streamItem.Length)
                ReleaseCurrentSegment();
        }

        return written;
    }

    // -----------------------------
    // Helpers
    // -----------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool EnsureReadableSync()
    {
        // If already have a segment, we're good.
        if (Volatile.Read(ref _streamHasItem) != 0)
            return true;

        // If we have a snapshot, try to get the first segment within it.
        if (Volatile.Read(ref _streamHasSnap) != 0 && TryGetRing(_streamSnap, out var it))
        {
            SetCurrentSegment(it);
            return true;
        }

        // Otherwise, wait for a batch (blocks).
        var rr = ReadAsync().GetAwaiter().GetResult();
        if (rr.IsClosed)
            return false;

        _streamSnap = rr.TailSnapshot;
        Volatile.Write(ref _streamHasSnap, 1);

        // Now try to get first segment
        if (TryGetRing(_streamSnap, out var item))
        {
            SetCurrentSegment(item);
            return true;
        }

        // Snapshot but no segments: treat as "no data now" (should be rare)
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool EnsureSegmentSync()
    {
        if (Volatile.Read(ref _streamHasItem) != 0)
            return true;

        // If we have a snapshot, try dequeue next within boundary.
        if (Volatile.Read(ref _streamHasSnap) != 0)
        {
            if (TryGetRing(_streamSnap, out var item))
            {
                SetCurrentSegment(item);
                return true;
            }

            // End of this batch. Prepare for next batch.
            Volatile.Write(ref _streamHasSnap, 0);
            _streamSnap = 0;
            ResetRead();

            // Don’t block again inside the same Read() loop; return what we have.
            return false;
        }

        // No snapshot; caller should call EnsureReadableSync() first.
        return false;
    }

    private async ValueTask<bool> EnsureReadableAsync(CancellationToken ct)
    {
        if (Volatile.Read(ref _streamHasItem) != 0)
            return true;

        if (Volatile.Read(ref _streamHasSnap) != 0 && TryGetRing(_streamSnap, out var it))
        {
            SetCurrentSegment(it);
            return true;
        }

        // Await a new batch
        var rr = await ReadAsync().ConfigureAwait(false);
        if (rr.IsClosed)
            return false;

        _streamSnap = rr.TailSnapshot;
        Volatile.Write(ref _streamHasSnap, 1);

        if (TryGetRing(_streamSnap, out var item))
            SetCurrentSegment(item);

        return true;
    }

    private async ValueTask<bool> EnsureSegmentAsync(CancellationToken ct)
    {
        if (Volatile.Read(ref _streamHasItem) != 0)
            return true;

        if (Volatile.Read(ref _streamHasSnap) != 0)
        {
            if (TryGetRing(_streamSnap, out var item))
            {
                SetCurrentSegment(item);
                return true;
            }

            // End of batch
            Volatile.Write(ref _streamHasSnap, 0);
            _streamSnap = 0;
            ResetRead();

            // Wait for next batch
            var rr = await ReadAsync().ConfigureAwait(false);
            if (rr.IsClosed)
                return false;

            _streamSnap = rr.TailSnapshot;
            Volatile.Write(ref _streamHasSnap, 1);

            if (TryGetRing(_streamSnap, out var it2))
            {
                SetCurrentSegment(it2);
                return true;
            }

            return true;
        }

        // No snapshot; caller should call EnsureReadableAsync first.
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetCurrentSegment(RingItem item)
    {
        _streamItem = item;
        _streamItemOffset = 0;
        Volatile.Write(ref _streamHasItem, 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReleaseCurrentSegment()
    {
        // Return buffer to reactor pool
        ReturnRing(_streamItem.BufferId);

        _streamItem = default;
        _streamItemOffset = 0;
        Volatile.Write(ref _streamHasItem, 0);
    }
}