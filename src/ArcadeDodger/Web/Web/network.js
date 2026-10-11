// Browser transport/input and accessible shell only. World/HUD stay in C#/WASM/raylib.
export function startNetwork(host, canvas, status) {
  host.ActivateArena();
  const params = new URLSearchParams(location.search);
  const style = document.createElement('style');
  style.textContent = `
    .arena-shell{color:#ebf0fa;font:16px/1.4 ui-monospace,Consolas,monospace;box-sizing:border-box}
    .arena-shell *{box-sizing:border-box}
    .arena-shell[hidden],.arena-shell [hidden]{display:none!important}
    .arena-panel{position:fixed;left:50%;top:50%;transform:translate(-50%,-50%);width:min(560px,calc(100vw - 32px));max-height:calc(100dvh - 90px);overflow:auto;padding:24px;background:rgba(4,14,22,.97);border:1px solid #287084;border-top:3px solid #41f5eb;box-shadow:0 0 0 6px #060817a0,0 0 36px #41f5eb18;z-index:5}
    .arena-shell h1,.arena-shell h2{margin:0 0 12px;color:#41f5eb;font-size:clamp(24px,4vw,34px);line-height:1.1;letter-spacing:.08em}
    .arena-shell p{margin:10px 0}
    .arena-shell .eyebrow{color:#a29aff;font-size:12px;letter-spacing:.18em}
    .arena-shell .muted{color:#a5b8cd;font-size:14px}
    .arena-shell label{display:block;margin:12px 0 6px}
    .arena-shell input:not([type=checkbox]){display:block;width:100%;margin:6px 0;padding:10px;background:#060817;color:#ebf0fa;border:1px solid #287084;font:inherit}
    .arena-shell button,.arena-shell .action{display:inline-block;cursor:pointer;border:1px solid #41f5eb;background:#0b2833;color:#41f5eb;padding:10px 16px;font:inherit;text-decoration:none;min-height:44px}
    .arena-shell a{color:#41f5eb;overflow-wrap:anywhere}
    .arena-shell button:hover,.arena-shell .action:hover{background:#153c48}
    .arena-shell :focus-visible{outline:3px solid #ffcd69;outline-offset:3px}
    .arena-shell button:disabled{cursor:wait;opacity:.6}
    .arena-shell .actions{display:flex;flex-wrap:wrap;gap:12px;margin-top:16px;align-items:center}
    #network-info{position:fixed;bottom:12px;right:12px;max-width:calc(100vw - 24px);padding:6px 10px;background:#040e16ed;border:1px solid #287084;z-index:6;font-size:12px;display:flex;gap:12px;align-items:center;flex-wrap:wrap}
    #network-info button{font-size:12px;padding:4px 10px}
    #arena-respawn{position:fixed;left:50%;top:65%;transform:translateX(-50%);max-width:calc(100vw - 32px);z-index:4;padding:14px 24px;background:#041622;color:#41f5eb;border:1px solid #41f5eb;cursor:pointer}
    .arena-notice{color:#ffcd69;font-size:14px;overflow-wrap:anywhere}
    @media(max-height:560px){.arena-panel{padding:12px;max-height:calc(100dvh - 80px);top:12px;transform:translateX(-50%);font-size:14px;line-height:1.25}.arena-shell p{margin:6px 0}.arena-shell h1,.arena-shell h2{font-size:24px;margin-bottom:6px}.arena-shell label{margin:6px 0}.arena-shell .actions{margin-top:8px}}
    @media(max-width:480px){.arena-panel{padding:16px}#network-info{font-size:11px;gap:8px}.arena-shell .actions>*{flex:1;text-align:center}}
  `;
  document.head.append(style);
  function element(tag, text, className) {
    const node = document.createElement(tag);
    if (text) node.textContent = text;
    if (className) node.className = className;
    return node;
  }
  const form = element('form', '', 'arena-shell arena-panel');
  form.id = 'join-form'; form.setAttribute('aria-labelledby', 'arena-join-title');
  const heading = element('h1', 'NEON OVERDRIVE'); heading.id = 'arena-join-title';
  const label = element('label', 'Pilot callsign'); label.htmlFor = 'nickname';
  const name = element('input');
  name.id = 'nickname'; name.maxLength = 16; name.pattern = '[A-Za-z0-9 _\\-]*'; name.value = 'Pilot'; name.required = true;
  name.autocomplete = 'nickname'; name.setAttribute('aria-describedby', 'arena-name-help');
  const help = element('p', '1–16 letters, numbers, spaces, hyphens or underscores.', 'muted'); help.id = 'arena-name-help';
  const notice = element('p', 'Choose a callsign to launch.', 'arena-notice'); notice.setAttribute('role', 'status');
  const button = element('button', 'Join arena'); button.type = 'submit';
  const single = element('a', 'Single-player'); single.href = new URL(location.pathname, location.href).href;
  const actions = element('div', '', 'actions'); actions.append(button, single);
  form.append(element('p', 'LIVE NETWORK / PVPVE', 'eyebrow'), heading,
    element('p', 'Drop into a live PvPvE arena. Hunt aliens, collect upgrades, and watch for rival pilots.'),
    element('p', params.get('room') ? `Invitation to room ${params.get('room')}. Join if a slot is available.` : 'Anonymous drop-in • no account needed', 'muted'),
    label, name, help, notice, actions);
  document.body.append(form);
  const info = element('output', '', 'arena-shell'); info.id = 'network-info';
  const connection = element('span', 'Offline • ready to join'); connection.setAttribute('role', 'status');
  const link = element('a'); link.hidden = true; link.title = 'Open or copy this link to invite another pilot';
  const menuButton = element('button', 'Menu [Esc]'); menuButton.type = 'button'; menuButton.hidden = true;
  menuButton.setAttribute('aria-controls', 'arena-menu'); menuButton.setAttribute('aria-expanded', 'false');
  info.append(connection, link, menuButton); document.body.append(info);
  // Keep transport controls above the canvas loadout strip, including when
  // the 16:9 canvas is letterboxed in a portrait or short viewport.
  function positionInfo() {
    const bounds = canvas.getBoundingClientRect();
    info.style.bottom = `${Math.max(12, innerHeight - bounds.bottom + bounds.height * 74 / 540)}px`;
  }
  new ResizeObserver(positionInfo).observe(canvas);
  window.addEventListener('resize', positionInfo);
  positionInfo();
  const menu = element('section', '', 'arena-shell arena-panel'); menu.id = 'arena-menu'; menu.hidden = true;
  menu.setAttribute('role', 'dialog'); menu.setAttribute('aria-modal', 'true'); menu.setAttribute('aria-labelledby', 'arena-menu-title');
  const menuHeading = element('h2', 'LIVE ROOM MENU'); menuHeading.id = 'arena-menu-title';
  const resume = element('button', 'Resume'); resume.type = 'button';
  const leave = element('button', 'Leave'); leave.type = 'button'; leave.title = 'Leave this live room and return to the join form';
  const solo = element('a', 'Single-player', 'action'); solo.href = single.href;
  const effectsLabel = element('label');
  const effects = element('input'); effects.type = 'checkbox'; effects.id = 'arena-reduced-effects';
  effectsLabel.append(effects, document.createTextNode(' Reduced effects [R]'));
  const menuActions = element('div', '', 'actions'); menuActions.append(resume, leave, solo);
  menu.append(element('p', 'PILOT CONSOLE', 'eyebrow'), menuHeading,
    element('p', 'The room stays live. Your ship can still be hit while this menu is open.', 'arena-notice'),
    element('p', 'WASD / arrows: move • Mouse / Space: fire • Shift: dash', 'muted'), effectsLabel, menuActions);
  document.body.append(menu);
  const retry = element('button', 'Ship destroyed — respawn', 'arena-shell'); retry.id = 'arena-respawn'; retry.type = 'button'; retry.hidden = true;
  document.body.append(retry);
  let socket, timer, joinTimeout, seq = 0, joined = false, menuOpen = false, dead = false, mouseFire = false, u = 0.5, v = 0.5;
  let reducedEffects = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  effects.checked = reducedEffects;
  const keys = new Set();
  const supported = new Set(['KeyW','KeyA','KeyS','KeyD','ArrowUp','ArrowDown','ArrowLeft','ArrowRight','Space','ShiftLeft','ShiftRight']);
  const held = (...codes) => codes.some(k => keys.has(k)) ? 1 : 0;
  const editable = target => target instanceof Element && !!target.closest('input:not([type=checkbox]),textarea,select,[contenteditable]:not([contenteditable=false])');
  const inputBlocked = () => menuOpen || !form.hidden || document.hidden || !document.hasFocus() || editable(document.activeElement) || (document.activeElement instanceof Element && !!document.activeElement.closest('button,a,input'));
  function presentation() { host.ArenaPresentation(menuOpen, reducedEffects); }
  function sendInput() {
    if (!joined || socket?.readyState !== WebSocket.OPEN) return;
    if (socket.bufferedAmount > 4096 || seq >= 2147483647) { socket.close(); return; }
    const blocked = inputBlocked();
    if (blocked) { keys.clear(); mouseFire = false; }
    socket.send(JSON.stringify({v:1, type:'input', seq:++seq,
      x:blocked ? 0 : held('KeyD','ArrowRight')-held('KeyA','ArrowLeft'),
      y:blocked ? 0 : held('KeyS','ArrowDown')-held('KeyW','ArrowUp'),
      aim:host.ArenaAim(u,v), fire:!blocked && (mouseFire || !!held('Space')), dash:!blocked && !!held('ShiftLeft','ShiftRight')}));
  }
  function clearInput() { keys.clear(); mouseFire = false; sendInput(); }
  function setMenu(open) {
    menuOpen = open && joined; menu.hidden = !menuOpen;
    menuButton.setAttribute('aria-expanded', String(menuOpen));
    canvas.inert = menuOpen || !form.hidden;
    link.inert = menuOpen; menuButton.inert = menuOpen;
    retry.hidden = !joined || !dead || menuOpen;
    clearInput(); presentation();
    if (menuOpen) resume.focus(); else if (joined) canvas.focus();
  }
  function setEffects(value) { reducedEffects = value; effects.checked = value; presentation(); }
  menuButton.addEventListener('click', () => setMenu(true));
  resume.addEventListener('click', () => setMenu(false));
  effects.addEventListener('change', () => setEffects(effects.checked));
  function resetConnection(message) {
    joined = false; clearInterval(timer); clearTimeout(joinTimeout); keys.clear(); mouseFire = false; dead = false;
    socket = undefined;
    for (const key of ['player','room','tick','snapshot']) delete info.dataset[key];
    form.hidden = false; button.disabled = false; button.textContent = 'Join arena';
    retry.hidden = true; retry.disabled = false; retry.textContent = 'Ship destroyed — respawn';
    menuButton.hidden = true; link.hidden = true;
    connection.textContent = 'Disconnected • rejoin available'; notice.textContent = message;
    host.ActivateArena();
    setMenu(false); name.focus();
  }
  leave.addEventListener('click', () => {
    clearInput(); const previous = socket; socket = undefined; previous?.close();
    resetConnection('You left the live room. Your ship may remain briefly. Rejoin starts a fresh run.');
  });
  retry.addEventListener('click', () => {
    if (!joined || menuOpen || socket?.readyState !== WebSocket.OPEN || retry.disabled) return;
    if (socket.bufferedAmount > 4096 || seq >= 2147483647) { socket.close(); return; }
    clearInput(); retry.disabled = true; retry.textContent = 'Respawning…';
    socket.send(JSON.stringify({v:1,type:'respawn',seq:++seq})); canvas.focus();
  });
  window.addEventListener('keydown', e => {
    if (joined && e.code === 'Escape' && !editable(e.target)) {
      e.preventDefault(); if (!e.repeat) setMenu(!menuOpen); return;
    }
    if (joined && e.code === 'KeyR' && !editable(e.target) && !e.ctrlKey && !e.metaKey && !e.altKey) {
      e.preventDefault(); if (!e.repeat) setEffects(!reducedEffects); return;
    }
    if (menuOpen && e.key === 'Tab') {
      const controls = [effects, resume, leave, solo];
      if (e.shiftKey && document.activeElement === controls[0]) { e.preventDefault(); solo.focus(); }
      else if (!e.shiftKey && document.activeElement === solo) { e.preventDefault(); effects.focus(); }
      return;
    }
    if (joined && supported.has(e.code) && !editable(e.target)) {
      // Do not let a suppressed fire key activate the focused Resume/Leave button
      // on keyup. Space still operates the effects checkbox accessibly.
      if (e.code !== 'Space' || e.target !== effects) e.preventDefault();
      // A key held through a menu/blur must be released before it becomes input
      // again; repeated keydown events must not revive cleared held state.
      if (!inputBlocked() && !e.repeat) keys.add(e.code);
    }
  });
  window.addEventListener('keyup', e => { keys.delete(e.code); });
  document.addEventListener('focusin', () => { if (inputBlocked()) clearInput(); });
  canvas.addEventListener('pointermove', e => {
    const r = canvas.getBoundingClientRect();
    if (r.width && r.height) { u = Math.max(0, Math.min(1, (e.clientX-r.left)/r.width)); v = Math.max(0, Math.min(1, (e.clientY-r.top)/r.height)); }
  });
  canvas.addEventListener('pointerdown', e => { if (joined && !menuOpen && e.button === 0) { canvas.focus(); if (!inputBlocked()) mouseFire = true; } });
  window.addEventListener('pointerup', e => { if (e.button === 0) mouseFire = false; });
  canvas.addEventListener('pointercancel', clearInput);
  window.addEventListener('blur', clearInput);
  document.addEventListener('visibilitychange', () => { if (document.hidden) clearInput(); });
  window.addEventListener('pagehide', () => { clearInput(); clearInterval(timer); clearTimeout(joinTimeout); socket?.close(); }, {once:true});
  form.addEventListener('submit', e => {
    e.preventDefault();
    if (button.disabled) return;
    if (!name.value.trim()) { notice.textContent = 'Enter a callsign before joining.'; name.focus(); return; }
    button.disabled = true; button.textContent = 'Connecting…'; notice.textContent = 'Connecting to the live room…'; connection.textContent = 'Connecting…';
    const url = new URL('/ws', location.href); url.protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
    let current, failure = '';
    try { current = new WebSocket(url); socket = current; }
    catch { resetConnection('Could not open a connection. Try joining again.'); return; }
    seq = 0;
    for (const key of ['player','room','tick','snapshot']) delete info.dataset[key];
    joinTimeout = setTimeout(() => {
      if (socket === current && !joined) { failure = 'The server did not answer. Check the server and try again.'; current.close(); }
    }, 15000);
    current.addEventListener('open', () => {
      if (socket === current) current.send(JSON.stringify({v:1,type:'join',name:name.value,room:params.get('room') || ''}));
    });
    current.addEventListener('message', e => {
      if (socket !== current) return;
      try {
        if (typeof e.data !== 'string' || e.data.length > 65536) throw new Error('Invalid server payload');
        const message = JSON.parse(e.data);
        if (message.v !== 1) throw new Error('Protocol mismatch');
        if (message.type === 'welcome') {
          if (joined) throw new Error('Duplicate welcome');
          clearTimeout(joinTimeout); host.ArenaWelcome(message.id); joined = true; form.hidden = true; dead = false;
          const invite = new URL(location.href); invite.search = new URLSearchParams({multiplayer:'1',room:message.room});
          link.href = invite.href; link.textContent = `Room ${message.room} • invite link`; link.hidden = false;
          connection.textContent = 'Connected • LIVE'; menuButton.hidden = false;
          info.dataset.player = message.id; info.dataset.room = message.room;
          setMenu(false); timer = setInterval(sendInput, 1000/30);
        } else if (message.type === 'snapshot') {
          if (!joined) throw new Error('Snapshot before welcome');
          host.ArenaSnapshot(e.data);
          const self = message.players.find(p => p.id === Number(info.dataset.player));
          const nowDead = !!self && self.hull <= 0;
          if (nowDead && !dead) { clearInput(); retry.disabled = false; retry.textContent = 'Ship destroyed — respawn'; }
          dead = nowDead; retry.hidden = !dead || menuOpen;
          // Read-only transport diagnostics also used by browser acceptance tests.
          info.dataset.tick = message.tick; info.dataset.snapshot = e.data;
        } else if (message.type === 'error') {
          const reason = `Server refused the request: ${message.code}.`;
          if (!joined) { failure = `${reason} Try another room or join again.`; notice.textContent = failure; current.close(); }
          else { connection.textContent = reason; retry.disabled = false; retry.textContent = 'Retry respawn'; }
        } else throw new Error('Unknown server message');
      } catch (error) {
        failure = 'Network data could not be read. Please try joining again.'; console.error(error); current.close();
      }
    });
    current.addEventListener('close', () => {
      if (socket !== current) return;
      resetConnection(failure || 'Disconnected. Rejoin starts a fresh run; the server retains your previous ship briefly.');
    });
    current.addEventListener('error', () => {
      if (socket !== current) return;
      failure = 'Connection failed. A multiplayer server must be running on this origin. Try again when it is available.';
      notice.textContent = failure; connection.textContent = 'Connection failed';
    });
  });
  // main.js retains its runtime error overlay; connection messages live in this shell.
  presentation(); canvas.inert = true; name.focus();
}
