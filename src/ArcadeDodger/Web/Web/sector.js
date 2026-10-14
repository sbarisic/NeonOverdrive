import { createContextActions } from './context-actions.js';
import { actionMessage } from './action-messages.js';
import { createServicePanel } from './sector-services.js';
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
  radio = () => {},
) {
  let definition,
    chassisCatalog = [],
    contracts = [],
    self,
    state,
    frame,
    mode = '',
    waypoint = null,
    lastDock = 0,
    lastReceipt = 0,
    receiptRun = '',
    pending = 0,
    objectiveKey = '';
  const style = el('style');
  style.textContent = stationStyles;
  document.head.append(style);
  const opener = el('button', 'Map [M]'),
    dockButton = el('button', 'Dock [E]');
  opener.type = dockButton.type = 'button';
  info.append(opener, dockButton);
  const cruiseButton = el('button', 'Cruise [Shift]');
  control(cruiseButton, 'mobility');
  info.append(cruiseButton);
  cruiseButton.onclick = () => action('cruise');
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
  const shipyard = el('div', '', 'station-board');
  panel.insertBefore(shipyard, board);
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
  let serviceTarget = 0,
    serviceChoices = [],
    lastHail = 0;
  const services = createServicePanel(
    el,
    panel,
    (kind, id) => {
      if (kind === 'manage') {
        serviceTarget = id;
        setOpen('outpost');
        return;
      }
      if (kind === 'dock' || kind === 'interact') setOpen('');
      action(kind, id);
      if (kind === 'hail') setOpen('contact');
      if (kind === 'goodbye') setOpen('');
    },
    () => {
      setOpen('', true);
      openShop();
    },
    (id) => {
      waypoint = definition.locations.find((l) => l.id === id);
      setOpen('map');
    },
  );
  const claimJob = el('button', 'Claim contact assignment');
  control(claimJob, 'apply');
  claimJob.onclick = () => action('job_claim');
  toolbar.append(claimJob);
  const pingButtons = new Map();
  const contextActions = createContextActions(el, (item) => {
    if (['radio', 'radio_trade'].includes(item.action)) {
      radio(item.id, item.action === 'radio_trade');
      return;
    }
    if (item.action === 'manage' || item.build) {
      serviceTarget = item.id;
      setOpen('outpost');
      return;
    }
    action(item.action, item.id);
    if (item.action === 'hail') setOpen('contact');
    else document.querySelector('#canvas')?.focus();
  });
  function nearbyActions() {
    if (!self || !definition) return [];
    const available = frame.sector?.services?.availability || [];
    const actions = [];
    const add = (entity, action, label, icon, build = false) => {
      const eligibility = available.find((a) => a.target === entity.id && a.action === action);
      if (eligibility?.reason) return;
      actions.push({
        id: entity.id,
        action,
        label,
        icon,
        build,
        distance: Math.hypot(entity.x - self.x, entity.y - self.y),
        pending: !build && action !== 'manage' && action !== 'radio' && !!pending,
      });
    };
    for (const l of definition.locations) {
      const a = available.find((a) => a.target === l.id && ['dock', 'interact'].includes(a.action));
      if (a && !a.reason)
        add(
          l,
          a.action,
          `${a.action === 'dock' ? 'Dock' : l.kind === 'cache' ? 'Search' : 'Salvage / Scan'}: ${l.name} [E]`,
          l.kind === 'station' ? 'station' : 'locate',
        );
    }
    for (const c of frame.sector?.services?.contacts || [])
      if (Math.hypot(c.x - self.x, c.y - self.y) <= 180)
        add(c, 'hail', `Hail: ${c.name}`, 'friends');
    for (const c of frame.enemies || [])
      if (
        c.hull > 0 &&
        ['friendly', 'neutral'].includes(c.faction) &&
        c.surfacePlanet === 0 &&
        !['installation', 'salvage-core', 'salvage-turret', 'repair-bot'].includes(c.kind) &&
        Math.hypot(c.x - self.x, c.y - self.y) <= 180 &&
        !(frame.sector?.services?.contacts || []).some((n) => n.id === c.id)
      )
        add(c, 'radio', `Radio: ${c.role || c.kind} #${c.id}`, 'friends');
    for (const contact of frame.sector?.world?.contacts || []) {
      const eligibility = available.find(
        (a) => a.target === contact.id && a.action === 'radio_trade',
      );
      const ship = frame.enemies?.find((e) => e.id === contact.id);
      if (ship && eligibility && !eligibility.reason)
        add(ship, 'radio_trade', `Trade: ${contact.name}`, 'shop');
    }
    for (const site of frame.sector?.services?.outposts || []) {
      if (Math.hypot(site.x - self.x, site.y - self.y) > 180) continue;
      add(site, 'manage', `Manage: ${site.name}`, 'loadout');
      for (const socket of site.sockets || []) {
        if (socket.hull > 0 || socket.pending || (socket.slot && !site.active)) continue;
        add(
          site,
          ['activate', 'turret_one', 'turret_two', 'repair_bot'][socket.slot],
          `${['Activate core', 'Build turret 1', 'Build turret 2', 'Build repair bot'][socket.slot]} · ${socket.cost} cr`,
          'shop',
          true,
        );
      }
    }
    for (const eligibility of available) {
      if (
        eligibility.reason ||
        !['tow', 'mission_scan', 'mission_claim'].includes(eligibility.action)
      )
        continue;
      const cargo = frame.sector?.world?.cargo?.find((c) => c.id === eligibility.target);
      const target = cargo || { id: eligibility.target, x: self.x, y: self.y };
      const label =
        eligibility.action === 'tow'
          ? target.id
            ? `Tow: ${cargo?.kind || 'cargo'} #${target.id}`
            : 'Stop towing'
          : eligibility.action === 'mission_scan'
            ? 'Mission: take sensor reading'
            : 'Claim mission reward';
      add(
        target,
        eligibility.action,
        label,
        eligibility.action === 'mission_claim' ? 'apply' : 'locate',
      );
    }
    const selected = document.querySelector('#target-details')?.dataset.targetKey;
    return actions.sort(
      (a, b) =>
        Number(selected === `npc:${b.id}` && ['hail', 'radio', 'radio_trade'].includes(b.action)) -
          Number(
            selected === `npc:${a.id}` && ['hail', 'radio', 'radio_trade'].includes(a.action),
          ) ||
        a.distance - b.distance ||
        a.id - b.id ||
        a.action.localeCompare(b.action),
    );
  }
  function setOpen(next, keepHail = false) {
    if (frame?.sector?.services?.hailing && next !== 'contact' && !keepHail) send('goodbye', 0);
    if (!self || self.hull <= 0) next = '';
    mode = next;
    panel.hidden = !mode;
    render();
    if (!keepHail) changed(!!mode);
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
          ['station', 'wreck', 'beacon', 'cache'].includes(l.kind) &&
          !(l.kind === 'cache' && frame?.sector?.services?.claimedCaches.includes(l.id)) &&
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
    const choices = [];
    for (const c of frame?.sector?.services?.contacts || [])
      if (Math.hypot(c.x - self.x, c.y - self.y) <= 180)
        choices.push({ id: c.id, name: `Hail ${c.name}`, action: 'hail' });
    for (const site of frame?.sector?.services?.outposts || [])
      if (Math.hypot(site.x - self.x, site.y - self.y) <= 180)
        choices.push({ id: site.id, name: `Manage ${site.name}`, action: 'manage' });
    const l = nearby();
    if (l)
      choices.push({
        id: l.id,
        name: `${l.kind === 'station' ? 'Dock at' : 'Search / interact with'} ${l.name}`,
        action: l.kind === 'station' ? 'dock' : 'interact',
      });
    if (choices.length > 1 || choices[0]?.action === 'manage') {
      serviceChoices = choices;
      setOpen('chooser');
    } else if (choices.length) {
      action(choices[0].action, choices[0].id);
      if (choices[0].action === 'hail') setOpen('contact');
    }
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
    contextActions.update(nearbyActions(), !!self && self.hull > 0);
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
    shipyard.hidden = mode !== 'station';
    for (const card of shipyard.children) {
      const design = chassisCatalog.find((d) => d.id === Number(card.dataset.chassis));
      card.hidden = design.station !== docked;
      const button = card.querySelector('button');
      button.disabled =
        !!pending || design.id === self.chassis || (frame?.workshop?.credits || 0) < design.price;
      button.textContent =
        design.id === self.chassis ? 'Current ship' : `Buy · ${design.price} credits`;
      const current = chassisCatalog.find((d) => d.id === self.chassis);
      if (current) {
        const delta = (n) => (n > 0 ? `+${n}` : String(n));
        card.querySelector('[data-comparison]').textContent =
          `Compared with ${current.name}: ${delta(design.hull - current.hull)} hull, ${delta(design.shield - current.shield)} shields, ${delta(design.speed - current.speed)} speed, ${delta(design.turnRate - current.turnRate)}°/s. No repair or trade-in.`;
      }
    }
    close.textContent = mode === 'station' ? 'Undock [E / Esc]' : 'Return [Esc]';
    repair.textContent = state?.repairCost
      ? `Repair hull · ${state.repairCost} credits`
      : 'Hull fully repaired';
    repair.disabled = !!pending || !state?.repairCost || !docked;
    claim.hidden = !active;
    claim.disabled = !!pending || !state?.ready || active?.station !== docked;
    shop.disabled = !docked;
    claimJob.hidden = !frame.sector?.services?.assignment?.ready;
    services.render(mode, serviceTarget, serviceChoices, frame, pending);
    if (['contact', 'chooser', 'outpost'].includes(mode)) {
      heading.textContent = mode === 'outpost' ? 'SALVAGE OUTPOST' : 'COMMUNICATIONS';
      summary.textContent =
        mode === 'outpost'
          ? 'Shared defenses and repairs · The sector stays live'
          : 'Nearby contacts and services';
    }
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
    const nearServices = [
      ...(frame.sector?.services?.contacts || []),
      ...(frame.sector?.services?.outposts || []),
    ].some((l) => Math.hypot(l.x - self.x, l.y - self.y) <= 180);
    dockButton.disabled = !docked && !near && !nearServices;
    cruiseButton.hidden = !self || self.hull <= 0;
    cruiseButton.textContent = `Cruise · ${self.cruiseState || 'ready'}${self.cruiseTicks ? ' ' + Math.ceil(self.cruiseTicks / 30) + 's' : ''} [Shift]`;
    if (nearServices && !docked) dockButton.textContent = 'Hail / Services [E]';
    let text = docked
      ? 'DOCKED · E to undock'
      : near
        ? `E · ${near.kind === 'station' ? 'Dock at' : 'Interact with'} ${near.name}`
        : '';
    if (waypoint?.kind === 'contact') {
      const contact = frame.sector?.services?.contacts.find((c) => c.id === waypoint.id);
      if (contact) Object.assign(waypoint, contact);
      else waypoint = null;
    }
    if (waypoint?.kind === 'cache' && frame.sector?.services?.claimedCaches.includes(waypoint.id)) {
      const nextCache = frame.sector.services.assignment.targets.find(
        (id) => !frame.sector.services.claimedCaches.includes(id),
      );
      waypoint = definition.locations.find((l) => l.id === nextCache) || null;
    }
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
    if (waypoint?.kind === 'assignment' && frame?.sector?.world?.objective?.id === waypoint.id)
      waypoint = { ...frame.sector.world.objective };
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
    if (nearServices && !docked) text += '\nE · Hail nearby ships / open services';
    const job = frame.sector?.services?.assignment;
    if (job?.kind)
      text += `\nCONTACT ${job.kind.toUpperCase()} · ${Math.min(3, job.progress)}/3 · ${job.ready ? 'Claim at any contact or station' : 'In progress'}`;
    const raid = frame.sector?.services?.raid;
    if (raid)
      text += `\nRAID · ${definition.locations.find((l) => l.id === raid.site)?.name} · ${raid.remaining} ships incoming`;
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
    get cloaked() {
      return !!mode && !['outpost', 'chooser'].includes(mode);
    },
    get mode() {
      return mode;
    },
    setOpen,
    cruise() {
      action('cruise');
    },
    navigateTo(location) {
      waypoint = location ? { ...location } : null;
      render();
    },
    setup(sector, catalog, chassis = []) {
      definition = sector;
      chassisCatalog = chassis;
      shipyard.replaceChildren();
      for (const design of chassis.filter((d) => d.id > 0)) {
        const card = el('article', '', 'contract-card');
        card.dataset.chassis = design.id;
        const button = el('button', `Buy · ${design.price} credits`);
        control(button, 'shop');
        button.onclick = () => action('buy_chassis', design.id);
        card.append(
          el('h3', design.name),
          el(
            'p',
            `${design.hull} hull · ${design.shield} shields · ${Math.round(design.armor * 100)}% armor`,
          ),
          el('p', `${design.speed} speed · ${design.turnRate}°/s turning · Equipment retained`),
          button,
        );
        const comparison = el('p');
        comparison.dataset.comparison = '';
        card.insertBefore(comparison, button);
        shipyard.append(card);
      }
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
        contextActions.update([], false);
        return;
      }
      const currentRun = p?.runId || state?.receipts?.[0]?.run || state?.receipt?.run;
      if (currentRun && receiptRun !== currentRun) {
        if (receiptRun) pending = 0;
        receiptRun = currentRun;
        lastReceipt = 0;
      }
      for (const receipt of state?.receipts || (state?.receipt ? [state.receipt] : [])) {
        if (['pending', 'refund_pending'].includes(receipt.result)) continue;
        if (pending >= receipt.sequence && pending <= (receipt.throughSequence || receipt.sequence))
          pending = 0;
        if ((receipt.cursor || receipt.sequence) <= lastReceipt) continue;
        lastReceipt = receipt.cursor || receipt.sequence;
        status.textContent = receipt.result === 'ok' ? 'Confirmed.' : actionMessage(receipt.result);
        if (receipt.result !== 'ok') contextActions.report(status.textContent);
      }
      if ((state?.docked || 0) !== lastDock) {
        lastDock = state?.docked || 0;
        setOpen(lastDock ? 'station' : '');
      }
      const hail = snapshot?.sector?.services?.hailing || 0;
      if (lastHail && !hail && mode === 'contact') setOpen('');
      lastHail = hail;
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
