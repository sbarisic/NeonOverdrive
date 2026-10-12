import { control } from './interface.js';

// DOM construction and layout. Connection and input state remain in network.js.
export function createJoinForm(element, params) {
  const form = element('form', '', 'arena-shell arena-panel');
  form.id = 'join-form';
  form.setAttribute('aria-labelledby', 'arena-join-title');
  const heading = element('h1', 'NEON OVERDRIVE');
  heading.id = 'arena-join-title';
  const label = element('label', 'Pilot callsign');
  label.htmlFor = 'nickname';
  const name = element('input');
  name.id = 'nickname';
  name.maxLength = 16;
  name.pattern = '[A-Za-z0-9 _\\-]*';
  name.value = 'Pilot';
  name.required = true;
  try {
    const saved = localStorage.getItem('neon.lastName');
    if (saved && /^[A-Za-z0-9 _-]{1,16}$/.test(saved)) name.value = saved;
  } catch {}
  name.autocomplete = 'nickname';
  name.setAttribute('aria-describedby', 'arena-name-help');
  const help = element('p', '1–16 letters, numbers, spaces, hyphens or underscores.', 'muted');
  help.id = 'arena-name-help';
  const notice = element('p', 'Choose a callsign to launch.', 'arena-notice');
  notice.setAttribute('role', 'status');
  const button = element('button', 'Join arena');
  button.type = 'submit';
  const actions = element('div', '', 'actions');
  actions.append(button);
  form.append(
    element('p', 'LIVE NETWORK / PVPVE', 'eyebrow'),
    heading,
    element(
      'p',
      'Drop into a live PvPvE arena. Hunt aliens, collect upgrades, and watch for rival pilots.',
    ),
    element(
      'p',
      params.get('room')
        ? `Invitation to room ${params.get('room')}. Join if a slot is available.`
        : 'Anonymous drop-in • no account needed',
      'muted',
    ),
    label,
    name,
    help,
    notice,
    actions,
  );
  document.body.append(form);
  return { form, name, notice, button };
}

export function createHudControls(element, canvas, host) {
  const info = element('div', '', 'arena-shell');
  info.id = 'network-info';
  const connection = element('span', 'Offline • ready to join');
  connection.setAttribute('role', 'status');
  const link = element('a');
  link.hidden = true;
  link.title = 'Open or copy this link to invite another pilot';
  const menuButton = element('button', 'Menu [Esc]');
  menuButton.type = 'button';
  menuButton.hidden = true;
  menuButton.setAttribute('aria-controls', 'arena-menu');
  menuButton.setAttribute('aria-expanded', 'false');
  info.append(connection, link, menuButton);
  document.body.append(info);
  // Keep transport controls above the uniformly scaled, bottom-anchored HUD.
  let radarZoom = 1,
    radarWaypoint = null;
  const radarControls = element('div', '', 'arena-shell');
  radarControls.id = 'radar-controls';
  radarControls.hidden = true;
  Object.assign(radarControls.style, {
    position: 'fixed',
    display: 'grid',
    gap: '4px',
    zIndex: '4',
  });
  const radarIn = element('button', '+'),
    radarOut = element('button', '−');
  radarIn.setAttribute('aria-label', 'Minimap zoom in');
  radarOut.setAttribute('aria-label', 'Minimap zoom out');
  for (const b of [radarIn, radarOut]) {
    b.type = 'button';
    Object.assign(b.style, {
      width: '38px',
      height: '38px',
      minHeight: '0',
      padding: '0',
      fontSize: '24px',
    });
    radarControls.append(b);
  }
  document.body.append(radarControls);
  function updateRadar() {
    host.ArenaRadar(radarWaypoint?.x || 0, radarWaypoint?.y || 0, !!radarWaypoint, radarZoom);
    radarControls.dataset.zoom = radarZoom;
    radarControls.dataset.waypoint = radarWaypoint?.name || '';
    radarIn.disabled = radarZoom >= 4;
    radarOut.disabled = radarZoom <= 0.5;
  }
  radarIn.onclick = () => {
    radarZoom = Math.min(4, radarZoom * 2);
    updateRadar();
  };
  radarOut.onclick = () => {
    radarZoom = Math.max(0.5, radarZoom / 2);
    updateRadar();
  };
  function positionInfo() {
    const bounds = canvas.getBoundingClientRect();
    const layout = host.ArenaHudLayout(bounds.width, bounds.height).split(',').map(Number);
    info.style.left = `${bounds.left + layout[0]}px`;
    info.style.top = `${bounds.top + layout[1]}px`;
    info.style.width = `${layout[2]}px`;
    info.style.height = `${layout[3]}px`;
    info.style.right = 'auto';
    info.style.bottom = 'auto';
    info.style.setProperty('--command-size', `${layout[4]}px`);
    document.documentElement.style.setProperty('--sector-guide-top', `${bounds.top + layout[5]}px`);
    info.dataset.layout = JSON.stringify(layout);
    radarControls.style.left = `${bounds.left + layout[10] + layout[12] + 10}px`;
    radarControls.style.top = `${bounds.top + layout[11] + (layout[13] - 80) / 2}px`;
  }
  new ResizeObserver(positionInfo).observe(canvas);
  window.addEventListener('resize', positionInfo);
  positionInfo();
  function setWaypoint(waypoint) {
    radarWaypoint = waypoint;
    updateRadar();
  }
  return { info, connection, link, menuButton, radarControls, setWaypoint };
}

export function createPauseMenu(element, audio, connection, link) {
  const menu = element('section', '', 'arena-shell arena-panel');
  menu.id = 'arena-menu';
  menu.hidden = true;
  menu.setAttribute('role', 'dialog');
  menu.setAttribute('aria-modal', 'true');
  menu.setAttribute('aria-labelledby', 'arena-menu-title');
  const menuHeading = element('h2', 'LIVE ROOM MENU');
  menuHeading.id = 'arena-menu-title';
  const resume = element('button', 'Resume');
  resume.type = 'button';
  const leave = element('button', 'Leave');
  leave.type = 'button';
  leave.title = 'Leave this live room and return to the join form';
  const soundLabel = element('label');
  const sound = element('input');
  sound.type = 'checkbox';
  sound.checked = true;
  sound.id = 'arena-sound';
  soundLabel.append(sound, document.createTextNode(' Sound effects'));
  sound.addEventListener('change', () => audio.setMuted(!sound.checked));
  const effectsLabel = element('label');
  const effects = element('input');
  effects.type = 'checkbox';
  effects.id = 'arena-reduced-effects';
  effectsLabel.append(effects, document.createTextNode(' Reduced effects [R]'));
  control(resume, 'apply', 'primary');
  control(leave, 'exit');
  const menuActions = element('div', '', 'actions');
  menuActions.append(resume, leave);
  menu.append(
    element('p', 'PILOT CONSOLE', 'eyebrow'),
    menuHeading,
    element(
      'p',
      'The room stays live. Your ship can still be hit while this menu is open.',
      'arena-notice',
    ),
    element('p', 'WASD / arrows: move • Mouse / Space: fire • Coast to stop', 'muted'),
    effectsLabel,
    soundLabel,
    menuActions,
  );
  const sessionDetails = element('div', '', 'muted');
  sessionDetails.append(connection, document.createTextNode(' · '), link);
  menu.append(sessionDetails);
  document.body.append(menu);
  return { menu, resume, leave, effects, sound };
}

export function decorateCommandButtons(info) {
  // Keep original button text for accessible names; snapshots may update skill counts.
  const commandArt = [
    '<path d="M14 18H50M14 32H50M14 46H50"/>',
    '<path d="m32 8 6 16 18 2-14 11 4 18-14-9-14 9 4-18L8 26l18-2Z"/>',
    '<path d="M12 48 39 12 51 22 24 55ZM24 30 38 40M38 10 42 4M51 30H59"/>',
    '<circle cx="23" cy="22" r="9"/><circle cx="44" cy="25" r="7"/><path d="M7 53V45Q23 28 38 45V53ZM39 38Q55 37 57 51"/>',
    '<path d="m8 16 16-6 16 7 16-6v39l-16 6-16-7-16 6ZM24 10V49M40 17V56"/>',
    '<path d="M12 10H52V54H12ZM12 20H52M22 40H45M36 31 45 40 36 49"/>',
  ];
  [...info.querySelectorAll('button')].forEach((button, i) => {
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64"><g fill="#9ac4bc22" stroke="${['#b9c9d2', '#edce82', '#edab75', '#94d6ae', '#93c7ed', '#bca0e8'][i]}" stroke-width="3" stroke-linejoin="round" stroke-linecap="round">${commandArt[i]}</g></svg>`;
    button.style.setProperty(
      '--command-icon',
      `url("data:image/svg+xml,${encodeURIComponent(svg)}")`,
    );
    const update = () => {
      const label = button.textContent;
      button.title = label;
      button.setAttribute('aria-label', label);
      button.dataset.hotkey =
        (label.match(/\[(.*?)\]/)?.[1] || '') + (label.match(/\+\d+/)?.[0] || '');
    };
    new MutationObserver(update).observe(button, {
      childList: true,
      characterData: true,
      subtree: true,
    });
    update();
  });
}
