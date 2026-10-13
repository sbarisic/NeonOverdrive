// Stable IDs keep edits independent of inventory ordering and arriving loot.
export class EquipmentDraft {
  constructor() {
    this.saved = new Map();
    this.gear = new Map();
    this.edits = new Map();
    this.pages = new Set();
    this.revision = -1;
    this.bays = [];
    this.ship = [];
    this.loose = Array(128).fill(0);
  }
  receive(page) {
    if (page.revision !== this.revision) {
      this.pages.clear();
      this.saved.clear();
      this.gear.clear();
      this.revision = page.revision;
    }
    this.pages.add(page.page);
    this.pageCount = page.pages;
    for (const w of page.weapons) this.saved.set(w.id, structuredClone(w));
    for (const g of page.gear) this.gear.set(g.id, g);
    this.savedBays = structuredClone(page.bays);
    this.savedShip = [...page.ship];
    this.loose = [...page.loose];
    if (!this.dirty) this.reset();
    else if (
      this.complete &&
      this.baseRevision !== this.revision &&
      JSON.stringify(this.savedBays) === this.baselineBays &&
      JSON.stringify(this.savedShip) === this.baselineShip &&
      [...this.edits.keys()].every(
        (id) => JSON.stringify(this.saved.get(id)) === JSON.stringify(this.baseline.get(id)),
      ) &&
      this.loose.every((_, id) => id < 5 || this.spare(id) >= 0)
    )
      this.baseRevision = this.revision;
  }
  reset() {
    this.edits.clear();
    this.bays = structuredClone(this.savedBays || []);
    this.ship = [...(this.savedShip || [])];
    this.dirty = false;
    this.baseRevision = this.revision;
  }
  weapon(id) {
    return this.edits.get(id) || this.saved.get(id);
  }
  mark() {
    if (!this.dirty) {
      this.baseRevision = this.revision;
      this.baseline = new Map(this.saved);
      this.baselineBays = JSON.stringify(this.savedBays);
      this.baselineShip = JSON.stringify(this.savedShip);
    }
    this.dirty = true;
  }
  part(id, stage, module) {
    if (!this.edits.has(id) && this.edits.size >= 32) return false;
    const w = this.weapon(id);
    if (!w || (module && this.spare(module) <= 0 && w.attachments[stage] !== module)) return false;
    this.mark();
    const copy = structuredClone(w);
    copy.attachments[stage] = module;
    this.edits.set(id, copy);
    return true;
  }
  spare(module) {
    let n = this.loose[module] || 0;
    for (const [id, w] of this.edits) {
      n += (this.saved.get(id)?.attachments || []).filter((m) => m === module).length;
      n -= w.attachments.filter((m) => m === module).length;
    }
    return n;
  }
  command() {
    return {
      action: 'apply',
      revision: this.baseRevision,
      edits: [...this.edits.values()].map((w) => ({ id: w.id, attachments: w.attachments })),
      bays: this.bays,
      ship: this.ship,
    };
  }
  get complete() {
    return this.pages.size === this.pageCount;
  }
}
