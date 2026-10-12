import { stationStyles } from './sector.styles.js';
import { control } from './interface.js';
import { createSectorChart } from './sector-chart.js';
export function createSectorConsole(
  el,
  info,
  send,
  changed,
  openShop,
  navigationChanged = () => {},
  route = () => [],
) {
  let definition,
    contracts = [],
    self,
    state,
    frame,
    mode = '',
    waypoint = null,
    lastDock = 0,
    lastReceipt = 0,
    pending = 0,
    objectiveKey = '';
  const style = el('style');
  style.textContent = stationStyles;
  document.head.append(style);
  const opener = el('button', 'Map [M]'),
    dockButton = el('button', 'Dock [E]');
  opener.type = dockButton.type = 'button';
  info.append(opener, dockButton);
  opener.hidden = dockButton.hidden = true;
  const panel = el('section', '', 'arena-shell arena-panel');
  panel.id = 'sector-console';
  panel.hidden = true;
  panel.setAttribute('role', 'dialog');
  panel.setAttribute('aria-modal', 'true');
  panel.setAttribute('aria-label', 'Sector navigation and station services');
  const heading = el('h2'),
    summary = el('p'),
    toolbar = el('div', '', 'sector-toolbar'),
    map = el('div', '', 'sector-map');
  const shop = el('button', 'Module shop'),
    repair = el('button', 'Repair hull'),
    claim = el('button', 'Claim reward'),
    board = el('div'),
    status = el('p', '', 'sector-status'),
    close = el('button', 'Return [Esc]');
  control(shop, 'shop', 'primary');
  control(repair, 'repair');
  control(claim, 'apply', 'primary');
  control(close, 'exit');
  board.className = 'station-board';
  status.setAttribute('role', 'status');
  toolbar.append(shop, repair, claim);
  panel.append(heading, summary, toolbar, map, board, status, close);
  const footer = el('div', '', 'sector-footer');
  footer.append(close);
  panel.append(footer);
  const caption = el('div', 'LOCAL CONTRACTS · One active assignment per pilot', 'station-caption');
  panel.insertBefore(caption, board);
  document.body.append(panel);
  const guide = el('div');
  guide.id = 'sector-guide';
  guide.hidden = true;
  document.body.append(guide);
  const cards = new Map(),
    received = el('div', '', 'sector-toolbar');
  panel.insertBefore(received, board);
  const chart = createSectorChart(el, map, {
    waypoint: () => waypoint,
    route,
    navigate: (l) => {
      waypoint = l ? { ...l } : null;
      render();
    },
    ping: (kind, l) => action(kind, l.id),
    defend: (id) => action('defend', id),
  });
  const pingButtons = new Map();
  function setOpen(next) {
    if (!self || self.hull <= 0) next = '';
    mode = next;
    panel.hidden = !mode;
    render();
    changed(!!mode);
    if (mode) close.focus({ preventScroll: true });
  }
  function action(action, target = 0) {
    if (pending) return;
    pending = send(action, target) || 0;
    status.textContent = pending ? 'Waiting for station confirmation…' : 'Disconnected.';
    render();
  }
  function objective(c) {
    if (!c) return null;
    const id =
      state?.ready || state?.carrying
        ? c.station
        : c.kind === 'survey'
          ? [50, 51, 52].find((id) => !state?.progress?.includes(id))
          : c.target;
    return definition?.locations.find((l) => l.id === id);
  }
  function nearby() {
    if (!definition || !self) return null;
    return definition.locations
      .filter(
        (l) =>
          ['station', 'wreck', 'beacon'].includes(l.kind) &&
          !(l.kind === 'beacon' && state?.progress?.includes(l.id)) &&
          !(l.kind === 'wreck' && state?.carrying) &&
          Math.hypot(l.x - self.x, l.y - self.y) < (l.kind === 'station' ? 180 : 100),
      )
      .sort(
        (a, b) => Math.hypot(a.x - self.x, a.y - self.y) - Math.hypot(b.x - self.x, b.y - self.y),
      )[0];
  }
  function interact() {
    if (state?.docked) {
      action('undock');
      return;
    }
    const l = nearby();
    if (l) action(l.kind === 'station' ? 'dock' : 'interact', l.id);
  }
  opener.onclick = () => setOpen(mode === 'map' ? '' : 'map');
  dockButton.onclick = () => (state?.docked ? setOpen('station') : interact());
  shop.onclick = () => {
    setOpen('');
    openShop();
  };
  repair.onclick = () => action('repair');
  claim.onclick = () => action('claim');
  close.onclick = () => (mode === 'station' ? action('undock') : setOpen(''));
  function render() {
    if (!self) return;
    const docked = state?.docked || 0;
    const location = definition?.locations.find((l) => l.id === docked);
    const active = contracts.find((c) => c.id === state?.contract);
    const next = objective(active),
      nextKey = active ? `${active.id}:${next?.id}:${!!state?.ready}` : '';
    if (nextKey !== objectiveKey) {
      if (next) waypoint = { ...next, contract: active.id };
      else if (waypoint?.contract) waypoint = null;
      objectiveKey = nextKey;
    }
    heading.textContent = mode === 'map' ? 'SECTOR CHART' : location?.name || 'STATION';
    summary.textContent =
      mode === 'map'
        ? `Sector ${definition?.seed} · Select to inspect · Double-click or Enter to navigate.`
        : `Docked · Sanctuary active · ${frame?.workshop?.credits || 0} credits`;
    panel.dataset.mode = mode;
    caption.hidden = mode !== 'station';
    map.hidden = mode !== 'map';
    toolbar.hidden = board.hidden = mode !== 'station';
    close.textContent = mode === 'station' ? 'Undock [E / Esc]' : 'Return [Esc]';
    repair.textContent = state?.repairCost
      ? `Repair hull · ${state.repairCost} credits`
      : 'Hull fully repaired';
    repair.disabled = !!pending || !state?.repairCost || !docked;
    claim.hidden = !active;
    claim.disabled = !!pending || !state?.ready || active?.station !== docked;
    shop.disabled = !docked;
    for (const [id, { card, button, text, description, badge }] of cards) {
      const c = contracts.find((c) => c.id === id),
        destroyed = frame?.sector?.locations?.find((l) => l.id === c.target)?.state === 'destroyed';
      card.hidden =
        c.station !== docked || (c.kind === 'suppress' && destroyed && state?.contract !== id);
      button.disabled =
        !!pending ||
        !!state?.contract ||
        state?.completed.includes(id) ||
        state?.coolingContracts?.includes(id);
      button.textContent = state?.coolingContracts?.includes(id)
        ? 'Cooling down'
        : state?.completed.includes(id)
          ? 'Completed'
          : state?.contract === id
            ? 'Active'
            : 'Accept contract';
      text.textContent = `${c.credits} credits + ${c.experience} XP`;
      const isActive = state?.contract === id;
      card.dataset.state = isActive ? (state.ready ? 'ready' : 'active') : 'available';
      badge.textContent = isActive
        ? state.ready
          ? 'COMPLETE · Claim your reward'
          : c.kind === 'survey'
            ? `ACTIVE · ${state.progress.length}/3 beacons scanned`
            : c.kind === 'interdict'
              ? `ACTIVE · ${state.progress.length}/3 corridor ships`
              : 'ACTIVE · Follow the marked objective'
        : state?.completed.includes(id)
          ? 'COMPLETED'
          : state?.coolingContracts?.includes(id)
            ? 'COOLDOWN · Available again later'
            : active
              ? 'Finish your current assignment first'
              : 'AVAILABLE';
      description.textContent = {
        survey: 'Scan three orbital beacons. Press E at each and hold position for three seconds.',
        recover: 'Retrieve data from a marked wreck, then bring it back to this station.',
        suppress: 'Contribute damage to the marked hostile array, destroy it, then return.',
        interdict: 'Help defeat three ships from the marked arrival corridor, then return.',
      }[c.kind];
    }
    if (mode === 'map') chart.update(definition, frame, self, state, pending);
    const near = nearby();
    dockButton.textContent = docked
      ? 'Station services'
      : near
        ? `${near.kind === 'station' ? 'Dock' : 'Interact'} [E]`
        : 'Dock [E]';
    dockButton.disabled = !docked && !near;
    let text = docked
      ? 'DOCKED · E to undock'
      : near
        ? `E · ${near.kind === 'station' ? 'Dock at' : 'Interact with'} ${near.name}`
        : '';
    const distress = frame.sector?.rescue;
    if (waypoint?.kind === 'rescue') {
      if (distress?.state === 'active' && waypoint.id === distress.id)
        Object.assign(waypoint, distress);
      else waypoint = null;
    }
    if (distress?.state === 'active')
      text += `\nEMERGENCY · Relief ship ${distress.hull}/${distress.maxHull} hull · ${distress.shield}/${distress.maxShield} shields · ${distress.remaining} attackers · M to locate`;
    else if (distress?.state === 'rescued')
      text +=
        '\nRESCUE COMPLETE · Relief ship saved · Contributors: collect your personal credit and XP pickups beside the ship';
    else if (distress?.state === 'failed') text += '\nRESCUE FAILED · Relief ship destroyed';
    if (waypoint?.pilot) {
      const ally = frame.players.find((p) => p.id === waypoint.pilot);
      if (ally) {
        waypoint.x = ally.x;
        waypoint.y = ally.y;
      }
    }
    if (waypoint && self) {
      const dx = waypoint.x - self.x,
        dy = waypoint.y - self.y;
      const compass = ['E', 'SE', 'S', 'SW', 'W', 'NW', 'N', 'NE'][
        (Math.round(Math.atan2(dy, dx) / (Math.PI / 4)) + 8) % 8
      ];
      text += `\n${compass} → ${waypoint.name} · ${Math.round(Math.hypot(dx, dy))} units`;
    }
    if (active)
      text += `\n${active.kind.toUpperCase()} · ${state.ready ? `Complete — dock at ${next?.name || 'issuing station'} and claim reward` : active.kind === 'survey' ? `${state.progress.length}/3 scanned — next: ${next?.name || 'beacon'} · Press E to scan` : active.kind === 'recover' ? `Retrieve data at ${next?.name} · Press E` : active.kind === 'interdict' ? `${state.progress.length}/3 corridor ships defeated` : 'Damage and destroy ' + (next?.name || 'marked array')}`;
    if (state?.interaction > 0)
      text += `\nScanning ${Math.round(state.interaction * 100)}% · Stay still`;
    if (state?.pings?.length)
      text += '\nShared signal from ' + state.pings.at(-1).name + ' · M to locate';
    if (state?.lootNotice) text += '\n' + state.lootNotice;
    if (state?.contributions?.length) text += '\nInstallation participation secured';
    for (const [id, b] of pingButtons)
      if (!state?.pings?.some((p) => p.id === id)) {
        b.remove();
        pingButtons.delete(id);
      }
    for (const ping of state?.pings || [])
      if (!pingButtons.has(ping.id)) {
        const l = definition.locations.find((l) => l.id === ping.location),
          ally = frame.players.find((p) => p.id === ping.pilot),
          patrol = definition.locations.find((l) => l.id === ping.patrol),
          b = el(
            'button',
            `${ping.name}: ${ping.pilot ? 'Defend ' + (ally?.name || 'ally') + ' · ' + (patrol?.name || 'patrol') : l?.name || 'Shared location'}`,
          );
        b.onclick = () => {
          waypoint = {
            id: ping.location,
            x: ping.x,
            y: ping.y,
            pilot: ping.pilot,
            name: ping.pilot ? 'Defend ' + (ally?.name || 'ally') : l?.name || 'Shared location',
          };
          setOpen('map');
        };
        received.append(b);
        pingButtons.set(ping.id, b);
      }
    navigationChanged(waypoint);
    guide.textContent = text.trim();
    guide.hidden = !text.trim();
  }
  return {
    trackRescue() {
      const distress = frame?.sector?.rescue;
      if (distress?.state !== 'active') return;
      waypoint = { ...distress, kind: 'rescue', name: 'Emergency: save the relief ship' };
      render();
    },
    get open() {
      return !!mode;
    },
    get mode() {
      return mode;
    },
    setOpen,
    setup(sector, catalog) {
      definition = sector;
      contracts = catalog || [];
      board.replaceChildren();
      cards.clear();
      waypoint = null;
      objectiveKey = '';
      navigationChanged(null);
      chart.setup(sector);
      for (const c of contracts) {
        const card = el('article', '', 'contract-card'),
          text = el('p', '', 'contract-reward'),
          description = el('p', '', 'contract-description'),
          badge = el('div', '', 'contract-state'),
          button = el('button', 'Accept contract'),
          locate = el('button', 'Locate objective');
        button.onclick = () => action('accept', c.id);
        locate.onclick = () => {
          const l = objective(c);
          waypoint = l;
          setOpen('map');
        };
        control(button, 'contract', 'primary');
        control(locate, 'locate');
        const actions = el('div', '', 'contract-actions');
        actions.append(button, locate);
        card.append(
          el('div', c.kind.toUpperCase(), 'contract-kind'),
          el(
            'h3',
            {
              survey: 'Orbital survey',
              recover: 'Data recovery',
              suppress: 'Silence the array',
              interdict: 'Patrol interdiction',
            }[c.kind],
          ),
          description,
          text,
          badge,
          actions,
        );
        board.append(card);
        cards.set(c.id, { card, text, button, description, badge });
      }
    },
    update(p, snapshot, connected) {
      self = p;
      frame = snapshot;
      state = snapshot?.sector?.pilot;
      opener.hidden = dockButton.hidden = !connected;
      if (!connected) {
        lastDock = 0;
        pending = 0;
        lastReceipt = 0;
        setOpen('');
        navigationChanged(null);
        guide.hidden = true;
        return;
      }
      if (
        state?.receipt &&
        state.receipt.sequence > lastReceipt &&
        state.receipt.result !== 'pending'
      ) {
        lastReceipt = state.receipt.sequence;
        pending = 0;
        status.textContent =
          state.receipt.result === 'ok' ? 'Confirmed.' : state.receipt.result.replaceAll('_', ' ');
      }
      if ((state?.docked || 0) !== lastDock) {
        lastDock = state?.docked || 0;
        setOpen(lastDock ? 'station' : '');
      }
      if (p?.hull <= 0 && mode) setOpen('');
      render();
    },
    keydown(e) {
      if (!self || self.hull <= 0) return false;
      if (mode === 'map' && e.code === 'Escape' && chart.keydown(e)) {
        e.preventDefault();
        return true;
      }
      if (mode && e.code === 'Escape') {
        e.preventDefault();
        if (!e.repeat) mode === 'station' ? action('undock') : setOpen('');
        return true;
      }
      if (e.code === 'KeyM') {
        e.preventDefault();
        if (!e.repeat) setOpen(mode === 'map' ? '' : 'map');
        return true;
      }
      if (e.code === 'KeyE') {
        e.preventDefault();
        if (!e.repeat) interact();
        return true;
      }
      if (mode === 'map' && chart.keydown(e)) return true;
      if (mode && e.key === 'Tab') {
        const buttons = [...panel.querySelectorAll('button:not(:disabled)')].filter(
          (b) => !b.closest('[hidden]'),
        );
        const i = buttons.indexOf(document.activeElement);
        if (e.shiftKey && i <= 0) {
          e.preventDefault();
          buttons.at(-1)?.focus();
        } else if (!e.shiftKey && i === buttons.length - 1) {
          e.preventDefault();
          buttons[0]?.focus();
        }
        return true;
      }
      return !!mode;
    },
  };
}
