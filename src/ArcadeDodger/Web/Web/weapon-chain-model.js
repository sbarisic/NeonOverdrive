import { color } from './weapon-art.js';

// A topology view, not a trajectory simulation. Sample complete paths uniformly
// before grouping ancestors so dense chains stay symmetric and bounded.
export function buildChainModel(weapon, stats, definitions) {
  const emitter = weapon.emitter % 32,
    emitterLevel = Math.floor(weapon.emitter / 32) + 1,
    effective = stats.find((s) => s.emitter === emitter),
    baseDamage =
      (effective?.damage ?? (emitter === 2 ? 4 : emitter >= 3 ? 2 : 1)) *
      (1 + 0.15 * (emitterLevel - 1)),
    stages = [];
  let damage = baseDamage,
    branches = 1,
    spread = 1,
    terminal = false,
    tint = color(emitter),
    projectile = ['', 'Kinetic', 'Laser', 'Rocket', 'Plasma'][emitter],
    speed = effective?.speed ?? (emitter === 3 ? 240 : emitter === 4 ? 300 : 650),
    speedMultiplier = speed / (emitter === 3 ? 240 : emitter === 4 ? 300 : 650),
    range = effective?.range ?? 1300,
    initialRange = range,
    radiusMultiplier = effective?.radiusMultiplier ?? 1,
    radius = (emitter === 3 ? 85 : emitter === 4 ? 45 : 0) * (effective?.radiusMultiplier ?? 1),
    unit = emitter === 2 ? 'DPS' : 'damage';
  for (const [index, id] of weapon.attachments.entries()) {
    const definition = definitions.get(id),
      kind = id % 32,
      level = Math.floor(id / 32) + 1,
      strength = 1 + 0.25 * (level - 1),
      reachable = !terminal && initialRange * (index + 1) * 0.05 < range;
    let split = 1,
      angle = null;
    if (reachable) {
      if (kind === 6) {
        if (projectile === 'Laser') range *= definition?.value ?? 1.25;
        else {
          speed *= definition?.value ?? 1.25;
          speedMultiplier *= definition?.value ?? 1.25;
        }
      }
      if (kind === 7) range *= definition?.value ?? 1.3;
      if (kind === 5) damage *= definition?.value ?? 2;
      if (kind === 9) spread *= definition?.value ?? 0.5;
      if ([11, 12, 13].includes(kind)) {
        split = definition?.value ?? (kind === 11 ? 2 : kind === 12 ? 3 : 6);
        const distance = initialRange * (index + 1) * 0.05;
        range = distance + (range - distance) * (1 + 0.1 * (level - 1)) * (kind === 13 ? 0.5 : 1);
        damage /= split;
        branches *= split;
        angle = (definition?.radius ?? (kind === 11 ? 0.22 : kind === 12 ? 0.3 : 0.6)) * spread;
      }
      if ([17, 18, 19, 20, 21, 25].includes(kind)) tint = color(kind);
      if (kind === 25 && projectile !== 'Plasma') {
        if (projectile === 'Laser') {
          damage /= 2;
          unit = 'damage';
        }
        const wasBeam = projectile === 'Laser';
        projectile = 'Plasma';
        speed = 300 * speedMultiplier;
        radiusMultiplier *= strength;
        radius = wasBeam ? 45 * radiusMultiplier : Math.max(radius, 45 * radiusMultiplier);
      }
      if (kind === 26) {
        terminal = true;
        projectile = 'Mine';
        speed = 0;
        radius = Math.max(radius, (definition?.radius ?? 70 * strength) * radiusMultiplier);
      }
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
      terminal: reachable && kind === 26,
      projectile,
      speed,
      range,
      radius,
      unit,
      description:
        (projectile === 'Laser' ? definition?.laserDescription : definition?.description) || '',
      conversion: reachable && kind === 25,
      effect:
        kind === 14
          ? `Seeks · ${definition?.value ?? 0.09} steering · ${definition?.radius ?? 600} acquisition`
          : kind === 8
            ? `${definition?.value ?? 2} additional contacts`
            : kind === 15
              ? `${definition?.value ?? 2} reflections`
              : kind === 25
                ? `Plasma · ${Math.round(speed)} u/s · ${Math.round(radius)} splash`
                : kind === 6 || kind === 7 || [11, 12, 13].includes(kind)
                  ? `${Math.round(range)} range · ${projectile === 'Laser' ? 'beam' : Math.round(speed) + ' u/s'}`
                  : kind >= 16
                    ? definition?.description || ''
                    : '',
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
    previousColor = color(emitter),
    previousProjectile = ['', 'Kinetic', 'Laser', 'Rocket', 'Plasma'][emitter];
  function connect() {
    const parents = new Map(levels.at(-1).map((node) => [node.key, node]));
    for (const node of level())
      links.push({
        from: levels.length - 2,
        y1: parents.get(node.parent).y,
        y2: node.y,
        power: previousDamage / baseDamage,
        color: previousColor,
        projectile: previousProjectile,
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
    previousProjectile = stage.projectile;
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
    range: initialRange,
    outputRange: range,
    outputSpeed: speed,
    projectile,
    rate: 30 / (effective?.interval ?? 5),
    unit,
    baseUnit: emitter === 2 ? 'DPS' : 'damage',
    laser: emitter === 2,
  };
}
