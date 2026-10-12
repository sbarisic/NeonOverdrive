import { color } from './weapon-art.js';

// A topology view, not a trajectory simulation. Sample complete paths uniformly
// before grouping ancestors so dense chains stay symmetric and bounded.
export function buildChainModel(weapon, stats, definitions) {
  const effective = stats.find((s) => s.emitter === weapon.emitter),
    baseDamage = effective?.damage ?? (weapon.emitter === 2 ? 4 : weapon.emitter >= 3 ? 2 : 1),
    stages = [];
  let damage = baseDamage,
    branches = 1,
    spread = 1,
    terminal = false,
    tint = color(weapon.emitter);
  for (const [index, id] of weapon.attachments.entries()) {
    const definition = definitions.get(id),
      reachable = !terminal;
    let split = 1,
      angle = null;
    if (reachable) {
      if (id === 5) damage *= definition?.value ?? 2;
      if (id === 9) spread *= 0.5;
      if ([11, 12, 13].includes(id)) {
        split = definition?.value ?? (id === 11 ? 2 : id === 12 ? 3 : 6);
        damage /= split;
        branches *= split;
        angle = (definition?.radius ?? (id === 11 ? 0.22 : id === 12 ? 0.3 : 0.6)) * spread;
      }
      if ([17, 18, 19, 20, 21, 25].includes(id)) tint = color(id);
      if (id === 26) terminal = true;
    }
    stages.push({
      id,
      index,
      name: definition?.name ?? 'Empty',
      reachable,
      damage,
      branches,
      split,
      angle,
      color: tint,
      terminal: reachable && id === 26,
    });
  }
  const active = stages.filter((s) => s.reachable),
    pathCount = Math.min(branches, 64),
    paths = Array.from({ length: pathCount }, (_, i) =>
      pathCount === 1 ? 0 : Math.round((i * (branches - 1)) / (pathCount - 1)),
    ),
    levels = [],
    links = [];
  const cursors = paths.map((index, rank) => ({ index, rank, key: '', parent: '' }));
  function level() {
    const groups = new Map();
    for (const cursor of cursors) {
      if (!groups.has(cursor.key))
        groups.set(cursor.key, { key: cursor.key, parent: cursor.parent, sum: 0, count: 0 });
      const node = groups.get(cursor.key);
      node.sum += pathCount === 1 ? 0.5 : cursor.rank / (pathCount - 1);
      node.count++;
    }
    const nodes = [...groups.values()].map((g) => ({ ...g, y: g.sum / g.count }));
    levels.push(nodes);
    return nodes;
  }
  level(); // Emitter.
  let remaining = branches,
    previousDamage = baseDamage,
    previousColor = color(weapon.emitter);
  function connect() {
    const parents = new Map(levels.at(-1).map((node) => [node.key, node]));
    for (const node of level())
      links.push({
        from: levels.length - 2,
        y1: parents.get(node.parent).y,
        y2: node.y,
        power: previousDamage / baseDamage,
        color: previousColor,
      });
  }
  for (const stage of active) {
    connect(); // Arrival at the checkpoint precedes its effect.
    if (stage.terminal) break;
    remaining /= stage.split;
    for (const cursor of cursors) {
      cursor.parent = cursor.key;
      if (stage.split > 1) cursor.key += '.' + (Math.floor(cursor.index / remaining) % stage.split);
    }
    previousDamage = stage.damage;
    previousColor = stage.color;
  }
  if (!terminal) connect();
  return {
    stages,
    levels,
    links,
    branches,
    pathCount,
    damage,
    baseDamage,
    terminal,
    emitter: definitions.get(weapon.emitter)?.name ?? 'Emitter',
    range: effective?.range ?? 1300,
    rate: 30 / (effective?.interval ?? 5),
    unit: weapon.emitter === 2 ? 'DPS' : 'damage',
    laser: weapon.emitter === 2,
  };
}
