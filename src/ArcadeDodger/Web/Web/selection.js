import { contactNumbers as sharedNumbers, resetContacts } from './contact-state.js';
import { control } from './interface.js';
import { reconcile } from './stable-dom.js';
export function createSelection(el, canvas, host, radio, navigate) {
  const style = el('style');
  style.textContent = `#ship-contacts{position:fixed;overflow:auto;z-index:5;background:#06111be8;width:190px;height:112px;font:12px monospace}
#ship-contacts button{display:block;width:100%;text-align:left;min-height:28px;padding:3px 6px;background:transparent;border:0;color:var(--faction);cursor:pointer}
#ship-contacts button[aria-pressed=true]{background:#263646;outline:1px solid #ffd569;outline-offset:-1px}
#target-details,#target-chooser{position:fixed;left:20px;top:150px;width:270px;background:#091725ed;border:1px solid #627e8c;padding:12px;z-index:6;color:#daeaf1;font:12px monospace}
#target-details button,#target-chooser button{margin:4px;min-height:28px;padding:4px 8px}
#contact-markers{position:fixed;pointer-events:none;z-index:5;overflow:hidden}
#contact-markers span{position:absolute;font:bold 10px monospace;color:var(--faction);background:#06111bdb;border-radius:2px;padding:0 2px;line-height:12px}
#contact-markers,#ship-contacts,#target-details,#target-chooser{box-sizing:border-box}
#target-details[hidden],#ship-contacts[hidden],#contact-markers[hidden],#target-chooser[hidden]{display:none}`;
  document.head.append(style);
  const list = el('div'),
    details = el('section'),
    markers = el('div'),
    chooser = el('div');
  list.id = 'ship-contacts';
  details.id = 'target-details';
  markers.id = 'contact-markers';
  chooser.id = 'target-chooser';
  list.hidden = details.hidden = markers.hidden = chooser.hidden = true;
  list.setAttribute('aria-label', 'Detected ships');
  details.setAttribute('aria-label', 'Selected ship');
  document.body.append(list, details, markers, chooser);
  let contacts = [],
    selected = '',
    hovered = '',
    hidden = true,
    enabled = false,
    last = 0,
    pointer = null,
    numbers = sharedNumbers,
    next = 1,
    mapMarks = [],
    sectorSeed;
  const color = (c) =>
    ({ friendly: '#41f5eb', neutral: '#ffd569', hostile: '#ff645f', pilot: '#ff8bb5' })[
      c.faction
    ] || '#ccc';
  window.addEventListener('neon-select-contact', (e) => select(e.detail));
  function select(key) {
    selected = key;
    details.dataset.targetKey = key;
    canvas.focus();
    chooser.hidden = true;
    host.ArenaSelection?.(selected, hovered);
    render();
  }
  function render() {
    list.hidden = markers.hidden = hidden || !enabled;
    if (list.hidden) {
      details.hidden = chooser.hidden = true;
      return;
    }
    const bounds = canvas.getBoundingClientRect(),
      l = host.ArenaHudLayout(bounds.width, bounds.height).split(',').map(Number);
    Object.assign(list.style, {
      left: `${bounds.left + l[10] - 200}px`,
      top: `${bounds.top + l[11]}px`,
      height: `${Math.min(112, l[13])}px`,
    });
    Object.assign(markers.style, {
      left: `${bounds.left + l[10]}px`,
      top: `${bounds.top + l[11]}px`,
      width: `${l[12]}px`,
      height: `${l[13] - 16}px`,
    });
    details.style.top = 'auto';
    details.style.bottom = `${window.innerHeight - bounds.top - l[11] + 12}px`;
    const content = el('div'),
      dots = el('div');
    mapMarks = [];
    for (const c of contacts) {
      const marker =
        c.role === 'trader'
          ? '$'
          : { friendly: '◆', neutral: '○', hostile: '▲', pilot: '□' }[c.faction] || '·';
      const row = el('button', `${numbers.get(c.key)} ${marker} ${c.name}`);
      row.dataset.key = c.key;
      row.style.setProperty('--faction', color(c));
      row.setAttribute('aria-pressed', String(c.key === selected));
      row.onclick = () => select(c.key);
      content.append(row);
      if (c.radarU >= 0 && c.radarU <= 1 && c.radarV >= 0 && c.radarV * l[13] < l[13] - 16) {
        const label = `${c.role === 'trader' ? '$' : ''}${numbers.get(c.key)}`;
        const mark = el('span', label);
        mark.dataset.key = c.key;
        const width = label.length * 6 + 4;
        let x = c.radarU * l[12] + 3,
          y = c.radarV * l[13] - 10;
        for (let attempt = 0; attempt < 40; attempt++) {
          x = Math.max(
            0,
            Math.min(l[12] - width, c.radarU * l[12] + 3 + Math.floor(attempt / 8) * 18),
          );
          y = Math.max(0, Math.min(l[13] - 28, c.radarV * l[13] - 10 + ((attempt % 8) - 3) * 12));
          if (
            !mapMarks.some(
              (m) => x < m.x + m.width + 2 && x + width + 2 > m.x && Math.abs(y - m.y) < 12,
            )
          )
            break;
        }
        mapMarks.push({ key: c.key, x, y, width });
        mark.style.cssText = `left:${x}px;top:${y}px;--faction:${color(c)}`;
        dots.append(mark);
      }
    }
    reconcile(list, content);
    reconcile(markers, dots);
    const c = contacts.find((c) => c.key === selected);
    details.hidden = !c;
    if (!c) return;
    const detail = el('div');
    detail.append(
      el('strong', `${numbers.get(c.key)} · ${c.name}`),
      el('p', `${c.faction} · ${c.role} · Level ${c.level}`),
      el('p', `Hull ${c.hull}/${c.maxHull} · Shields ${c.shield}/${c.maxShield}`),
      el('p', `${Math.round(c.distance)} units · ${c.state}`),
    );
    if (c.radio) {
      const call = el('button', 'Radio / Hail / Trade');
      control(call, 'friends');
      call.onclick = () => radio.openTarget(c.id);
      detail.append(call);
    }
    const close = el('button', 'Clear selection');
    control(close, 'exit');
    close.onclick = () => select('');
    detail.append(close);
    reconcile(details, detail);
  }
  function choices(found) {
    if (found.length < 2) {
      select(found[0]?.key || '');
      return;
    }
    chooser.hidden = false;
    const content = el('div');
    content.append(el('p', 'Choose target'));
    for (const c of found) {
      const b = el('button', `${numbers.get(c.key)} · ${c.name}`);
      control(b, 'locate');
      b.onclick = () => select(c.key);
      content.append(b);
    }
    const cancel = el('button', 'Cancel');
    control(cancel, 'exit');
    cancel.onclick = () => {
      chooser.hidden = true;
    };
    content.append(cancel);
    reconcile(chooser, content);
  }
  function worldHits(u, v) {
    const r = canvas.getBoundingClientRect();
    return contacts
      .filter(
        (c) =>
          Math.hypot((c.u - u) * r.width, (c.v - v) * r.height) <=
          Math.max(12, c.radius * r.width + 6),
      )
      .sort((a, b) => Math.hypot(a.u - u, a.v - v) - Math.hypot(b.u - u, b.v - v));
  }
  canvas.addEventListener('pointermove', (e) => {
    const r = canvas.getBoundingClientRect();
    pointer = [(e.clientX - r.left) / r.width, (e.clientY - r.top) / r.height];
  });
  canvas.addEventListener('pointerleave', () => {
    pointer = null;
    hovered = '';
    host.ArenaSelection?.(selected, '');
  });
  function tick(now) {
    if (enabled && !hidden && now - last >= 50) {
      last = now;
      contacts = JSON.parse(host.ArenaContacts?.() || '[]');
      for (const c of contacts) if (!numbers.has(c.key)) numbers.set(c.key, numbers.size + 1);
      if (!contacts.some((c) => c.key === selected)) selected = '';
      hovered = pointer ? worldHits(...pointer)[0]?.key || '' : '';
      host.ArenaSelection?.(selected, hovered);
      render();
    }
    requestAnimationFrame(tick);
  }
  requestAnimationFrame(tick);
  return {
    reset(room) {
      if (sectorSeed !== room) {
        resetContacts(room);
        next = 1;
        sectorSeed = room;
        selected = '';
      }
    },
    update(p, frame) {
      enabled = !!p && p.hull > 0;
      if (!enabled) {
        contacts = [];
        select('');
      }
      render();
    },
    suspend(value) {
      if (hidden === value) return;
      hidden = value;
      hovered = '';
      host.ArenaSelection?.(selected, '');
      render();
    },
    clear() {
      if (!selected && chooser.hidden) return false;
      select('');
      return true;
    },
    pick(u, v) {
      if (hidden || !enabled) return;
      const r = canvas.getBoundingClientRect(),
        l = host.ArenaHudLayout(r.width, r.height).split(',').map(Number),
        x = u * r.width,
        y = v * r.height;
      if (x >= l[10] && x <= l[10] + l[12] && y >= l[11] && y <= l[11] + l[13]) {
        const found = contacts.filter(
          (c) =>
            Math.hypot(l[10] + c.radarU * l[12] - x, l[11] + c.radarV * l[13] - y) < 12 ||
            mapMarks.some(
              (m) =>
                m.key === c.key &&
                x >= l[10] + m.x &&
                x <= l[10] + m.x + m.width &&
                y >= l[11] + m.y &&
                y <= l[11] + m.y + 12,
            ),
        );
        if (found.length) choices(found);
        else {
          const point = host.ArenaRadarPoint?.((x - l[10]) / l[12], (y - l[11]) / l[13]);
          if (point) {
            const [px, py] = point.split(',').map(Number);
            navigate({ x: px, y: py, name: 'Map waypoint' });
          }
        }
        return;
      }
      choices(worldHits(u, v));
    },
  };
}
