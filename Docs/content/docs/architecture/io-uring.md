---
title: io_uring
weight: 2
---

`io_uring` is a Linux kernel interface for asynchronous I/O. zerg uses it as its sole I/O mechanism -- there are no `epoll`, `kqueue`, or `libuv` fallbacks.

<style>
  #iouring-guide { --bg: #0b0d11; --bg2: #12151c; --bg3: #181c25; --bg4: #1e2330; --border: #2a3040; --text: #e0e4ef; --text2: #8a90a8; --muted: #555c73; --blue: #4a90ff; --cyan: #3ee8d0; --green: #3ee88a; --orange: #ff8a3e; --red: #ff4a5e; --purple: #a06aff; --yellow: #ffd04a; }
  #iouring-guide * { box-sizing: border-box; }
  #iouring-guide .iu-section { margin: 3rem 0; }
  #iouring-guide .iu-stag { display:inline-block; padding:.2rem .65rem; border-radius:5px; font-size:.7rem; font-weight:700; text-transform:uppercase; letter-spacing:1.2px; margin-bottom:.75rem; }
  #iouring-guide .iu-stag-blue { background:rgba(74,144,255,.12); color:var(--blue); border:1px solid rgba(74,144,255,.2); }
  #iouring-guide .iu-stag-cyan { background:rgba(62,232,208,.1); color:var(--cyan); border:1px solid rgba(62,232,208,.2); }
  #iouring-guide .iu-stag-green { background:rgba(62,232,138,.1); color:var(--green); border:1px solid rgba(62,232,138,.2); }
  #iouring-guide .iu-stag-orange { background:rgba(255,138,62,.1); color:var(--orange); border:1px solid rgba(255,138,62,.2); }
  #iouring-guide .iu-stag-purple { background:rgba(160,106,255,.1); color:var(--purple); border:1px solid rgba(160,106,255,.2); }
  #iouring-guide .iu-lead { color:var(--text2); font-size:1.05rem; margin-bottom:2rem; max-width:650px; }
  #iouring-guide .ring-svg { width:100%; height:auto; display:block; }
  #iouring-guide .svg-text { font-family:'Inter',system-ui,sans-serif; fill:var(--text); }
  #iouring-guide .svg-mono { font-family:'JetBrains Mono',ui-monospace,monospace; fill:var(--text2); }
  #iouring-guide .iu-cards { display:grid; grid-template-columns:repeat(auto-fit,minmax(220px,1fr)); gap:1rem; margin-top:1.5rem; }
  #iouring-guide .iu-card { background:var(--bg3); border:1px solid var(--border); border-radius:12px; padding:1.25rem; transition:border-color .3s; }
  #iouring-guide .iu-card:hover { border-color:rgba(62,232,208,.3); }
  #iouring-guide .iu-card h4 { font-size:.9rem; font-weight:700; margin-bottom:.35rem; }
  #iouring-guide .iu-card p { font-size:.82rem; color:var(--text2); line-height:1.55; }
  #iouring-guide .iu-card code { font-family:'JetBrains Mono',ui-monospace,monospace; font-size:.78rem; background:var(--bg); padding:.1rem .35rem; border-radius:4px; color:var(--cyan); }
  #iouring-guide .iu-two-col { display:grid; grid-template-columns:1fr 1fr; gap:2rem; align-items:start; }
  @media (max-width:800px) { #iouring-guide .iu-two-col { grid-template-columns:1fr; } }
  #iouring-guide .iu-divider { width:100%; height:1px; background:linear-gradient(90deg,transparent,var(--border),transparent); margin:2.5rem 0; }
  /* Stepper */
  #iouring-guide .stepper { display:flex; align-items:center; gap:1rem; justify-content:center; margin:1.5rem 0 1rem; }
  #iouring-guide .step-btn { width:40px; height:40px; border-radius:50%; border:2px solid var(--border); background:var(--bg3); color:var(--text); font-size:1rem; cursor:pointer; display:flex; align-items:center; justify-content:center; transition:all .2s; }
  #iouring-guide .step-btn:hover { border-color:var(--cyan); color:var(--cyan); }
  #iouring-guide .step-btn:disabled { opacity:.3; cursor:default; }
  #iouring-guide .step-dots { display:flex; gap:.4rem; }
  #iouring-guide .step-dot { width:10px; height:10px; border-radius:50%; background:var(--bg4); border:2px solid var(--border); transition:all .3s; cursor:pointer; }
  #iouring-guide .step-dot.active { background:var(--cyan); border-color:var(--cyan); box-shadow:0 0 8px rgba(62,232,208,.4); }
  #iouring-guide .step-dot.done { background:var(--muted); border-color:var(--muted); }
  #iouring-guide .step-desc { background:var(--bg3); border:1px solid var(--border); border-radius:12px; padding:1.25rem 1.5rem; margin-bottom:1rem; min-height:70px; }
  #iouring-guide .step-desc h4 { font-size:.95rem; font-weight:700; margin-bottom:.35rem; }
  #iouring-guide .step-desc p { font-size:.88rem; color:var(--text2); line-height:1.6; }
  #iouring-guide .step-desc code { font-family:'JetBrains Mono',ui-monospace,monospace; font-size:.78rem; background:var(--bg); padding:.1rem .35rem; border-radius:4px; color:var(--cyan); }
  /* Step elements */
  #iouring-guide .step-el { opacity:0; transition:opacity .5s,transform .5s; transform:translateY(8px); pointer-events:none; }
  #iouring-guide .step-el.show { opacity:1; transform:translateY(0); pointer-events:auto; }
  /* Full flow panels */
  #iouring-guide .ff-panel { animation:iuFadeIn .45s ease both; }
  @keyframes iuFadeIn { from{opacity:0;transform:translateY(16px)} to{opacity:1;transform:translateY(0)} }
  /* Phase bar */
  #iouring-guide .phase-bar { display:flex; gap:2px; margin-bottom:1.5rem; border-radius:8px; overflow:hidden; }
  #iouring-guide .phase-seg { flex:1; height:6px; background:var(--bg4); transition:background .4s; }
  /* Codeblock */
  #iouring-guide .iu-codeblock { background:var(--bg); border:1px solid var(--border); border-radius:10px; overflow:hidden; }
  #iouring-guide .iu-codeblock-head { padding:.5rem 1rem; background:rgba(255,255,255,.02); border-bottom:1px solid var(--border); font-size:.75rem; color:var(--muted); font-family:'JetBrains Mono',ui-monospace,monospace; }
  #iouring-guide .iu-codeblock pre { padding:1rem 1.25rem; font-family:'JetBrains Mono',ui-monospace,monospace; font-size:.8rem; line-height:1.7; overflow-x:auto; color:var(--text); margin:0; }
  #iouring-guide .kw { color:#c792ea; } #iouring-guide .ty { color:#82aaff; } #iouring-guide .fn { color:#ffd700; } #iouring-guide .cm { color:#546e7a; font-style:italic; } #iouring-guide .num { color:#f78c6c; } #iouring-guide .op { color:#89ddff; }
</style>

<div id="iouring-guide">

<!-- ═══════════════ SECTION 1: THE RING MODEL ═══════════════ -->
<div class="iu-section">
<div class="iu-stag iu-stag-blue">The Ring Model</div>

### How io_uring Works

<p class="iu-lead">io_uring uses two lock-free ring buffers shared between userspace and the kernel. Your app writes SQEs (requests), the kernel writes CQEs (results). No syscall needed for submission.</p>

<svg viewBox="0 0 900 520" class="ring-svg" xmlns="http://www.w3.org/2000/svg">
  <defs>
    <linearGradient id="gSQ" x1="0" y1="0" x2="1" y2="1"><stop offset="0%" stop-color="#4a90ff"/><stop offset="100%" stop-color="#3e6aff"/></linearGradient>
    <linearGradient id="gCQ" x1="0" y1="0" x2="1" y2="1"><stop offset="0%" stop-color="#3ee88a"/><stop offset="100%" stop-color="#2ab870"/></linearGradient>
    <linearGradient id="gK" x1="0" y1="0" x2="1" y2="1"><stop offset="0%" stop-color="#ff8a3e"/><stop offset="100%" stop-color="#e06020"/></linearGradient>
    <marker id="arrowB" markerWidth="8" markerHeight="6" refX="8" refY="3" orient="auto"><polygon points="0 0, 8 3, 0 6" fill="#4a90ff"/></marker>
    <marker id="arrowG" markerWidth="8" markerHeight="6" refX="8" refY="3" orient="auto"><polygon points="0 0, 8 3, 0 6" fill="#3ee88a"/></marker>
    <marker id="arrowO" markerWidth="8" markerHeight="6" refX="8" refY="3" orient="auto"><polygon points="0 0, 8 3, 0 6" fill="#ff8a3e"/></marker>
    <filter id="glowB"><feDropShadow dx="0" dy="0" stdDeviation="4" flood-color="#4a90ff" flood-opacity=".5"/></filter>
    <filter id="glowG"><feDropShadow dx="0" dy="0" stdDeviation="4" flood-color="#3ee88a" flood-opacity=".5"/></filter>
  </defs>
  <!-- USERSPACE BOX -->
  <rect x="30" y="20" width="840" height="180" rx="14" fill="none" stroke="#4a90ff" stroke-width="1.5" stroke-dasharray="6 4" opacity=".3"/>
  <text x="55" y="48" class="svg-text" font-size="13" font-weight="700" fill="#4a90ff" opacity=".6">USERSPACE (your C# app)</text>
  <!-- KERNEL BOX -->
  <rect x="30" y="310" width="840" height="190" rx="14" fill="none" stroke="#ff8a3e" stroke-width="1.5" stroke-dasharray="6 4" opacity=".3"/>
  <text x="55" y="340" class="svg-text" font-size="13" font-weight="700" fill="#ff8a3e" opacity=".6">KERNEL</text>
  <!-- SHARED MEMORY BAND -->
  <rect x="30" y="210" width="840" height="90" rx="10" fill="rgba(255,255,255,.02)" stroke="#2a3040" stroke-width="1"/>
  <text x="450" y="262" class="svg-text" font-size="12" font-weight="700" fill="#555c73" text-anchor="middle">SHARED MEMORY (mmap'd)</text>
  <!-- SQ RING -->
  <g transform="translate(150, 220)">
    <rect x="0" y="0" width="220" height="70" rx="10" fill="rgba(74,144,255,.08)" stroke="#4a90ff" stroke-width="2" filter="url(#glowB)"/>
    <text x="110" y="25" class="svg-text" font-size="14" font-weight="800" fill="#4a90ff" text-anchor="middle">Submission Queue</text>
    <rect x="12" y="36" width="28" height="24" rx="4" fill="#4a90ff" opacity=".8"/><text x="26" y="52" class="svg-mono" font-size="9" fill="#fff" text-anchor="middle">SQE</text>
    <rect x="44" y="36" width="28" height="24" rx="4" fill="#4a90ff" opacity=".6"/><text x="58" y="52" class="svg-mono" font-size="9" fill="#fff" text-anchor="middle">SQE</text>
    <rect x="76" y="36" width="28" height="24" rx="4" fill="#4a90ff" opacity=".4"/><text x="90" y="52" class="svg-mono" font-size="9" fill="#fff" text-anchor="middle">SQE</text>
    <rect x="108" y="36" width="28" height="24" rx="4" fill="rgba(74,144,255,.15)" stroke="#4a90ff" stroke-width="1" stroke-dasharray="3 2"/>
    <rect x="140" y="36" width="28" height="24" rx="4" fill="rgba(74,144,255,.15)" stroke="#4a90ff" stroke-width="1" stroke-dasharray="3 2"/>
    <rect x="172" y="36" width="28" height="24" rx="4" fill="rgba(74,144,255,.15)" stroke="#4a90ff" stroke-width="1" stroke-dasharray="3 2"/>
  </g>
  <!-- CQ RING -->
  <g transform="translate(530, 220)">
    <rect x="0" y="0" width="220" height="70" rx="10" fill="rgba(62,232,138,.08)" stroke="#3ee88a" stroke-width="2" filter="url(#glowG)"/>
    <text x="110" y="25" class="svg-text" font-size="14" font-weight="800" fill="#3ee88a" text-anchor="middle">Completion Queue</text>
    <rect x="12" y="36" width="28" height="24" rx="4" fill="#3ee88a" opacity=".8"/><text x="26" y="52" class="svg-mono" font-size="9" fill="#0a0a0f" text-anchor="middle">CQE</text>
    <rect x="44" y="36" width="28" height="24" rx="4" fill="#3ee88a" opacity=".6"/><text x="58" y="52" class="svg-mono" font-size="9" fill="#0a0a0f" text-anchor="middle">CQE</text>
    <rect x="76" y="36" width="28" height="24" rx="4" fill="rgba(62,232,138,.15)" stroke="#3ee88a" stroke-width="1" stroke-dasharray="3 2"/>
    <rect x="108" y="36" width="28" height="24" rx="4" fill="rgba(62,232,138,.15)" stroke="#3ee88a" stroke-width="1" stroke-dasharray="3 2"/>
    <rect x="140" y="36" width="28" height="24" rx="4" fill="rgba(62,232,138,.15)" stroke="#3ee88a" stroke-width="1" stroke-dasharray="3 2"/>
    <rect x="172" y="36" width="28" height="24" rx="4" fill="rgba(62,232,138,.15)" stroke="#3ee88a" stroke-width="1" stroke-dasharray="3 2"/>
  </g>
  <!-- ARROWS -->
  <line x1="260" y1="150" x2="260" y2="215" stroke="#4a90ff" stroke-width="2" marker-end="url(#arrowB)" opacity=".7"/>
  <text x="278" y="185" class="svg-mono" font-size="10" fill="#4a90ff">write SQEs</text>
  <line x1="260" y1="295" x2="260" y2="360" stroke="#ff8a3e" stroke-width="2" marker-end="url(#arrowO)" opacity=".7"/>
  <text x="178" y="335" class="svg-mono" font-size="10" fill="#ff8a3e">kernel reads SQ</text>
  <line x1="640" y1="360" x2="640" y2="295" stroke="#3ee88a" stroke-width="2" marker-end="url(#arrowG)" opacity=".7"/>
  <text x="655" y="335" class="svg-mono" font-size="10" fill="#3ee88a">kernel writes CQ</text>
  <line x1="640" y1="215" x2="640" y2="150" stroke="#3ee88a" stroke-width="2" marker-end="url(#arrowG)" opacity=".7"/>
  <text x="655" y="185" class="svg-mono" font-size="10" fill="#3ee88a">read CQEs</text>
  <!-- Kernel processing -->
  <rect x="350" y="370" width="200" height="60" rx="10" fill="rgba(255,138,62,.08)" stroke="#ff8a3e" stroke-width="1.5"/>
  <text x="450" y="395" class="svg-text" font-size="12" font-weight="700" fill="#ff8a3e" text-anchor="middle">I/O Processing</text>
  <text x="450" y="415" class="svg-mono" font-size="10" fill="#8a90a8" text-anchor="middle">accept / recv / send</text>
  <!-- App boxes -->
  <rect x="120" y="70" width="180" height="65" rx="10" fill="#181c25" stroke="#2a3040" stroke-width="1"/>
  <text x="210" y="95" class="svg-text" font-size="12" font-weight="700" text-anchor="middle">Your Code</text>
  <text x="210" y="115" class="svg-mono" font-size="10" fill="#8a90a8" text-anchor="middle">prep_recv, prep_send...</text>
  <rect x="570" y="70" width="200" height="65" rx="10" fill="#181c25" stroke="#2a3040" stroke-width="1"/>
  <text x="670" y="95" class="svg-text" font-size="12" font-weight="700" text-anchor="middle">Your Handler</text>
  <text x="670" y="115" class="svg-mono" font-size="10" fill="#8a90a8" text-anchor="middle">process results, route...</text>
  <!-- SQE STRUCTURE -->
  <g transform="translate(60,445)">
    <text x="0" y="0" class="svg-text" font-size="11" font-weight="700" fill="#4a90ff">SQE Structure (Submission)</text>
    <g transform="translate(0,12)">
      <rect x="0" y="0" width="80" height="22" rx="3" fill="rgba(74,144,255,.12)" stroke="#4a90ff" stroke-width="1"/>
      <text x="40" y="15" class="svg-mono" font-size="9" fill="#4a90ff" text-anchor="middle">opcode</text>
      <rect x="84" y="0" width="60" height="22" rx="3" fill="rgba(74,144,255,.12)" stroke="#4a90ff" stroke-width="1"/>
      <text x="114" y="15" class="svg-mono" font-size="9" fill="#4a90ff" text-anchor="middle">fd</text>
      <rect x="148" y="0" width="70" height="22" rx="3" fill="rgba(74,144,255,.12)" stroke="#4a90ff" stroke-width="1"/>
      <text x="183" y="15" class="svg-mono" font-size="9" fill="#4a90ff" text-anchor="middle">buf/len</text>
      <rect x="222" y="0" width="100" height="22" rx="3" fill="rgba(74,144,255,.12)" stroke="#4a90ff" stroke-width="1"/>
      <text x="272" y="15" class="svg-mono" font-size="9" fill="#4a90ff" text-anchor="middle">user_data</text>
      <rect x="326" y="0" width="60" height="22" rx="3" fill="rgba(74,144,255,.12)" stroke="#4a90ff" stroke-width="1"/>
      <text x="356" y="15" class="svg-mono" font-size="9" fill="#4a90ff" text-anchor="middle">flags</text>
    </g>
  </g>
  <!-- CQE STRUCTURE -->
  <g transform="translate(510,445)">
    <text x="0" y="0" class="svg-text" font-size="11" font-weight="700" fill="#3ee88a">CQE Structure (Completion)</text>
    <g transform="translate(0,12)">
      <rect x="0" y="0" width="100" height="22" rx="3" fill="rgba(62,232,138,.12)" stroke="#3ee88a" stroke-width="1"/>
      <text x="50" y="15" class="svg-mono" font-size="9" fill="#3ee88a" text-anchor="middle">user_data</text>
      <rect x="104" y="0" width="70" height="22" rx="3" fill="rgba(62,232,138,.12)" stroke="#3ee88a" stroke-width="1"/>
      <text x="139" y="15" class="svg-mono" font-size="9" fill="#3ee88a" text-anchor="middle">res</text>
      <rect x="178" y="0" width="100" height="22" rx="3" fill="rgba(62,232,138,.12)" stroke="#3ee88a" stroke-width="1"/>
      <text x="228" y="15" class="svg-mono" font-size="9" fill="#3ee88a" text-anchor="middle">flags</text>
    </g>
  </g>
</svg>

<div class="iu-cards">
  <div class="iu-card">
    <h4 style="color:#4a90ff;">SQE — Submission Queue Entry</h4>
    <p><code>opcode</code> = what to do (recv, send, accept)<br>
       <code>fd</code> = which socket<br>
       <code>user_data</code> = your 64-bit tag (returned in CQE)<br>
       <code>flags</code> = BUFFER_SELECT, etc.</p>
  </div>
  <div class="iu-card">
    <h4 style="color:#3ee88a;">CQE — Completion Queue Entry</h4>
    <p><code>user_data</code> = your original tag (identifies the op)<br>
       <code>res</code> = result (bytes transferred, new fd, or -errno)<br>
       <code>flags</code> = MORE, BUFFER (contains buffer_id)</p>
  </div>
  <div class="iu-card">
    <h4 style="color:#ff8a3e;">Shared Memory</h4>
    <p>Both rings are mmap'd. The kernel and your app write to them directly. No copy,
       no syscall for enqueue. Only <code>io_uring_enter()</code> needed to wake the kernel.</p>
  </div>
</div>
</div>

<!-- ═══════════════ SECTION 2: I/O LIFECYCLE ═══════════════ -->
<div class="iu-section">
<div class="iu-stag iu-stag-cyan">Interactive</div>

### The I/O Lifecycle

<p class="iu-lead">Step through the exact sequence: your app queues an SQE, the kernel processes it, and you read the CQE result. All in shared memory.</p>

<div id="lifecycleScene">
  <svg viewBox="0 0 900 400" class="ring-svg">
    <rect x="20" y="10" width="860" height="140" rx="12" fill="none" stroke="#4a90ff" stroke-width="1" stroke-dasharray="4 3" opacity=".2"/>
    <text x="40" y="34" class="svg-text" font-size="11" font-weight="700" fill="#4a90ff" opacity=".5">USERSPACE</text>
    <line x1="20" y1="195" x2="880" y2="195" stroke="#2a3040" stroke-width="1" stroke-dasharray="6 4"/>
    <text x="435" y="190" class="svg-text" font-size="10" font-weight="700" fill="#555c73" text-anchor="middle">━━ kernel boundary ━━</text>
    <rect x="20" y="210" width="860" height="180" rx="12" fill="none" stroke="#ff8a3e" stroke-width="1" stroke-dasharray="4 3" opacity=".2"/>
    <text x="40" y="234" class="svg-text" font-size="11" font-weight="700" fill="#ff8a3e" opacity=".5">KERNEL</text>
    <!-- STEP 1 -->
    <g id="lc-s1" class="step-el">
      <rect x="80" y="55" width="160" height="55" rx="10" fill="#181c25" stroke="#4a90ff" stroke-width="2"/>
      <text x="160" y="80" class="svg-text" font-size="12" font-weight="700" fill="#4a90ff" text-anchor="middle">get_sqe()</text>
      <text x="160" y="96" class="svg-mono" font-size="9" text-anchor="middle">grab empty SQE slot</text>
    </g>
    <!-- STEP 2 -->
    <g id="lc-s2" class="step-el">
      <rect x="280" y="55" width="180" height="55" rx="10" fill="#181c25" stroke="#4a90ff" stroke-width="2"/>
      <text x="370" y="80" class="svg-text" font-size="12" font-weight="700" fill="#4a90ff" text-anchor="middle">prep_recv(sqe, fd)</text>
      <text x="370" y="96" class="svg-mono" font-size="9" text-anchor="middle">fill opcode + fd + flags</text>
      <line x1="242" y1="82" x2="276" y2="82" stroke="#4a90ff" stroke-width="1.5" marker-end="url(#arrowB)" opacity=".6"/>
    </g>
    <!-- STEP 3 -->
    <g id="lc-s3" class="step-el">
      <rect x="500" y="55" width="190" height="55" rx="10" fill="#181c25" stroke="#4a90ff" stroke-width="2"/>
      <text x="595" y="80" class="svg-text" font-size="12" font-weight="700" fill="#4a90ff" text-anchor="middle">set_data64(sqe, tag)</text>
      <text x="595" y="96" class="svg-mono" font-size="9" text-anchor="middle">attach your 64-bit token</text>
      <line x1="462" y1="82" x2="496" y2="82" stroke="#4a90ff" stroke-width="1.5" marker-end="url(#arrowB)" opacity=".6"/>
    </g>
    <!-- STEP 4 -->
    <g id="lc-s4" class="step-el">
      <rect x="730" y="55" width="120" height="55" rx="10" fill="#181c25" stroke="#3ee8d0" stroke-width="2"/>
      <text x="790" y="80" class="svg-text" font-size="12" font-weight="700" fill="#3ee8d0" text-anchor="middle">submit()</text>
      <text x="790" y="96" class="svg-mono" font-size="9" text-anchor="middle">io_uring_enter</text>
      <line x1="692" y1="82" x2="726" y2="82" stroke="#4a90ff" stroke-width="1.5" marker-end="url(#arrowB)" opacity=".6"/>
      <line x1="790" y1="115" x2="790" y2="230" stroke="#ff8a3e" stroke-width="2" marker-end="url(#arrowO)" opacity=".6" stroke-dasharray="6 4"/>
    </g>
    <!-- STEP 5 -->
    <g id="lc-s5" class="step-el">
      <rect x="620" y="250" width="220" height="55" rx="10" fill="rgba(255,138,62,.08)" stroke="#ff8a3e" stroke-width="2"/>
      <text x="730" y="275" class="svg-text" font-size="12" font-weight="700" fill="#ff8a3e" text-anchor="middle">Kernel processes I/O</text>
      <text x="730" y="291" class="svg-mono" font-size="9" text-anchor="middle">recv(fd) → data into buffer</text>
    </g>
    <!-- STEP 6 -->
    <g id="lc-s6" class="step-el">
      <rect x="320" y="250" width="220" height="55" rx="10" fill="rgba(62,232,138,.08)" stroke="#3ee88a" stroke-width="2"/>
      <text x="430" y="275" class="svg-text" font-size="12" font-weight="700" fill="#3ee88a" text-anchor="middle">CQE written to CQ</text>
      <text x="430" y="291" class="svg-mono" font-size="9" text-anchor="middle">user_data + res + flags</text>
      <line x1="616" y1="277" x2="544" y2="277" stroke="#3ee88a" stroke-width="1.5" marker-end="url(#arrowG)" opacity=".6"/>
      <line x1="430" y1="245" x2="430" y2="140" stroke="#3ee88a" stroke-width="2" marker-end="url(#arrowG)" opacity=".6" stroke-dasharray="6 4"/>
    </g>
    <!-- STEP 7 -->
    <g id="lc-s7" class="step-el">
      <rect x="80" y="250" width="180" height="55" rx="10" fill="rgba(62,232,138,.08)" stroke="#3ee88a" stroke-width="2"/>
      <text x="170" y="275" class="svg-text" font-size="12" font-weight="700" fill="#3ee88a" text-anchor="middle">App reads CQE</text>
      <text x="170" y="291" class="svg-mono" font-size="9" text-anchor="middle">dispatch by user_data</text>
      <line x1="316" y1="277" x2="264" y2="277" stroke="#3ee88a" stroke-width="1.5" marker-end="url(#arrowG)" opacity=".6"/>
    </g>
    <!-- STEP 8 -->
    <g id="lc-s8" class="step-el">
      <rect x="80" y="330" width="180" height="50" rx="10" fill="#181c25" stroke="#555c73" stroke-width="1.5"/>
      <text x="170" y="354" class="svg-text" font-size="12" font-weight="700" fill="#8a90a8" text-anchor="middle">cq_advance(count)</text>
      <text x="170" y="370" class="svg-mono" font-size="9" fill="#555c73" text-anchor="middle">mark CQEs consumed</text>
    </g>
  </svg>
</div>

<div class="stepper">
  <button class="step-btn" id="lcPrev" onclick="lcStep(-1)">&#9664;</button>
  <div class="step-dots" id="lcDots"></div>
  <button class="step-btn" id="lcNext" onclick="lcStep(1)">&#9654;</button>
</div>
<div class="step-desc" id="lcDesc"></div>

<div class="iu-codeblock" style="max-width:700px;margin:0 auto;">
  <div class="iu-codeblock-head">zerg — single syscall pattern</div>
  <pre><span class="cm">// 1. Queue work (no syscall)</span>
<span class="ty">io_uring_sqe</span>* sqe = <span class="fn">shim_get_sqe</span>(ring);
<span class="fn">shim_prep_recv_multishot_select</span>(sqe, fd, bgid, <span class="num">0</span>);
<span class="fn">shim_sqe_set_data64</span>(sqe, <span class="fn">PackUd</span>(<span class="ty">UdKind</span>.Recv, fd));

<span class="cm">// 2. Submit + wait in ONE syscall</span>
<span class="fn">shim_submit_and_wait_timeout</span>(ring, &amp;cqes, <span class="num">1</span>, &amp;ts);

<span class="cm">// 3. Batch-read completions (no syscall)</span>
<span class="kw">int</span> got = <span class="fn">shim_peek_batch_cqe</span>(ring, cqes, batchSize);

<span class="cm">// 4. Process results</span>
<span class="kw">for</span> (<span class="kw">int</span> i = <span class="num">0</span>; i &lt; got; i++) {
    <span class="ty">UdKind</span> kind = <span class="fn">UdKindOf</span>(<span class="fn">shim_cqe_get_data64</span>(cqes[i]));
    <span class="kw">int</span> res = cqes[i]-&gt;res;
    <span class="cm">// dispatch...</span>
}
<span class="fn">shim_cq_advance</span>(ring, (<span class="ty">uint</span>)got); <span class="cm">// 5. Mark consumed</span></pre>
</div>
</div>

<!-- ═══════════════ SECTION 3: MULTISHOT ═══════════════ -->
<div class="iu-section">
<div class="iu-stag iu-stag-green">Key Feature</div>

### Multishot Operations

<p class="iu-lead">Traditional I/O: 1 SQE → 1 CQE. Multishot: 1 SQE → many CQEs. The kernel keeps producing completions until an error or you cancel.</p>

<div class="iu-two-col">
  <div>
    <h4 style="font-weight:700;margin-bottom:1rem;">Traditional (one-shot)</h4>
    <svg viewBox="0 0 420 280" class="ring-svg">
      <text x="10" y="20" class="svg-text" font-size="11" font-weight="600" fill="#555c73">Submit recv for each read</text>
      <g transform="translate(20,35)">
        <rect x="0" y="0" width="80" height="28" rx="6" fill="rgba(74,144,255,.15)" stroke="#4a90ff" stroke-width="1.5"/>
        <text x="40" y="18" class="svg-mono" font-size="9" fill="#4a90ff" text-anchor="middle">SQE recv</text>
        <line x1="84" y1="14" x2="140" y2="14" stroke="#555c73" stroke-width="1" marker-end="url(#arrowB)"/>
        <rect x="144" y="0" width="80" height="28" rx="6" fill="rgba(62,232,138,.15)" stroke="#3ee88a" stroke-width="1.5"/>
        <text x="184" y="18" class="svg-mono" font-size="9" fill="#3ee88a" text-anchor="middle">CQE data</text>
      </g>
      <g transform="translate(20,75)">
        <rect x="0" y="0" width="80" height="28" rx="6" fill="rgba(74,144,255,.15)" stroke="#4a90ff" stroke-width="1.5"/>
        <text x="40" y="18" class="svg-mono" font-size="9" fill="#4a90ff" text-anchor="middle">SQE recv</text>
        <line x1="84" y1="14" x2="140" y2="14" stroke="#555c73" stroke-width="1" marker-end="url(#arrowB)"/>
        <rect x="144" y="0" width="80" height="28" rx="6" fill="rgba(62,232,138,.15)" stroke="#3ee88a" stroke-width="1.5"/>
        <text x="184" y="18" class="svg-mono" font-size="9" fill="#3ee88a" text-anchor="middle">CQE data</text>
      </g>
      <g transform="translate(20,115)">
        <rect x="0" y="0" width="80" height="28" rx="6" fill="rgba(74,144,255,.15)" stroke="#4a90ff" stroke-width="1.5"/>
        <text x="40" y="18" class="svg-mono" font-size="9" fill="#4a90ff" text-anchor="middle">SQE recv</text>
        <line x1="84" y1="14" x2="140" y2="14" stroke="#555c73" stroke-width="1" marker-end="url(#arrowB)"/>
        <rect x="144" y="0" width="80" height="28" rx="6" fill="rgba(62,232,138,.15)" stroke="#3ee88a" stroke-width="1.5"/>
        <text x="184" y="18" class="svg-mono" font-size="9" fill="#3ee88a" text-anchor="middle">CQE data</text>
      </g>
      <g transform="translate(20,155)">
        <rect x="0" y="0" width="80" height="28" rx="6" fill="rgba(74,144,255,.15)" stroke="#4a90ff" stroke-width="1.5"/>
        <text x="40" y="18" class="svg-mono" font-size="9" fill="#4a90ff" text-anchor="middle">SQE recv</text>
        <line x1="84" y1="14" x2="140" y2="14" stroke="#555c73" stroke-width="1" marker-end="url(#arrowB)"/>
        <rect x="144" y="0" width="80" height="28" rx="6" fill="rgba(62,232,138,.15)" stroke="#3ee88a" stroke-width="1.5"/>
        <text x="184" y="18" class="svg-mono" font-size="9" fill="#3ee88a" text-anchor="middle">CQE data</text>
      </g>
      <text x="280" y="115" class="svg-text" font-size="32" font-weight="800" fill="#ff4a5e" opacity=".6">4 SQEs</text>
      <text x="280" y="140" class="svg-text" font-size="11" fill="#8a90a8">4 submissions</text>
      <text x="280" y="155" class="svg-text" font-size="11" fill="#8a90a8">4 completions</text>
      <text x="20" y="220" class="svg-text" font-size="11" font-weight="600" fill="#ff4a5e">Cost: re-arm after every read</text>
      <text x="20" y="238" class="svg-text" font-size="10" fill="#8a90a8">More SQE slots consumed</text>
      <text x="20" y="254" class="svg-text" font-size="10" fill="#8a90a8">More CPU cycles on submission</text>
    </svg>
  </div>
  <div>
    <h4 style="font-weight:700;margin-bottom:1rem;">Multishot (zerg)</h4>
    <svg viewBox="0 0 420 280" class="ring-svg">
      <text x="10" y="20" class="svg-text" font-size="11" font-weight="600" fill="#555c73">Submit once, get many completions</text>
      <g transform="translate(20,35)">
        <rect x="0" y="0" width="120" height="150" rx="10" fill="rgba(74,144,255,.08)" stroke="#4a90ff" stroke-width="2"/>
        <text x="60" y="25" class="svg-text" font-size="12" font-weight="700" fill="#4a90ff" text-anchor="middle">1 SQE</text>
        <text x="60" y="42" class="svg-mono" font-size="9" fill="#8a90a8" text-anchor="middle">recv_multishot</text>
        <line x1="124" y1="45" x2="175" y2="30" stroke="#3ee88a" stroke-width="1.5" marker-end="url(#arrowG)"/>
        <line x1="124" y1="70" x2="175" y2="70" stroke="#3ee88a" stroke-width="1.5" marker-end="url(#arrowG)"/>
        <line x1="124" y1="95" x2="175" y2="110" stroke="#3ee88a" stroke-width="1.5" marker-end="url(#arrowG)"/>
        <line x1="124" y1="120" x2="175" y2="150" stroke="#3ee88a" stroke-width="1.5" marker-end="url(#arrowG)"/>
        <rect x="180" y="16" width="90" height="28" rx="6" fill="rgba(62,232,138,.15)" stroke="#3ee88a" stroke-width="1.5"/>
        <text x="225" y="34" class="svg-mono" font-size="9" fill="#3ee88a" text-anchor="middle">CQE + MORE</text>
        <rect x="180" y="56" width="90" height="28" rx="6" fill="rgba(62,232,138,.15)" stroke="#3ee88a" stroke-width="1.5"/>
        <text x="225" y="74" class="svg-mono" font-size="9" fill="#3ee88a" text-anchor="middle">CQE + MORE</text>
        <rect x="180" y="96" width="90" height="28" rx="6" fill="rgba(62,232,138,.15)" stroke="#3ee88a" stroke-width="1.5"/>
        <text x="225" y="114" class="svg-mono" font-size="9" fill="#3ee88a" text-anchor="middle">CQE + MORE</text>
        <rect x="180" y="136" width="90" height="28" rx="6" fill="rgba(62,232,138,.15)" stroke="#3ee88a" stroke-width="1.5"/>
        <text x="225" y="154" class="svg-mono" font-size="9" fill="#3ee88a" text-anchor="middle">CQE final</text>
        <rect x="285" y="16" width="100" height="28" rx="6" fill="rgba(255,208,74,.08)" stroke="#ffd04a" stroke-width="1"/>
        <text x="335" y="34" class="svg-mono" font-size="8" fill="#ffd04a" text-anchor="middle">F_MORE = 1</text>
        <rect x="285" y="56" width="100" height="28" rx="6" fill="rgba(255,208,74,.08)" stroke="#ffd04a" stroke-width="1"/>
        <text x="335" y="74" class="svg-mono" font-size="8" fill="#ffd04a" text-anchor="middle">F_MORE = 1</text>
        <rect x="285" y="96" width="100" height="28" rx="6" fill="rgba(255,208,74,.08)" stroke="#ffd04a" stroke-width="1"/>
        <text x="335" y="114" class="svg-mono" font-size="8" fill="#ffd04a" text-anchor="middle">F_MORE = 1</text>
        <rect x="285" y="136" width="100" height="28" rx="6" fill="rgba(255,74,94,.08)" stroke="#ff4a5e" stroke-width="1"/>
        <text x="335" y="154" class="svg-mono" font-size="8" fill="#ff4a5e" text-anchor="middle">F_MORE = 0</text>
      </g>
      <text x="20" y="220" class="svg-text" font-size="11" font-weight="600" fill="#3ee88a">Win: 1 submission, N completions</text>
      <text x="20" y="238" class="svg-text" font-size="10" fill="#8a90a8">Kernel sets IORING_CQE_F_MORE on each CQE</text>
      <text x="20" y="254" class="svg-text" font-size="10" fill="#8a90a8">When MORE=0 → multishot ended, re-arm</text>
      <text x="20" y="272" class="svg-text" font-size="10" fill="#8a90a8">Used for both accept and recv in zerg</text>
    </svg>
  </div>
</div>

<div class="iu-divider"></div>

#### user_data Packing

Each SQE carries a 64-bit token so the completion handler knows what operation completed and on which socket.

<svg viewBox="0 0 800 110" class="ring-svg" style="max-width:800px;margin:1rem auto;display:block;">
  <text x="400" y="18" class="svg-text" font-size="12" font-weight="700" text-anchor="middle">64-bit user_data</text>
  <rect x="50" y="30" width="340" height="45" rx="8" fill="rgba(160,106,255,.1)" stroke="#a06aff" stroke-width="2"/>
  <text x="220" y="50" class="svg-text" font-size="13" font-weight="700" fill="#a06aff" text-anchor="middle">UdKind (bits 63-32)</text>
  <text x="220" y="66" class="svg-mono" font-size="10" text-anchor="middle">1=Accept  2=Recv  3=Send  4=Cancel</text>
  <rect x="410" y="30" width="340" height="45" rx="8" fill="rgba(74,144,255,.1)" stroke="#4a90ff" stroke-width="2"/>
  <text x="580" y="50" class="svg-text" font-size="13" font-weight="700" fill="#4a90ff" text-anchor="middle">File Descriptor (bits 31-0)</text>
  <text x="580" y="66" class="svg-mono" font-size="10" text-anchor="middle">socket fd cast to uint</text>
  <text x="400" y="100" class="svg-mono" font-size="10" fill="#3ee8d0" text-anchor="middle">PackUd(kind, fd) = ((ulong)kind &lt;&lt; 32) | (uint)fd</text>
</svg>
</div>

<!-- ═══════════════ SECTION 4: BUFFER RING ═══════════════ -->
<div class="iu-section">
<div class="iu-stag iu-stag-purple">Zero Copy</div>

### Provided Buffer Ring

<p class="iu-lead">Instead of passing a buffer with each recv, you pre-register a pool. The kernel picks one, fills it, and tells you which ID it used. You return it when done.</p>

<svg viewBox="0 0 900 500" class="ring-svg">
  <defs>
    <marker id="arrowP" markerWidth="8" markerHeight="6" refX="8" refY="3" orient="auto"><polygon points="0 0, 8 3, 0 6" fill="#a06aff"/></marker>
  </defs>
  <text x="30" y="25" class="svg-text" font-size="11" font-weight="700" fill="#4a90ff" opacity=".5">USERSPACE</text>
  <text x="30" y="335" class="svg-text" font-size="11" font-weight="700" fill="#ff8a3e" opacity=".5">KERNEL</text>
  <line x1="20" y1="305" x2="880" y2="305" stroke="#2a3040" stroke-width="1" stroke-dasharray="6 4"/>
  <!-- BUFFER SLAB -->
  <text x="60" y="55" class="svg-text" font-size="12" font-weight="700" fill="#a06aff">Buffer Slab (NativeMemory)</text>
  <g transform="translate(60, 65)">
    <rect x="0" y="0" width="60" height="35" rx="5" fill="rgba(160,106,255,.2)" stroke="#a06aff" stroke-width="1.5"/>
    <text x="30" y="15" class="svg-mono" font-size="8" fill="#a06aff" text-anchor="middle">buf 0</text>
    <text x="30" y="28" class="svg-mono" font-size="7" fill="#555c73" text-anchor="middle">32KB</text>
    <rect x="66" y="0" width="60" height="35" rx="5" fill="rgba(160,106,255,.2)" stroke="#a06aff" stroke-width="1.5"/>
    <text x="96" y="15" class="svg-mono" font-size="8" fill="#a06aff" text-anchor="middle">buf 1</text>
    <text x="96" y="28" class="svg-mono" font-size="7" fill="#555c73" text-anchor="middle">32KB</text>
    <rect x="132" y="0" width="60" height="35" rx="5" fill="rgba(160,106,255,.5)" stroke="#a06aff" stroke-width="2"/>
    <text x="162" y="15" class="svg-mono" font-size="8" fill="#fff" text-anchor="middle">buf 2</text>
    <text x="162" y="28" class="svg-mono" font-size="7" fill="rgba(255,255,255,.6)" text-anchor="middle">32KB</text>
    <rect x="198" y="0" width="60" height="35" rx="5" fill="rgba(160,106,255,.2)" stroke="#a06aff" stroke-width="1.5"/>
    <text x="228" y="15" class="svg-mono" font-size="8" fill="#a06aff" text-anchor="middle">buf 3</text>
    <text x="228" y="28" class="svg-mono" font-size="7" fill="#555c73" text-anchor="middle">32KB</text>
    <rect x="264" y="0" width="60" height="35" rx="5" fill="rgba(160,106,255,.15)" stroke="#a06aff" stroke-width="1" stroke-dasharray="3 2"/>
    <text x="294" y="22" class="svg-mono" font-size="8" fill="#555c73" text-anchor="middle">...</text>
    <rect x="330" y="0" width="60" height="35" rx="5" fill="rgba(160,106,255,.2)" stroke="#a06aff" stroke-width="1.5"/>
    <text x="360" y="15" class="svg-mono" font-size="8" fill="#a06aff" text-anchor="middle">buf N</text>
    <text x="360" y="28" class="svg-mono" font-size="7" fill="#555c73" text-anchor="middle">32KB</text>
  </g>
  <!-- BUFFER RING -->
  <text x="60" y="135" class="svg-text" font-size="12" font-weight="700" fill="#a06aff">Buffer Ring (shared with kernel)</text>
  <g transform="translate(60, 145)">
    <rect x="0" y="0" width="450" height="50" rx="10" fill="rgba(160,106,255,.06)" stroke="#a06aff" stroke-width="2"/>
    <rect x="10" y="10" width="50" height="30" rx="4" fill="#a06aff" opacity=".7"/><text x="35" y="29" class="svg-mono" font-size="8" fill="#fff" text-anchor="middle">id:0</text>
    <rect x="65" y="10" width="50" height="30" rx="4" fill="#a06aff" opacity=".7"/><text x="90" y="29" class="svg-mono" font-size="8" fill="#fff" text-anchor="middle">id:1</text>
    <rect x="120" y="10" width="50" height="30" rx="4" fill="#a06aff" opacity=".3"/><text x="145" y="29" class="svg-mono" font-size="8" fill="#a06aff" text-anchor="middle">id:2</text>
    <text x="145" y="16" class="svg-mono" font-size="7" fill="#ff8a3e" text-anchor="middle">used</text>
    <rect x="175" y="10" width="50" height="30" rx="4" fill="#a06aff" opacity=".7"/><text x="200" y="29" class="svg-mono" font-size="8" fill="#fff" text-anchor="middle">id:3</text>
    <rect x="230" y="10" width="50" height="30" rx="4" fill="#a06aff" opacity=".7"/><text x="255" y="29" class="svg-mono" font-size="8" fill="#fff" text-anchor="middle">id:4</text>
    <rect x="285" y="10" width="50" height="30" rx="4" fill="#a06aff" opacity=".7"/><text x="310" y="29" class="svg-mono" font-size="8" fill="#fff" text-anchor="middle">id:5</text>
    <rect x="340" y="10" width="50" height="30" rx="4" fill="#a06aff" opacity=".5"/><text x="365" y="29" class="svg-mono" font-size="8" fill="#a06aff" text-anchor="middle">...</text>
    <rect x="395" y="10" width="45" height="30" rx="4" fill="#a06aff" opacity=".7"/><text x="417" y="29" class="svg-mono" font-size="8" fill="#fff" text-anchor="middle">id:N</text>
  </g>
  <!-- RECV FLOW -->
  <text x="550" y="135" class="svg-text" font-size="11" font-weight="700" fill="#ff8a3e">Recv Flow</text>
  <rect x="540" y="145" width="320" height="145" rx="12" fill="#181c25" stroke="#2a3040" stroke-width="1"/>
  <text x="560" y="170" class="svg-text" font-size="10" font-weight="700" fill="#ffd04a">1</text>
  <text x="578" y="170" class="svg-text" font-size="10" fill="#8a90a8">Kernel picks buf from ring</text>
  <text x="560" y="195" class="svg-text" font-size="10" font-weight="700" fill="#ffd04a">2</text>
  <text x="578" y="195" class="svg-text" font-size="10" fill="#8a90a8">recv() fills it with data</text>
  <text x="560" y="220" class="svg-text" font-size="10" font-weight="700" fill="#ffd04a">3</text>
  <text x="578" y="220" class="svg-text" font-size="10" fill="#8a90a8">CQE.flags contains buf id</text>
  <text x="578" y="235" class="svg-mono" font-size="9" fill="#3ee8d0">bid = flags >> 16</text>
  <text x="560" y="260" class="svg-text" font-size="10" font-weight="700" fill="#ffd04a">4</text>
  <text x="578" y="260" class="svg-text" font-size="10" fill="#8a90a8">CQE.res = bytes received</text>
  <text x="560" y="280" class="svg-text" font-size="10" font-weight="700" fill="#ffd04a">5</text>
  <text x="578" y="280" class="svg-text" font-size="10" fill="#8a90a8">App returns buf via buf_ring_add</text>
  <!-- KERNEL -->
  <rect x="200" y="340" width="500" height="55" rx="10" fill="rgba(255,138,62,.06)" stroke="#ff8a3e" stroke-width="1.5"/>
  <text x="450" y="365" class="svg-text" font-size="13" font-weight="700" fill="#ff8a3e" text-anchor="middle">Kernel: recv(fd) → picks buf 2 → fills 1,420 bytes</text>
  <text x="450" y="382" class="svg-mono" font-size="10" fill="#8a90a8" text-anchor="middle">CQE { user_data: [Recv|fd], res: 1420, flags: (2 &lt;&lt; 16) | F_BUFFER | F_MORE }</text>
  <!-- RETURN FLOW -->
  <g transform="translate(60, 415)">
    <text x="0" y="15" class="svg-text" font-size="12" font-weight="700" fill="#3ee88a">Buffer Return (app → ring)</text>
    <rect x="0" y="25" width="700" height="50" rx="10" fill="#181c25" stroke="#2a3040" stroke-width="1"/>
    <text x="20" y="50" class="svg-mono" font-size="10" fill="#8a90a8">connection.ReturnRing(bid)</text>
    <text x="25" y="65" class="svg-mono" font-size="9" fill="#555c73">→ MPSC queue → reactor drains → shim_buf_ring_add(ring, addr, len, bid, mask, idx) → shim_buf_ring_advance(1)</text>
  </g>
</svg>
</div>

<!-- ═══════════════ SECTION 5: FULL FLOW ═══════════════ -->
<div class="iu-section">
<div class="iu-stag iu-stag-orange">End to End</div>

### Full zerg Flow

<p class="iu-lead">Walk through the complete lifecycle: client connects, data flows in, your app responds, buffers recycle. Step through each phase one at a time.</p>

<div class="phase-bar" id="ffBar">
  <div class="phase-seg" id="ffSeg0"></div>
  <div class="phase-seg" id="ffSeg1"></div>
  <div class="phase-seg" id="ffSeg2"></div>
  <div class="phase-seg" id="ffSeg3"></div>
  <div class="phase-seg" id="ffSeg4"></div>
  <div class="phase-seg" id="ffSeg5"></div>
  <div class="phase-seg" id="ffSeg6"></div>
  <div class="phase-seg" id="ffSeg7"></div>
</div>

<div id="ffSvgWrap" style="min-height:300px;position:relative;">

  <!-- STEP 0 -->
  <div class="ff-panel" id="ffP0">
    <svg viewBox="0 0 860 280" class="ring-svg">
      <rect x="10" y="10" width="840" height="260" rx="14" fill="rgba(74,144,255,.03)" stroke="#4a90ff" stroke-width="1" stroke-dasharray="5 3" opacity=".35"/>
      <text x="30" y="34" class="svg-text" font-size="11" font-weight="700" fill="#4a90ff" opacity=".5">ACCEPTOR THREAD</text>
      <rect x="40" y="90" width="110" height="70" rx="10" fill="#181c25" stroke="#8a90a8" stroke-width="2"/>
      <text x="95" y="120" class="svg-text" font-size="14" font-weight="800" text-anchor="middle">Client</text>
      <text x="95" y="140" class="svg-mono" font-size="10" fill="#555c73" text-anchor="middle">TCP SYN</text>
      <line x1="155" y1="125" x2="230" y2="125" stroke="#8a90a8" stroke-width="2.5" marker-end="url(#arrowB)"/>
      <text x="192" y="115" class="svg-mono" font-size="9" fill="#555c73" text-anchor="middle">connect</text>
      <rect x="235" y="75" width="190" height="100" rx="12" fill="rgba(255,138,62,.06)" stroke="#ff8a3e" stroke-width="2"/>
      <text x="330" y="104" class="svg-text" font-size="13" font-weight="800" fill="#ff8a3e" text-anchor="middle">Kernel</text>
      <text x="330" y="124" class="svg-mono" font-size="10" fill="#8a90a8" text-anchor="middle">multishot accept</text>
      <text x="330" y="142" class="svg-mono" font-size="10" fill="#8a90a8" text-anchor="middle">completes → CQE</text>
      <text x="330" y="160" class="svg-mono" font-size="9" fill="#ff8a3e" text-anchor="middle">res = new client fd</text>
      <line x1="428" y1="125" x2="510" y2="125" stroke="#3ee88a" stroke-width="2.5" marker-end="url(#arrowG)"/>
      <rect x="515" y="85" width="150" height="80" rx="12" fill="rgba(62,232,138,.06)" stroke="#3ee88a" stroke-width="2"/>
      <text x="590" y="115" class="svg-text" font-size="13" font-weight="800" fill="#3ee88a" text-anchor="middle">Accept CQE</text>
      <text x="590" y="135" class="svg-mono" font-size="10" fill="#8a90a8" text-anchor="middle">fd = 42</text>
      <text x="590" y="152" class="svg-mono" font-size="9" fill="#3ee88a" text-anchor="middle">F_MORE = 1 (stay armed)</text>
      <line x1="668" y1="125" x2="720" y2="125" stroke="#3ee8d0" stroke-width="2.5" marker-end="url(#arrowB)"/>
      <rect x="725" y="85" width="110" height="80" rx="12" fill="#181c25" stroke="#3ee8d0" stroke-width="2"/>
      <text x="780" y="113" class="svg-text" font-size="12" font-weight="700" fill="#3ee8d0" text-anchor="middle">Round</text>
      <text x="780" y="130" class="svg-text" font-size="12" font-weight="700" fill="#3ee8d0" text-anchor="middle">Robin</text>
      <text x="780" y="152" class="svg-mono" font-size="9" fill="#8a90a8" text-anchor="middle">next % N</text>
      <text x="430" y="230" class="svg-text" font-size="11" fill="#8a90a8" text-anchor="middle">Multishot accept is armed once at startup. Each new connection produces a CQE automatically.</text>
      <text x="430" y="250" class="svg-text" font-size="11" fill="#8a90a8" text-anchor="middle">The acceptor never re-submits. F_MORE flag tells us the kernel will keep producing CQEs.</text>
    </svg>
  </div>

  <!-- STEP 1 -->
  <div class="ff-panel" id="ffP1" style="display:none;">
    <svg viewBox="0 0 860 280" class="ring-svg">
      <rect x="10" y="10" width="380" height="130" rx="14" fill="rgba(74,144,255,.03)" stroke="#4a90ff" stroke-width="1" stroke-dasharray="5 3" opacity=".35"/>
      <text x="30" y="34" class="svg-text" font-size="11" font-weight="700" fill="#4a90ff" opacity=".5">ACCEPTOR</text>
      <rect x="470" y="10" width="380" height="260" rx="14" fill="rgba(62,232,208,.03)" stroke="#3ee8d0" stroke-width="1" stroke-dasharray="5 3" opacity=".35"/>
      <text x="490" y="34" class="svg-text" font-size="11" font-weight="700" fill="#3ee8d0" opacity=".5">REACTOR 0</text>
      <rect x="40" y="55" width="140" height="65" rx="10" fill="#181c25" stroke="#4a90ff" stroke-width="1.5"/>
      <text x="110" y="80" class="svg-text" font-size="12" font-weight="700" fill="#4a90ff" text-anchor="middle">Acceptor</text>
      <text x="110" y="98" class="svg-mono" font-size="10" fill="#8a90a8" text-anchor="middle">fd = 42</text>
      <line x1="184" y1="87" x2="470" y2="87" stroke="#3ee8d0" stroke-width="3" marker-end="url(#arrowB)"/>
      <rect x="260" y="68" width="140" height="38" rx="8" fill="#0b0d11" stroke="#3ee8d0" stroke-width="2"/>
      <text x="330" y="84" class="svg-text" font-size="11" font-weight="700" fill="#3ee8d0" text-anchor="middle">MPSC Queue</text>
      <text x="330" y="99" class="svg-mono" font-size="9" fill="#555c73" text-anchor="middle">lock-free enqueue</text>
      <rect x="505" y="55" width="200" height="65" rx="10" fill="#181c25" stroke="#3ee8d0" stroke-width="2"/>
      <text x="605" y="78" class="svg-text" font-size="12" font-weight="700" fill="#3ee8d0" text-anchor="middle">Reactor dequeues fd</text>
      <text x="605" y="98" class="svg-mono" font-size="10" fill="#8a90a8" text-anchor="middle">creates Connection object</text>
      <line x1="605" y1="124" x2="605" y2="162" stroke="#4a90ff" stroke-width="2.5" marker-end="url(#arrowB)"/>
      <rect x="490" y="166" width="230" height="65" rx="10" fill="rgba(74,144,255,.08)" stroke="#4a90ff" stroke-width="2"/>
      <text x="605" y="190" class="svg-text" font-size="12" font-weight="700" fill="#4a90ff" text-anchor="middle">Arm recv_multishot</text>
      <text x="605" y="210" class="svg-mono" font-size="10" fill="#8a90a8" text-anchor="middle">prep_recv(sqe, fd=42, bgid=1)</text>
      <text x="605" y="224" class="svg-mono" font-size="9" fill="#555c73" text-anchor="middle">+ set_data64([Recv | 42])</text>
      <text x="430" y="265" class="svg-text" font-size="11" fill="#8a90a8" text-anchor="middle">The reactor now owns this fd. No other thread touches it. Zero contention by design.</text>
    </svg>
  </div>

  <!-- STEP 2 -->
  <div class="ff-panel" id="ffP2" style="display:none;">
    <svg viewBox="0 0 860 300" class="ring-svg">
      <rect x="10" y="10" width="840" height="280" rx="14" fill="rgba(255,138,62,.03)" stroke="#ff8a3e" stroke-width="1" stroke-dasharray="5 3" opacity=".35"/>
      <text x="30" y="34" class="svg-text" font-size="11" font-weight="700" fill="#ff8a3e" opacity=".5">KERNEL I/O</text>
      <rect x="40" y="70" width="120" height="60" rx="10" fill="#181c25" stroke="#8a90a8" stroke-width="1.5"/>
      <text x="100" y="96" class="svg-text" font-size="12" font-weight="700" text-anchor="middle">Client</text>
      <text x="100" y="114" class="svg-mono" font-size="9" fill="#555c73" text-anchor="middle">sends data</text>
      <line x1="164" y1="100" x2="230" y2="100" stroke="#8a90a8" stroke-width="2" marker-end="url(#arrowO)"/>
      <rect x="235" y="55" width="200" height="90" rx="12" fill="rgba(255,138,62,.08)" stroke="#ff8a3e" stroke-width="2"/>
      <text x="335" y="82" class="svg-text" font-size="13" font-weight="800" fill="#ff8a3e" text-anchor="middle">Kernel recv()</text>
      <text x="335" y="102" class="svg-mono" font-size="10" fill="#8a90a8" text-anchor="middle">multishot → picks buf</text>
      <text x="335" y="120" class="svg-mono" font-size="10" fill="#8a90a8" text-anchor="middle">from buf_ring (bgid=1)</text>
      <line x1="438" y1="100" x2="510" y2="100" stroke="#a06aff" stroke-width="2.5" marker-end="url(#arrowP)"/>
      <rect x="515" y="60" width="160" height="80" rx="12" fill="rgba(160,106,255,.1)" stroke="#a06aff" stroke-width="2"/>
      <text x="595" y="88" class="svg-text" font-size="12" font-weight="700" fill="#a06aff" text-anchor="middle">Buffer #7</text>
      <text x="595" y="108" class="svg-mono" font-size="10" fill="#8a90a8" text-anchor="middle">32KB pre-allocated</text>
      <text x="595" y="126" class="svg-mono" font-size="10" fill="#a06aff" text-anchor="middle">filled: 1,420 bytes</text>
      <line x1="430" y1="150" x2="430" y2="188" stroke="#3ee88a" stroke-width="2.5" marker-end="url(#arrowG)"/>
      <rect x="240" y="192" width="380" height="70" rx="12" fill="rgba(62,232,138,.06)" stroke="#3ee88a" stroke-width="2"/>
      <text x="430" y="215" class="svg-text" font-size="13" font-weight="800" fill="#3ee88a" text-anchor="middle">CQE produced</text>
      <text x="430" y="237" class="svg-mono" font-size="11" fill="#8a90a8" text-anchor="middle">user_data: [Recv | 42]   res: 1420   flags: (7 &lt;&lt; 16) | F_BUFFER | F_MORE</text>
      <text x="430" y="255" class="svg-mono" font-size="9" fill="#555c73" text-anchor="middle">buffer_id=7 encoded in flags upper 16 bits • F_MORE means multishot continues</text>
      <rect x="690" y="75" width="140" height="50" rx="8" fill="#181c25" stroke="#ffd04a" stroke-width="1.5"/>
      <text x="760" y="97" class="svg-text" font-size="10" font-weight="700" fill="#ffd04a" text-anchor="middle">Zero Copy</text>
      <text x="760" y="112" class="svg-mono" font-size="8" fill="#8a90a8" text-anchor="middle">NIC → your buffer</text>
    </svg>
  </div>

  <!-- STEP 3 -->
  <div class="ff-panel" id="ffP3" style="display:none;">
    <svg viewBox="0 0 860 300" class="ring-svg">
      <rect x="10" y="10" width="840" height="280" rx="14" fill="rgba(62,232,208,.03)" stroke="#3ee8d0" stroke-width="1" stroke-dasharray="5 3" opacity=".35"/>
      <text x="30" y="34" class="svg-text" font-size="11" font-weight="700" fill="#3ee8d0" opacity=".5">REACTOR EVENT LOOP</text>
      <rect x="40" y="60" width="200" height="70" rx="10" fill="#181c25" stroke="#3ee8d0" stroke-width="2"/>
      <text x="140" y="86" class="svg-text" font-size="12" font-weight="700" fill="#3ee8d0" text-anchor="middle">peek_batch_cqe</text>
      <text x="140" y="108" class="svg-mono" font-size="10" fill="#8a90a8" text-anchor="middle">got 3 CQEs</text>
      <line x1="244" y1="95" x2="300" y2="95" stroke="#3ee8d0" stroke-width="2" marker-end="url(#arrowB)"/>
      <rect x="305" y="60" width="200" height="70" rx="10" fill="#181c25" stroke="#8a90a8" stroke-width="1.5"/>
      <text x="405" y="84" class="svg-text" font-size="12" font-weight="700" text-anchor="middle">Unpack user_data</text>
      <text x="405" y="106" class="svg-mono" font-size="10" fill="#8a90a8" text-anchor="middle">kind = UdKindOf(ud)</text>
      <text x="405" y="120" class="svg-mono" font-size="10" fill="#8a90a8" text-anchor="middle">fd   = UdFdOf(ud)</text>
      <line x1="405" y1="134" x2="140" y2="190" stroke="#3ee88a" stroke-width="2" marker-end="url(#arrowG)"/>
      <line x1="405" y1="134" x2="405" y2="190" stroke="#4a90ff" stroke-width="2" marker-end="url(#arrowB)"/>
      <line x1="405" y1="134" x2="670" y2="190" stroke="#ff4a5e" stroke-width="2" marker-end="url(#arrowB)"/>
      <rect x="50" y="195" width="180" height="75" rx="10" fill="rgba(62,232,138,.06)" stroke="#3ee88a" stroke-width="2"/>
      <text x="140" y="220" class="svg-text" font-size="13" font-weight="800" fill="#3ee88a" text-anchor="middle">Recv</text>
      <text x="140" y="240" class="svg-mono" font-size="9" fill="#8a90a8" text-anchor="middle">extract buffer_id</text>
      <text x="140" y="256" class="svg-mono" font-size="9" fill="#8a90a8" text-anchor="middle">enqueue to connection</text>
      <rect x="315" y="195" width="180" height="75" rx="10" fill="rgba(74,144,255,.06)" stroke="#4a90ff" stroke-width="2"/>
      <text x="405" y="220" class="svg-text" font-size="13" font-weight="800" fill="#4a90ff" text-anchor="middle">Send</text>
      <text x="405" y="240" class="svg-mono" font-size="9" fill="#8a90a8" text-anchor="middle">update WriteHead += res</text>
      <text x="405" y="256" class="svg-mono" font-size="9" fill="#8a90a8" text-anchor="middle">complete flush if done</text>
      <rect x="580" y="195" width="180" height="75" rx="10" fill="rgba(255,74,94,.06)" stroke="#ff4a5e" stroke-width="2"/>
      <text x="670" y="220" class="svg-text" font-size="13" font-weight="800" fill="#ff4a5e" text-anchor="middle">Cancel</text>
      <text x="670" y="240" class="svg-mono" font-size="9" fill="#8a90a8" text-anchor="middle">cleanup acknowledged</text>
      <text x="670" y="256" class="svg-mono" font-size="9" fill="#8a90a8" text-anchor="middle">nothing to do</text>
    </svg>
  </div>

  <!-- STEP 4 -->
  <div class="ff-panel" id="ffP4" style="display:none;">
    <svg viewBox="0 0 860 280" class="ring-svg">
      <rect x="10" y="10" width="840" height="260" rx="14" fill="rgba(62,232,208,.03)" stroke="#3ee8d0" stroke-width="1" stroke-dasharray="5 3" opacity=".35"/>
      <text x="30" y="34" class="svg-text" font-size="11" font-weight="700" fill="#3ee8d0" opacity=".5">CONNECTION READ PATH</text>
      <rect x="40" y="70" width="170" height="80" rx="10" fill="rgba(62,232,138,.06)" stroke="#3ee88a" stroke-width="2"/>
      <text x="125" y="96" class="svg-text" font-size="12" font-weight="700" fill="#3ee88a" text-anchor="middle">Recv CQE</text>
      <text x="125" y="116" class="svg-mono" font-size="10" fill="#8a90a8" text-anchor="middle">buf_id=7, 1420B</text>
      <text x="125" y="134" class="svg-mono" font-size="9" fill="#555c73" text-anchor="middle">ptr = slab + 7*32KB</text>
      <line x1="214" y1="110" x2="280" y2="110" stroke="#3ee88a" stroke-width="2" marker-end="url(#arrowG)"/>
      <rect x="285" y="70" width="180" height="80" rx="10" fill="#181c25" stroke="#a06aff" stroke-width="2"/>
      <text x="375" y="96" class="svg-text" font-size="12" font-weight="700" fill="#a06aff" text-anchor="middle">MPSC Recv Ring</text>
      <text x="375" y="116" class="svg-mono" font-size="10" fill="#8a90a8" text-anchor="middle">EnqueueRingItem</text>
      <text x="375" y="134" class="svg-mono" font-size="9" fill="#555c73" text-anchor="middle">(ptr, len, buf_id)</text>
      <line x1="469" y1="110" x2="535" y2="110" stroke="#3ee8d0" stroke-width="2" marker-end="url(#arrowB)"/>
      <rect x="540" y="60" width="270" height="100" rx="12" fill="rgba(62,232,208,.06)" stroke="#3ee8d0" stroke-width="2"/>
      <text x="675" y="90" class="svg-text" font-size="14" font-weight="800" fill="#3ee8d0" text-anchor="middle">ReadAsync() completes</text>
      <text x="675" y="114" class="svg-mono" font-size="10" fill="#8a90a8" text-anchor="middle">ValueTask signals your handler</text>
      <text x="675" y="134" class="svg-mono" font-size="10" fill="#8a90a8" text-anchor="middle">result.IsClosed = false</text>
      <text x="675" y="150" class="svg-mono" font-size="9" fill="#3ee8d0" text-anchor="middle">zero allocation (ManualResetValueTaskSourceCore)</text>
      <text x="430" y="210" class="svg-text" font-size="11" fill="#8a90a8" text-anchor="middle">The reactor enqueues the raw pointer + length + buffer_id into the connection's lock-free recv ring.</text>
      <text x="430" y="230" class="svg-text" font-size="11" fill="#8a90a8" text-anchor="middle">Your await ReadAsync() resumes with zero allocations via ValueTask.</text>
      <text x="430" y="252" class="svg-text" font-size="11" fill="#8a90a8" text-anchor="middle">You now have direct pointer access to the kernel-filled buffer. No copy happened.</text>
    </svg>
  </div>

  <!-- STEP 5 -->
  <div class="ff-panel" id="ffP5" style="display:none;">
    <svg viewBox="0 0 860 300" class="ring-svg">
      <rect x="10" y="10" width="840" height="280" rx="14" fill="rgba(62,232,208,.03)" stroke="#3ee8d0" stroke-width="1" stroke-dasharray="5 3" opacity=".35"/>
      <text x="30" y="34" class="svg-text" font-size="11" font-weight="700" fill="#3ee8d0" opacity=".5">YOUR APPLICATION HANDLER</text>
      <rect x="40" y="60" width="200" height="100" rx="10" fill="#181c25" stroke="#3ee88a" stroke-width="2"/>
      <text x="140" y="86" class="svg-text" font-size="12" font-weight="700" fill="#3ee88a" text-anchor="middle">Read Buffers</text>
      <text x="140" y="106" class="svg-mono" font-size="10" fill="#3ee8d0" text-anchor="middle">GetAllSnapshot</text>
      <text x="140" y="122" class="svg-mono" font-size="10" fill="#3ee8d0" text-anchor="middle">RingsAsUnmanagedMemory</text>
      <text x="140" y="142" class="svg-mono" font-size="9" fill="#555c73" text-anchor="middle">or ToReadOnlySequence()</text>
      <line x1="244" y1="110" x2="295" y2="110" stroke="#8a90a8" stroke-width="2" marker-end="url(#arrowB)"/>
      <rect x="300" y="70" width="170" height="80" rx="10" fill="#181c25" stroke="#8a90a8" stroke-width="1.5"/>
      <text x="385" y="100" class="svg-text" font-size="12" font-weight="700" text-anchor="middle">Parse Request</text>
      <text x="385" y="120" class="svg-mono" font-size="10" fill="#8a90a8" text-anchor="middle">your business logic</text>
      <text x="385" y="137" class="svg-mono" font-size="9" fill="#555c73" text-anchor="middle">HTTP parse, JSON, etc.</text>
      <line x1="474" y1="110" x2="525" y2="110" stroke="#8a90a8" stroke-width="2" marker-end="url(#arrowB)"/>
      <rect x="530" y="60" width="280" height="100" rx="10" fill="rgba(74,144,255,.06)" stroke="#4a90ff" stroke-width="2"/>
      <text x="670" y="86" class="svg-text" font-size="12" font-weight="700" fill="#4a90ff" text-anchor="middle">Write Response</text>
      <text x="670" y="108" class="svg-mono" font-size="10" fill="#3ee8d0" text-anchor="middle">connection.Write(data)</text>
      <text x="670" y="126" class="svg-mono" font-size="10" fill="#8a90a8" text-anchor="middle">copies into 16KB staging slab</text>
      <text x="670" y="146" class="svg-mono" font-size="9" fill="#555c73" text-anchor="middle">NativeMemory, no GC pressure</text>
      <line x1="670" y1="164" x2="670" y2="200" stroke="#4a90ff" stroke-width="2" marker-end="url(#arrowB)"/>
      <rect x="530" y="205" width="280" height="60" rx="10" fill="rgba(74,144,255,.1)" stroke="#4a90ff" stroke-width="2"/>
      <text x="670" y="230" class="svg-text" font-size="13" font-weight="800" fill="#4a90ff" text-anchor="middle">await FlushAsync()</text>
      <text x="670" y="250" class="svg-mono" font-size="10" fill="#8a90a8" text-anchor="middle">queues send SQE for reactor</text>
      <text x="250" y="230" class="svg-text" font-size="11" fill="#8a90a8" text-anchor="middle">Write() copies into the unmanaged staging buffer.</text>
      <text x="250" y="250" class="svg-text" font-size="11" fill="#8a90a8" text-anchor="middle">FlushAsync() signals the reactor to submit a</text>
      <text x="250" y="270" class="svg-text" font-size="11" fill="#8a90a8" text-anchor="middle">send SQE on the next loop iteration.</text>
    </svg>
  </div>

  <!-- STEP 6 -->
  <div class="ff-panel" id="ffP6" style="display:none;">
    <svg viewBox="0 0 860 280" class="ring-svg">
      <rect x="10" y="10" width="420" height="260" rx="14" fill="rgba(62,232,208,.03)" stroke="#3ee8d0" stroke-width="1" stroke-dasharray="5 3" opacity=".35"/>
      <text x="30" y="34" class="svg-text" font-size="11" font-weight="700" fill="#3ee8d0" opacity=".5">REACTOR</text>
      <rect x="440" y="10" width="410" height="260" rx="14" fill="rgba(255,138,62,.03)" stroke="#ff8a3e" stroke-width="1" stroke-dasharray="5 3" opacity=".35"/>
      <text x="460" y="34" class="svg-text" font-size="11" font-weight="700" fill="#ff8a3e" opacity=".5">KERNEL</text>
      <rect x="40" y="60" width="180" height="65" rx="10" fill="#181c25" stroke="#4a90ff" stroke-width="2"/>
      <text x="130" y="84" class="svg-text" font-size="11" font-weight="700" fill="#4a90ff" text-anchor="middle">Drain Flush Q</text>
      <text x="130" y="104" class="svg-mono" font-size="9" fill="#8a90a8" text-anchor="middle">prep_send(fd, buf, len)</text>
      <line x1="224" y1="92" x2="265" y2="92" stroke="#4a90ff" stroke-width="2" marker-end="url(#arrowB)"/>
      <rect x="270" y="60" width="145" height="65" rx="10" fill="rgba(62,232,208,.08)" stroke="#3ee8d0" stroke-width="2"/>
      <text x="342" y="82" class="svg-text" font-size="11" font-weight="700" fill="#3ee8d0" text-anchor="middle">submit_and</text>
      <text x="342" y="98" class="svg-text" font-size="11" font-weight="700" fill="#3ee8d0" text-anchor="middle">_wait_timeout</text>
      <text x="342" y="115" class="svg-mono" font-size="8" fill="#555c73" text-anchor="middle">1 syscall</text>
      <line x1="418" y1="92" x2="475" y2="92" stroke="#ff8a3e" stroke-width="2.5" marker-end="url(#arrowO)"/>
      <rect x="480" y="55" width="200" height="80" rx="12" fill="rgba(255,138,62,.08)" stroke="#ff8a3e" stroke-width="2"/>
      <text x="580" y="82" class="svg-text" font-size="13" font-weight="800" fill="#ff8a3e" text-anchor="middle">Kernel send()</text>
      <text x="580" y="102" class="svg-mono" font-size="10" fill="#8a90a8" text-anchor="middle">staging buf → TCP socket</text>
      <text x="580" y="120" class="svg-mono" font-size="10" fill="#8a90a8" text-anchor="middle">→ client receives data</text>
      <line x1="580" y1="140" x2="580" y2="178" stroke="#3ee88a" stroke-width="2" marker-end="url(#arrowG)"/>
      <rect x="475" y="182" width="210" height="65" rx="10" fill="rgba(62,232,138,.06)" stroke="#3ee88a" stroke-width="2"/>
      <text x="580" y="208" class="svg-text" font-size="12" font-weight="700" fill="#3ee88a" text-anchor="middle">Send CQE</text>
      <text x="580" y="228" class="svg-mono" font-size="10" fill="#8a90a8" text-anchor="middle">res = bytes_sent</text>
      <text x="580" y="240" class="svg-mono" font-size="9" fill="#3ee88a" text-anchor="middle">reactor: CompleteFlush()</text>
      <line x1="472" y1="214" x2="240" y2="214" stroke="#3ee88a" stroke-width="2" marker-end="url(#arrowG)"/>
      <rect x="40" y="185" width="196" height="60" rx="10" fill="#181c25" stroke="#3ee88a" stroke-width="1.5"/>
      <text x="138" y="208" class="svg-text" font-size="11" font-weight="700" fill="#3ee88a" text-anchor="middle">FlushAsync returns</text>
      <text x="138" y="228" class="svg-mono" font-size="9" fill="#8a90a8" text-anchor="middle">your handler continues</text>
    </svg>
  </div>

  <!-- STEP 7 -->
  <div class="ff-panel" id="ffP7" style="display:none;">
    <svg viewBox="0 0 860 300" class="ring-svg">
      <rect x="10" y="10" width="840" height="280" rx="14" fill="rgba(160,106,255,.03)" stroke="#a06aff" stroke-width="1" stroke-dasharray="5 3" opacity=".35"/>
      <text x="30" y="34" class="svg-text" font-size="11" font-weight="700" fill="#a06aff" opacity=".5">BUFFER RECYCLING</text>
      <rect x="40" y="65" width="190" height="80" rx="10" fill="#181c25" stroke="#3ee8d0" stroke-width="2"/>
      <text x="135" y="92" class="svg-text" font-size="12" font-weight="700" fill="#3ee8d0" text-anchor="middle">Your Handler</text>
      <text x="135" y="112" class="svg-mono" font-size="10" fill="#3ee8d0" text-anchor="middle">ReturnRing(bid=7)</text>
      <text x="135" y="130" class="svg-mono" font-size="10" fill="#3ee8d0" text-anchor="middle">ResetRead()</text>
      <line x1="234" y1="105" x2="290" y2="105" stroke="#a06aff" stroke-width="2.5" marker-end="url(#arrowP)"/>
      <rect x="295" y="70" width="170" height="70" rx="10" fill="#181c25" stroke="#a06aff" stroke-width="2"/>
      <text x="380" y="96" class="svg-text" font-size="12" font-weight="700" fill="#a06aff" text-anchor="middle">Return Queue</text>
      <text x="380" y="118" class="svg-mono" font-size="10" fill="#8a90a8" text-anchor="middle">MPSC enqueue(7)</text>
      <line x1="469" y1="105" x2="525" y2="105" stroke="#3ee8d0" stroke-width="2.5" marker-end="url(#arrowB)"/>
      <rect x="530" y="65" width="175" height="80" rx="10" fill="#181c25" stroke="#3ee8d0" stroke-width="2"/>
      <text x="617" y="92" class="svg-text" font-size="12" font-weight="700" fill="#3ee8d0" text-anchor="middle">Reactor drains</text>
      <text x="617" y="112" class="svg-mono" font-size="10" fill="#8a90a8" text-anchor="middle">DrainReturnQ()</text>
      <text x="617" y="130" class="svg-mono" font-size="9" fill="#555c73" text-anchor="middle">on next loop iter</text>
      <line x1="617" y1="149" x2="617" y2="185" stroke="#a06aff" stroke-width="2.5" marker-end="url(#arrowP)"/>
      <rect x="440" y="190" width="360" height="80" rx="12" fill="rgba(160,106,255,.06)" stroke="#a06aff" stroke-width="2"/>
      <text x="620" y="218" class="svg-text" font-size="13" font-weight="800" fill="#a06aff" text-anchor="middle">buf_ring_add + buf_ring_advance</text>
      <text x="620" y="240" class="svg-mono" font-size="10" fill="#8a90a8" text-anchor="middle">buffer #7 is back in the pool</text>
      <text x="620" y="258" class="svg-mono" font-size="10" fill="#8a90a8" text-anchor="middle">kernel can use it for the next recv</text>
      <rect x="40" y="200" width="230" height="60" rx="10" fill="rgba(62,232,208,.06)" stroke="#3ee8d0" stroke-width="2"/>
      <text x="155" y="224" class="svg-text" font-size="12" font-weight="700" fill="#3ee8d0" text-anchor="middle">await ReadAsync()</text>
      <text x="155" y="244" class="svg-mono" font-size="10" fill="#8a90a8" text-anchor="middle">ready for next request</text>
      <text x="430" y="290" class="svg-text" font-size="12" font-weight="700" fill="#3ee8d0" text-anchor="middle">← Loop repeats. Zero allocations. 1 syscall per iteration. →</text>
    </svg>
  </div>

</div>

<div class="stepper">
  <button class="step-btn" id="ffPrev" onclick="ffStep(-1)">&#9664;</button>
  <div class="step-dots" id="ffDots"></div>
  <button class="step-btn" id="ffNext" onclick="ffStep(1)">&#9654;</button>
</div>
<div class="step-desc" id="ffDesc"></div>

</div>

<!-- ═══════════════ FEATURES USED BY ZERG ═══════════════ -->
<div class="iu-section">

### Features Used by zerg

<div class="iu-cards" style="grid-template-columns:repeat(auto-fit,minmax(280px,1fr));">
  <div class="iu-card">
    <h4 style="color:#4a90ff;">Multishot Accept</h4>
    <p>A single SQE arms the kernel to produce one CQE per accepted connection indefinitely. The acceptor thread never re-arms. Each CQE contains the new client fd in <code>cqe->res</code> and <code>IORING_CQE_F_MORE</code> to indicate more will follow.</p>
  </div>
  <div class="iu-card">
    <h4 style="color:#3ee88a;">Multishot Recv + Buffer Selection</h4>
    <p>A single SQE arms recv for a connection. Each time data arrives, the kernel picks a buffer from the buf_ring, fills it, and produces a CQE with the buffer ID in the flags. Eliminates per-recv buffer allocation.</p>
  </div>
  <div class="iu-card">
    <h4 style="color:#a06aff;">Buffer Rings (Provided Buffers)</h4>
    <p>Pre-allocated buffer pool registered with the kernel via <code>shim_setup_buf_ring()</code>. Buffers are added with <code>buf_ring_add()</code> and recycled after use. See <a href="../buffer-rings/" style="color:#a06aff;">Buffer Rings</a> for the full lifecycle.</p>
  </div>
  <div class="iu-card">
    <h4 style="color:#3ee8d0;">SINGLE_ISSUER</h4>
    <p>Tells the kernel only one thread submits to this ring. Skips SQ locking for better throughput. Matches zerg's model where each reactor is the sole submitter to its ring.</p>
  </div>
  <div class="iu-card">
    <h4 style="color:#ff8a3e;">DEFER_TASKRUN</h4>
    <p>Defers kernel task_work until the next ring entry. Reduces latency spikes from interrupt-context work and makes completions arrive at predictable points for better async/await integration.</p>
  </div>
  <div class="iu-card">
    <h4 style="color:#ffd04a;">SQPOLL (Optional)</h4>
    <p>Creates a kernel thread polling the SQ continuously, eliminating the <code>io_uring_enter()</code> syscall. Trades a dedicated CPU core for the lowest possible submission latency.</p>
  </div>
  <div class="iu-card">
    <h4 style="color:#ff6ab0;">Submit-and-Wait</h4>
    <p>zerg's reactor uses <code>shim_submit_and_wait_timeout()</code> — a single syscall that submits all pending SQEs AND waits for at least one CQE. One syscall instead of two.</p>
  </div>
  <div class="iu-card">
    <h4 style="color:#82aaff;">CQE Batching</h4>
    <p>Instead of one CQE at a time, the reactor peeks a batch with <code>shim_peek_batch_cqe()</code> and processes all before advancing the CQ head. Amortizes the head update across completions.</p>
  </div>
</div>
</div>

</div><!-- /iouring-guide -->

<script>
// Lifecycle stepper
const lcSteps = [
  { el: 'lc-s1', title: '1. Get a free SQE slot', desc: 'Call <code>shim_get_sqe(ring)</code> to grab an empty Submission Queue Entry from the ring. This is a userspace-only operation — no syscall. If the SQ is full, submit first to flush pending entries.' },
  { el: 'lc-s2', title: '2. Prepare the operation', desc: 'Fill the SQE with what you want: <code>shim_prep_recv_multishot_select(sqe, fd, bgid, 0)</code>. This sets the opcode (RECV), the file descriptor, and enables buffer selection from the buf_ring.' },
  { el: 'lc-s3', title: '3. Attach your user_data', desc: 'Call <code>shim_sqe_set_data64(sqe, PackUd(Recv, fd))</code>. This 64-bit tag will be returned verbatim in the CQE, so you can identify which operation completed and on which socket.' },
  { el: 'lc-s4', title: '4. Submit to kernel', desc: '<code>shim_submit_and_wait_timeout(ring, &amp;cqes, 1, &amp;ts)</code> — this single syscall flushes all queued SQEs AND waits for at least 1 completion. One syscall instead of two.' },
  { el: 'lc-s5', title: '5. Kernel processes the I/O', desc: 'The kernel sees your recv SQE, picks a buffer from the buf_ring, and when data arrives on the socket, fills that buffer. Zero-copy from NIC to your pre-allocated memory.' },
  { el: 'lc-s6', title: '6. CQE written to Completion Queue', desc: 'The kernel writes a CQE containing your <code>user_data</code>, <code>res</code> (bytes received), and <code>flags</code> (including the buffer_id in the upper 16 bits and the F_MORE flag).' },
  { el: 'lc-s7', title: '7. App reads and dispatches CQE', desc: 'Your app calls <code>shim_peek_batch_cqe()</code> to grab all available CQEs. Unpack <code>user_data</code> to determine UdKind (Recv/Send/Cancel) and the fd. Process the result.' },
  { el: 'lc-s8', title: '8. Advance the CQ head', desc: 'Call <code>shim_cq_advance(ring, count)</code> to mark all processed CQEs as consumed. This is a single userspace write to the ring head pointer — no syscall needed.' },
];
let lcCur = -1;
function lcBuildDots() {
  const c = document.getElementById('lcDots');
  lcSteps.forEach((_, i) => { const d = document.createElement('div'); d.className = 'step-dot'; d.onclick = () => lcGoTo(i); c.appendChild(d); });
}
lcBuildDots();
function lcGoTo(idx) {
  lcCur = idx;
  lcSteps.forEach((s, i) => { const el = document.getElementById(s.el); if (i <= idx) el.classList.add('show'); else el.classList.remove('show'); });
  document.querySelectorAll('#lcDots .step-dot').forEach((d, i) => { d.classList.remove('active','done'); if (i < idx) d.classList.add('done'); if (i === idx) d.classList.add('active'); });
  document.getElementById('lcDesc').innerHTML = '<h4><span style="color:#3ee8d0;">'+lcSteps[idx].title+'</span></h4><p>'+lcSteps[idx].desc+'</p>';
  document.getElementById('lcPrev').disabled = idx <= 0;
  document.getElementById('lcNext').disabled = idx >= lcSteps.length - 1;
}
function lcStep(dir) { const n = lcCur + dir; if (n >= 0 && n < lcSteps.length) lcGoTo(n); }
lcGoTo(0);

// Full flow stepper
const ffSteps = [
  { panel:'ffP0', title:'Phase 1 — Client Connects', desc:'A client sends a TCP SYN. The kernel\'s multishot accept (armed once at startup) produces a CQE with the new file descriptor. The acceptor never re-submits — F_MORE keeps it armed. The fd is round-robin\'d to a reactor.', color:'#4a90ff' },
  { panel:'ffP1', title:'Phase 2 — FD Handed to Reactor', desc:'The acceptor pushes the fd into the reactor\'s lock-free MPSC queue. The reactor dequeues it, creates a pooled Connection object, and arms <code>recv_multishot</code> with buffer selection. From now on, only this reactor touches this fd — zero contention.', color:'#3ee8d0' },
  { panel:'ffP2', title:'Phase 3 — Data Arrives', desc:'The client sends data. The kernel\'s multishot recv automatically picks a buffer from the buf_ring, fills it, and writes a CQE. The buffer_id is encoded in the upper 16 bits of <code>cqe.flags</code>. Your pre-allocated memory was filled directly — zero copy.', color:'#ff8a3e' },
  { panel:'ffP3', title:'Phase 4 — CQE Dispatch', desc:'The reactor calls <code>peek_batch_cqe()</code> to grab all available completions. For each CQE, it unpacks <code>user_data</code> into UdKind + fd and dispatches: <b style="color:#3ee88a">Recv</b> = enqueue to connection, <b style="color:#4a90ff">Send</b> = complete flush, <b style="color:#ff4a5e">Cancel</b> = cleanup.', color:'#3ee88a' },
  { panel:'ffP4', title:'Phase 5 — ReadAsync Completes', desc:'The recv data (raw pointer + length + buffer_id) is enqueued into the connection\'s MPSC recv ring. This signals the <code>ManualResetValueTaskSourceCore</code>, completing your <code>await ReadAsync()</code> with zero allocation. You now have direct pointer access to the kernel-filled buffer.', color:'#3ee8d0' },
  { panel:'ffP5', title:'Phase 6 — Process + Write Response', desc:'Your handler reads the buffers via <code>GetAllSnapshotRingsAsUnmanagedMemory()</code> or <code>ToReadOnlySequence()</code>. After processing, call <code>connection.Write(data)</code> to copy into the unmanaged staging slab, then <code>FlushAsync()</code> to signal the reactor to send.', color:'#4a90ff' },
  { panel:'ffP6', title:'Phase 7 — Reactor Sends Response', desc:'On the next loop iteration, the reactor drains the flush queue, builds a send SQE, and fires <code>submit_and_wait_timeout</code> — one syscall for all pending work. The kernel delivers the response to the client. The send CQE completes your <code>FlushAsync()</code>.', color:'#ff8a3e' },
  { panel:'ffP7', title:'Phase 8 — Buffer Recycle + Loop', desc:'Your handler calls <code>ReturnRing(bid)</code> to recycle the buffer, then <code>ResetRead()</code>. The reactor drains the return queue and calls <code>buf_ring_add + buf_ring_advance</code> to give the buffer back to the kernel. Then <code>await ReadAsync()</code> again. The loop repeats with zero allocations.', color:'#a06aff' },
];
let ffCur = -1;
function ffBuildDots() {
  const c = document.getElementById('ffDots');
  ffSteps.forEach((_, i) => { const d = document.createElement('div'); d.className = 'step-dot'; d.onclick = () => ffGoTo(i); c.appendChild(d); });
}
ffBuildDots();
function ffGoTo(idx) {
  ffCur = idx;
  ffSteps.forEach((s, i) => { const el = document.getElementById(s.panel); if (i === idx) { el.style.display=''; el.style.animation='none'; el.offsetHeight; el.style.animation=''; } else el.style.display='none'; });
  document.querySelectorAll('#ffDots .step-dot').forEach((d, i) => { d.classList.remove('active','done'); if (i < idx) d.classList.add('done'); if (i === idx) d.classList.add('active'); });
  ffSteps.forEach((s, i) => { const seg = document.getElementById('ffSeg'+i); if (i < idx) seg.style.background='#555c73'; else if (i === idx) seg.style.background=s.color; else seg.style.background=''; });
  document.getElementById('ffDesc').innerHTML = '<h4><span style="color:'+ffSteps[idx].color+';">'+ffSteps[idx].title+'</span></h4><p>'+ffSteps[idx].desc+'</p>';
  document.getElementById('ffPrev').disabled = idx <= 0;
  document.getElementById('ffNext').disabled = idx >= ffSteps.length - 1;
}
function ffStep(dir) { const n = ffCur + dir; if (n >= 0 && n < ffSteps.length) ffGoTo(n); }
ffGoTo(0);
</script>
