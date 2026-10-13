import { names, colors, paths } from './sector-chart-symbols.js';
import { chartStyles } from './sector-chart.styles.js';
import { territoryCells } from './territory.js';
// Desktop tactical chart. Marker coordinates stay fixed in world space; labels never move them.
export function createSectorChart(el, root, actions) {
  const css = el('style');
  css.textContent = chartStyles;
  document.head.append(css);
  root.replaceChildren();
  const view = el('div', '', 'chart-view'),
    canvas = el('canvas'),
    detail = el('aside', '', 'chart-detail');
  view.tabIndex = 0;
  view.setAttribute('aria-label', 'Pan sector map');
  canvas.setAttribute('aria-label', 'Tactical sector map');
  view.append(canvas);
  root.append(view, detail);
  const tools = el('div', '', 'chart-tools'),
    minus = el('button', '−'),
    plus = el('button', '+'),
    center = el('button', 'Center on player'),
    fit = el('button', 'Fit sector'),
    zoomLabel = el('button', '100%');
  minus.setAttribute('aria-label', 'Zoom out');
  plus.setAttribute('aria-label', 'Zoom in');
  zoomLabel.disabled = true;
  tools.append(minus, zoomLabel, plus, center, fit);
  const player = el('div', '', 'chart-player');
  player.title = 'Your ship';
  player.setAttribute('aria-label', 'Your ship');
  view.append(player);
  view.append(
    tools,
    el('div', 'DRAG / WASD: PAN   ·   WHEEL: ZOOM   ·   ENTER: WAYPOINT', 'chart-help'),
  );
  const category = el('div', 'NAVIGATION', 'chart-category'),
    title = el('h3', 'Select a location'),
    metadata = el('p'),
    description = el('p', 'Inspect a marker to see services, regional threats, and distance.'),
    navigate = el('button', 'Set waypoint', 'chart-primary'),
    ping = el('button', 'Ping friends'),
    broadcast = el('button', 'Broadcast to room'),
    defend = el('button', 'Defend ally'),
    waypointText = el('p'),
    clear = el('button', 'Clear waypoint'),
    filters = el('div', '', 'chart-filters');
  detail.append(
    category,
    title,
    metadata,
    description,
    navigate,
    ping,
    broadcast,
    defend,
    waypointText,
    clear,
    filters,
  );
  const context = el('div', '', 'chart-context');
  context.hidden = true;
  view.append(context);
  let cells = [],
    sector,
    frame,
    self,
    state,
    selected = null,
    zoom = 1,
    cx = 6000,
    cy = 4000,
    drag = null,
    pending = false,
    suppressClick = false;
  const markers = new Map(),
    enabled = new Set([
      'planet',
      'station',
      'base',
      'wreck',
      'beacon',
      'corridor',
      'pilot',
      'danger',
      'rescue',
      'outpost',
      'contact',
      'cache',
    ]);
  for (const [label, kinds] of [
    ['Planets', ['planet']],
    ['Stations', ['station']],
    ['Hostiles', ['base', 'corridor']],
    ['Salvage', ['wreck', 'cache']],
    ['Beacons', ['beacon']],
    ['Danger', ['danger']],
  ]) {
    const b = el('button', label);
    b.setAttribute('aria-pressed', 'true');
    b.onclick = () => {
      const on = !enabled.has(kinds[0]);
      for (const k of kinds) on ? enabled.add(k) : enabled.delete(k);
      b.setAttribute('aria-pressed', String(on));
      draw();
    };
    filters.append(b);
  }
  function transform() {
    const w = view.clientWidth,
      h = view.clientHeight;
    const scale = Math.min((w - 64) / sector.width, (h - 80) / sector.height) * zoom;
    return { w, h, scale, x: w / 2 - cx * scale, y: h / 2 - cy * scale };
  }
  function select(l) {
    selected = l;
    context.hidden = true;
    refresh();
    draw();
  }
  function setWaypoint() {
    if (selected) actions.navigate(selected);
    context.hidden = true;
    refresh();
    draw();
  }
  navigate.onclick = setWaypoint;
  ping.onclick = () => selected && actions.ping('ping', selected);
  broadcast.onclick = () => selected && actions.ping('broadcast', selected);
  defend.onclick = () => selected?.pilot && actions.defend(selected.pilot);
  clear.onclick = () => {
    actions.navigate(null);
    refresh();
    draw();
  };
  function refresh() {
    const l = selected,
      locationState = frame?.sector?.locations?.find((s) => s.id === l?.id),
      destroyed = locationState?.state === 'destroyed';
    const region = l?.biome || sector?.locations.find((p) => p.id === l?.parent)?.biome || '';
    const level = { inhabited: 1, volcanic: 2, gas: 3, shattered: 4 }[region];
    category.textContent = l ? names[l.kind] || l.kind : 'NAVIGATION';
    title.textContent = l?.name || 'Select a location';
    metadata.textContent =
      l && self
        ? `${Math.round(Math.hypot(l.x - self.x, l.y - self.y)).toLocaleString()} units away${level ? ' · Level ' + level + ' region' : ''}${region ? ' · ' + region : ''}${l.kind === 'base' ? ' · ' + (destroyed ? 'Destroyed' : 'Active') : ''}`
        : '';
    description.textContent = !l
      ? 'Select an icon to inspect it. Set a waypoint when you are ready to travel.'
      : l.kind === 'rescue'
        ? `Save the relief ship: ${l.hull}/${l.maxHull} hull · ${l.shield}/${l.maxShield} shields · ${l.remaining} attackers. Open cooperative mission.`
        : l.kind === 'contact'
          ? 'Roaming friendly contact. Approach within 180 units and hail with E for trade or assignments.'
          : l.kind === 'cache'
            ? 'Hold still and search for three seconds. Personal credits and XP; each pilot can claim once per sector.'
            : l.kind === 'station'
              ? 'Safe docking, module shop, hull repairs, and local contracts. Sanctuary extends 300 units.'
              : l.kind === 'base'
                ? destroyed
                  ? 'This installation is permanently destroyed. Its wreck remains cover.'
                  : `Destroying this installation stops its launches. Eligible contributors receive ${40 * (level || 1)} credits and ${30 * (level || 1)} XP in pickups, plus a chance of a module.`
                : l.kind === 'corridor'
                  ? 'Patrols arrive here from outside the sector. This navigation corridor cannot be destroyed.'
                  : l.kind === 'wreck'
                    ? 'Recovery-contract wreck and shared salvage site. Approach to fund a core, two turrets, and a repair drone bay.'
                    : l.kind === 'beacon'
                      ? 'Survey contract location. Hold position while interacting to scan.'
                      : l.kind === 'pilot'
                        ? 'Friendly pilot. A defense ping asks allies to assist against a threatening patrol.'
                        : l.solid
                          ? 'Solid planet. Navigate around its rim; surface outposts and patrols guard its exterior.'
                          : 'Gas giant. Orbital hazards and installations occupy flight space.';
    navigate.disabled = !l;
    ping.hidden = broadcast.hidden = !l || l.kind === 'pilot';
    defend.hidden = l?.kind !== 'pilot';
    ping.disabled = broadcast.disabled = !!pending || !state?.discovered?.includes(l?.id);
    ping.title = broadcast.title = ping.disabled ? 'Visit this location before sharing it.' : '';
    defend.disabled = !!pending;
    const wp = actions.waypoint();
    waypointText.textContent = wp
      ? `WAYPOINT · ${wp.name} · ${Math.round(Math.hypot(wp.x - self.x, wp.y - self.y))} u`
      : 'WAYPOINT · None';
    clear.hidden = !wp;
  }
  function addMarker(l) {
    const b = el('button', '', 'map-location');
    b.type = 'button';
    b.dataset.kind = l.kind;
    b.style.setProperty('--marker', colors[l.kind]);
    b.setAttribute('aria-label', l.name);
    b.title = `${l.name} · ${names[l.kind]}`;
    b.innerHTML = `<svg viewBox="0 0 24 24" aria-hidden="true">${paths[l.kind]}</svg>`;
    if (l.kind === 'planet') b.append(el('span', l.name, 'planet-name'));
    b.onclick = () => {
      if (!suppressClick) select(l);
    };
    b.ondblclick = () => {
      select(l);
      setWaypoint();
    };
    b.onkeydown = (e) => {
      if (e.key === 'Enter') {
        e.preventDefault();
        e.stopPropagation();
        select(l);
        setWaypoint();
      }
    };
    b.oncontextmenu = (e) => {
      e.preventDefault();
      select(l);
      context.replaceChildren();
      for (const [label, fn, disabled] of [
        ['Set waypoint', setWaypoint, false],
        ...(l.kind === 'pilot'
          ? [['Defend ally', () => actions.defend(l.pilot), pending]]
          : [
              ['Ping friends', () => actions.ping('ping', l), ping.disabled],
              ['Broadcast to room', () => actions.ping('broadcast', l), broadcast.disabled],
            ]),
      ]) {
        const item = el('button', label);
        item.disabled = disabled;
        item.onclick = () => {
          fn();
          context.hidden = true;
        };
        context.append(item);
      }
      const rect = view.getBoundingClientRect();
      context.style.left =
        Math.max(0, Math.min(view.clientWidth - 180, e.clientX - rect.left)) + 'px';
      context.style.top =
        Math.max(0, Math.min(view.clientHeight - 115, e.clientY - rect.top)) + 'px';
      context.hidden = false;
      context.querySelector('button')?.focus();
    };
    view.append(b);
    markers.set(['pilot', 'rescue', 'contact'].includes(l.kind) ? l.kind + ':' + l.id : l.id, {
      b,
      l,
    });
  }
  function draw() {
    if (!sector || root.hidden || !view.clientWidth) return;
    const t = transform(),
      dpr = Math.min(devicePixelRatio || 1, 2);
    canvas.width = Math.round(t.w * dpr);
    canvas.height = Math.round(t.h * dpr);
    const c = canvas.getContext('2d');
    c.scale(dpr, dpr);
    c.fillStyle = '#07111e';
    c.fillRect(0, 0, t.w, t.h);
    c.save();
    c.translate(t.x, t.y);
    c.scale(t.scale, t.scale);
    const line = 1 / t.scale;
    c.lineWidth = line;
    c.strokeStyle = '#263b504d';
    for (let x = 0; x <= sector.width; x += 1000) {
      c.beginPath();
      c.moveTo(x, 0);
      c.lineTo(x, sector.height);
      c.stroke();
    }
    for (let y = 0; y <= sector.height; y += 1000) {
      c.beginPath();
      c.moveTo(0, y);
      c.lineTo(sector.width, y);
      c.stroke();
    }
    c.strokeStyle = '#4c7184';
    c.strokeRect(0, 0, sector.width, sector.height);
    for (const cell of cells) {
      const ownership = frame?.sector?.world?.territory?.find((t) => t.id === cell.id);
      if (!ownership || cell.points.length < 3) continue;
      c.beginPath();
      cell.points.forEach(([x, y], i) => (i ? c.lineTo(x, y) : c.moveTo(x, y)));
      c.closePath();
      const tint = ownership.owner === 'friendly' ? '#4ade80' : '#fb7185';
      c.fillStyle = tint + '0b';
      c.strokeStyle = tint + '55';
      c.fill();
      c.stroke();
    }
    c.fillStyle = '#446176';
    for (let i = 0; i < 180; i++) {
      const x = (i * 7919 + sector.seed) % sector.width,
        y = (i * 3571 + sector.seed) % sector.height;
      c.fillRect(x, y, line, line);
    }
    const circle = (x, y, r, fill, stroke) => {
      c.beginPath();
      c.arc(x, y, r, 0, Math.PI * 2);
      if (fill) {
        c.fillStyle = fill;
        c.fill();
      }
      if (stroke) {
        c.strokeStyle = stroke;
        c.stroke();
      }
    };
    for (const l of sector.locations) {
      if (l.kind === 'planet' && enabled.has('planet')) {
        const color = {
          inhabited: '#36778a',
          volcanic: '#a65b37',
          gas: '#71638e',
          shattered: '#66798a',
        }[l.biome];
        circle(l.x, l.y, l.radius * 1.2, null, color + '35');
        circle(l.x, l.y, l.radius * 1.5, null, '#4d657b40');
        const g = c.createRadialGradient(
          l.x - l.radius * 0.3,
          l.y - l.radius * 0.3,
          0,
          l.x,
          l.y,
          l.radius,
        );
        g.addColorStop(0, color);
        g.addColorStop(1, '#112133');
        circle(l.x, l.y, l.radius, g, color + '90');
        c.save();
        c.beginPath();
        c.arc(l.x, l.y, l.radius, 0, Math.PI * 2);
        c.clip();
        for (let i = -3; i < 4; i++) {
          c.strokeStyle = color + '65';
          c.beginPath();
          c.ellipse(
            l.x,
            l.y + i * l.radius * 0.27,
            l.radius,
            l.radius * 0.16,
            0.15,
            0,
            Math.PI * 2,
          );
          c.stroke();
        }
        c.restore();
      }
      if (l.kind === 'station' && enabled.has('station'))
        circle(
          l.x,
          l.y,
          300,
          '#5bdca510',
          frame?.sector?.world?.territory?.some(
            (t) => t.id === l.id && (t.owner === 'hostile' || t.contested),
          )
            ? '#ff796c80'
            : '#5bdca560',
        );
      if (
        l.kind === 'base' &&
        enabled.has('danger') &&
        frame?.sector?.locations?.find((s) => s.id === l.id)?.state !== 'destroyed'
      )
        circle(l.x, l.y, 700, '#ee645414', '#ee645435');
      if (l.kind === 'cluster')
        for (let i = 0; i < 7; i++)
          circle(
            l.x + Math.cos(i * 2.4) * i * 25,
            l.y + Math.sin(i * 2.4) * i * 25,
            18 + i * 3,
            '#61718580',
          );
    }
    if (self) {
      const event = frame?.sector?.world?.eventObjective;
      if (event) circle(event.x, event.y, Math.max(event.uncertainty, 12 * line), null, '#f2bf65');
      circle(self.x, self.y, self.surveyRadius || 1200, '#5adce008', '#5adce040');
      for (const e of frame?.enemies || [])
        if (e.hull > 0 && Math.hypot(e.x - self.x, e.y - self.y) <= (self.detectionRadius || 1200))
          circle(
            e.x,
            e.y,
            2.5 * line,
            e.faction === 'friendly' ? '#65e6be' : e.faction === 'neutral' ? '#e8ca82' : '#ff796c',
          );
      const wp = actions.waypoint();
      if (wp) {
        c.strokeStyle = '#edcc7a';
        c.setLineDash([5 * line, 5 * line]);
        c.beginPath();
        c.moveTo(self.x, self.y);
        for (const point of actions.route?.(self, wp) || [wp]) c.lineTo(point.x, point.y);
        c.stroke();
        c.setLineDash([]);
        circle(wp.x, wp.y, 10 * line, null, '#edcc7a');
      }
      circle(self.x, self.y, 8 * line, null, '#6ef4e1');
      circle(self.x, self.y, 4 * line, '#ffffff');
    }
    c.restore();
    c.fillStyle = '#7596ac';
    c.font = '10px monospace';
    for (let x = 0; x <= sector.width; x += 2000) {
      const px = t.x + x * t.scale;
      if (px > 8 && px < t.w - 45) c.fillText(String(x), px, t.h - 27);
    }
    zoomLabel.textContent = Math.round(zoom * 100) + '%';
    player.hidden = !self;
    if (self) {
      const x = t.x + self.x * t.scale,
        y = t.y + self.y * t.scale;
      player.hidden = x < 10 || x > t.w - 10 || y < 10 || y > t.h - 10;
      player.style.left = x + 'px';
      player.style.top = y + 'px';
    }
    const contacts = [...markers.values()]
      .filter((m) => m.l.kind === 'contact')
      .sort((a, b) => a.l.id - b.l.id);
    for (const { b, l } of markers.values()) {
      let x = t.x + l.x * t.scale,
        y = t.y + l.y * t.scale;
      // Contacts gather near stations. Fan their icons out without moving navigation targets.
      if (l.kind === 'contact') {
        x += (contacts.findIndex((m) => m.l === l) - 1) * 26;
        y += 30;
      }
      b.hidden =
        (l.kind === 'cache' &&
          !state?.discovered?.includes(l.id) &&
          !frame.sector?.services?.assignment?.targets.includes(l.id)) ||
        !enabled.has(l.kind) ||
        x < 14 ||
        x > t.w - 14 ||
        y < 14 ||
        y > t.h - 14;
      b.style.left = x + 'px';
      b.style.top = y + 'px';
      b.setAttribute('aria-pressed', String(selected === l));
    }
  }
  function zoomTo(value, x = view.clientWidth / 2, y = view.clientHeight / 2) {
    const t = transform(),
      wx = (x - t.x) / t.scale,
      wy = (y - t.y) / t.scale;
    zoom = Math.max(1, Math.min(5, value));
    const next = transform();
    cx = wx - (x - next.w / 2) / next.scale;
    cy = wy - (y - next.h / 2) / next.scale;
    draw();
  }
  minus.onclick = () => zoomTo(zoom / 1.25);
  plus.onclick = () => zoomTo(zoom * 1.25);
  fit.onclick = () => {
    zoom = 1;
    cx = sector.width / 2;
    cy = sector.height / 2;
    draw();
  };
  center.onclick = () => {
    if (self) {
      cx = self.x;
      cy = self.y;
      draw();
    }
  };
  view.addEventListener(
    'wheel',
    (e) => {
      e.preventDefault();
      const r = view.getBoundingClientRect();
      zoomTo(zoom * Math.exp(-e.deltaY * 0.001), e.clientX - r.left, e.clientY - r.top);
    },
    { passive: false },
  );
  view.addEventListener('pointerdown', (e) => {
    if (e.button !== 0 || e.target.closest('button')) return;
    context.hidden = true;
    suppressClick = false;
    drag = { id: e.pointerId, x: e.clientX, y: e.clientY, cx, cy };
    view.setPointerCapture(e.pointerId);
  });
  view.addEventListener('pointermove', (e) => {
    if (!drag) return;
    const t = transform();
    cx = drag.cx - (e.clientX - drag.x) / t.scale;
    cy = drag.cy - (e.clientY - drag.y) / t.scale;
    suppressClick = true;
    draw();
  });
  view.addEventListener('pointerup', () => {
    drag = null;
    suppressClick = false;
  });
  view.addEventListener('pointercancel', () => (drag = null));
  new ResizeObserver(draw).observe(view);
  return {
    setup(s) {
      sector = s;
      cells = territoryCells(s);
      selected = null;
      zoom = 1;
      cx = sector.width / 2;
      cy = sector.height / 2;
      context.hidden = true;
      for (const { b } of markers.values()) b.remove();
      markers.clear();
      for (const l of s?.locations || []) if (paths[l.kind]) addMarker(l);
    },
    update(s, f, p, st, waiting) {
      sector = s;
      frame = f;
      self = p;
      state = st;
      pending = waiting;
      const allies = f.players.filter(
        (a) =>
          a.id !== p.id &&
          a.hull > 0 &&
          a.connected &&
          f.social?.friends.some((n) => n.toLowerCase() === a.name.toLowerCase()),
      );
      for (const [key, m] of markers)
        if (m.l.kind === 'pilot' && !allies.some((a) => a.id === m.l.id)) {
          if (selected === m.l) selected = null;
          m.b.remove();
          markers.delete(key);
        }
      for (const a of allies) {
        const old = markers.get('pilot:' + a.id);
        if (old) {
          old.l.x = a.x;
          old.l.y = a.y;
        } else addMarker({ ...a, kind: 'pilot', pilot: a.id });
      }
      const contacts = [
        ...(f.sector?.services?.contacts || []),
        ...(f.enemies || [])
          .filter(
            (e) =>
              e.faction !== 'hostile' &&
              e.surfacePlanet === 0 &&
              !e.kind.startsWith('salvage-') &&
              e.kind !== 'installation',
          )
          .filter((e) => !(f.sector?.services?.contacts || []).some((c) => c.id === e.id))
          .map((e) => ({ ...e, name: e.role + ' · ' + e.state })),
      ];
      for (const [key, marker] of markers)
        if (marker.l.kind === 'contact' && !contacts.some((c) => c.id === marker.l.id)) {
          marker.b.remove();
          markers.delete(key);
          if (selected === marker.l) selected = null;
        }
      for (const c of contacts) {
        const old = markers.get('contact:' + c.id);
        if (old) Object.assign(old.l, c);
        else addMarker({ ...c, kind: 'contact' });
      }
      for (const site of f.sector?.services?.outposts || []) {
        const marker = markers.get(site.id);
        if (marker) {
          marker.l.name = site.active ? site.name + ' · Active outpost' : site.name;
          marker.b.setAttribute('aria-label', marker.l.name);
        }
      }
      const distress = f.sector?.rescue;
      for (const [key, marker] of markers)
        if (
          marker.l.kind === 'rescue' &&
          (distress?.state !== 'active' || marker.l.id !== distress.id)
        ) {
          if (selected === marker.l) selected = null;
          marker.b.remove();
          markers.delete(key);
        }
      if (distress?.state === 'active') {
        const previous = markers.get('rescue:' + distress.id);
        if (previous) Object.assign(previous.l, distress);
        else addMarker({ ...distress, kind: 'rescue', name: 'Emergency: save the relief ship' });
      }
      refresh();
      draw();
    },
    keydown(e) {
      if (e.target.closest('input,select,textarea')) return false;
      if (e.code === 'Escape' && !context.hidden) {
        context.hidden = true;
        return true;
      }
      if (e.code === 'Enter' && e.target === view) {
        setWaypoint();
        return true;
      }
      const direction = { KeyW: [0, -1], KeyS: [0, 1], KeyA: [-1, 0], KeyD: [1, 0] }[e.code];
      if (direction) {
        e.preventDefault();
        const t = transform();
        cx += (direction[0] * 45) / t.scale;
        cy += (direction[1] * 45) / t.scale;
        draw();
        return true;
      }
      return false;
    },
  };
}
