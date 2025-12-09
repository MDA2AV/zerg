using System.Runtime.InteropServices;
using System.Threading.Channels;
using Microsoft.Extensions.ObjectPool;
using Overdrive.HttpProtocol;
using static Overdrive.ABI.Native;

namespace Overdrive.Engine;

public sealed unsafe partial class OverdriveEngine
{
    // Global pool for Connection objects.
    // NOTE: Pool size is tuned for high-throughput scenarios; adjust as needed.
    private static readonly ObjectPool<Connection> ConnectionPool =
        new DefaultObjectPool<Connection>(new ConnectionPoolPolicy(), 1024*32);

    private class ConnectionPoolPolicy : PooledObjectPolicy<Connection>
    {
        /// <summary>
        /// Create a new Connection instance with the configured per-worker limits and slab sizes.
        /// </summary>
        public override Connection Create() => new();

        /// <summary>
        /// Return a Connection to the pool. Consider resetting/clearing per-request state here.
        /// </summary>
        public override bool Return(Connection connection)
        {
            // Potentially reset buffers here (e.g., context.Reset()) to avoid data leaks across usages.
            connection.Clear();
            
            return true;
        }
    }
    
    public static Worker[] s_Workers = null!;
    
    public static Dictionary<int, Connection>[] Connections = null!;
    
    public class Worker
    {
        public Worker(int workerIndex)
        {
            _workerIndex = workerIndex;
        }
        private readonly int _workerIndex;

        public io_uring* PRing;
        internal io_uring_buf_ring* BufferRing;
        internal byte* BufferRingSlab;
        internal uint BufferRingIndex = 0;
        internal uint BufferRingMask;
        internal int ConnCount = 0;

        internal void InitPRing()
        {
            PRing = shim_create_ring((uint)s_ringEntries, out var err);
            
            if (PRing == null || err < 0)
            {
                Console.Error.WriteLine($"[w{_workerIndex}] create_ring failed: {err}");
                return;
            }
            
            // Setup buffer ring

            // TODO: Investigate this c_bufferRingGID
            BufferRing = shim_setup_buf_ring(PRing, (uint)s_bufferRingEntries, c_bufferRingGID, 0, out var ret);
            if (BufferRing == null || ret < 0)
                throw new Exception($"setup_buf_ring failed: ret={ret}");

            BufferRingMask = (uint)(s_bufferRingEntries - 1);
            nuint slabSize = (nuint)(s_bufferRingEntries * s_recvBufferSize);
            BufferRingSlab = (byte*)NativeMemory.AlignedAlloc(slabSize, 64);

            for (ushort bid = 0; bid < s_bufferRingEntries; bid++)
            {
                byte* addr = BufferRingSlab + (nuint)bid * (nuint)s_recvBufferSize;
                shim_buf_ring_add(BufferRing, addr, (uint)s_recvBufferSize, bid, (ushort)BufferRingMask, BufferRingIndex++);
            }
            shim_buf_ring_advance(BufferRing, (uint)s_bufferRingEntries);
        }
        
        public Channel<SendRequest> SendQueue { get; } =
            Channel.CreateUnbounded<SendRequest>(new UnboundedChannelOptions
            {
                SingleReader = true,  // worker thread
                SingleWriter = false, // many handlers
            });
    }
    
    public readonly unsafe struct SendRequest
    {
        public readonly int Fd;
        public readonly byte* Ptr;
        public readonly nuint Length;

        public SendRequest(int fd, byte* ptr, nuint length)
        {
            Fd = fd;
            Ptr = ptr;
            Length = length;
        }
    }
    
    public static bool TryQueueSend(Connection connection, byte* ptr, nuint length)
    {
        var worker = s_Workers[connection.WorkerIndex];
        var writer = worker.SendQueue.Writer;

        // Non-blocking enqueue; you can decide if you want to await instead
        return writer.TryWrite(new SendRequest(connection.Fd, ptr, length));
    }

    private static void WorkerLoop(int workerIndex)
    {
        // Per-worker connection map
        var connections = Connections[workerIndex];

        var worker   = s_Workers[workerIndex];
        var pring    = worker.PRing;
        var BR_Slab  = worker.BufferRingSlab;
        var BR       = worker.BufferRing;
        var BR_Mask  = worker.BufferRingMask;
        // IMPORTANT: ref so we persist increments back into worker state
        ref uint BR_Idx = ref worker.BufferRingIndex;

        var myQueue    = WorkerQueues[workerIndex];     // new FDs from acceptor
        var sendReader = worker.SendQueue.Reader;       // outbound sends from app

        var cqes = new io_uring_cqe*[s_batchCQES];

        Console.WriteLine($"[w{workerIndex}] Started and ready");

        try
        {
            while (!StopAll)
            {
                //Thread.Sleep(1);
                
                //
                // 1) Drain new connections → arm recv multishot
                //
                int newFds = 0;
                while (myQueue.TryDequeue(out int newFd))
                {
                    ArmRecvMultishot(pring, newFd, c_bufferRingGID);
                    worker.ConnCount++;
                    newFds++;
                }

                //
                // 2) Drain outbound send requests (queued by application code)
                //
                while (sendReader.TryRead(out var send))
                {
                    if (connections.TryGetValue(send.Fd, out var connection))
                    {
                        connection.OutPtr  = send.Ptr;
                        connection.OutHead = 0;
                        connection.OutTail = send.Length;
                        connection.Sending = true;

                        SubmitSend(
                            pring,
                            connection.Fd,
                            connection.OutPtr,
                            connection.OutHead,
                            connection.OutTail);
                    }
                    // If fd is gone, just drop the send request
                }

                //
                // 3) Process completions (non-blocking peek)
                //
                int got;
                fixed (io_uring_cqe** pC = cqes)
                    got = shim_peek_batch_cqe(pring, pC, (uint)s_batchCQES);

                if (got <= 0)
                {
                    // Nothing completed this round.
                    // If truly idle, sleep a bit, otherwise just yield.
                    if (worker.ConnCount == 0 &&
                        myQueue.IsEmpty &&
                        !sendReader.TryPeek(out _))
                    {
                        Thread.Sleep(1);
                    }
                    else
                    {
                        Thread.Yield();
                    }

                    // Submit anything we queued (new recvs / sends)
                    if (shim_sq_ready(pring) > 0)
                    {
                        //Console.WriteLine("Submitting1");
                        shim_submit(pring);
                    }

                    continue;
                }

                //
                // 4) Handle all CQEs
                //
                for (int i = 0; i < got; i++)
                {
                    var cqe = cqes[i];
                    ulong ud  = shim_cqe_get_data64(cqe);
                    var kind  = UdKindOf(ud);
                    int res   = cqe->res;

                    if (kind == UdKind.Recv)
                    {
                        int fd = UdFdOf(ud);
                        ushort bid       = 0;
                        bool hasBuffer   = shim_cqe_has_buffer(cqe) != 0;
                        bool hasMore     = (cqe->flags & IORING_CQE_F_MORE) != 0;

                        if (hasBuffer)
                            bid = (ushort)shim_cqe_buffer_id(cqe);

                        if (res <= 0)
                        {
                            // Error / EOF: return buffer and close connection
                            if (hasBuffer)
                            {
                                byte* addr = BR_Slab + (nuint)bid * (nuint)s_recvBufferSize;
                                shim_buf_ring_add(
                                    BR,
                                    addr,
                                    (uint)s_recvBufferSize,
                                    bid,
                                    (ushort)BR_Mask,
                                    BR_Idx++);
                                shim_buf_ring_advance(BR, 1);
                            }

                            if (connections.TryGetValue(fd, out var connection))
                            {
                                ConnectionPool.Return(connection);
                                close(fd);
                                worker.ConnCount--;
                            }
                        }
                        else
                        {
                            // Normal recv: hand buffer to connection + signal app
                            if (connections.TryGetValue(fd, out var connection))
                            {
                                connection.Tcs.SetResult(true);

                                // Re-arm multishot if kernel ended it
                                if (!hasMore)
                                {
                                    ArmRecvMultishot(pring, fd, c_bufferRingGID);
                                }
                            }

                            // Return buffer to ring after processing
                            if (hasBuffer)
                            {
                                byte* addr = BR_Slab + (nuint)bid * (nuint)s_recvBufferSize;
                                shim_buf_ring_add(
                                    BR,
                                    addr,
                                    (uint)s_recvBufferSize,
                                    bid,
                                    (ushort)BR_Mask,
                                    BR_Idx++);
                                shim_buf_ring_advance(BR, 1);
                            }
                        }
                    }
                    else if (kind == UdKind.Send)
                    {
                        int fd = UdFdOf(ud);

                        if (connections.TryGetValue(fd, out var connection))
                        {
                            // Advance send progress.
                            connection.OutHead += (nuint)res;

                            if (connection.OutHead < connection.OutTail)
                            {
                                // Not done yet → queue another partial send
                                SubmitSend(
                                    pring,
                                    connection.Fd,
                                    connection.OutPtr,
                                    connection.OutHead,
                                    connection.OutTail);
                            }
                            else
                            {
                                // Finished sending this response
                                connection.Sending = false;
                            }
                        }
                    }

                    shim_cqe_seen(pring, cqe);
                }

                //
                // 5) Submit all SQEs we queued this iteration
                //
                /*if (shim_sq_ready(pring) > 0)
                {
                    //Console.WriteLine("Submitting2");
                    shim_submit(pring);
                }*/
            }
        }
        finally
        {
            // Close any remaining connections
            CloseAll(connections);

            // Free buffer ring BEFORE destroying the ring
            if (pring != null && BR != null)
            {
                shim_free_buf_ring(pring, BR, (uint)s_bufferRingEntries, c_bufferRingGID);
                BR = null;
            }

            // Destroy ring (unregisters CQ/SQ memory mappings)
            if (pring != null)
            {
                shim_destroy_ring(pring);
                pring = null;
            }

            // Free slab memory used by buf ring
            if (BR_Slab != null)
            {
                NativeMemory.AlignedFree(BR_Slab);
                BR_Slab = null;
            }

            Console.WriteLine($"[w{workerIndex}] Shutdown complete.");
        }
    }
    
    private static void CloseAll(Dictionary<int, Connection> connections)
    {
        foreach (var connection in connections)
        {
            try
            {
                close(connection.Value.Fd);
                ConnectionPool.Return(connection.Value);
            }
            catch
            {
                /* ignore */ 
            }
        }
    }
}