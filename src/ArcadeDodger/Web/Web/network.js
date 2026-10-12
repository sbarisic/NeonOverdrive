import {
  createJoinForm,
  createHudControls,
  createPauseMenu,
  decorateCommandButtons,
} from './network-shell.js';
import { shellStyles } from './network.styles.js';
import { presentSnapshot, expandCombat } from './snapshot.js';
// Browser transport/input and accessible shell only. World/HUD stay in C#/WASM/raylib.
import { createSectorConsole } from './sector.js';
import { createSkillTree } from './skills.js';
import { createWeaponBench } from './weapons.js';
import { createArenaAudio } from './audio.js';
import { createFriendsMenu } from './friends.js';
export function startNetwork(host, canvas, status) {
  document.body.classList.add('arena-fullscreen');
  const audio = createArenaAudio();
  const params = new URLSearchParams(location.search);
  const style = document.createElement('style');
  style.textContent = shellStyles;
  document.head.append(style);
  function element(tag, text, className) {
    const node = document.createElement(tag);
    if (text) node.textContent = text;
    if (className) node.className = className;
    return node;
  }
  const { form, name, notice, button } = createJoinForm(element, params);
  const payout = element('div', '', 'credit-payout');
  payout.setAttribute('role', 'status');
  payout.hidden = true;
  document.body.append(payout);
  let lastCredit = 0,
    lastCreditLife = -1,
    payoutTimer;
  const { info, connection, link, menuButton, radarControls, setWaypoint } = createHudControls(
    element,
    canvas,
    host,
  );
  const { menu, resume, leave, effects, sound } = createPauseMenu(element, audio, connection, link);
  const retry = element('button', 'Ship destroyed — respawn', 'arena-shell');
  retry.id = 'arena-respawn';
  retry.type = 'button';
  retry.hidden = true;
  document.body.append(retry);
  let socket,
    timer,
    joinTimeout,
    seq = 0,
    joined = false,
    menuOpen = false,
    dead = false,
    mouseFire = false,
    u = 0.5,
    v = 0.5,
    aim = 0,
    pointer = null;
  let reducedEffects = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  effects.checked = reducedEffects;
  const keys = new Set();
  const supported = new Set([
    'KeyW',
    'KeyA',
    'KeyS',
    'KeyD',
    'ArrowUp',
    'ArrowDown',
    'ArrowLeft',
    'ArrowRight',
    'Space',
  ]);
  const held = (...codes) => (codes.some((k) => keys.has(k)) ? 1 : 0);
  const editable = (target) =>
    target instanceof Element &&
    !!target.closest(
      'input:not([type=checkbox]),textarea,select,[contenteditable]:not([contenteditable=false])',
    );
  let bench, friends, sector;
  const skills = createSkillTree(
    element,
    info,
    (id) => {
      if (!joined || dead || socket?.readyState !== WebSocket.OPEN) return;
      if (socket.bufferedAmount > 4096 || seq >= 2147483647) {
        socket.close();
        return;
      }
      socket.send(JSON.stringify({ v: 6, type: 'choice', seq: ++seq, choice: id }));
    },
    (open) => {
      if (open && sector?.open) sector.setOpen('');
      if (open && bench?.open) bench.setOpen(false);
      if (open && friends?.open) friends.setOpen(false);
      info.inert = open || !!bench?.open;
      canvas.inert = open || menuOpen || !form.hidden;
      clearInput();
      presentation();
      if (!open && joined) canvas.focus();
    },
  );
  bench = createWeaponBench(
    element,
    info,
    (weapons) => {
      if (!joined || dead || socket?.readyState !== WebSocket.OPEN) return;
      if (socket.bufferedAmount > 4096 || seq >= 2147483647) {
        socket.close();
        return;
      }
      sendInput();
      socket.send(JSON.stringify({ v: 6, type: 'equip', seq: ++seq, weapons }));
      return seq;
    },
    (module) => {
      if (!joined || dead || socket?.readyState !== WebSocket.OPEN) return;
      if (socket.bufferedAmount > 4096 || seq >= 2147483647) {
        socket.close();
        return;
      }
      sendInput();
      socket.send(JSON.stringify({ v: 6, type: 'buy', seq: ++seq, module }));
      return seq;
    },
    (open) => {
      if (open && sector?.open) sector.setOpen('');
      if (open && skills.open) skills.setOpen(false);
      if (open && friends?.open) friends.setOpen(false);
      info.inert = open || skills.open;
      canvas.inert = open || skills.open || menuOpen || !form.hidden;
      clearInput();
      presentation();
      if (!open && joined) canvas.focus();
    },
  );
  friends = createFriendsMenu(
    element,
    info,
    (name, action) => {
      if (!joined || socket?.readyState !== WebSocket.OPEN) return;
      if (socket.bufferedAmount > 4096 || seq >= 2147483647) {
        socket.close();
        return;
      }
      socket.send(JSON.stringify({ v: 6, type: 'friend', seq: ++seq, name, action }));
      return seq;
    },
    (open) => {
      if (open && bench.open) bench.setOpen(false);
      if (open && sector?.open) sector.setOpen('');
      if (open && skills.open) skills.setOpen(false);
      info.inert = open || bench.open || skills.open;
      canvas.inert = open || bench.open || skills.open || menuOpen || !form.hidden;
      clearInput();
      presentation();
      if (!open && joined) canvas.focus();
    },
  );
  sector = createSectorConsole(
    element,
    info,
    (action, target) => {
      if (!joined || dead || socket?.readyState !== WebSocket.OPEN) return;
      if (socket.bufferedAmount > 4096 || seq >= 2147483647) {
        socket.close();
        return;
      }
      socket.send(JSON.stringify({ v: 6, type: 'sector', seq: ++seq, action, target }));
      return seq;
    },
    (open) => {
      if (open) {
        bench.setOpen(false);
        skills.setOpen(false);
        friends.setOpen(false);
      }
      canvas.inert = open || bench.open || skills.open || friends.open || menuOpen || !form.hidden;
      info.inert = open || bench.open || skills.open || friends.open;
      clearInput();
      presentation();
      if (!open && joined) canvas.focus();
    },
    () => bench.openShop(),
    setWaypoint,
  );
  decorateCommandButtons(info);
  const inputBlocked = () =>
    sector?.open ||
    friends?.open ||
    bench?.open ||
    skills.open ||
    menuOpen ||
    !form.hidden ||
    document.hidden ||
    !document.hasFocus() ||
    editable(document.activeElement) ||
    (document.activeElement instanceof Element &&
      !!document.activeElement.closest('button,a,input'));
  function presentation() {
    host.ArenaPresentation(
      !!sector?.open || menuOpen || skills.open || !!bench?.open || !!friends?.open,
      reducedEffects,
    );
  }
  function localInput() {
    const blocked = !joined || dead || inputBlocked();
    if (blocked) {
      keys.clear();
      mouseFire = false;
    } else {
      if (pointer) trackPointer(pointer);
      aim = host.ArenaAim(u, v);
    }
    const intent = {
      x: blocked ? 0 : held('KeyD', 'ArrowRight') - held('KeyA', 'ArrowLeft'),
      y: blocked ? 0 : held('KeyS', 'ArrowDown') - held('KeyW', 'ArrowUp'),
      aim,
      fire: !blocked && (mouseFire || !!held('Space')),
      menu: !!sector?.open || skills.open || !!bench?.open || !!friends?.open || menuOpen,
      editing: !!sector?.open || skills.open || !!bench?.open || !!friends?.open,
    };
    host.ArenaInput(intent.x, intent.y, intent.aim, intent.menu);
    return intent;
  }
  function sendInput() {
    if (!joined || socket?.readyState !== WebSocket.OPEN) return;
    if (socket.bufferedAmount > 4096 || seq >= 2147483647) {
      socket.close();
      return;
    }
    const intent = localInput();
    socket.send(JSON.stringify({ v: 6, type: 'input', seq: ++seq, ...intent }));
    host.ArenaSentInput(seq);
  }
  // Movement presentation runs at display cadence, independent of 30 Hz transport.
  let inputFrame;
  function refreshLocalInput() {
    localInput();
    inputFrame = requestAnimationFrame(refreshLocalInput);
  }
  inputFrame = requestAnimationFrame(refreshLocalInput);
  function clearInput() {
    keys.clear();
    mouseFire = false;
    localInput();
    sendInput();
  }
  function setMenu(open) {
    if (skills.open) skills.setOpen(false);
    if (bench?.open) bench.setOpen(false);
    if (friends?.open) friends.setOpen(false);
    menuOpen = open && joined;
    menu.hidden = !menuOpen;
    menuButton.setAttribute('aria-expanded', String(menuOpen));
    canvas.inert = menuOpen || !form.hidden;
    link.inert = menuOpen;
    menuButton.inert = menuOpen;
    retry.hidden = !joined || !dead || menuOpen;
    clearInput();
    presentation();
    if (menuOpen) resume.focus();
    else if (joined) canvas.focus();
  }
  function setEffects(value) {
    reducedEffects = value;
    effects.checked = value;
    presentation();
  }
  menuButton.addEventListener('click', () => setMenu(true));
  resume.addEventListener('click', () => setMenu(false));
  effects.addEventListener('change', () => setEffects(effects.checked));
  function resetConnection(message) {
    joined = false;
    radarControls.hidden = true;
    clearInterval(timer);
    clearTimeout(joinTimeout);
    keys.clear();
    mouseFire = false;
    dead = false;
    socket = undefined;
    for (const key of ['player', 'room', 'tick', 'snapshot']) delete info.dataset[key];
    form.hidden = false;
    button.disabled = false;
    button.textContent = 'Join arena';
    retry.hidden = true;
    retry.disabled = false;
    retry.textContent = 'Ship destroyed — respawn';
    menuButton.hidden = true;
    link.hidden = true;
    connection.textContent = 'Disconnected • rejoin available';
    notice.textContent = message;
    document.body.classList.add('arena-fullscreen');
    audio.reset();
    sector.update(null, {}, false);
    friends.update(null, {}, false);
    bench.update(null, false);
    skills.update(null, false);
    setMenu(false);
    name.focus();
  }
  leave.addEventListener('click', () => {
    clearInput();
    const previous = socket;
    socket = undefined;
    previous?.close();
    resetConnection(
      'You left the live room. Your ship may remain briefly. Rejoin starts a fresh run.',
    );
  });
  retry.addEventListener('click', () => {
    if (!joined || menuOpen || socket?.readyState !== WebSocket.OPEN || retry.disabled) return;
    if (socket.bufferedAmount > 4096 || seq >= 2147483647) {
      socket.close();
      return;
    }
    clearInput();
    retry.disabled = true;
    retry.textContent = 'Respawning…';
    socket.send(JSON.stringify({ v: 6, type: 'respawn', seq: ++seq }));
    canvas.focus();
  });
  window.addEventListener('keydown', (e) => {
    if (
      joined &&
      !editable(e.target) &&
      !bench.open &&
      !skills.open &&
      !friends.open &&
      !menuOpen &&
      sector.keydown(e)
    )
      return;
    if (friends.keydown(e)) return;
    if (
      joined &&
      !menuOpen &&
      !bench.open &&
      !skills.open &&
      e.code === 'KeyP' &&
      !editable(e.target)
    ) {
      e.preventDefault();
      if (!e.repeat) friends.setOpen(true);
      return;
    }
    if (bench.keydown(e)) return;
    if (skills.keydown(e)) return;
    if (joined && !dead && !menuOpen && e.code === 'KeyI' && !editable(e.target)) {
      e.preventDefault();
      if (!e.repeat) bench.setOpen(true);
      return;
    }
    if (joined && !dead && !menuOpen && e.code === 'Tab' && !editable(e.target)) {
      e.preventDefault();
      if (!e.repeat) skills.setOpen(true);
      return;
    }
    if (joined && e.code === 'Escape' && !editable(e.target)) {
      e.preventDefault();
      if (!e.repeat) setMenu(!menuOpen);
      return;
    }
    if (
      joined &&
      e.code === 'KeyR' &&
      !editable(e.target) &&
      !e.ctrlKey &&
      !e.metaKey &&
      !e.altKey
    ) {
      e.preventDefault();
      if (!e.repeat) setEffects(!reducedEffects);
      return;
    }
    if (menuOpen && e.key === 'Tab') {
      const controls = [effects, sound, resume, leave, solo];
      if (e.shiftKey && document.activeElement === controls[0]) {
        e.preventDefault();
        solo.focus();
      } else if (!e.shiftKey && document.activeElement === solo) {
        e.preventDefault();
        effects.focus();
      }
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
  window.addEventListener('keyup', (e) => {
    keys.delete(e.code);
    if (supported.has(e.code)) localInput();
  });
  document.addEventListener('focusin', () => {
    if (inputBlocked()) clearInput();
  });
  function trackPointer(e) {
    pointer = { clientX: e.clientX, clientY: e.clientY };
    const r = canvas.getBoundingClientRect();
    if (r.width && r.height) {
      u = Math.max(0, Math.min(1, (e.clientX - r.left) / r.width));
      v = Math.max(0, Math.min(1, (e.clientY - r.top) / r.height));
    }
  }
  // Track the physical cursor over equipment too, so closing it cannot restore an old aim position.
  // Gameplay remains neutral while a menu owns input, including during pointer capture/dragging.
  window.addEventListener('pointermove', (e) => {
    trackPointer(e);
    if (!inputBlocked()) localInput();
  });
  canvas.addEventListener('pointerdown', (e) => {
    if (joined && !menuOpen && e.button === 0) {
      trackPointer(e);
      canvas.focus();
      if (!inputBlocked()) mouseFire = true;
      localInput();
    }
  });
  window.addEventListener('pointerup', (e) => {
    if (e.button === 0) {
      mouseFire = false;
      localInput();
    }
  });
  canvas.addEventListener('pointercancel', clearInput);
  window.addEventListener('blur', clearInput);
  document.addEventListener('visibilitychange', () => {
    if (document.hidden) clearInput();
  });
  window.addEventListener(
    'pagehide',
    () => {
      clearInput();
      cancelAnimationFrame(inputFrame);
      clearInterval(timer);
      clearTimeout(joinTimeout);
      socket?.close();
    },
    { once: true },
  );
  form.addEventListener('submit', (e) => {
    e.preventDefault();
    if (button.disabled) return;
    if (!name.value.trim()) {
      notice.textContent = 'Enter a callsign before joining.';
      name.focus();
      return;
    }
    button.disabled = true;
    button.textContent = 'Connecting…';
    notice.textContent = 'Connecting to the live room…';
    connection.textContent = 'Connecting…';
    const url = new URL('/ws', location.href);
    url.protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
    let current,
      failure = '';
    try {
      current = new WebSocket(url);
      socket = current;
    } catch {
      resetConnection('Could not open a connection. Try joining again.');
      return;
    }
    seq = 0;
    for (const key of ['player', 'room', 'tick', 'snapshot']) delete info.dataset[key];
    joinTimeout = setTimeout(() => {
      if (socket === current && !joined) {
        failure = 'The server did not answer. Check the server and try again.';
        current.close();
      }
    }, 15000);
    current.addEventListener('open', () => {
      if (socket === current)
        current.send(
          JSON.stringify({ v: 6, type: 'join', name: name.value, room: params.get('room') || '' }),
        );
    });
    current.addEventListener('message', (e) => {
      if (socket !== current) return;
      try {
        if (typeof e.data !== 'string' || e.data.length > 524288)
          throw new Error('Invalid server payload');
        const message = JSON.parse(e.data);
        if (message.v !== 6) throw new Error('Protocol mismatch');
        if (message.type === 'welcome') {
          lastCredit = 0;
          lastCreditLife = -1;
          payout.hidden = true;
          if (joined) throw new Error('Duplicate welcome');
          bench.setCatalog(message.catalog, message.unlockLevels);
          skills.setCatalog(message.skills);
          sector.setup(message.sector, message.contracts);
          host.ArenaSector(JSON.stringify(message.sector));
          try {
            localStorage.setItem('neon.lastName', name.value.trim());
          } catch {}
          clearTimeout(joinTimeout);
          host.ArenaWelcome(message.id);
          joined = true;
          radarControls.hidden = false;
          form.hidden = true;
          dead = false;
          const invite = new URL(location.href);
          invite.search = new URLSearchParams({ room: message.room });
          link.href = invite.href;
          link.textContent = `Room ${message.room} • invite link`;
          link.hidden = false;
          connection.textContent = 'Connected • LIVE';
          menuButton.hidden = false;
          info.dataset.player = message.id;
          info.dataset.room = message.room;
          setMenu(false);
          timer = setInterval(sendInput, 1000 / 30);
        } else if (message.type === 'snapshot') {
          if (!joined) throw new Error('Snapshot before welcome');
          expandCombat(message);
          for (const key of ['workshop', 'social', 'skillReceipt', 'creditReward']) {
            if (message[key] == null) message[key] = current.privateState?.[key] ?? null;
          }
          if (message.sector && message.sector.pilot == null)
            message.sector.pilot = current.privateState?.sector?.pilot ?? null;
          current.privateState = message;
          const fullSnapshot = JSON.stringify(message);
          presentSnapshot(host, message);
          const self = message.players.find((p) => p.id === Number(info.dataset.player));
          const nowDead = !!self && self.hull <= 0;
          if (nowDead && !dead) {
            clearInput();
            retry.disabled = false;
            retry.textContent = 'Ship destroyed — respawn';
          }
          dead = nowDead;
          retry.hidden = !dead || menuOpen;
          if (self?.life !== lastCreditLife) {
            lastCredit = 0;
            lastCreditLife = self?.life;
            payout.hidden = true;
          }
          if (message.creditReward?.sequence > lastCredit) {
            const reward = message.creditReward;
            lastCredit = reward.sequence;
            payout.textContent = `${reward.lucky ? 'Lucky! ' : ''}+${reward.amount} credits`;
            payout.hidden = false;
            clearTimeout(payoutTimer);
            payoutTimer = setTimeout(() => (payout.hidden = true), 1600);
          }
          sector.update(self, message, joined);
          friends.update(self, message, joined);
          skills.update(self, joined, message.skillReceipt);
          bench.update(self, joined, message.workshop);
          audio.observe(message, Number(info.dataset.player));
          // Read-only transport diagnostics also used by browser acceptance tests.
          info.dataset.tick = message.tick;
          info.dataset.snapshot = fullSnapshot;
        } else if (message.type === 'error') {
          const reason =
            message.code === 'refresh_required'
              ? 'Game updated. Refresh this page to continue.'
              : message.code === 'name_in_use'
                ? 'That callsign is already connected. Choose another name or close the other session.'
                : `Server refused the request: ${message.code}.`;
          if (!joined) {
            failure = `${reason} Try another room or join again.`;
            notice.textContent = failure;
            current.close();
          } else {
            connection.textContent = reason;
            retry.disabled = false;
            retry.textContent = 'Retry respawn';
          }
        } else throw new Error('Unknown server message');
      } catch (error) {
        failure =
          'The game files or server data are incompatible. Reload this page to get the current build.';
        console.error(error);
        current.close();
      }
    });
    current.addEventListener('close', () => {
      if (socket !== current) return;
      resetConnection(
        failure ||
          'Disconnected. Rejoin starts a fresh run; the server retains your previous ship briefly.',
      );
    });
    current.addEventListener('error', () => {
      if (socket !== current) return;
      failure ||=
        'Connection failed. A multiplayer server must be running on this origin. Try again when it is available.';
      notice.textContent = failure;
      connection.textContent = 'Connection failed';
    });
  });
  // main.js retains its runtime error overlay; connection messages live in this shell.
  presentation();
  canvas.inert = true;
  name.focus();
}
