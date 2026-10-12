import { glyph, color } from './weapon-art.js';
import { drawChain } from './weapon-preview.js';
import { createChainPreview } from './weapon-chain-view.js';
import { createModuleDrag } from './weapon-drag.js';
import { loadoutStyles, shopStyles, compactStyles } from './weapons.styles.js';
import { control } from './interface.js';
// The server supplies names, prices, parameters, and descriptions. This module owns presentation only.
export function createWeaponBench(element, info, equip, buy, onToggle) {
  document.head.append(element('style', loadoutStyles));

  document.head.append(element('style', shopStyles));
  document.head.append(element('style', compactStyles));
  const opener = element('button', 'Weapons [I]');
  opener.type = 'button';
  opener.hidden = true;
  info.append(opener);
  const panel = element('section', '', 'arena-shell');
  panel.id = 'weapon-bench';
  panel.hidden = true;
  panel.setAttribute('role', 'dialog');
  panel.setAttribute('aria-modal', 'true');
  panel.setAttribute('aria-labelledby', 'weapon-title');
  opener.setAttribute('aria-controls', panel.id);
  opener.setAttribute('aria-expanded', 'false');
  const title = element('h2', 'MODULAR ARSENAL');
  title.id = 'weapon-title';
  title.tabIndex = -1;
  const wallet = element('span', '100 CREDITS', 'wallet'),
    levelLabel = element('span', '', 'forge-level'),
    heading = element('div', '', 'forge-heading');
  heading.append(title, wallet, levelLabel);
  const tabs = element('div', '', 'workshop-tabs');
  tabs.setAttribute('role', 'tablist');
  const shopTab = element('button', 'Shop'),
    loadoutTab = element('button', 'Loadout');
  tabs.append(shopTab, loadoutTab);
  control(shopTab, 'shop');
  control(loadoutTab, 'loadout');
  const body = element('div', '', 'forge-body'),
    shop = element('section', '', 'workshop-tab'),
    loadout = element('section', '', 'workshop-tab');
  shop.id = 'workshop-shop';
  loadout.id = 'workshop-loadout';
  for (const [button, section] of [
    [shopTab, shop],
    [loadoutTab, loadout],
  ]) {
    button.type = 'button';
    button.id = section.id + '-tab';
    button.setAttribute('role', 'tab');
    button.setAttribute('aria-controls', section.id);
    section.setAttribute('role', 'tabpanel');
    section.setAttribute('aria-labelledby', button.id);
  }
  const filters = element('div', '', 'shop-filters'),
    shopGrid = element('div', '', 'shop-grid');
  shop.append(filters, shopGrid);
  const rows = [],
    slots = [],
    items = [],
    cards = [],
    weaponButtons = [];
  const weaponSelector = element('div', '', 'weapon-selector');
  weaponSelector.setAttribute('role', 'tablist');
  weaponSelector.setAttribute('aria-label', 'Select weapon to edit');
  loadout.append(weaponSelector);
  const detail = element('div', '', 'bench-detail');
  detail.id = 'module-detail';
  const detailName = element('strong', 'Build your fleet'),
    detailText = element(
      'p',
      'Choose an emitter, then place attachments at fixed distance checkpoints.',
    );
  detail.append(detailName, detailText);
  const preview = createChainPreview(element, (stage) => {
    const slot = weapon * 9 + stage + 1;
    describe(part(slot), slot);
    slots[slot].focus({ preventScroll: true });
  });
  const inventory = element('div', '', 'module-catalog');
  inventory.setAttribute(
    'aria-label',
    'Owned inventory. Drop an equipped module here to unequip it.',
  );
  const state = element('p', '', 'bench-status');
  state.setAttribute('role', 'status');
  const apply = element('button', 'Apply loadout'),
    reset = element('button', 'Reset draft'),
    clear = element('button', 'Clear selected'),
    close = element('button', 'Return to arena [I / Esc]');
  control(apply, 'apply', 'primary');
  control(reset, 'reset');
  control(clear, 'clear');
  control(close, 'exit');
  const actions = element('div', '', 'bench-actions');
  actions.append(apply, reset, clear, close);
  for (const b of [apply, reset, clear, close]) b.type = 'button';
  body.append(shop, loadout);
  panel.append(
    heading,
    tabs,
    element(
      'p',
      'Choose a weapon to edit. To move a part, drop it on another weapon tab or select it, switch weapons, then choose a socket.',
      'forge-help',
    ),
    body,
    detail,
    state,
    actions,
  );
  document.body.append(panel);
  let docked = false,
    opened = false,
    available = false,
    level = 1,
    stats = [],
    unlocked = [true, false, false, false],
    credits = 0,
    owned = Array(29).fill(0),
    catalog = [],
    definitions = new Map(),
    unlock = [1, 5, 10, 15];
  const starter = () =>
    Array.from({ length: 4 }, () => ({ emitter: 1, attachments: Array(8).fill(0), enabled: true }));
  let saved = starter(),
    draft = starter(),
    selected = null,
    pending = 0,
    pendingKind = '',
    aliveLife = null,
    weapon = 0,
    filter = 'All',
    tab = 'loadout',
    animation = 0;
  const clone = (w) => w.map((x) => ({ ...x, attachments: [...x.attachments] }));
  const part = (i) =>
    i % 9 === 0
      ? draft[Math.floor(i / 9)].emitter
      : draft[Math.floor(i / 9)].attachments[(i % 9) - 1];
  function setPart(i, id) {
    if (i % 9 === 0) draft[Math.floor(i / 9)].emitter = id;
    else draft[Math.floor(i / 9)].attachments[(i % 9) - 1] = id;
  }
  const accepts = (slot, id) =>
    id === 0 || (slot % 9 === 0 ? id >= 1 && id <= 4 : id >= 5 && id <= 28);
  const used = (id) =>
    draft.reduce(
      (n, w) => n + (w.emitter === id ? 1 : 0) + w.attachments.filter((x) => x === id).length,
      0,
    );
  const spare = (id) => (owned[id] || 0) - used(id);
  const name = (id) => definitions.get(id)?.name || 'Empty';
  function describe(id, slot = -1) {
    const d = definitions.get(id);
    if (slot >= 0) weapon = Math.floor(slot / 9);
    detailName.textContent = d?.name || (slot % 9 === 0 ? 'Emitter socket' : 'Empty checkpoint');
    detailText.textContent = d
      ? d.description +
        (draft[weapon].emitter === 2 && d.laserDescription ? ' Laser: ' + d.laserDescription : '')
      : 'Drag an owned copy here. Effects activate after travelling to this checkpoint.';
  }
  function switchTab(value) {
    cancelDrag();
    tab = value;
    shop.hidden = value !== 'shop';
    loadout.hidden = value !== 'loadout';
    shopTab.setAttribute('aria-selected', String(value === 'shop'));
    loadoutTab.setAttribute('aria-selected', String(value === 'loadout'));
    shopTab.tabIndex = value === 'shop' ? 0 : -1;
    loadoutTab.tabIndex = value === 'loadout' ? 0 : -1;
    render();
  }
  shopTab.addEventListener('click', () => switchTab('shop'));
  loadoutTab.addEventListener('click', () => switchTab('loadout'));
  tabs.addEventListener('keydown', (e) => {
    if (['ArrowLeft', 'ArrowRight'].includes(e.key)) {
      e.preventDefault();
      switchTab(tab === 'shop' ? 'loadout' : 'shop');
      (tab === 'shop' ? shopTab : loadoutTab).focus();
    }
  });
  function selectWeapon(index) {
    cancelDrag();
    weapon = index;
    describe(part(index * 9), index * 9);
    render();
  }
  weaponSelector.addEventListener('keydown', (e) => {
    if (['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(e.key)) {
      e.preventDefault();
      const index =
        e.key === 'Home' ? 0 : e.key === 'End' ? 3 : (weapon + (e.key === 'ArrowLeft' ? 3 : 1)) % 4;
      selectWeapon(index);
      weaponButtons[index].focus();
    }
  });
  function render() {
    wallet.textContent = `${credits} CREDITS${docked ? '' : ' · Dock at a station to purchase'}`;
    levelLabel.textContent = `LEVEL ${level} · ${unlocked.filter(Boolean).length}/4 WEAPONS`;
    slots.forEach((slot, i) => {
      const id = part(i);
      slot.dataset.module = id;
      slot.style.setProperty('--part', color(id));
      slot.querySelector('.slot-icon').textContent = glyph(id);
      slot.querySelector('.slot-name').textContent = id
        ? name(id)
        : i % 9 === 0
          ? 'Emitter'
          : 'Empty';
      slot.setAttribute(
        'aria-label',
        `Weapon ${Math.floor(i / 9) + 1}, ${i % 9 === 0 ? 'emitter' : (i % 9) * 5 + '%'}: ${name(id)}`,
      );
      slot.dataset.selected = String(selected?.slot === i);
    });
    rows.forEach((row, i) => {
      row.section.hidden = i !== weapon;
      weaponButtons[i].setAttribute('aria-selected', String(i === weapon));
      weaponButtons[i].tabIndex = i === weapon ? 0 : -1;
      weaponButtons[i].querySelector('span').textContent = !unlocked[i]
        ? 'Locked · L' + unlock[i]
        : draft[i].enabled
          ? 'Enabled'
          : 'Disabled';
      row.section.dataset.locked = String(!unlocked[i]);
      row.label.textContent = `WEAPON ${i + 1} · ${!unlocked[i] ? 'REQUIRES BAY ' + (i + 1) + ' · LEVEL ' + unlock[i] : 'READY'}`;
      row.toggle.textContent = draft[i].enabled ? 'Enabled' : 'Disabled';
      row.toggle.setAttribute('aria-pressed', String(draft[i].enabled));
    });
    items.forEach((item) => {
      const id = +item.dataset.moduleId;
      item.hidden = (owned[id] || 0) === 0;
      item.querySelector('.module-count').textContent =
        `${Math.max(0, spare(id))} free · ${owned[id] || 0} owned`;
      item.setAttribute('aria-disabled', String(spare(id) <= 0));
      item.dataset.selected = String(selected?.slot === -1 && selected.module === id);
    });
    for (const b of filters.children)
      b.setAttribute('aria-pressed', String(b.textContent === filter));
    cards.forEach((card) => {
      const d = definitions.get(+card.dataset.moduleId);
      card.hidden = filter !== 'All' && filter !== d.category;
      card.querySelector('.owned-count').textContent =
        `${owned[d.id] || 0} owned · ${Math.max(0, spare(d.id))} available`;
      const b = card.querySelector('button');
      b.disabled = !docked || !!pending || d.id === 1 || credits < d.price;
      card.querySelector('p').textContent =
        d.description +
        (draft[weapon].emitter === 2 && d.laserDescription ? ' Laser: ' + d.laserDescription : '');
    });
    apply.disabled =
      !!pending || draft.some((w) => w.emitter < 0) || catalog.some((d) => spare(d.id) < 0);
    reset.disabled = !!pending;
    clear.disabled = !!pending || !selected || selected.slot < 0;
    preview.update(draft[weapon], weapon, stats, definitions);
  }
  function changed() {
    state.textContent = 'Draft changed · Apply loadout to save · CLOAK ACTIVE';
    render();
  }
  function remove(slot) {
    if (pending) return;
    setPart(slot, 0);
    selected = null;
    changed();
  }
  function place(source, target) {
    if (pending) return;
    if (
      !accepts(target, source.module) ||
      (source.slot >= 0 && !accepts(source.slot, part(target)))
    ) {
      state.textContent = 'Emitters go in emitter sockets; attachments go in percentage boxes.';
      return;
    }
    if (source.slot < 0 && spare(source.module) <= 0 && part(target) !== source.module) {
      state.textContent = 'No spare copy. Buy another or move an equipped one.';
      return;
    }
    if (source.slot >= 0) {
      const old = part(target);
      setPart(target, source.module);
      setPart(source.slot, old);
    } else setPart(target, source.module);
    selected = null;
    describe(part(target), target);
    changed();
  }
  function choose(source) {
    if (!opened || pending) return;
    if (dragController.consumeClick()) return;
    describe(source.module, source.slot);
    if (source.slot < 0) {
      if (spare(source.module) <= 0) {
        state.textContent = 'All copies are equipped, or none are owned. Buy a copy in Shop.';
        return;
      }
      selected = source;
    } else if (selected) place(selected, source.slot);
    else selected = source.module ? source : null;
    render();
  }
  const dragController = createModuleDrag({
    element,
    panel,
    body,
    inventory,
    slots,
    weaponButtons,
    rows,
    accepts,
    part,
    spare,
    place,
    remove,
    describe,
    render,
    choose,
    canInteract: () => opened && !pending,
  });
  const { bind, cancelDrag } = dragController;
  for (let w = 0; w < 4; w++) {
    const section = element('section'),
      head = element('div', '', 'weapon-heading'),
      label = element('strong'),
      toggle = element('button', 'Enabled');
    toggle.type = 'button';
    control(toggle, 'apply');
    toggle.setAttribute('aria-label', `Enable weapon ${w + 1}`);
    toggle.addEventListener('click', () => {
      if (!pending) {
        draft[w].enabled = !draft[w].enabled;
        weapon = w;
        changed();
      }
    });
    head.append(label, toggle);
    section.id = `weapon-chain-${w}`;
    section.setAttribute('role', 'tabpanel');
    const select = element('button', `Weapon ${w + 1}`);
    select.type = 'button';
    select.id = `weapon-select-${w}`;
    select.dataset.weapon = String(w);
    select.setAttribute('role', 'tab');
    select.setAttribute('aria-controls', section.id);
    section.setAttribute('aria-labelledby', select.id);
    select.append(element('span'));
    select.addEventListener('click', () => selectWeapon(w));
    weaponButtons.push(select);
    weaponSelector.append(select);
    const scroll = element('div', '', 'weapon-scroll'),
      row = element('div', '', 'weapon-row');
    scroll.append(row);
    section.append(head, scroll);
    loadout.append(section);
    rows.push({ section, label, toggle, scroll });
    for (let stage = 0; stage < 9; stage++) {
      const i = w * 9 + stage,
        slot = element('button', '', 'weapon-slot');
      slot.id = `weapon-slot-${i}`;
      slot.type = 'button';
      slot.dataset.slot = i;
      slot.setAttribute('aria-describedby', detail.id);
      slot.append(
        element('span', stage === 0 ? 'EMITTER' : stage * 5 + '%', 'slot-index'),
        element('span', '', 'slot-icon'),
        element('span', '', 'slot-name'),
      );
      bind(slot, () => ({ slot: i, module: part(i) }));
      slot.addEventListener('contextmenu', (e) => {
        e.preventDefault();
        cancelDrag();
        remove(i);
      });
      slots.push(slot);
      row.append(slot);
    }
  }
  const lower = element('div', '', 'loadout-lower'),
    ownedPanel = element('section', '', 'owned-panel');
  ownedPanel.append(element('h3', 'OWNED INVENTORY'), inventory);
  lower.append(preview.root, ownedPanel);
  loadout.append(lower);
  function setCatalog(data, levels) {
    catalog = data;
    definitions = new Map(data.map((d) => [d.id, d]));
    unlock = levels || unlock;
    inventory.replaceChildren();
    shopGrid.replaceChildren();
    filters.replaceChildren();
    items.length = 0;
    cards.length = 0;
    for (const category of ['All', 'Emitter', 'Damage', 'Trajectory', 'Elemental', 'Utility']) {
      const b = element('button', category);
      b.type = 'button';
      control(
        b,
        {
          All: 'loadout',
          Emitter: 'strength',
          Damage: 'strength',
          Trajectory: 'locate',
          Elemental: 'mastery',
          Utility: 'repair',
        }[category],
      );
      b.addEventListener('click', () => {
        filter = category;
        render();
      });
      filters.append(b);
    }
    for (const d of catalog) {
      const item = element('button', '', 'module-item');
      item.type = 'button';
      item.dataset.moduleId = d.id;
      item.style.setProperty('--part', color(d.id));
      item.append(
        element('span', glyph(d.id), 'module-glyph'),
        element('span', d.name, 'module-name'),
        element('span', '', 'module-count'),
      );
      bind(item, () => ({ slot: -1, module: d.id }));
      items.push(item);
      inventory.append(item);
      const card = element('article', '', 'shop-card');
      card.dataset.moduleId = d.id;
      const animation = element('canvas');
      animation.width = 280;
      animation.height = 60;
      animation.setAttribute('aria-label', d.name + ' effect preview');
      const purchase = element(
        'button',
        d.id === 1 ? 'Starter equipment' : `Buy · ${d.price} credits`,
      );
      purchase.type = 'button';
      control(purchase, 'shop', 'primary');
      purchase.setAttribute('aria-label', `Buy ${d.name}`);
      purchase.addEventListener('click', () => {
        if (pending) return;
        pending = buy(d.id) || 0;
        pendingKind = 'buy';
        state.textContent = pending ? 'Buying…' : 'Not connected.';
        render();
      });
      card.append(
        element('strong', d.name),
        animation,
        element('p', d.description),
        element('span', '', 'owned-count'),
        purchase,
      );
      cards.push(card);
      shopGrid.append(card);
    }
    render();
  }
  function animate(t) {
    if (!opened) return;
    if (tab === 'loadout') preview.draw(t / 1000);
    else
      cards.forEach((card) => {
        if (!card.hidden) {
          const id = +card.dataset.moduleId;
          drawChain(
            card.querySelector('canvas'),
            {
              emitter: id <= 4 ? id : draft[weapon].emitter || 2,
              attachments: id <= 4 ? [] : [id],
            },
            t / 1000,
            stats,
            definitions,
            true,
          );
        }
      });
    animation = requestAnimationFrame(animate);
  }
  function restore() {
    cancelDrag();
    selected = null;
    draft = clone(saved);
    describe(draft[weapon].emitter, weapon * 9);
    render();
  }
  function setOpen(value) {
    opened = value && available;
    panel.hidden = !opened;
    info.inert = opened;
    opener.setAttribute('aria-expanded', String(opened));
    cancelDrag();
    dragController.resetClick();
    cancelAnimationFrame(animation);
    if (opened) {
      restore();
      state.textContent = 'CLOAK ACTIVE · Invincible and hidden from aliens';
      animation = requestAnimationFrame(animate);
    }
    onToggle(opened);
    if (opened) title.focus({ preventScroll: true });
  }
  opener.addEventListener('click', () => setOpen(true));
  close.addEventListener('click', () => setOpen(false));
  reset.addEventListener('click', restore);
  clear.addEventListener('click', () => {
    if (selected?.slot >= 0) remove(selected.slot);
  });
  apply.addEventListener('click', () => {
    if (pending) return;
    pending = equip(clone(draft)) || 0;
    pendingKind = 'equip';
    state.textContent = pending ? 'Saving loadout…' : 'Not connected.';
    render();
  });
  switchTab('loadout');
  return {
    get open() {
      return opened;
    },
    setOpen,
    setCatalog,
    openShop() {
      setOpen(true);
      switchTab('shop');
    },
    update(self, connected, profile) {
      available = connected && !!self && self.hull > 0;
      opener.hidden = !connected;
      opener.disabled = !available;
      docked = !!self?.docked;
      level = self?.level || 1;
      stats = self?.weaponStats || [];
      unlocked = self?.unlockedWeapons || [true, false, false, false];
      if (profile) {
        credits = profile.credits;
        owned = profile.owned;
        saved = clone(profile.weapons);
      }
      const life = self ? `${self.id}:${self.life}` : null;
      if (life !== aliveLife) {
        aliveLife = life;
        pending = 0;
        restore();
      }
      if (pending && profile?.sequence >= pending && profile.result !== 'pending') {
        const was = pendingKind;
        pending = 0;
        state.textContent =
          profile.result === 'ok'
            ? (was === 'buy' ? 'Module purchased ✓' : 'Loadout equipped ✓') + ' · CLOAK ACTIVE'
            : `Not applied: ${profile.result.replaceAll('_', ' ')}.`;
      }
      render();
      if (!available && opened) setOpen(false);
    },
    keydown(e) {
      if (!opened) return false;
      if (e.code === 'Escape' || e.code === 'KeyI') {
        e.preventDefault();
        if (!e.repeat) setOpen(false);
        return true;
      }
      if (['Delete', 'Backspace'].includes(e.code)) {
        const slot = document.activeElement?.dataset.slot;
        e.preventDefault();
        if (slot !== undefined) remove(+slot);
      }
      if (
        ['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'].includes(e.code) &&
        document.activeElement?.dataset.slot !== undefined
      ) {
        e.preventDefault();
        const delta =
          e.code === 'ArrowLeft' ? -1 : e.code === 'ArrowRight' ? 1 : e.code === 'ArrowUp' ? -9 : 9;
        const current = +document.activeElement.dataset.slot;
        const next = Math.max(
          delta === 1 || delta === -1 ? weapon * 9 : 0,
          Math.min(delta === 1 || delta === -1 ? weapon * 9 + 8 : 35, current + delta),
        );
        if (Math.floor(next / 9) !== weapon) selectWeapon(Math.floor(next / 9));
        slots[next].focus({ preventScroll: true });
        slots[next].scrollIntoView({ block: 'nearest', inline: 'nearest' });
      }
      if (e.key === 'Tab') {
        const controls = [...panel.querySelectorAll('button:not(:disabled)')].filter(
          (n) => !n.closest('[hidden]') && n.tabIndex >= 0,
        );
        if (
          e.shiftKey &&
          (document.activeElement === controls[0] || !controls.includes(document.activeElement))
        ) {
          e.preventDefault();
          controls.at(-1).focus();
        } else if (
          !e.shiftKey &&
          (document.activeElement === controls.at(-1) || !controls.includes(document.activeElement))
        ) {
          e.preventDefault();
          controls[0].focus();
        }
      }
      if (e.code === 'Space' && e.repeat) e.preventDefault();
      return true;
    },
  };
}
