import { glyph, color } from './weapon-art.js';
import { drawChain } from './weapon-preview.js';
import { createChainPreview } from './weapon-chain-view.js';
import { loadoutStyles, shopStyles, compactStyles } from './weapons.styles.js';
import { equipmentStyles } from './equipment.styles.js';
import { EquipmentDraft } from './equipment-draft.js';
import { control } from './interface.js';

export function createWeaponBench(element, info, send, buy, onToggle) {
  document.head.append(
    element('style', loadoutStyles + shopStyles + compactStyles + equipmentStyles),
  );
  const panel = element('section', '', 'arena-shell');
  panel.id = 'weapon-bench';
  panel.hidden = true;
  panel.setAttribute('role', 'dialog');
  panel.setAttribute('aria-modal', 'true');
  panel.setAttribute('aria-labelledby', 'weapon-title');
  const title = element('h2', 'MODULAR ARSENAL');
  title.id = 'weapon-title';
  title.tabIndex = -1;
  const wallet = element('span', '', 'wallet'),
    levelLabel = element('span', '', 'forge-level'),
    heading = element('div', '', 'forge-heading');
  heading.append(title, wallet, levelLabel);
  const tabs = element('div', '', 'workshop-tabs'),
    body = element('div', '', 'forge-body'),
    shop = element('section'),
    loadout = element('section');
  shop.id = 'workshop-shop';
  loadout.id = 'workshop-loadout';
  const button = (label, icon, click) => {
    const b = element('button', label);
    b.type = 'button';
    if (icon) control(b, icon);
    b.addEventListener('click', click);
    return b;
  };
  const shopTab = button('Shop', 'shop', () => switchTab('shop')),
    loadoutTab = button('Loadout', 'loadout', () => switchTab('loadout'));
  tabs.append(shopTab, loadoutTab);
  tabs.setAttribute('role', 'tablist');
  shopTab.setAttribute('role', 'tab');
  loadoutTab.setAttribute('role', 'tab');
  const selectors = element('div', '', 'weapon-selector'),
    head = element('div', '', 'weapon-heading'),
    rowLabel = element('strong'),
    toggle = button('Enabled', 'apply', () => {
      if (!canEdit() || level < unlock[bay]) return;
      model.mark();
      model.bays[bay].enabled = !model.bays[bay].enabled;
      render();
    });
  head.append(rowLabel, toggle);
  const chain = element('div', '', 'equipment-chain');
  chain.id = 'weapon-chain-0';
  const slots = Array.from({ length: 9 }, (_, i) => {
    const b = button('', null, () => place(i));
    b.className = 'weapon-slot';
    b.dataset.slot = i;
    b.id = `weapon-slot-${i}`;
    b.append(
      element('span', i ? 'MOD ' + i : 'WEAPON', 'slot-index'),
      element('span', '', 'slot-icon'),
      element('span', '', 'slot-name'),
    );
    bindDrag(
      b,
      () =>
        i
          ? { type: 'part', id: editingId(), stage: i - 1, module: current()?.attachments[i - 1] }
          : { type: 'weapon', id: editingId() },
      () => place(i),
    );
    b.addEventListener('contextmenu', (e) => {
      e.preventDefault();
      unequipSlot(i);
    });
    chain.append(b);
    return b;
  });
  const middle = element('div', '', 'equipment-middle'),
    shipPanel = element('section', '', 'ship-panel');
  shipPanel.setAttribute('aria-label', 'Ship attachments');
  const shipArt = element('div', '', 'ship-silhouette');
  shipArt.innerHTML =
    '<svg viewBox="0 0 100 180" aria-hidden="true"><path d="M50 8 68 68 88 115 82 148 65 135 60 166 40 166 35 135 18 148 12 115 32 68Z" fill="#143240" stroke="#82dce1" stroke-width="2"/><path d="M50 34 61 82 50 103 39 82Z" fill="#6de2e5"/><path d="M36 151 34 176M64 151 66 176" stroke="#eccf83" stroke-width="4"/></svg>';
  shipPanel.append(shipArt);
  const slotNames = ['Shield', 'Engine', 'Radar', 'Utility', 'Hull', 'Reactor'],
    shipIcons = ['◇', '»', '◎', '⌁', '⬡', 'ϟ'];
  const shipButtons = slotNames.map((name, i) => {
    const b = button('', null, () => placeGear(i));
    b.className = 'ship-socket';
    b.dataset.shipSlot = i;
    b.dataset.gearCategory = i;
    bindDrag(
      b,
      () => ({ type: 'gear', id: model.ship[i] }),
      () => placeGear(i),
    );
    b.addEventListener('contextmenu', (e) => {
      e.preventDefault();
      if (canEdit()) {
        model.mark();
        model.ship[i] = null;
        render();
      }
    });
    shipPanel.append(b);
    return b;
  });
  const preview = createChainPreview(element, (i) => slots[i + 1].focus());
  const emptyPreview = element('section', 'Select a weapon for this bay.', 'empty-preview');
  middle.append(shipPanel, preview.root, emptyPreview);
  const inventoryToolbar = element('div', '', 'inventory-toolbar');
  inventoryToolbar.append(element('strong', 'INVENTORY'));
  const grid = element('div', '', 'inventory-grid'),
    spacer = element('div'),
    windowRows = element('div', '', 'inventory-window');
  grid.setAttribute('aria-label', 'Owned inventory');
  grid.append(spacer, windowRows);
  const detail = element('div', '', 'bench-detail'),
    detailName = element('strong', 'Equipment'),
    detailText = element(
      'p',
      'Select an item, then a compatible socket. Right-click a socket to unequip.',
    );
  detail.append(detailName, detailText);
  const state = element('p', '', 'bench-status');
  state.setAttribute('role', 'status');
  const apply = button('Apply loadout', 'apply', () => {
      if (!canEdit() || !model.dirty) return;
      pending = send(model.command()) || 0;
      pendingKind = 'apply';
      render();
    }),
    reset = button('Reset draft', 'reset', () => {
      model.reset();
      inspected = null;
      selected = null;
      render();
    }),
    clear = button('Unequip selected', 'clear', removeSelected),
    sell = button('Sell selected', 'shop', sellSelected),
    close = button('Return to arena [I / Esc]', 'exit', () => setOpen(false));
  const actions = element('div', '', 'bench-actions');
  actions.append(apply, reset, clear, sell, close);
  const full = button('Full build', 'loadout', () => {
    fullBuild = !fullBuild;
    render();
  });
  head.append(full);
  const help = element(
    'p',
    'Select a weapon or system, then a socket. Installed mods stay with each weapon. Level unlocks activate them.',
    'forge-help',
  );
  loadout.append(selectors, head, chain, middle, inventoryToolbar, grid);
  body.append(shop, loadout);
  panel.append(heading, tabs, help, body, detail, state, actions);
  document.body.append(panel);
  const model = new EquipmentDraft();
  let opened = false,
    available = false,
    docked = false,
    trader = false,
    level = 1,
    stats = [],
    credits = 0,
    bay = 0,
    inspected = null,
    selected = null,
    pending = 0,
    pendingKind = '',
    life = null,
    tab = 'loadout',
    animation = 0,
    fullBuild = false,
    filter = 'All',
    catalog = [],
    gearCatalog = [],
    definitions = new Map(),
    unlock = [1, 8, 16, 24],
    requestedPage = null,
    inventoryKey = '',
    drag = null;
  const bayButtons = Array.from({ length: 4 }, (_, i) => {
    const b = button(`Weapon ${i + 1}`, null, () => {
      bay = i;
      inspected = null;
      render();
    });
    b.id = `weapon-select-${i}`;
    b.dataset.weapon = i;
    selectors.append(b);
    return b;
  });
  for (const name of ['All', 'Weapons', 'Mods', 'Ship gear'])
    inventoryToolbar.append(
      button(name, 'loadout', () => {
        filter = name;
        grid.scrollTop = 0;
        inventoryKey = '';
        renderInventory();
      }),
    );
  const canEdit = () => !pending && model.complete;
  const editingId = () => inspected || model.bays[bay]?.weapon;
  const current = () => model.weapon(editingId());
  const moduleName = (id) => definitions.get(id)?.name || 'Empty';
  const gearName = (g) =>
    g ? `${gearCatalog[g.kind]?.name || 'Ship equipment'} · T${g.tier}` : 'Empty';
  const weaponName = (w) =>
    w ? `${moduleName(w.emitter)}${w.tier ? ' · T' + w.tier : ''}` : 'Empty weapon bay';
  function describe(item) {
    if (!item) return;
    delete detailName.parentElement.dataset.gearCategory;
    if (item.type === 'weapon') {
      const w = model.weapon(item.id);
      detailName.textContent = weaponName(w);
      detailText.textContent = w
        ? `${w.attachments.filter(Boolean).length}/8 mods installed · Select a weapon bay socket to equip. Double-click to inspect stored build.`
        : '';
    } else if (item.type === 'gear') {
      const g = model.gear.get(item.id);
      detailName.textContent = gearName(g);
      detailName.parentElement.dataset.gearCategory = g.kind === 12 ? 1 : g.kind >> 1;
      const d = gearCatalog[g?.kind];
      const old = d ? model.gear.get(model.ship[d.slot]) : null;
      detailText.textContent =
        (d?.description || '') + ` · Tier ${g?.tier || 1} · Equipped: ${gearName(old)}`;
    } else {
      detailName.textContent = moduleName(item.module);
      detailText.textContent = definitions.get(item.module)?.description || 'Empty checkpoint';
    }
  }
  function bindDrag(b, source, drop) {
    b.draggable = true;
    b.addEventListener('dragstart', (e) => {
      if (!canEdit()) {
        e.preventDefault();
        return;
      }
      drag = source();
      selected = drag;
      e.dataTransfer.setData('text/plain', 'equipment');
    });
    b.addEventListener('dragend', () => {
      drag = null;
      panel
        .querySelectorAll('.equipment-drop-target')
        .forEach((n) => n.classList.remove('equipment-drop-target'));
    });
    b.addEventListener('dragover', (e) => {
      e.preventDefault();
      b.classList.add('equipment-drop-target');
    });
    b.addEventListener('dragleave', () => b.classList.remove('equipment-drop-target'));
    b.addEventListener('drop', (e) => {
      b.classList.remove('equipment-drop-target');
      e.preventDefault();
      selected = drag;
      drop();
      drag = null;
    });
  }
  grid.addEventListener('dragover', (e) => e.preventDefault());
  grid.addEventListener('drop', (e) => {
    if (e.target.closest('.inventory-cell')) return;
    e.preventDefault();
    selected = drag;
    removeSelected();
    drag = null;
  });
  function place(i) {
    if (!canEdit()) return;
    if (i === 0) {
      if (selected?.type === 'weapon' && level >= unlock[bay]) {
        model.mark();
        const id = selected.id;
        for (const [index, b] of model.bays.entries())
          if (b.weapon === id) {
            if (level < unlock[index]) {
              state.textContent = 'That weapon is reserved in a locked bay.';
              return;
            }
            b.weapon = null;
          }
        model.bays[bay].weapon = id;
        inspected = null;
        selected = null;
        render();
        return;
      }
      selected = current() ? { type: 'weapon', id: editingId() } : null;
    } else if (selected?.module >= 5 && current()) {
      if (selected.type === 'part') {
        const source = model.weapon(selected.id);
        const old = current().attachments[i - 1];
        if (source && !(selected.id === editingId() && selected.stage === i - 1)) {
          model.part(selected.id, selected.stage, 0);
          const ok = model.part(editingId(), i - 1, selected.module);
          if (ok && old) model.part(selected.id, selected.stage, old);
        }
      } else model.part(editingId(), i - 1, selected.module);
      selected = null;
    } else
      selected = {
        type: 'part',
        id: editingId(),
        stage: i - 1,
        module: current()?.attachments[i - 1],
      };
    describe(selected);
    render();
  }
  function placeGear(i) {
    if (!canEdit()) return;
    if (
      selected?.type === 'gear' &&
      (model.gear.get(selected.id)?.kind === 12 ? 1 : model.gear.get(selected.id)?.kind >> 1) === i
    ) {
      model.mark();
      model.ship[i] = selected.id;
      selected = null;
    } else selected = model.ship[i] ? { type: 'gear', id: model.ship[i] } : null;
    describe(selected);
    render();
  }
  function unequipSlot(i) {
    if (!canEdit()) return;
    if (i === 0) {
      if (level < unlock[bay]) return;
      model.mark();
      model.bays[bay].weapon = null;
      inspected = null;
    } else if (current()) model.part(editingId(), i - 1, 0);
    selected = null;
    render();
  }
  function removeSelected() {
    if (!selected || !canEdit()) return;
    if (selected.type === 'part') model.part(selected.id, selected.stage, 0);
    if (selected.type === 'weapon') {
      const index = model.bays.findIndex((b) => b.weapon === selected.id);
      if (index >= 0 && level >= unlock[index]) {
        model.mark();
        model.bays[index].weapon = null;
      }
    }
    if (selected.type === 'gear') {
      const i = model.ship.indexOf(selected.id);
      if (i >= 0) {
        model.mark();
        model.ship[i] = null;
      }
    }
    selected = null;
    render();
  }
  function sellSelected() {
    if (!selected || pending || model.dirty || !docked) return;
    const command = { action: 'sell', revision: model.revision };
    if (selected.type === 'weapon') {
      const w = model.weapon(selected.id);
      if (!w || model.bays.some((b) => b.weapon === w.id)) return;
      const price = [w.emitter, ...w.attachments].reduce(
        (n, id) => n + Math.floor((definitions.get(id)?.price || 0) / 4),
        0,
      );
      if (
        !confirm(
          `Sell ${weaponName(w)} for ${price} credits?\nIncluded mods: ${w.attachments.filter(Boolean).map(moduleName).join(', ') || 'none'}`,
        )
      )
        return;
      command.item = w.id;
    } else if (selected.type === 'gear') {
      if (model.ship.includes(selected.id)) return;
      command.item = selected.id;
    } else if (selected.type === 'module') {
      command.module = selected.module;
      command.quantity = 1;
    } else return;
    pending = send(command) || 0;
    pendingKind = 'sell';
    render();
  }
  function inventoryItems() {
    const result = [];
    if (filter === 'All' || filter === 'Weapons')
      for (const w of model.saved.values())
        if (!model.bays.some((b) => b.weapon === w.id)) result.push({ type: 'weapon', id: w.id });
    if (filter === 'All' || filter === 'Mods')
      for (let id = 5; id < model.loose.length; id++)
        if (model.spare(id) > 0) result.push({ type: 'module', module: id });
    if (filter === 'All' || filter === 'Ship gear')
      for (const g of model.gear.values())
        if (!model.ship.includes(g.id)) result.push({ type: 'gear', id: g.id });
    return result;
  }
  function renderInventory() {
    if (!opened || tab !== 'loadout') return;
    const items = inventoryItems(),
      cols = Math.max(1, Math.floor(grid.clientWidth / 112)),
      rowHeight = 73,
      start = Math.max(0, Math.floor(grid.scrollTop / rowHeight) - 1),
      count = Math.ceil(grid.clientHeight / rowHeight) + 2;
    const key = JSON.stringify([
      model.revision,
      model.pages.size,
      model.bays,
      model.ship,
      [...model.edits],
      filter,
      start,
      cols,
      count,
      selected,
    ]);
    if (key === inventoryKey) return;
    inventoryKey = key;
    const focused = document.activeElement?.dataset.itemKey;
    spacer.style.height = `${Math.ceil(items.length / cols) * rowHeight + 10}px`;
    windowRows.style.top = `${start * rowHeight}px`;
    windowRows.style.gridTemplateColumns = `repeat(${cols},minmax(0,1fr))`;
    windowRows.replaceChildren();
    for (const [offset, item] of items.slice(start * cols, (start + count) * cols).entries()) {
      const w = item.type === 'weapon' ? model.weapon(item.id) : null,
        g = item.type === 'gear' ? model.gear.get(item.id) : null;
      const b = button('', null, () => {
        selected = item;
        describe(item);
        render();
      });
      b.className = 'inventory-cell module-item';
      if (g) b.dataset.gearCategory = g.kind === 12 ? 1 : g.kind >> 1;
      b.dataset.itemKey = item.id || `mod-${item.module}`;
      b.dataset.index = start * cols + offset;
      if (item.module) b.dataset.moduleId = item.module;
      b.dataset.selected = String(
        selected?.id
          ? selected.id === item.id
          : selected?.module && selected.module === item.module,
      );
      b.append(
        element(
          'b',
          w
            ? glyph(w.emitter)
            : g
              ? shipIcons[g.kind === 12 ? 1 : g.kind >> 1]
              : glyph(item.module),
        ),
        element('span', w ? weaponName(w) : g ? gearName(g) : moduleName(item.module)),
        element(
          'small',
          w
            ? `${w.attachments.filter(Boolean).length}/8 mods`
            : g
              ? slotNames[g.kind === 12 ? 1 : g.kind >> 1]
              : `×${model.spare(item.module)}`,
        ),
      );
      b.title = w ? weaponName(w) : g ? gearName(g) : moduleName(item.module);
      bindDrag(
        b,
        () => item,
        () => {},
      );
      b.addEventListener('dblclick', () => {
        if (w) {
          inspected = w.id;
          selected = null;
          render();
        }
      });
      windowRows.append(b);
    }
    if (focused)
      [...windowRows.children]
        .find((b) => b.dataset.itemKey === focused)
        ?.focus({ preventScroll: true });
  }
  grid.addEventListener('scroll', renderInventory);
  new ResizeObserver(() => {
    inventoryKey = '';
    renderInventory();
  }).observe(grid);
  function render() {
    wallet.textContent = `${credits} CREDITS${docked ? ' · STATION MARKET' : trader ? ' · ROAMING TRADER · Purchases only' : ' · Dock or hail a trader to buy'}`;
    levelLabel.textContent = `LEVEL ${level} · ${unlock.filter((l) => level >= l).length}/4 WEAPON BAYS`;
    chain.id = `weapon-chain-${bay}`;
    const w = current(),
      availableMods = Math.max(0, Math.min(8, level - unlock[bay]));
    bayButtons.forEach((b, i) => {
      b.textContent = `Weapon ${i + 1} · ${level < unlock[i] ? 'L' + unlock[i] : model.bays[i]?.enabled ? 'Enabled' : 'Disabled'}`;
      b.setAttribute('aria-selected', String(i === bay && !inspected));
    });
    rowLabel.textContent = `${level < unlock[bay] ? `LEVEL ${unlock[bay]} · ` : ''}${inspected ? 'STORED BUILD' : `WEAPON ${bay + 1}`} · ${w ? w.attachments.slice(0, availableMods).filter(Boolean).length : 0} ACTIVE / ${w ? w.attachments.filter(Boolean).length : 0} INSTALLED`;
    toggle.textContent = model.bays[bay]?.enabled ? 'Enabled' : 'Disabled';
    toggle.disabled = !canEdit() || level < unlock[bay] || !!inspected;
    full.setAttribute('aria-pressed', String(fullBuild));
    slots.forEach((b, i) => {
      b.id = `weapon-slot-${bay * 9 + i}`;
      const id = i ? w?.attachments[i - 1] : w?.emitter;
      b.querySelector('.slot-icon').textContent = glyph(id || 0);
      b.querySelector('.slot-name').textContent = i
        ? moduleName(id) + (i > availableMods ? ` · L${unlock[bay] + i}` : '')
        : weaponName(w);
      b.dataset.dormant = String(i > availableMods);
      b.dataset.module = id || 0;
      b.style.setProperty('--part', color(id || 0));
      b.disabled = !canEdit() || (i > 0 && !w);
      b.title = i
        ? `${moduleName(id)} · activates at level ${unlock[bay] + i}`
        : 'Select a complete weapon from inventory to replace this build';
      b.setAttribute(
        'aria-label',
        i ? `Mod ${i}: ${moduleName(id)}` : `Weapon socket: ${weaponName(w)}`,
      );
    });
    shipButtons.forEach((b, i) => {
      const g = model.gear.get(model.ship[i]);
      b.replaceChildren(
        element('span', shipIcons[i] + ' ' + slotNames[i]),
        element('small', gearName(g)),
      );
      b.disabled = !canEdit();
      b.title = g ? gearCatalog[g.kind]?.description || '' : `Equip ${slotNames[i]}`;
    });
    const config = {
      emitter: w?.emitter || 0,
      attachments: (w?.attachments || Array(8).fill(0)).map((id, i) =>
        fullBuild || i < availableMods ? id : 0,
      ),
    };
    preview.root.hidden = !w;
    emptyPreview.hidden = !!w;
    preview.update(config, bay, stats, definitions);
    apply.disabled = !!pending || !model.dirty || !model.complete || model.edits.size > 32;
    reset.disabled = !!pending;
    clear.disabled = !selected || !!pending;
    sell.disabled = !selected || !!pending || model.dirty || !docked || !model.complete;
    if (!pending && model.dirty)
      state.textContent =
        model.baseRevision !== model.revision
          ? 'Inventory changed. Reset draft to refresh before applying.'
          : model.edits.size > 32
            ? 'Apply at most 32 edited weapons per draft.'
            : 'Draft changed · Apply to save · CLOAK ACTIVE';
    renderInventory();
  }
  const shopFilters = element('div', '', 'shop-filters'),
    shopGrid = element('div', '', 'shop-grid');
  shop.append(shopFilters, shopGrid);
  let shopFilter = 'All';
  function setCatalog(data, levels, ships) {
    catalog = data || [];
    unlock = levels || unlock;
    gearCatalog = ships || [];
    definitions = new Map(catalog.map((d) => [d.id, d]));
    shopFilters.replaceChildren();
    for (const category of ['All', 'Emitter', 'Damage', 'Trajectory', 'Elemental', 'Utility'])
      shopFilters.append(
        button(category, 'loadout', () => {
          shopFilter = category;
          renderShop();
        }),
      );
    renderShop();
    render();
  }
  function renderShop() {
    shopGrid.replaceChildren();
    for (const d of catalog.filter(
      (d) => (d.level || 1) === 1 && (shopFilter === 'All' || d.category === shopFilter),
    )) {
      const card = element('article', '', 'shop-card');
      card.dataset.moduleId = d.id;
      const canvas = element('canvas');
      canvas.width = 280;
      canvas.height = 60;
      const purchase = button(
        d.id === 1 ? 'Starter equipment' : `Buy · ${d.price} credits`,
        'shop',
        () => {
          if (pending) return;
          pending = buy(d.id) || 0;
          pendingKind = 'buy';
          renderShop();
          render();
        },
      );
      purchase.disabled = (!docked && !trader) || !!pending || d.id === 1 || credits < d.price;
      purchase.setAttribute('aria-label', `Buy ${d.name}`);
      card.append(element('strong', d.name), canvas, element('p', d.description), purchase);
      shopGrid.append(card);
      drawChain(
        canvas,
        { emitter: d.id <= 4 ? d.id : 1, attachments: d.id <= 4 ? [] : [d.id] },
        0,
        stats,
        definitions,
        true,
      );
    }
  }
  function switchTab(value) {
    tab = value;
    shop.hidden = tab !== 'shop';
    loadout.hidden = tab !== 'loadout';
    shopTab.setAttribute('aria-selected', String(tab === 'shop'));
    loadoutTab.setAttribute('aria-selected', String(tab === 'loadout'));
    if (tab === 'shop') renderShop();
    render();
  }
  function animate(t) {
    if (!opened) return;
    if (tab === 'loadout') preview.draw(t / 1000);
    animation = requestAnimationFrame(animate);
  }
  function setOpen(value) {
    opened = value && available;
    panel.hidden = !opened;
    cancelAnimationFrame(animation);
    if (opened) {
      selected = null;
      inspected = null;
      state.textContent = 'CLOAK ACTIVE · Equipment edits are saved with Apply';
      render();
      animation = requestAnimationFrame(animate);
    }
    onToggle(opened);
    if (opened) title.focus({ preventScroll: true });
  }
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
    update(self, connected, profile, services) {
      available = connected && !!self && self.hull > 0;
      docked = !!self?.docked;
      trader = !!services?.contacts?.some((c) => c.id === services.hailing && c.role === 'trader');
      level = self?.level || 1;
      stats = self?.weaponStats || [];
      credits = profile?.credits ?? credits;
      const nextLife = self ? `${self.id}:${self.life}` : null;
      if (life !== nextLife) {
        life = nextLife;
        model.reset();
        model.revision = -1;
        model.pages.clear();
        model.saved.clear();
        model.gear.clear();
        model.pageCount = undefined;
        pending = 0;
        selected = null;
        inspected = null;
        requestedPage = null;
      }
      const page = profile?.inventory;
      if (page) {
        model.receive(page);
        if (requestedPage === page.page) requestedPage = null;
      }
      if (pending && profile?.sequence >= pending && profile.result !== 'pending') {
        const ok = profile.result === 'ok';
        pending = 0;
        state.textContent = ok
          ? 'Saved ✓ · CLOAK ACTIVE'
          : `Not applied: ${profile.result.replaceAll('_', ' ')}. Reset draft to refresh.`;
        if (ok) {
          if (pendingKind === 'apply' || pendingKind === 'sell') model.reset();
          selected = null;
          inspected = null;
        }
        renderShop();
      }
      if (opened && model.pageCount && !model.complete && requestedPage === null) {
        const next = Array.from({ length: model.pageCount }, (_, i) => i).find(
          (i) => !model.pages.has(i),
        );
        requestedPage = next;
        send({ action: 'page', page: next });
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
        e.preventDefault();
        const i = document.activeElement?.dataset.slot;
        if (i !== undefined) unequipSlot(+i);
        else removeSelected();
      }
      const index = document.activeElement?.dataset.index;
      if (
        index !== undefined &&
        ['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'].includes(e.code)
      ) {
        e.preventDefault();
        const cols = Math.max(1, Math.floor(grid.clientWidth / 112));
        const delta =
          e.code === 'ArrowLeft'
            ? -1
            : e.code === 'ArrowRight'
              ? 1
              : e.code === 'ArrowUp'
                ? -cols
                : cols;
        const n = Math.max(0, Math.min(inventoryItems().length - 1, +index + delta));
        grid.scrollTop = Math.floor(n / cols) * 73;
        inventoryKey = '';
        renderInventory();
        windowRows.querySelector(`[data-index="${n}"]`)?.focus();
      }
      if (
        document.activeElement?.dataset.slot !== undefined &&
        ['ArrowLeft', 'ArrowRight'].includes(e.code)
      ) {
        e.preventDefault();
        slots[
          Math.max(
            0,
            Math.min(8, +document.activeElement.dataset.slot + (e.code === 'ArrowRight' ? 1 : -1)),
          )
        ].focus();
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
          controls.at(-1)?.focus();
        } else if (
          !e.shiftKey &&
          (document.activeElement === controls.at(-1) || !controls.includes(document.activeElement))
        ) {
          e.preventDefault();
          controls[0]?.focus();
        }
      }
      return true;
    },
  };
}
