import { presentSnapshot, expandCombat } from './snapshot.js';
// Browser transport/input and accessible shell only. World/HUD stay in C#/WASM/raylib.
import { createSectorConsole } from './sector.js';
import { createSkillTree } from './skills.js';
import { createWeaponBench } from './weapons.js';
import { createArenaAudio } from './audio.js';
import { createFriendsMenu } from './friends.js';
export function startNetwork(host, canvas, status) {
  document.body.classList.add("arena-fullscreen");
  const audio = createArenaAudio();
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
    #network-info{position:fixed;bottom:12px;right:18px;padding:5px;background:#07111bed;border:2px ridge #85744e;z-index:6;display:grid;grid-template-columns:repeat(3,var(--command-size,48px));gap:5px;box-shadow:inset 0 0 14px #000}
    #network-info>span,#network-info>a{display:none}
    #network-info:not(:has(button:not([hidden]))){display:none}
    #network-info button{position:relative;width:var(--command-size,48px);height:var(--command-size,48px);min-height:44px;padding:0;font-size:0;border:3px ridge #7b806e;background:radial-gradient(ellipse at 30% 15%,#425252,#111e2b 70%);box-shadow:inset 0 0 8px #000;color:#ffe5a1}
    #network-info button::before{content:'';position:absolute;inset:3px 3px 10px;background:var(--command-icon) center/contain no-repeat;filter:drop-shadow(0 2px 2px #000)}
    #network-info button::after{content:attr(data-hotkey);position:absolute;right:2px;bottom:0;font:bold 10px/1.2 monospace;color:#ffe5a1;text-shadow:1px 1px #000}
    #network-info button:hover,#network-info button:focus-visible{border-color:#f7d780;background-color:#425347;box-shadow:0 0 9px #e6c37970}
    #arena-respawn{position:fixed;left:50%;top:65%;transform:translateX(-50%);max-width:calc(100vw - 32px);z-index:4;padding:14px 24px;background:#041622;color:#41f5eb;border:1px solid #41f5eb;cursor:pointer}
    .credit-payout{position:fixed;top:30%;left:50%;transform:translateX(-50%);color:#ffdd79;text-shadow:0 2px 4px #000;font:bold 22px monospace;pointer-events:none;z-index:8;background:#171208bb;padding:10px 16px;border:1px solid #baa35b}
    .arena-notice{color:#ffcd69;font-size:14px;overflow-wrap:anywhere}
    @media(max-height:560px){.arena-panel{padding:12px;max-height:calc(100dvh - 80px);top:12px;transform:translateX(-50%);font-size:14px;line-height:1.25}.arena-shell p{margin:6px 0}.arena-shell h1,.arena-shell h2{font-size:24px;margin-bottom:6px}.arena-shell label{margin:6px 0}.arena-shell .actions{margin-top:8px}}
    @media(max-width:480px){.arena-panel{padding:16px}.arena-shell .actions>*{flex:1;text-align:center}}
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
  try { const saved = localStorage.getItem('neon.lastName'); if (saved && /^[A-Za-z0-9 _-]{1,16}$/.test(saved)) name.value = saved; } catch {}
  name.autocomplete = 'nickname'; name.setAttribute('aria-describedby', 'arena-name-help');
  const help = element('p', '1–16 letters, numbers, spaces, hyphens or underscores.', 'muted'); help.id = 'arena-name-help';
  const notice = element('p', 'Choose a callsign to launch.', 'arena-notice'); notice.setAttribute('role', 'status');
  const button = element('button', 'Join arena'); button.type = 'submit';
  const actions = element('div', '', 'actions'); actions.append(button);
  form.append(element('p', 'LIVE NETWORK / PVPVE', 'eyebrow'), heading,
    element('p', 'Drop into a live PvPvE arena. Hunt aliens, collect upgrades, and watch for rival pilots.'),
    element('p', params.get('room') ? `Invitation to room ${params.get('room')}. Join if a slot is available.` : 'Anonymous drop-in • no account needed', 'muted'),
    label, name, help, notice, actions);
  document.body.append(form);
  const payout=element('div','','credit-payout');payout.setAttribute('role','status');payout.hidden=true;document.body.append(payout);
  let lastCredit=0,lastCreditLife=-1,payoutTimer;
  const info = element('div', '', 'arena-shell'); info.id = 'network-info';
  const connection = element('span', 'Offline • ready to join'); connection.setAttribute('role', 'status');
  const link = element('a'); link.hidden = true; link.title = 'Open or copy this link to invite another pilot';
  const menuButton = element('button', 'Menu [Esc]'); menuButton.type = 'button'; menuButton.hidden = true;
  menuButton.setAttribute('aria-controls', 'arena-menu'); menuButton.setAttribute('aria-expanded', 'false');
  info.append(connection, link, menuButton); document.body.append(info);
  // Keep transport controls above the uniformly scaled, bottom-anchored HUD.
  function positionInfo() {
    const bounds = canvas.getBoundingClientRect();
    const layout=host.ArenaHudLayout(bounds.width,bounds.height).split(',').map(Number);
    info.style.left=`${bounds.left+layout[0]}px`;info.style.top=`${bounds.top+layout[1]}px`;
    info.style.width=`${layout[2]}px`;info.style.height=`${layout[3]}px`;
    info.style.right='auto';info.style.bottom='auto';
    info.style.setProperty('--command-size',`${layout[4]}px`);
    document.documentElement.style.setProperty('--sector-guide-top',`${bounds.top+layout[5]}px`);
    info.dataset.layout=JSON.stringify(layout);

  }
  new ResizeObserver(positionInfo).observe(canvas);
  window.addEventListener('resize', positionInfo);
  positionInfo();
  const menu = element('section', '', 'arena-shell arena-panel'); menu.id = 'arena-menu'; menu.hidden = true;
  menu.setAttribute('role', 'dialog'); menu.setAttribute('aria-modal', 'true'); menu.setAttribute('aria-labelledby', 'arena-menu-title');
  const menuHeading = element('h2', 'LIVE ROOM MENU'); menuHeading.id = 'arena-menu-title';
  const resume = element('button', 'Resume'); resume.type = 'button';
  const leave = element('button', 'Leave'); leave.type = 'button'; leave.title = 'Leave this live room and return to the join form';
  const soundLabel = element('label');
  const sound = element('input'); sound.type='checkbox'; sound.checked=true; sound.id='arena-sound';
  soundLabel.append(sound,document.createTextNode(' Sound effects'));
  sound.addEventListener('change',()=>audio.setMuted(!sound.checked));
  const effectsLabel = element('label');
  const effects = element('input'); effects.type = 'checkbox'; effects.id = 'arena-reduced-effects';
  effectsLabel.append(effects, document.createTextNode(' Reduced effects [R]'));
  const menuActions = element('div', '', 'actions'); menuActions.append(resume, leave);
  menu.append(element('p', 'PILOT CONSOLE', 'eyebrow'), menuHeading,
    element('p', 'The room stays live. Your ship can still be hit while this menu is open.', 'arena-notice'),
    element('p', 'WASD / arrows: move • Mouse / Space: fire • Coast to stop', 'muted'), effectsLabel, soundLabel, menuActions);
  const sessionDetails=element("div","","muted");sessionDetails.append(connection,document.createTextNode(" · "),link);menu.append(sessionDetails);
  document.body.append(menu);
  const retry = element('button', 'Ship destroyed — respawn', 'arena-shell'); retry.id = 'arena-respawn'; retry.type = 'button'; retry.hidden = true;
  document.body.append(retry);
  let socket, timer, joinTimeout, seq = 0, joined = false, menuOpen = false, dead = false, mouseFire = false, u = 0.5, v = 0.5, aim = 0, pointer = null;
  let reducedEffects = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  effects.checked = reducedEffects;
  const keys = new Set();
  const supported = new Set(['KeyW','KeyA','KeyS','KeyD','ArrowUp','ArrowDown','ArrowLeft','ArrowRight','Space']);
  const held = (...codes) => codes.some(k => keys.has(k)) ? 1 : 0;
  const editable = target => target instanceof Element && !!target.closest('input:not([type=checkbox]),textarea,select,[contenteditable]:not([contenteditable=false])');
  let bench, friends, sector;
  const skills = createSkillTree(element, info, id => {
    if (!joined || dead || socket?.readyState !== WebSocket.OPEN) return;
    if (socket.bufferedAmount > 4096 || seq >= 2147483647) { socket.close(); return; }
    socket.send(JSON.stringify({v:6,type:'choice',seq:++seq,choice:id}));
  }, open => {
    if(open&&sector?.open)sector.setOpen('');
    if (open && bench?.open) bench.setOpen(false);
    if (open && friends?.open) friends.setOpen(false);
    info.inert = open || !!bench?.open;
    canvas.inert = open || menuOpen || !form.hidden;
    clearInput(); presentation();
    if (!open && joined) canvas.focus();
  });
  bench = createWeaponBench(element, info, weapons => {
    if (!joined || dead || socket?.readyState !== WebSocket.OPEN) return;
    if (socket.bufferedAmount > 4096 || seq >= 2147483647) { socket.close(); return; }
    sendInput();
    socket.send(JSON.stringify({v:6,type:'equip',seq:++seq,weapons}));
    return seq;
  }, module => {
    if (!joined || dead || socket?.readyState !== WebSocket.OPEN) return;
    if (socket.bufferedAmount > 4096 || seq >= 2147483647) { socket.close(); return; }
    sendInput(); socket.send(JSON.stringify({v:6,type:'buy',seq:++seq,module})); return seq;
  }, open => {
    if(open&&sector?.open)sector.setOpen('');
    if (open && skills.open) skills.setOpen(false);
    if (open && friends?.open) friends.setOpen(false);
    info.inert = open || skills.open;
    canvas.inert = open || skills.open || menuOpen || !form.hidden;
    clearInput(); presentation(); if (!open && joined) canvas.focus();
  });
  friends = createFriendsMenu(element, info, (name, action) => {
    if (!joined || socket?.readyState !== WebSocket.OPEN) return;
    if (socket.bufferedAmount > 4096 || seq >= 2147483647) { socket.close(); return; }
    socket.send(JSON.stringify({v:6,type:'friend',seq:++seq,name,action})); return seq;
  }, open => {
    if (open && bench.open) bench.setOpen(false);
    if(open&&sector?.open)sector.setOpen('');
    if (open && skills.open) skills.setOpen(false);
    info.inert = open || bench.open || skills.open;
    canvas.inert = open || bench.open || skills.open || menuOpen || !form.hidden;
    clearInput(); presentation(); if (!open && joined) canvas.focus();
  });
  sector=createSectorConsole(element,info,(action,target)=>{
    if(!joined||dead||socket?.readyState!==WebSocket.OPEN)return;
    if(socket.bufferedAmount>4096||seq>=2147483647){socket.close();return;}
    socket.send(JSON.stringify({v:6,type:'sector',seq:++seq,action,target}));return seq;
  },open=>{
    if(open){bench.setOpen(false);skills.setOpen(false);friends.setOpen(false);}
    canvas.inert=open||bench.open||skills.open||friends.open||menuOpen||!form.hidden;
    info.inert=open||bench.open||skills.open||friends.open;
    clearInput();presentation();if(!open&&joined)canvas.focus();
  },()=>bench.openShop());
  // Keep original button text for accessible names; snapshots may update skill counts.
  const commandArt=[
    '<path d="M14 18H50M14 32H50M14 46H50"/>',
    '<path d="m32 8 6 16 18 2-14 11 4 18-14-9-14 9 4-18L8 26l18-2Z"/>',
    '<path d="M12 48 39 12 51 22 24 55ZM24 30 38 40M38 10 42 4M51 30H59"/>',
    '<circle cx="23" cy="22" r="9"/><circle cx="44" cy="25" r="7"/><path d="M7 53V45Q23 28 38 45V53ZM39 38Q55 37 57 51"/>',
    '<path d="m8 16 16-6 16 7 16-6v39l-16 6-16-7-16 6ZM24 10V49M40 17V56"/>',
    '<path d="M12 10H52V54H12ZM12 20H52M22 40H45M36 31 45 40 36 49"/>'
  ];
  [...info.querySelectorAll('button')].forEach((button,i)=>{
    const svg=`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64"><g fill="#9ac4bc22" stroke="${['#b9c9d2','#edce82','#edab75','#94d6ae','#93c7ed','#bca0e8'][i]}" stroke-width="3" stroke-linejoin="round" stroke-linecap="round">${commandArt[i]}</g></svg>`;
    button.style.setProperty('--command-icon',`url("data:image/svg+xml,${encodeURIComponent(svg)}")`);
    const update=()=>{const label=button.textContent;button.title=label;button.setAttribute('aria-label',label);button.dataset.hotkey=(label.match(/\[(.*?)\]/)?.[1]||'')+(label.match(/\+\d+/)?.[0]||'');};
    new MutationObserver(update).observe(button,{childList:true,characterData:true,subtree:true});update();
  });
  const inputBlocked = () => sector?.open || friends?.open || bench?.open || skills.open || menuOpen || !form.hidden || document.hidden || !document.hasFocus() || editable(document.activeElement) || (document.activeElement instanceof Element && !!document.activeElement.closest('button,a,input'));
  function presentation() { host.ArenaPresentation(!!sector?.open || menuOpen || skills.open || !!bench?.open || !!friends?.open, reducedEffects); }
  function localInput() {
    const blocked = !joined || dead || inputBlocked();
    if (blocked) { keys.clear(); mouseFire = false; }
    else { if (pointer) trackPointer(pointer); aim = host.ArenaAim(u,v); }
    const intent = {
      x:blocked ? 0 : held('KeyD','ArrowRight')-held('KeyA','ArrowLeft'),
      y:blocked ? 0 : held('KeyS','ArrowDown')-held('KeyW','ArrowUp'),
      aim, fire:!blocked && (mouseFire || !!held('Space')),
      menu:!!sector?.open || skills.open || !!bench?.open || !!friends?.open || menuOpen, editing:!!sector?.open || skills.open || !!bench?.open || !!friends?.open};
    host.ArenaInput(intent.x, intent.y, intent.aim, intent.menu);
    return intent;
  }
  function sendInput() {
    if (!joined || socket?.readyState !== WebSocket.OPEN) return;
    if (socket.bufferedAmount > 4096 || seq >= 2147483647) { socket.close(); return; }
    const intent = localInput();
    socket.send(JSON.stringify({v:6, type:'input', seq:++seq, ...intent}));
    host.ArenaSentInput(seq);
  }
  // Movement presentation runs at display cadence, independent of 30 Hz transport.
  let inputFrame;
  function refreshLocalInput() { localInput(); inputFrame = requestAnimationFrame(refreshLocalInput); }
  inputFrame = requestAnimationFrame(refreshLocalInput);
  function clearInput() { keys.clear(); mouseFire = false; localInput(); sendInput(); }
  function setMenu(open) {
    if (skills.open) skills.setOpen(false);
    if (bench?.open) bench.setOpen(false);
    if (friends?.open) friends.setOpen(false);
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
    document.body.classList.add("arena-fullscreen");
    audio.reset(); sector.update(null,{},false); friends.update(null, {}, false); bench.update(null, false); skills.update(null, false); setMenu(false); name.focus();
  }
  leave.addEventListener('click', () => {
    clearInput(); const previous = socket; socket = undefined; previous?.close();
    resetConnection('You left the live room. Your ship may remain briefly. Rejoin starts a fresh run.');
  });
  retry.addEventListener('click', () => {
    if (!joined || menuOpen || socket?.readyState !== WebSocket.OPEN || retry.disabled) return;
    if (socket.bufferedAmount > 4096 || seq >= 2147483647) { socket.close(); return; }
    clearInput(); retry.disabled = true; retry.textContent = 'Respawning…';
    socket.send(JSON.stringify({v:6,type:'respawn',seq:++seq})); canvas.focus();
  });
  window.addEventListener('keydown', e => {
    if(joined&&!editable(e.target)&&!bench.open&&!skills.open&&!friends.open&&!menuOpen&&sector.keydown(e))return;
    if (friends.keydown(e)) return;
    if (joined && !menuOpen && !bench.open && !skills.open && e.code === 'KeyP' && !editable(e.target)) {
      e.preventDefault(); if (!e.repeat) friends.setOpen(true); return;
    }
    if (bench.keydown(e)) return;
    if (skills.keydown(e)) return;
    if (joined && !dead && !menuOpen && e.code === 'KeyI' && !editable(e.target)) {
      e.preventDefault(); if (!e.repeat) bench.setOpen(true); return;
    }
    if (joined && !dead && !menuOpen && e.code === 'Tab' && !editable(e.target)) {
      e.preventDefault(); if (!e.repeat) skills.setOpen(true); return;
    }
    if (joined && e.code === 'Escape' && !editable(e.target)) {
      e.preventDefault(); if (!e.repeat) setMenu(!menuOpen); return;
    }
    if (joined && e.code === 'KeyR' && !editable(e.target) && !e.ctrlKey && !e.metaKey && !e.altKey) {
      e.preventDefault(); if (!e.repeat) setEffects(!reducedEffects); return;
    }
    if (menuOpen && e.key === 'Tab') {
      const controls = [effects, sound, resume, leave, solo];
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
      localInput();
    }
  });
  window.addEventListener('keyup', e => { keys.delete(e.code); if (supported.has(e.code)) localInput(); });
  document.addEventListener('focusin', () => { if (inputBlocked()) clearInput(); });
  function trackPointer(e) {
    pointer = {clientX:e.clientX, clientY:e.clientY};
    const r = canvas.getBoundingClientRect();
    if (r.width && r.height) { u = Math.max(0, Math.min(1, (e.clientX-r.left)/r.width)); v = Math.max(0, Math.min(1, (e.clientY-r.top)/r.height)); }
  }
  // Track the physical cursor over equipment too, so closing it cannot restore an old aim position.
  // Gameplay remains neutral while a menu owns input, including during pointer capture/dragging.
  window.addEventListener('pointermove', e => {
    trackPointer(e); if (!inputBlocked()) localInput();
  });
  canvas.addEventListener('pointerdown', e => { if (joined && !menuOpen && e.button === 0) { trackPointer(e); canvas.focus(); if (!inputBlocked()) mouseFire = true; localInput(); } });
  window.addEventListener('pointerup', e => { if (e.button === 0) { mouseFire = false; localInput(); } });
  canvas.addEventListener('pointercancel', clearInput);
  window.addEventListener('blur', clearInput);
  document.addEventListener('visibilitychange', () => { if (document.hidden) clearInput(); });
  window.addEventListener('pagehide', () => { clearInput(); cancelAnimationFrame(inputFrame); clearInterval(timer); clearTimeout(joinTimeout); socket?.close(); }, {once:true});
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
      if (socket === current) current.send(JSON.stringify({v:6,type:'join',name:name.value,room:params.get('room') || ''}));
    });
    current.addEventListener('message', e => {
      if (socket !== current) return;
      try {
        if (typeof e.data !== 'string' || e.data.length > 524288) throw new Error('Invalid server payload');
        const message = JSON.parse(e.data);
        if (message.v !== 6) throw new Error('Protocol mismatch');
        if (message.type === 'welcome') {
          lastCredit=0;lastCreditLife=-1;payout.hidden=true;
          if (joined) throw new Error('Duplicate welcome');
          bench.setCatalog(message.catalog, message.unlockLevels); skills.setCatalog(message.skills);
          sector.setup(message.sector,message.contracts);host.ArenaSector(JSON.stringify(message.sector));
          try { localStorage.setItem('neon.lastName',name.value.trim()); } catch {}
          clearTimeout(joinTimeout); host.ArenaWelcome(message.id); joined = true; form.hidden = true; dead = false;
          const invite = new URL(location.href); invite.search = new URLSearchParams({room:message.room});
          link.href = invite.href; link.textContent = `Room ${message.room} • invite link`; link.hidden = false;
          connection.textContent = 'Connected • LIVE'; menuButton.hidden = false;
          info.dataset.player = message.id; info.dataset.room = message.room;
          setMenu(false); timer = setInterval(sendInput, 1000/30);
        } else if (message.type === 'snapshot') {
          if (!joined) throw new Error('Snapshot before welcome');
          expandCombat(message);
          for(const key of ['workshop','social','skillReceipt','creditReward']) {
            if(message[key]==null)message[key]=current.privateState?.[key]??null;
          }
          if(message.sector && message.sector.pilot==null)message.sector.pilot=current.privateState?.sector?.pilot??null;
          current.privateState=message;
          const fullSnapshot=JSON.stringify(message);
          presentSnapshot(host,message);
          const self = message.players.find(p => p.id === Number(info.dataset.player));
          const nowDead = !!self && self.hull <= 0;
          if (nowDead && !dead) { clearInput(); retry.disabled = false; retry.textContent = 'Ship destroyed — respawn'; }
          dead = nowDead; retry.hidden = !dead || menuOpen;
          if(self?.life!==lastCreditLife){lastCredit=0;lastCreditLife=self?.life;payout.hidden=true;}
          if(message.creditReward?.sequence>lastCredit){const reward=message.creditReward;lastCredit=reward.sequence;payout.textContent=`${reward.lucky?'Lucky! ':''}+${reward.amount} credits`;payout.hidden=false;clearTimeout(payoutTimer);payoutTimer=setTimeout(()=>payout.hidden=true,1600);}
          sector.update(self,message,joined); friends.update(self, message, joined); skills.update(self, joined, message.skillReceipt); bench.update(self, joined, message.workshop); audio.observe(message, Number(info.dataset.player));
          // Read-only transport diagnostics also used by browser acceptance tests.
          info.dataset.tick = message.tick; info.dataset.snapshot = fullSnapshot;
        } else if (message.type === 'error') {
          const reason = message.code === "refresh_required" ? "Game updated. Refresh this page to continue." : message.code === "name_in_use" ? "That callsign is already connected. Choose another name or close the other session." : `Server refused the request: ${message.code}.`;
          if (!joined) { failure = `${reason} Try another room or join again.`; notice.textContent = failure; current.close(); }
          else { connection.textContent = reason; retry.disabled = false; retry.textContent = 'Retry respawn'; }
        } else throw new Error('Unknown server message');
      } catch (error) {
        failure = 'The game files or server data are incompatible. Reload this page to get the current build.'; console.error(error); current.close();
      }
    });
    current.addEventListener('close', () => {
      if (socket !== current) return;
      resetConnection(failure || 'Disconnected. Rejoin starts a fresh run; the server retains your previous ship briefly.');
    });
    current.addEventListener('error', () => {
      if (socket !== current) return;
      failure ||= 'Connection failed. A multiplayer server must be running on this origin. Try again when it is available.';
      notice.textContent = failure; connection.textContent = 'Connection failed';
    });
  });
  // main.js retains its runtime error overlay; connection messages live in this shell.
  presentation(); canvas.inert = true; name.focus();
}
