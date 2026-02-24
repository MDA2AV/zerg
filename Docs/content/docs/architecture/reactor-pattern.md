---
title: Reactor Pattern
weight: 1
---

zerg implements a classic **reactor pattern** with a split-architecture design: one dedicated acceptor thread and N independent reactor threads.

## Overview

<div id="reactor-diagram" style="position:relative;width:100%;max-width:800px;margin:2rem auto;font-family:system-ui,sans-serif;user-select:none">
<style>
  #reactor-diagram *{box-sizing:border-box}
  #reactor-diagram .zone{border-radius:12px;padding:20px;margin-bottom:16px;position:relative}
  #reactor-diagram .zone-kernel{background:linear-gradient(135deg,#1a1a2e,#16213e);border:1px solid #2a3a5c}
  #reactor-diagram .zone-user{background:linear-gradient(135deg,#0f1729,#131b2e);border:1px solid #1e2d4a}
  #reactor-diagram .zone-label{position:absolute;top:10px;right:14px;font-size:10px;font-weight:700;letter-spacing:1.5px;text-transform:uppercase;color:#4a5e7a}
  #reactor-diagram .node{border-radius:10px;padding:14px 18px;text-align:center;position:relative;transition:transform .2s,box-shadow .2s}
  #reactor-diagram .node:hover{transform:translateY(-2px);box-shadow:0 8px 24px rgba(59,130,246,.2)}
  #reactor-diagram .node-client{background:linear-gradient(135deg,#1e3a5f,#1a2d4a);border:1px solid #2e5a8a;color:#7eb8f0}
  #reactor-diagram .node-acceptor{background:linear-gradient(135deg,#2d1b4e,#1f1338);border:1px solid #5b3a8a;color:#c4a5f0}
  #reactor-diagram .node-reactor{background:linear-gradient(135deg,#1b3a2e,#132a1f);border:1px solid #2e7a5a;color:#7ef0b8;flex:1;min-width:0}
  #reactor-diagram .node-app{background:linear-gradient(135deg,#3a2a1b,#2a1f13);border:1px solid #8a6a2e;color:#f0d07e}
  #reactor-diagram .node-title{font-weight:700;font-size:14px;margin-bottom:6px}
  #reactor-diagram .node-sub{font-size:11px;opacity:.7;line-height:1.5}
  #reactor-diagram .connector{display:flex;align-items:center;justify-content:center;padding:8px 0;position:relative}
  #reactor-diagram .connector-line{width:2px;height:32px;background:linear-gradient(to bottom,transparent,#3b82f6,transparent)}
  #reactor-diagram .connector-fan{display:flex;gap:12px;justify-content:center;align-items:flex-start;position:relative;padding-top:8px}
  #reactor-diagram .connector-arrow{color:#3b82f6;font-size:13px;font-weight:700;animation:pulse-arrow 2s infinite}
  #reactor-diagram .connector-arrow-rr{color:#8b5cf6;font-size:11px;font-weight:600}
  #reactor-diagram .badge{display:inline-block;font-size:10px;font-weight:600;padding:2px 8px;border-radius:20px;margin:2px}
  #reactor-diagram .badge-uring{background:rgba(139,92,246,.2);color:#a78bfa;border:1px solid rgba(139,92,246,.3)}
  #reactor-diagram .badge-buf{background:rgba(59,130,246,.15);color:#60a5fa;border:1px solid rgba(59,130,246,.3)}
  #reactor-diagram .badge-queue{background:rgba(16,185,129,.15);color:#34d399;border:1px solid rgba(16,185,129,.3)}
  #reactor-diagram .badge-lock{background:rgba(245,158,11,.15);color:#fbbf24;border:1px solid rgba(245,158,11,.3)}
  #reactor-diagram .flow-dot{width:6px;height:6px;border-radius:50%;background:#3b82f6;position:absolute;animation:flow-down 2s infinite}
  #reactor-diagram .reactor-internals{display:flex;flex-wrap:wrap;gap:4px;margin-top:8px;justify-content:center}
  #reactor-diagram .clients-grid{display:flex;gap:6px;justify-content:center;flex-wrap:wrap;margin-bottom:8px}
  #reactor-diagram .client-dot{width:36px;height:36px;border-radius:50%;background:linear-gradient(135deg,#2563eb,#1d4ed8);border:2px solid #3b82f6;display:flex;align-items:center;justify-content:center;font-size:10px;font-weight:700;color:#bfdbfe;transition:transform .2s}
  #reactor-diagram .client-dot:hover{transform:scale(1.15)}
  @keyframes pulse-arrow{0%,100%{opacity:.4}50%{opacity:1}}
  @keyframes flow-down{0%{top:0;opacity:0}50%{opacity:1}100%{top:100%;opacity:0}}
  @media(max-width:640px){
    #reactor-diagram .connector-fan{flex-direction:column;align-items:center}
    #reactor-diagram .node-reactor{width:100%}
  }
</style>

<!-- Kernel Space -->
<div class="zone zone-kernel">
  <div class="zone-label">Kernel Space</div>
  <div class="clients-grid">
    <div class="client-dot">C1</div>
    <div class="client-dot">C2</div>
    <div class="client-dot">C3</div>
    <div class="client-dot">C4</div>
    <div class="client-dot">C5</div>
    <div class="client-dot" style="opacity:.5">...</div>
  </div>
  <div style="text-align:center;margin-top:4px">
    <span class="connector-arrow">TCP</span>
    <span style="color:#4a5e7a;font-size:12px"> &darr; </span>
  </div>
  <div class="node node-client" style="max-width:320px;margin:8px auto 0">
    <div class="node-title">Listening Socket</div>
    <div class="node-sub">bind + listen &bull; backlog queue</div>
  </div>
</div>

<!-- Connector -->
<div class="connector"><div class="connector-line" style="position:relative"><div class="flow-dot"></div></div></div>

<!-- User Space -->
<div class="zone zone-user">
  <div class="zone-label">User Space</div>

  <!-- Acceptor -->
  <div class="node node-acceptor" style="max-width:480px;margin:0 auto 16px">
    <div class="node-title">Acceptor Thread</div>
    <div class="node-sub">Single SQE &rarr; multishot accept &rarr; one CQE per connection</div>
    <div style="margin-top:8px">
      <span class="badge badge-uring">io_uring</span>
      <span class="badge badge-lock">TCP_NODELAY</span>
    </div>
  </div>

  <!-- Round Robin label -->
  <div style="text-align:center;padding:4px 0">
    <span class="connector-arrow-rr">round-robin fd distribution</span>
    <div style="display:flex;justify-content:center;gap:60px;margin-top:6px">
      <span class="connector-arrow">&darr;</span>
      <span class="connector-arrow" style="animation-delay:.3s">&darr;</span>
      <span class="connector-arrow" style="animation-delay:.6s">&darr;</span>
    </div>
    <div style="display:flex;justify-content:center;gap:30px;margin-top:2px">
      <span class="badge badge-queue">ConcurrentQueue</span>
      <span class="badge badge-queue">ConcurrentQueue</span>
      <span class="badge badge-queue">ConcurrentQueue</span>
    </div>
  </div>

  <!-- Reactors -->
  <div class="connector-fan" style="margin-top:12px">
    <div class="node node-reactor">
      <div class="node-title">Reactor 0</div>
      <div class="reactor-internals">
        <span class="badge badge-uring">io_uring</span>
        <span class="badge badge-buf">buf_ring</span>
        <span class="badge badge-queue">SPSC ring</span>
      </div>
      <div class="reactor-internals">
        <span class="badge badge-lock">conn_map</span>
        <span class="badge badge-queue">flush_Q</span>
      </div>
      <div class="node-sub" style="margin-top:6px">multishot recv + send</div>
    </div>
    <div class="node node-reactor">
      <div class="node-title">Reactor 1</div>
      <div class="reactor-internals">
        <span class="badge badge-uring">io_uring</span>
        <span class="badge badge-buf">buf_ring</span>
        <span class="badge badge-queue">SPSC ring</span>
      </div>
      <div class="reactor-internals">
        <span class="badge badge-lock">conn_map</span>
        <span class="badge badge-queue">flush_Q</span>
      </div>
      <div class="node-sub" style="margin-top:6px">multishot recv + send</div>
    </div>
    <div class="node node-reactor">
      <div class="node-title">Reactor N</div>
      <div class="reactor-internals">
        <span class="badge badge-uring">io_uring</span>
        <span class="badge badge-buf">buf_ring</span>
        <span class="badge badge-queue">SPSC ring</span>
      </div>
      <div class="reactor-internals">
        <span class="badge badge-lock">conn_map</span>
        <span class="badge badge-queue">flush_Q</span>
      </div>
      <div class="node-sub" style="margin-top:6px">multishot recv + send</div>
    </div>
  </div>

  <!-- Channel -->
  <div class="connector"><div class="connector-line" style="position:relative"><div class="flow-dot" style="animation-delay:.5s"></div></div></div>
  <div style="text-align:center">
    <span class="badge badge-queue" style="font-size:12px;padding:4px 14px">Channel&lt;ConnectionItem&gt;</span>
  </div>
  <div class="connector"><div class="connector-line" style="position:relative"><div class="flow-dot" style="animation-delay:1s"></div></div></div>

  <!-- Application -->
  <div class="node node-app" style="max-width:480px;margin:0 auto">
    <div class="node-title">Application Handlers</div>
    <div class="node-sub">Engine.AcceptAsync() &rarr; ReadAsync &harr; Write + FlushAsync</div>
  </div>
</div>
</div>

Every thread in the system owns its own `io_uring` instance. There is no shared ring, and no lock contention on the I/O path.

## Acceptor Thread

The acceptor is responsible for one job: accepting new TCP connections.

1. Creates a listening socket (IPv4 or IPv6 dual-stack, configurable via `IPVersion`)
2. Binds and listens with the configured `Backlog`
3. Sets up its own `io_uring` and arms a **multishot accept** SQE
4. Enters an event loop that:
   - Peeks a batch of CQEs (accepted file descriptors)
   - Sets `TCP_NODELAY` on each accepted socket
   - Distributes fds to reactors in round-robin order via lock-free `ConcurrentQueue<int>` (one per reactor)
   - Sleeps in `io_uring_wait_cqes()` when idle

Multishot accept means a single submission produces a CQE for every incoming connection without re-arming. The acceptor never allocates per-connection -- it just hands off integer file descriptors.

### Acceptor Event Loop

```
loop:
    cqeCount = peek_batch_cqe(ring, cqes, batchSize)
    if cqeCount == 0:
        submit_and_wait_timeout(ring, timeout)
        continue

    for each cqe in cqes:
        if cqe.res < 0:
            log error, continue
        clientFd = cqe.res
        setsockopt(clientFd, TCP_NODELAY)
        reactorQueues[nextReactor++ % reactorCount].Enqueue(clientFd)

    cq_advance(ring, cqeCount)
```

## Reactor Threads

Each reactor thread owns:

- Its own `io_uring` instance (created with `SINGLE_ISSUER | DEFER_TASKRUN` by default)
- A **buffer ring** for zero-copy receive operations
- A `Dictionary<int, Connection>` mapping file descriptors to connection objects
- Lock-free queues for receiving new fds from the acceptor and flush requests from handlers

### Reactor Event Loop

Each reactor runs a tight loop:

```
loop:
    // 1. Drain newly accepted connections
    while reactorQueue.TryDequeue(out clientFd):
        connection = pool.Get() or new Connection()
        connection.SetFd(clientFd).SetReactor(this)
        connections[clientFd] = connection
        arm multishot_recv_select(clientFd, bufferGroupId)
        notify application via Channel

    // 2. Drain buffer returns
    while returnQ.TryDequeue(out bufferId):
        buf_ring_add(bufferRing, slab + bufferId * bufSize, bufSize, bufferId, mask, idx++)
    buf_ring_advance(bufferRing, returnCount)

    // 3. Drain flush requests
    while flushQ.TryDequeue(out flushFd):
        connection = connections[flushFd]
        prep_send(sqe, flushFd, connection.WriteBuffer, connection.WriteInFlight, 0)
        submit pending sends

    // 4. Process completions
    cqeCount = peek_batch_cqe(ring, cqes, batchSize)
    for each cqe:
        kind = UdKindOf(cqe.user_data)
        fd   = UdFdOf(cqe.user_data)

        if kind == Recv:
            if cqe.res <= 0: close connection, return buffer
            else: enqueue RingItem to connection, wake handler

        if kind == Send:
            advance WriteHead, resubmit if partial, signal flush complete

        if kind == Cancel:
            handle cancellation completion

    cq_advance(ring, cqeCount)
    submit_and_wait_timeout(ring, timeout)
```

## Connection Distribution

The acceptor distributes connections using a simple round-robin counter:

```
reactorIndex = acceptCount++ % reactorCount
```

Each reactor gets approximately equal load. The distribution is via `ConcurrentQueue<int>` -- one queue per reactor -- so the acceptor never blocks waiting for a reactor.

## Application Integration

After a reactor registers a new connection, it pushes a `ConnectionItem` (reactor ID + client fd) into an unbounded `Channel<ConnectionItem>`. The `Engine.AcceptAsync()` method reads from this channel, returning fully-registered `Connection` objects to the application.

This means by the time your handler receives a connection:
- The connection is already assigned to a reactor
- Multishot recv is already armed
- The buffer ring is ready to receive data
- You can immediately call `ReadAsync()`
