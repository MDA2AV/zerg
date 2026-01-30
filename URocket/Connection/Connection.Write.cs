using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks.Sources;
using URocket.Utils;
using URocket.Utils.UnmanagedMemoryManager;

namespace URocket.Connection;

/// <remarks>
/// <br/>
/// **Write Remarks**
/// This type implements a fixed-size unmanaged slab used as a staging buffer
/// for outgoing data. Writes append into the slab and are later flushed by
/// the reactor thread. No internal synchronization is performed: this instance
/// is expected to be owned by a single reactor at a time.
///
/// Invariants:
/// - Writes append at <see cref="WriteTail"/>.
/// - The buffer is flushed when <see cref="CanWrite"/> is set.
/// - After a flush completes, <see cref="ResetWriteBuffer"/> must be called
///   before the buffer can be reused.
/// <br/>
/// </remarks>
public unsafe partial class Connection : IBufferWriter<byte>, IValueTaskSource, IDisposable 
{
    /// <summary>Size of the unmanaged write slab.</summary>
    private readonly int _writeSlabSize;

    /// <summary>Memory owner for the unmanaged write slab.</summary>
    private readonly UnmanagedMemoryManager _manager;

    /// <summary>Pointer to the unmanaged write buffer.</summary>
    public byte* WriteBuffer { get; }

    /// <summary>Logical start of valid data (currently unused; reserved for future streaming).</summary>
    public int WriteHead { get; set; }

    /// <summary>Logical end of written data in <see cref="WriteBuffer"/>.</summary>
    public int WriteTail { get; private set; }

    /// <summary> Number of bytes that were sent to kernel, we need this value to validate whether all flushed data was processed by the kernel. </summary>
    internal int WriteInFlight { get; set; }

    /// <summary>Indicates that the buffer may be flushed by the reactor.</summary>
    public volatile bool CanFlush = true;

    /// <summary>Indicates that new data is available for sending.</summary>
    public volatile bool CanWrite = false;

    /// <summary>
    /// Creates a new unmanaged write slab for the connection.
    /// </summary>
    /// <param name="writeSlabSize">Size in bytes of the slab.</param>
    public Connection(int writeSlabSize = 1024 * 16) {
        _writeSlabSize = writeSlabSize;
        WriteBuffer = (byte*)NativeMemory.AlignedAlloc((nuint)(writeSlabSize), 64);
        WriteHead = 0; WriteTail = 0;
        
        _manager = new UnmanagedMemoryManager(WriteBuffer, writeSlabSize);
    }
    
    /// <summary>
    /// Copies unmanaged memory into the write slab.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Write(byte* ptr, int length)
    {
        if (Volatile.Read(ref _flushInProgress) != 0)
            throw new InvalidOperationException("Cannot write while flush is in progress.");
        
        if ((uint)length > (uint)_writeSlabSize) // also rejects negative
            throw new ArgumentOutOfRangeException(nameof(length));

        if (WriteTail + length > _writeSlabSize)
            throw new InvalidOperationException("Buffer too small.");

        // copy unmanaged -> unmanaged
        Buffer.MemoryCopy(
            source: ptr,
            destination: WriteBuffer + WriteTail,
            destinationSizeInBytes: _writeSlabSize - WriteTail,
            sourceBytesToCopy: length);

        WriteTail += length;
    }
    
    /// <summary>
    /// Appends a managed buffer into the write slab.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(ReadOnlyMemory<byte> source)
    {
        if (Volatile.Read(ref _flushInProgress) != 0)
            throw new InvalidOperationException("Cannot write while flush is in progress.");
        
        int len = source.Length;
        if (WriteTail + len > _writeSlabSize)
            throw new InvalidOperationException("Buffer too small.");

        source.Span.CopyTo(
            new Span<byte>(WriteBuffer + WriteTail, len)
        );

        WriteTail += len;
    }
    
    /// <summary>
    /// Appends a span into the write slab.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(ReadOnlySpan<byte> source) 
    {
        if (Volatile.Read(ref _flushInProgress) != 0)
            throw new InvalidOperationException("Cannot write while flush is in progress.");
        
        int len = source.Length;
        if (WriteTail + len > _writeSlabSize)
            throw new InvalidOperationException("Buffer too small.");

        source.CopyTo(new Span<byte>(WriteBuffer + WriteTail, len));
        WriteTail += len;
    }
    
    /// <summary>
    /// Advances the logical tail after direct buffer writes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
    {
        if (Volatile.Read(ref _flushInProgress) != 0)
            throw new InvalidOperationException("Cannot write while flush is in progress.");
        
        WriteTail += count;
    }

    // ** IBufferWriter Implementation **
    /// <summary>
    /// Returns a writable <see cref="Memory{Byte}"/> view into the remaining slab.
    /// </summary>
    public Memory<byte> GetMemory(int sizeHint = 0) 
    {
        if (Volatile.Read(ref _flushInProgress) != 0)
            throw new InvalidOperationException("Cannot write while flush is in progress.");
        
        int remaining = _writeSlabSize - WriteTail;
        if (sizeHint > remaining)
            throw new InvalidOperationException("Buffer too small.");
        
        return _manager.Memory.Slice(WriteTail, remaining);
    }
    
    /// <summary>
    /// Returns a writable <see cref="Span{Byte}"/> view into the remaining slab.
    /// </summary>
    public Span<byte> GetSpan(int sizeHint = 0) 
    {
        if (Volatile.Read(ref _flushInProgress) != 0)
            throw new InvalidOperationException("Cannot write while flush is in progress.");
        
        if (WriteTail + sizeHint > _writeSlabSize)
            throw new InvalidOperationException("Buffer too small.");

        return new Span<byte>(WriteBuffer + WriteTail, _writeSlabSize - WriteTail);
    }
    
    // ** Reset and Dispose **
    /// <summary>
    /// Resets the slab so it can be reused for the next batch.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetWriteBuffer()
    {
        WriteHead = 0;
        WriteTail = 0; 
    }
    
    /// <summary>
    /// Frees the unmanaged slab and releases the memory manager.
    /// </summary>
    public void Dispose() {
        _manager.Free();
        ((IDisposable)_manager).Dispose();
    }
    
    
    // ***************************************

     // 1 waiter max (the client thread)
    private ManualResetValueTaskSourceCore<bool> _flushSignal;
    private int _flushArmed;

    // When 1: client promises it will NOT write until flush completes.
    private int _flushInProgress;

    // Tail snapshot captured at FlushAsync() time.
    internal int FlushTarget { get; private set; }

    // Expose status to reactor
    internal bool IsFlushInProgress => Volatile.Read(ref _flushInProgress) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask FlushAsync()
    {
        // Start flush barrier (client must not write until completion)
        if (Interlocked.Exchange(ref _flushInProgress, 1) == 1)
            throw new InvalidOperationException("FlushAsync already in progress.");

        // Snapshot "everything written so far"
        int target = WriteTail;                // single writer => plain read ok
        FlushTarget = target;

        // Fast path: nothing to flush
        if (target == 0)
        {
            Volatile.Write(ref _flushInProgress, 0);
            return default;
        }

        // Arm single waiter
        if (Interlocked.Exchange(ref _flushArmed, 1) == 1)
            throw new InvalidOperationException("FlushAsync already armed.");

        // Tell reactor we want a flush
        CanWrite = true;

        // IMPORTANT: enqueue fd to reactor (see section 3)
        Reactor.EnqueueFlush(ClientFd);

        return new ValueTask(this, token: 0);
    }

    // Reactor completes this when it finished sending up to FlushTarget
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void CompleteFlush()
    {
        _flushSignal.SetResult(true);
    }

    // Client calls this after awaiting OR reactor will call it right before SetResult
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ResetFlushState()
    {
        Volatile.Write(ref _flushInProgress, 0);
        Volatile.Write(ref _flushArmed, 0);
        _flushSignal.Reset();
    }

    // IValueTaskSource plumbing (no tokening needed because single-threaded client + single waiter)
    void IValueTaskSource.GetResult(short token)
    {
        _flushSignal.GetResult(_flushSignal.Version);
    }

    ValueTaskSourceStatus IValueTaskSource.GetStatus(short token)
        => _flushSignal.GetStatus(_flushSignal.Version);

    void IValueTaskSource.OnCompleted(Action<object?> continuation, object? state, short token,
        ValueTaskSourceOnCompletedFlags flags)
        => _flushSignal.OnCompleted(continuation, state, _flushSignal.Version, flags);
}