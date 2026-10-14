import { reconcile } from './stable-dom.js';
import { actionMessage } from './action-messages.js';
import { control } from './interface.js';

export function createLivingSector(
  el,
  canvas,
  host,
  send,
  navigate = () => {},
  openShop = () => {},
) {
  const style = el('style');
  style.textContent = `
    #radio-feed{position:fixed;right:18px;bottom:165px;width:310px;max-height:135px;overflow:hidden;color:#b6c9db;font:12px monospace;pointer-events:none;background:#06111bc9;padding:8px;border-left:2px solid #60a5fa;z-index:8}
    #radio-console{position:fixed;right:18px;top:100px;width:350px;max-height:58vh;overflow:auto;padding:12px;background:#101827ed;border:1px solid #dbc181;color:#e8d9a8;font:13px monospace;z-index:35}
    #radio-console button{margin:3px;min-height:30px;padding:5px 9px}
    #radio-shell button[hidden]{display:none!important}
    #radio-console header{position:sticky;top:-12px;background:#101827;z-index:1;padding:8px 0;border-bottom:1px solid #655b45}
    #radio-console select{display:block;width:100%;margin:10px 0;padding:8px;color:#e8d9a8;background:#0b1926;border:1px solid #65717a;font:12px monospace}
    #radio-console h3{font:20px Georgia,serif;letter-spacing:.08em;margin:2px 0 12px} #radio-console h4{border-top:1px solid #655b45;padding-top:12px;margin-bottom:8px;color:#b7cddd}
    #radio-console [data-stock]{gap:10px;padding:6px 0} #radio-console [data-stock] span{flex:1;min-width:0}
    #world-status{position:fixed;left:20px;top:145px;max-width:650px;font:13px monospace;color:#e8d9a8;pointer-events:none;z-index:8}
    #radio-toggle{position:fixed;right:18px;bottom:305px;z-index:8} #radio-console [data-stock]{display:flex;justify-content:space-between;align-items:center;border-top:1px solid #394559}
  `;
  document.head.append(style);
  const feed = el('div'),
    panel = el('section'),
    status = el('div'),
    toggle = el('button', 'Radio / Missions');
  feed.id = 'radio-feed';
  panel.id = 'radio-console';
  panel.classList.add('arena-surface');
  status.id = 'world-status';
  toggle.id = 'radio-toggle';
  panel.setAttribute('aria-label', 'Live radio communications');
  panel.hidden = true;
  toggle.hidden = true;
  control(toggle, 'friends');
  const shell = el('div');
  shell.id = 'radio-shell';
  shell.append(toggle);
  document.body.append(feed, panel, status, shell);
  let frame,
    self,
    selected = 0,
    pendingTrade = 0,
    pendingRun = '',
    lastKey = '',
    mute = false,
    suspended = false;
  const pendingActions = new Map();
  const economic = (action) =>
    [
      'convoy_buy',
      'mission_claim',
      'job_claim',
      'police_pay',
      'wreck_reactor',
      'wreck_cargo',
      'wreck_navigation',
      'radio_trade',
    ].includes(action);
  const act = (action, target = 0) => {
    const key = action + ':' + target;
    if (pendingActions.has(key)) return;
    const sequence = send(action, target);
    if (sequence && economic(action)) {
      pendingActions.set(key, sequence);
      lastKey = '';
    }
    if (action === 'radio_trade') pendingTrade = sequence || 0;
    if (action === 'radio' || action === 'radio_close') pendingTrade = 0;
    canvas.focus();
  };
  function button(label, action, target = 0) {
    const b = el('button', label);
    b.dataset.radioAction = `${action}:${target}`;
    control(
      b,
      'locate',
      'secondary',
      action.includes('buy') || action === 'police_pay'
        ? 'purchase'
        : /abandon|close|refuse/.test(action)
          ? 'danger'
          : /claim|surrender/.test(action)
            ? 'success'
            : /radio|race/.test(action)
              ? 'special'
              : 'action',
    );
    const reason = frame?.sector?.services?.availability?.find(
      (v) => v.target === target && v.action === action,
    )?.reason;
    b.disabled = pendingActions.has(`${action}:${target}`) || !!reason;
    if (reason) b.title = actionMessage(reason);
    b.onclick = () => {
      act(action, target);
      render();
    };
    return b;
  }
  toggle.onclick = () => {
    panel.hidden = !panel.hidden;
    lastKey = '';
    render();
    canvas.focus();
  };
  function render() {
    toggle.hidden = suspended || !self || !panel.hidden;
    feed.hidden = status.hidden = suspended || !self;
    feed.style.right = panel.hidden ? '18px' : 'auto';
    feed.style.left = panel.hidden ? 'auto' : '20px';
    const world = frame?.sector?.world;
    if (!world || !self) return;
    const notices = world.radio.filter(
      (m) => !mute || !['conversation', 'trade'].includes(m.channel),
    );
    feed.replaceChildren(...notices.slice(-4).map((m) => el('div', `${m.speaker}: ${m.text}`)));
    const race = world.race;
    status.textContent = [
      world.job && `${world.job.toUpperCase()} · ${world.jobState}`,
      world.police && `POLICE · ${world.police} ${Math.round(world.scan * 100)}%`,
      world.event && `${world.event.toUpperCase()} · ${world.eventSeconds}s`,
      race &&
        `${race.category.toUpperCase()} · ${race.state} · Gate ${Math.min(race.checkpoint + 1, race.course.length / 2)}/${race.course.length / 2} · ${race.state === 'countdown' ? race.countdown + 's to start' : race.seconds.toFixed(2) + 's'}`,
    ]
      .filter(Boolean)
      .join(' | ');
    if (panel.hidden) return;
    const key = JSON.stringify([
      selected,
      world.conversation,
      world.stock.map((s) => s.id),
      world.contacts?.map((c) => [c.id, c.cargoRevision]),
      world.cargo.map((c) => [c.id, c.owner]),
      frame.sector?.services?.assignment,
      frame.sector?.services?.availability,
      world.job,
      world.jobState,
      world.protection,
      race?.stagingSeconds,
      world.objective?.uncertainty,
      world.event,
      world.eventTarget,
      world.police,
      world.tow,
      race?.state,
      frame.sector?.pilot?.receipt,
      frame.sector?.services?.hailing,
      mute,
      [...pendingActions],
    ]);
    if (key === lastKey) return;
    lastKey = key;
    const scroll = panel.scrollTop;
    const focused = panel.contains(document.activeElement)
      ? document.activeElement.dataset.radioAction
      : null;
    const content = el('div');
    const header = el('header');
    header.append(el('h3', 'LIVE RADIO'));
    content.append(header);
    const receipt = frame.sector?.pilot?.receipt;
    if (receipt && !['ok', 'pending'].includes(receipt.result))
      content.append(el('p', actionMessage(receipt.result)));
    const closeButton = el('button', 'Close');
    control(closeButton, 'exit');
    closeButton.onclick = () => {
      close();
    };
    const muteButton = el('button', mute ? 'Unmute chatter' : 'Mute chatter');
    control(muteButton, 'friends');
    muteButton.onclick = () => {
      mute = !mute;
      lastKey = '';
      render();
      canvas.focus();
    };
    header.append(closeButton, muteButton);
    if (world.eventObjective) {
      const event = el('button', 'Navigate: ' + world.event.replaceAll('-', ' '));
      control(event, 'locate');
      event.onclick = () => {
        navigate(frame?.sector?.world?.eventObjective);
        canvas.focus();
      };
      content.append(event);
    }
    const nearby = world.contacts || [];
    if (nearby.length) {
      const picker = el('select');
      picker.setAttribute('aria-label', 'Radio contact');
      picker.append(new Option('Choose a nearby ship', '0'));
      for (const contact of nearby)
        picker.append(new Option(`${contact.name} · ${contact.role}`, String(contact.id)));
      picker.value = String(selected);
      picker.onchange = () => {
        selected = Number(picker.value);
        lastKey = '';
        if (selected) act('radio', selected);
        render();
      };
      content.append(picker);
    }
    if (world.cargo.length) {
      const picker = el('select');
      picker.setAttribute('aria-label', 'Nearby salvage');
      picker.append(new Option('Choose nearby salvage', '0'));
      for (const cargo of world.cargo)
        picker.append(
          new Option(
            `${cargo.kind} · ${cargo.id}${cargo.owner ? ' · in tow' : ''}`,
            String(cargo.id),
          ),
        );
      picker.value = String(selected);
      picker.onchange = () => {
        selected = Number(picker.value);
        act('radio_close');
        lastKey = '';
        render();
      };
      content.append(picker);
    }
    const cargo = world.cargo.find((c) => c.id === selected);
    if (cargo) {
      content.append(el('p', `${cargo.kind} · ${cargo.credits} credits`));
      if (cargo.kind === 'derelict') {
        content.append(
          el(
            'p',
            [
              'Clue: the reactor is stable; other compartments are venting.',
              'Clue: cargo seals are intact; the reactor is cold.',
              'Clue: navigation uplink has power; cargo is contaminated.',
            ][cargo.id % 3],
          ),
        );
        for (const choice of ['reactor', 'cargo', 'navigation', 'abandon'])
          content.append(button(choice, 'wreck_' + choice, cargo.id));
      } else if (cargo.kind === 'anomaly') {
        content.append(
          el(
            'p',
            'Ancient signal: accept an exploration assignment from a contact, then take three sensor readings.',
          ),
        );
      } else if (!cargo.owner) content.append(button('Tow cargo', 'tow', cargo.id));
    }
    if (world.tow) content.append(button('Stop tow', 'tow', 0));
    const contact = nearby.find((c) => c.id === selected);
    if (contact) {
      content.append(
        el('p', `${contact.name} · ${contact.state}`),
        button('Hail / banter', 'radio', selected),
      );
      if (contact.trade) content.append(button('Close-range trade', 'radio_trade', selected));
      if (
        frame.sector?.services?.contacts.some((c) => c.id === selected && c.role === 'trader') &&
        frame.sector.services.hailing === selected
      ) {
        const shop = el('button', 'Open module shop');
        control(shop, 'shop');
        shop.onclick = openShop;
        content.append(shop);
      }
    }
    for (const stock of world.stock) {
      const row = el('div');
      row.dataset.stock = stock.id;
      row.append(el('span', stock.name), button(`${stock.price} credits`, 'convoy_buy', stock.id));
      content.append(row);
    }
    content.append(el('h4', 'ASSIGNMENTS'));
    const legacy = frame.sector?.services?.assignment;
    if (legacy?.kind) {
      content.append(
        el(
          'p',
          `${legacy.kind}: ${Math.min(3, legacy.progress)}/3${legacy.ready ? ' · Return to a contact or station' : ''}`,
        ),
      );
      if (legacy.ready) content.append(button('Claim completed work', 'job_claim'));
      content.append(button('Abandon assignment', 'mission_abandon'));
    } else if (!world.job)
      for (const kind of [
        'bounty',
        'interception',
        'smuggling',
        'recovery',
        'exploration',
        'protection',
      ])
        content.append(button(kind, 'mission_' + kind));
    else {
      content.append(el('p', `${world.job}: ${world.jobState}`));
      const guidance = {
        bounty: 'Damage the marked enemy and help destroy it.',
        interception: 'Destroy the marked courier before it reaches its station.',
        protection: 'Help destroy every designated attacker while the protected ship survives.',
        recovery: 'Tow the marked salvage to a friendly station before a competitor delivers it.',
        exploration:
          'Take readings within 2,000, 900, then 180 units. The last reading requires a clear sensor path.',
        smuggling: 'Dock at the marked station without having your cargo confiscated.',
      };
      content.append(
        el(
          'p',
          world.jobState === 'ready'
            ? 'Work complete. Approach a friendly contact or dock at a friendly station to claim.'
            : world.jobState === 'failed'
              ? 'Assignment failed. Abandon it to take another job.'
              : guidance[world.job],
        ),
      );
      if (world.jobState === 'waiting')
        content.append(
          el(
            'p',
            'Waiting for a friendly, uncontested delivery station. Cargo and reward are preserved.',
          ),
        );
      if (world.protection) {
        const p = world.protection;
        content.append(
          el(
            'p',
            `Protected ship: ${p.hull} hull · ${p.shield} shields · ${p.remaining} attackers remaining · ${p.participated ? 'Participation secured' : 'Damage an attacker to qualify'}`,
          ),
          el('p', p.reason),
        );
        for (const mark of p.attackers) {
          const locate = el(
            'button',
            `${mark.visible ? 'Attacker' : 'Last known attacker'} #${mark.id}`,
          );
          control(locate, 'locate');
          locate.onclick = () => navigate({ x: mark.x, y: mark.y, name: locate.textContent });
          content.append(locate);
        }
      }
      if (world.jobState === 'ready')
        content.append(button('Claim completed work', 'mission_claim'));
      content.append(button('Abandon assignment', 'mission_abandon'));
      if (world.job === 'exploration')
        content.append(button('Take sensor reading', 'mission_scan'));
      if (world.objective) {
        const locate = el('button', 'Navigate to objective');
        control(locate, 'locate');
        locate.onclick = () => {
          navigate(frame?.sector?.world?.objective);
          canvas.focus();
        };
        content.append(locate);
      }
    }
    if (world.police === 'scanning') content.append(button('Surrender cargo', 'police_surrender'));
    if (world.police)
      content.append(
        button('Confiscation + fine', 'police_pay'),
        button('Refuse inspection', 'police_refuse'),
      );
    content.append(el('h4', 'RACING · Arrival Port start'));
    ['Sprint', 'Obstacle', 'Rally'].forEach((name, i) =>
      content.append(button(name, 'race_join', i)),
    );
    if (race) {
      content.append(button('Leave race', 'race_stop'));
      if (race.state === 'staging') {
        content.append(
          el(
            'p',
            `Fly to your staging marker, stop below 5 u/s and align with its arrow. ${race.stagingSeconds}s remaining.`,
          ),
        );
        const stage = el('button', 'Navigate to staging slot');
        control(stage, 'locate');
        stage.onclick = () => navigate({ x: race.stageX, y: race.stageY, name: 'Race staging' });
        content.append(stage);
      }
    }
    if (race?.state === 'save_failed') content.append(button('Retry saving result', 'race_retry'));
    const friendly = world.territory.filter((t) => t.owner === 'friendly').length;
    content.append(
      el(
        'p',
        `Territory: ${Math.round((friendly / Math.max(1, world.territory.length)) * 100)}% friendly`,
      ),
    );
    reconcile(panel, content);
    if (focused)
      [...panel.querySelectorAll('[data-radio-action]')]
        .find((b) => b.dataset.radioAction === focused)
        ?.focus({ preventScroll: true });
    panel.scrollTop = scroll;
  }
  function close(keepTrade = false) {
    if (panel.hidden) return false;
    panel.hidden = true;
    if (!keepTrade) act('radio_close');
    render();
    return true;
  }
  return {
    close,
    suspend(value, keepTrade = false) {
      if (suspended === value) return;
      suspended = value;
      if (value) close(keepTrade && !!frame?.sector?.services?.hailing);
      render();
    },
    openTarget(id, trade = false) {
      if (suspended) return;
      selected = id;
      panel.hidden = false;
      lastKey = '';
      act(trade ? 'radio_trade' : 'radio', id);
      render();
    },
    get trading() {
      return (
        !panel.hidden &&
        (pendingTrade > 0 ||
          (!!frame?.sector?.services?.hailing &&
            frame.sector.services.hailing === frame?.sector?.world?.conversation))
      );
    },
    update(p, snapshot) {
      if (!p || pendingRun !== p.runId) {
        pendingActions.clear();
        pendingTrade = 0;
        pendingRun = p?.runId || '';
      }
      self = p;
      frame = snapshot;
      const receipt = frame?.sector?.pilot?.receipt;
      for (const [key, seq] of pendingActions)
        if (
          !p ||
          (frame?.sector?.pilot?.receipts || [receipt]).some(
            (r) =>
              r &&
              seq >= r.sequence &&
              seq <= (r.throughSequence || r.sequence) &&
              !['pending', 'refund_pending'].includes(r.result),
          )
        )
          pendingActions.delete(key);
      if (
        !p ||
        (frame?.sector?.pilot?.receipts || [receipt]).some(
          (r) =>
            r &&
            pendingTrade >= r.sequence &&
            pendingTrade <= (r.throughSequence || r.sequence) &&
            !['pending', 'refund_pending'].includes(r.result),
        )
      )
        pendingTrade = 0;
      toggle.hidden = !p;
      feed.hidden = status.hidden = !p;
      if (!p) panel.hidden = true;
      render();
    },
  };
}
