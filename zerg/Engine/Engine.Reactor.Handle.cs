using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using static zerg.ABI.ABI;

namespace zerg.Engine;

public sealed unsafe partial class Engine {
    public partial class Reactor {
        internal void Handle() {
            Dictionary<int, Connection> connections = _engine.Connections[Id];
            ConcurrentQueue<int> reactorQueue = ReactorQueues[Id];
            io_uring_cqe*[] cqes = new io_uring_cqe*[Config.BatchCqes];
            
            try {
                io_uring_cqe* cqe;
                __kernel_timespec ts;
                ts.tv_sec  = 0;
                ts.tv_nsec = Config.CqTimeout;
                int _dRecvErr = 0, _dRecvOverflow = 0, _dEnobufs = 0;
                Dictionary<int, int> _errCodes = new();
                long _diagTick = Environment.TickCount64;
                while (_engine.ServerRunning) {
                    if (Id == 0) {
                        long now = Environment.TickCount64;
                        if (now - _diagTick > 2000) {
                            string errStr = string.Join(",", _errCodes.Select(kv => $"{kv.Key}:{kv.Value}"));
                            Console.WriteLine($"[w0] recvErr={_dRecvErr} overflow={_dRecvOverflow} enobufs={_dEnobufs} conns={connections.Count} errs=[{errStr}]");
                            _diagTick = now;
                        }
                    }
                    while (reactorQueue.TryDequeue(out int newFd)) {
                        Connection conn = _engine.ConnectionPool.Get()
                            .SetFd(newFd)
                            .SetReactor(_engine.Reactors[Id]);
                        connections[newFd] = conn;
                        // Queue multishot recv SQE (will be flushed by submit_and_wait_timeout)
                        ArmRecvMultishot(io_uring_instance, newFd, c_bufferRingGID);
                        bool connectionAdded = _engine.ConnectionQueues.Writer.TryWrite(new ConnectionItem(conn, conn.Generation));
                        if (!connectionAdded) Console.WriteLine("Failed to write connection!!");
                    }
                    DrainReturnQ();
                    DrainFlushQ();
                    if (shim_sq_ready(io_uring_instance) > 0)
                        shim_submit(io_uring_instance);
                    int got;
                    fixed (io_uring_cqe** pC = cqes) {
                        got = shim_peek_batch_cqe(io_uring_instance, pC, (uint)Config.BatchCqes);
                        if (got == 0) {
                            int rc = shim_submit_and_wait_timeout(io_uring_instance, pC, 1u, &ts);
                            if (rc < 0) continue;
                            got = shim_peek_batch_cqe(io_uring_instance, pC, (uint)Config.BatchCqes);
                        }
                    }

                    for (int i = 0; i < got; i++) {
                        cqe = cqes[i];
                        ulong ud = shim_cqe_get_data64(cqe);
                        UdKind kind = UdKindOf(ud);
                        int res = cqe->res;
                        if (kind == UdKind.Recv) {
                            int fd = UdFdOf(ud);
                            bool hasBuffer = shim_cqe_has_buffer(cqe) != 0;
                            bool hasMore   = (cqe->flags & IORING_CQE_F_MORE) != 0;
                            if (res <= 0) {
                                // ENOBUFS (-105): buf_ring temporarily empty — NOT fatal.
                                if (res == -105) {
                                    _dEnobufs++;
                                    if (!hasMore)
                                        ArmRecvMultishot(io_uring_instance, fd, c_bufferRingGID);
                                    continue;
                                }
                                if (hasBuffer) {
                                    ushort bufferId = (ushort)shim_cqe_buffer_id(cqe);
                                    if (_incrementalBuffers) {
                                        _bufferKernelDone![bufferId] = true;
                                        if (_bufferRefCounts![bufferId] > 0)
                                            goto skipReturnError; // outstanding RingItems will return it
                                    }
                                    byte* addr = _bufferRingSlab + (nuint)bufferId * (nuint)Config.RecvBufferSize;
                                    ReturnBufferRing(addr, bufferId);
                                    skipReturnError:;
                                }
                                _dRecvErr++;
                                if (Id == 0) { _errCodes.TryGetValue(res, out int c); _errCodes[res] = c + 1; }
                                if (connections.Remove(fd, out var connection)) {
                                    connection.MarkClosed(res);
                                    SubmitCancelRecv(io_uring_instance, fd);
                                    close(fd);
                                }
                                continue;
                            }
                            if (!hasBuffer) continue;
                            ushort bid = (ushort)shim_cqe_buffer_id(cqe);
                            byte* ptr;
                            if (_incrementalBuffers) {
                                bool bufMore = (cqe->flags & IORING_CQE_F_BUF_MORE) != 0;
                                ptr = _bufferRingSlab + (nuint)bid * (nuint)Config.RecvBufferSize + (nuint)_bufferOffsets![bid];
                                _bufferOffsets[bid] += res;
                                _bufferRefCounts![bid]++;
                                if (!bufMore) {
                                    _bufferKernelDone![bid] = true;
                                    // If all handler returns already processed (refcount was
                                    // decremented to 0 by earlier DrainReturnQ calls while
                                    // kernelDone was still false), only the current CQE's ref
                                    // remains. It will be returned by the handler normally.
                                    // But if refcount is 1 and no items will be enqueued (e.g.,
                                    // connection already closed), we handle it in the normal path.
                                }
                            } else {
                                ptr = _bufferRingSlab + (nuint)bid * (nuint)Config.RecvBufferSize;
                            }
                            if (connections.TryGetValue(fd, out var connection2)) {
                                if (!connection2.EnqueueRingItem(ptr, res, bid)) {
                                    _dRecvOverflow++;
                                    // Connection was force-closed (ring overflow or already closed).
                                    if (connections.Remove(fd)) {
                                        SubmitCancelRecv(io_uring_instance, fd);
                                        close(fd);
                                    }
                                    // Undo the buffer refcount since the item was not enqueued.
                                    if (_incrementalBuffers) {
                                        if (--_bufferRefCounts![bid] <= 0 && _bufferKernelDone![bid])
                                            ReturnBufferRing(_bufferRingSlab + (nuint)bid * (nuint)Config.RecvBufferSize, bid);
                                    } else {
                                        ReturnBufferRing(_bufferRingSlab + (nuint)bid * (nuint)Config.RecvBufferSize, bid);
                                    }
                                    continue;
                                }
                                if (!hasMore) {
                                    ArmRecvMultishot(io_uring_instance, fd, c_bufferRingGID);
                                }
                            } else {
                                if (_incrementalBuffers) {
                                    // No connection: mark kernel done and check if we can return immediately
                                    _bufferKernelDone![bid] = true;
                                    if (--_bufferRefCounts![bid] > 0)
                                        continue; // other RingItems still hold refs
                                }
                                ReturnBufferRing(_bufferRingSlab + (nuint)bid * (nuint)Config.RecvBufferSize, bid);
                            }
                        } else if (kind == UdKind.Send) {
                            int fd = UdFdOf(ud);
                            if (connections.TryGetValue(fd, out var connection)) {
                                if (res <= 0) {
                                    // error/close handling
                                    Volatile.Write(ref connection.SendInflight, 0);
                                    continue;
                                }
                                connection.WriteHead += res;
                                int target = connection.WriteInFlight;
                                // Still flushing target snapshot
                                if (connection.WriteHead < target) {
                                    // Correct: len is total-end (target), not remaining
                                    SubmitSend(io_uring_instance, fd, connection.WriteBuffer, (uint)connection.WriteHead, (uint)target);
                                    continue;
                                }
                                Volatile.Write(ref connection.SendInflight, 0); // Flush batch done
                                connection.WriteInFlight = 0;
                                connection.ResetWriteBuffer(); // safe because _flushInProgress forbids concurrent writes
                                if (connection.IsFlushInProgress) connection.CompleteFlush();
                            }
                        } else if (kind == UdKind.Cancel) { }
                    }
                    shim_cq_advance(io_uring_instance, (uint)got);
                }
            }finally {
                // Close any remaining connections
                CloseAll(connections);
                // Free buffer ring BEFORE destroying the ring
                if (io_uring_instance != null && _bufferRing != null) {
                    DrainReturnQ();
                    shim_free_buf_ring(io_uring_instance, _bufferRing, (uint)Config.BufferRingEntries, c_bufferRingGID);
                    _bufferRing = null;
                }
                // Destroy ring
                if (io_uring_instance != null) {
                    shim_destroy_ring(io_uring_instance); 
                    io_uring_instance = null; 
                }
                // Free slab memory used by buf ring
                if (_bufferRingSlab != null) {
                    NativeMemory.AlignedFree(_bufferRingSlab); 
                    _bufferRingSlab = null; 
                }
                Console.WriteLine($"Reactor[{Id}] Shutdown complete.");
            }
        }
    }
}