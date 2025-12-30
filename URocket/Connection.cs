using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;

namespace URocket;

public readonly unsafe struct RecvItem {
    public readonly byte* Ptr;
    public readonly int Length;
    public readonly ushort BufferId;

    public RecvItem(byte* ptr, int length, ushort bufferId) {
        Ptr = ptr;
        Length = length;
        BufferId = bufferId;
    }
}

[SkipLocalsInit]
public sealed unsafe class Connection : IValueTaskSource<bool>
{
    public int ClientFd { get; private set; }
    public Engine.Engine.Reactor Reactor { get; private set; } = null!;

    // Out buffer
    public nuint OutHead { get; set; }
    public nuint OutTail { get; set; }
    public byte* OutPtr { get; set; }

    // Reusable completion primitive
    private ManualResetValueTaskSourceCore<bool> _readSignal;

    // 0/1 flags (atomic)
    private int _armed;    // there is an outstanding await that needs waking
    private int _pending;  // data arrived while not armed / before reset

    // Queue of received buffers for this connection
    private readonly ConcurrentQueue<RecvItem> _recvQ = new();

    // Called by reactor thread: push recv and wake if needed.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnqueueRecv(byte* ptr, int length, ushort bufferId)
    {
        _recvQ.Enqueue(new RecvItem(ptr, length, bufferId));

        // Edge-trigger: if a waiter is armed, wake it; otherwise mark pending.
        if (Interlocked.Exchange(ref _armed, 0) == 1)
        {
            _readSignal.SetResult(true);
        }
        else
        {
            Volatile.Write(ref _pending, 1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryDequeueRecv(out RecvItem item) => _recvQ.TryDequeue(out item);

    /// <summary>Await until there is at least one recv item available.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<bool> ReadAsync()
    {
        // Fast path: data already pending or already queued
        if (Volatile.Read(ref _pending) == 1 || !_recvQ.IsEmpty)
        {
            Volatile.Write(ref _pending, 0);
            return new ValueTask<bool>(true);
        }

        // Arm exactly one waiter
        if (Interlocked.Exchange(ref _armed, 1) == 1)
            throw new InvalidOperationException("ReadAsync already armed.");

        return new ValueTask<bool>(this, _readSignal.Version);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetRead()
    {
        // Prepare the value-task source for the next wait cycle.
        _readSignal.Reset();

        // If data arrived between the time we finished draining and now,
        // ensure the next ReadAsync() completes synchronously.
        if (!_recvQ.IsEmpty)
            Volatile.Write(ref _pending, 1);
    }

    public void Clear()
    {
        OutPtr = null;
        OutHead = 0;
        OutTail = 0;

        Volatile.Write(ref _armed, 0);
        Volatile.Write(ref _pending, 0);
        _readSignal.Reset();

        while (_recvQ.TryDequeue(out _)) { }
    }

    public Connection SetFd(int fd) { ClientFd = fd; return this; }
    public Connection SetReactor(Engine.Engine.Reactor reactor) { Reactor = reactor; return this; }

    // IValueTaskSource<bool>
    bool IValueTaskSource<bool>.GetResult(short token) => _readSignal.GetResult(token);
    ValueTaskSourceStatus IValueTaskSource<bool>.GetStatus(short token) => _readSignal.GetStatus(token);
    void IValueTaskSource<bool>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        => _readSignal.OnCompleted(continuation, state, token, flags);
}

[SkipLocalsInit]
public sealed unsafe class Connection2 : IValueTaskSource<bool>
{
    public bool HasBuffer;
    public ushort BufferId { get; set; }

    public int ClientFd { get; private set; }
    public Engine.Engine.Reactor Reactor { get; private set; } = null!;

    // In buffer (points into reactor's buffer-ring slab)
    public byte* InPtr { get; internal set; }
    public int InLength  { get; internal set; }

    // Out buffer
    public nuint OutHead { get; set; }
    public nuint OutTail { get; set; }
    public byte* OutPtr { get; set; }

    // Reusable completion primitive (no Task/TCS allocations per read)
    private ManualResetValueTaskSourceCore<bool> _readSignal;

    // Debug guard: enforces "one outstanding ReadAsync at a time"
    private bool _readArmed;

    /// <summary>
    /// Await until the reactor signals that new bytes are available in InPtr/InLength.
    /// One outstanding await is supported at a time.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<bool> ReadAsync() {
        if (_readArmed) 
            throw new InvalidOperationException("ReadAsync() already armed. Call ResetRead() after consuming the buffer.");
        _readArmed = true;
        Console.WriteLine($"{Reactor.Id} _readArmed TRUE");
        return new ValueTask<bool>(this, _readSignal.Version);
    }

    /// <summary>
    /// Called by the reactor thread when it has produced readable bytes for this connection.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SignalReadReady() { _readSignal.SetResult(true); }

    /// <summary>
    /// Called by the consumer after it finishes using InPtr/InLength and wants to await the next read.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetRead() {
        _readArmed = false;

        // Prepare for next await cycle
        Console.WriteLine($"{Reactor.Id} Resetting..");
        _readSignal.Reset();
        Console.WriteLine($"{Reactor.Id} _readArmed FALSE");

        // Clear "current read" metadata (optional but helps avoid misuse)
        //InPtr = null;
        //InLength = 0;
        //HasBuffer = false;
        //BufferId = 0;
    }

    public void Clear() {
        // Reset send state
        OutPtr = null;
        OutHead = 0;
        OutTail = 0;

        // Reset read state so pooled connections don't remain signaled
        _readArmed = false;
        _readSignal.Reset();

        InPtr = null;
        InLength = 0;
        HasBuffer = false;
        BufferId = 0;
    }

    // Setters for pooled connections
    public Connection2 SetFd(int fd) { ClientFd = fd; return this; }
    public Connection2 SetReactor(Engine.Engine.Reactor reactor) { Reactor = reactor; return this; }
    
    // IValueTaskSource<bool> plumbing
    bool IValueTaskSource<bool>.GetResult(short token) => _readSignal.GetResult(token);
    ValueTaskSourceStatus IValueTaskSource<bool>.GetStatus(short token) => _readSignal.GetStatus(token);
    void IValueTaskSource<bool>.OnCompleted(
        Action<object?> continuation,
        object? state,
        short token,
        ValueTaskSourceOnCompletedFlags flags)
        => _readSignal.OnCompleted(continuation, state, token, flags);
}