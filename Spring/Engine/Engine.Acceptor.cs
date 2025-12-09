using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using static Overdrive.ABI.Native;

namespace Overdrive.Engine;

public sealed unsafe partial class OverdriveEngine
{
    public static void AcceptorLoop(string ip, ushort port, int workerCount)
    {
        int lfd = CreateListen(ip, port);
        
        io_uring* pring = null;
        
        try
        {
            Console.WriteLine($"[acceptor] Listening on {ip}:{port}");

            pring = shim_create_ring(256, out int err);
            if (pring == null || err < 0)
            {
                Console.Error.WriteLine($"[acceptor] create_ring failed: {err}");
                return;
            }

            // Start multishot accept
            var sqe = SqeGet(pring);
            shim_prep_multishot_accept(sqe, lfd, SOCK_NONBLOCK);
            shim_sqe_set_data64(sqe, PackUd(UdKind.Accept, lfd));
            shim_submit(pring);

            Console.WriteLine("[acceptor] Multishot accept armed");

            var cqes = new io_uring_cqe*[32];
            int nextWorker = 0;
            int one = 1;
            long acceptedTotal = 0;

            Console.WriteLine($"[acceptor] Load balancing across {workerCount} workers");

            while (!StopAll)
            {
                int got;
                fixed (io_uring_cqe** pC = cqes)
                    got = shim_peek_batch_cqe(pring, pC, (uint)cqes.Length);

                if (got <= 0)
                {
                    io_uring_cqe* oneCqe = null;
                    if (shim_wait_cqe(pring, &oneCqe) != 0) continue;
                    cqes[0] = oneCqe;
                    got = 1;
                }

                for (int i = 0; i < got; i++)
                {
                    var cqe = cqes[i];
                    ulong ud = shim_cqe_get_data64(cqe);
                    var kind = UdKindOf(ud);
                    int res = cqe->res;

                    if (kind == UdKind.Accept)
                    {
                        if (res >= 0)
                        {
                            int clientFd = res;

                            // TCP_NODELAY
                            setsockopt(clientFd, IPPROTO_TCP, TCP_NODELAY, &one, (uint)sizeof(int));

                            // Round-robin to next worker
                            var targetWorker = nextWorker;
                            nextWorker = (nextWorker + 1) % workerCount;

                            WorkerQueues[targetWorker].Enqueue(clientFd);
                            Connections[targetWorker][clientFd] = ConnectionPool.Get().SetFd(clientFd).SetWorkerIndex(targetWorker);
                            
                            var connectionAdded = ConnectionQueues.Writer.TryWrite(new ConnectionItem(targetWorker, clientFd));
                            if (!connectionAdded)
                            {
                                Console.WriteLine("Failed to write connection!!");
                            }

                            //Interlocked.Increment(ref WorkerConnectionCounts[targetWorker]);

                            /*acceptedTotal++;
                            if (acceptedTotal % 1000 == 0)
                            {
                                Console.WriteLine($"[acceptor] Accepted {acceptedTotal} connections");
                                var distrib = string.Join(", ", WorkerConnectionCounts.Select((c, idx) => $"w{idx}:{c}"));
                                Console.WriteLine($"[acceptor] Distribution: {distrib}");
                            }*/
                        }
                        else
                        {
                            Console.WriteLine($"[acceptor] Accept error: {res}");
                        }
                    }

                    shim_cqe_seen(pring, cqe);
                }

                if (shim_sq_ready(pring) > 0)
                {
                    Console.WriteLine("Submitting3");
                    shim_submit(pring);
                }
            }
        }
        finally
        {
            // close listener and ring even on exception/StopAll
            if (lfd >= 0) 
                close(lfd);
            
            if (pring != null) 
                shim_destroy_ring(pring);
            
            Console.WriteLine($"[acceptor] Shutdown complete.");
        }
    }
    
    // io_uring completion flags
    private const uint IORING_CQE_F_MORE = (1U << 1);

    public static byte* OK_PTR;
    public static nuint OK_LEN;

    private static void InitOk()
    {
        var s = "HTTP/1.1 200 OK\r\nContent-Length: 13\r\nConnection: keep-alive\r\nContent-Type: text/plain\r\n\r\nHello, World!";
        var a = Encoding.UTF8.GetBytes(s);
        OK_LEN = (nuint)a.Length;
        OK_PTR = (byte*)NativeMemory.Alloc(OK_LEN);
        for (int i = 0; i < a.Length; i++)
            OK_PTR[i] = a[i];
    }

    private static void FreeOk()
    {
        if (OK_PTR != null)
        {
            NativeMemory.Free(OK_PTR);
            OK_PTR = null;
            OK_LEN = 0;
        }
    }

    private static io_uring_sqe* SqeGet(io_uring* pring)
    {
        var sqe = shim_get_sqe(pring);
        if (sqe == null)
        {
            Console.WriteLine("Submitting4");
            shim_submit(pring); 
            sqe = shim_get_sqe(pring); 
        }
        return sqe;
    }

    public static void SubmitSend(io_uring* pring, int fd, byte* buf, nuint off, nuint len)
    {
        var sqe = SqeGet(pring);
        shim_prep_send(sqe, fd, buf + off, (uint)(len - off), 0);
        shim_sqe_set_data64(sqe, PackUd(UdKind.Send, fd));
    }

    private static void ArmRecvMultishot(io_uring* pring, int fd, uint bgid)
    {
        var sqe = SqeGet(pring);
        shim_prep_recv_multishot_select(sqe, fd, bgid, 0);
        shim_sqe_set_data64(sqe, PackUd(UdKind.Recv, fd));
    }
    
    private static int CreateListen(string ip, ushort port)
    {
        int lfd = socket(AF_INET, SOCK_STREAM, 0);
        int one = 1;

        setsockopt(lfd, SOL_SOCKET, SO_REUSEADDR, &one, (uint)sizeof(int));
        setsockopt(lfd, SOL_SOCKET, SO_REUSEPORT, &one, (uint)sizeof(int));

        sockaddr_in addr = default;
        addr.sin_family = (ushort)AF_INET;
        addr.sin_port = Htons(port);

        var ipb = Encoding.UTF8.GetBytes(ip + "\0");
        fixed (byte* pip = ipb)
            inet_pton(AF_INET, (sbyte*)pip, &addr.sin_addr);

        bind(lfd, &addr, (uint)sizeof(sockaddr_in));
        listen(lfd, s_backlog);

        int fl = fcntl(lfd, F_GETFL, 0);
        fcntl(lfd, F_SETFL, fl | O_NONBLOCK);

        return lfd;
    }
}