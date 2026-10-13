export function expandCombat(frame) {
  if (Array.isArray(frame.shots?.[0]))
    frame.shots = frame.shots.map((s) => ({
      id: s[0],
      owner: s[1],
      x: s[2],
      y: s[3],
      dx: s[4],
      dy: s[5],
      born: s[6],
      kind: s[7],
      effects: s[8],
      modular: !!s[9],
      parent: s[10],
      birthX: s[11],
      birthY: s[12],
      weapon: s[13],
      stage: s[14],
      power: s[15],
      ancestors: s[16],
    }));
  if (Array.isArray(frame.beams?.[0]))
    frame.beams = frame.beams.map((b) => ({
      owner: b[0],
      segment: b[1],
      x: b[2],
      y: b[3],
      endX: b[4],
      endY: b[5],
      effects: b[6],
      weapon: b[7],
      stage: b[8],
      power: b[9],
    }));
  return frame;
}
// Compact JS -> WASM presentation handoff. The server's JSON remains authoritative.
export function presentSnapshot(host, f) {
  if (!host.ArenaSnapshotPacked) {
    host.ArenaSnapshot(JSON.stringify(f));
    return;
  }
  const a = [f.v, f.tick],
    text = [];
  const label = (value) => {
    text.push(value || '');
    return text.length - 1;
  };
  const list = (items, write) => {
    a.push(items?.length || 0);
    for (const item of items || []) write(item);
  };
  list(f.players, (p) =>
    a.push(
      p.id,
      label(p.name),
      p.x,
      p.y,
      p.aim,
      p.hull,
      p.shield,
      p.ack,
      +p.protected,
      +p.connected,
      p.velocityX,
      p.experience,
      p.level,
      p.velocityY,
      p.acceleration,
      p.steering,
      p.life,
      p.skillPoints,
      p.maxShield,
      label(p.weapon),
      p.maxHull,
      p.moveSpeed,
      +p.menu,
      +p.editing,
      p.docked,
      p.detectionRadius ?? 1200,
      p.surveyRadius ?? 1200,
      p.heading ?? 0,
      p.throttle ?? 0,
      p.playthrough ?? 1,
      p.chassis ?? 0,
      p.turnRate ?? (Math.PI * 2) / 3,
      p.activeBays ?? 1,
      label(p.cruiseState || 'ready'),
      p.cruiseTicks || 0,
      +!!p.braking,
      p.cruiseMultiplier ?? 1,
    ),
  );
  list(f.shots, (s) => {
    a.push(
      s.id,
      s.owner,
      s.x,
      s.y,
      s.dx,
      s.dy,
      s.born,
      s.kind,
      s.effects,
      +s.modular,
      s.parent,
      s.birthX,
      s.birthY,
      s.weapon,
      s.stage,
      s.power ?? 1,
    );
    list(s.ancestors, (id) => a.push(id));
  });
  const kinds = [
    'rammer',
    'gunner',
    'interceptor',
    'sniper',
    'tender',
    'installation',
    'turret',
    'rover',
    'relief',
    'contact',
    'salvage-core',
    'salvage-turret',
    'repair-bot',
  ];
  list(f.enemies, (e) =>
    a.push(
      e.id,
      kinds.indexOf(e.kind),
      e.x,
      e.y,
      e.aim,
      e.hull,
      +e.telegraph,
      e.effects,
      e.home,
      label(e.state),
      e.level ?? 1,
      e.radius ?? 20,
      e.maxHull ?? 3,
      label(e.faction || 'hostile'),
      e.shield || 0,
      e.maxShield || 0,
      e.surfacePlanet || 0,
      e.chassis || 0,
      label(e.role || 'combat'),
    ),
  );
  list(f.energy, (e) => a.push(e.id, e.x, e.y, e.value, e.owner || 0));
  list(f.credits, (e) => a.push(e.id, e.x, e.y, e.value, e.owner || 0));
  list(f.moduleDrops, (e) =>
    a.push(e.id, e.x, e.y, e.module, e.owner, label(e.kind), e.tier || 0, e.gearKind ?? -1),
  );
  list(f.beams, (b) =>
    a.push(
      b.owner,
      b.segment,
      b.x,
      b.y,
      b.endX,
      b.endY,
      b.effects,
      b.weapon,
      b.stage,
      b.power ?? 1,
    ),
  );
  list(f.mines, (m) => a.push(m.id, m.owner, m.x, m.y, +m.armed, m.power ?? 1));
  list(f.explosions, (e) => a.push(e.id, e.x, e.y, e.radius, e.born));
  list(f.sector?.locations, (l) => a.push(l.id, l.hull, label(l.state), l.launchTicks));
  list(f.social?.friends, (n) => a.push(label(n)));
  a.push(f.workshop?.credits ?? 0);
  list(f.combatEvents, (e) =>
    a.push(e.id, e.shot, e.kind, e.x, e.y, e.born, e.size, e.dx || 0, e.dy || 0),
  );
  a.push(f.sector?.pilot?.interactionTarget || 0, f.sector?.pilot?.interaction || 0);
  const rescue = f.sector?.rescue;
  a.push(+!!rescue);
  if (rescue)
    a.push(
      rescue.id,
      rescue.ship,
      label(rescue.state),
      rescue.x,
      rescue.y,
      rescue.hull,
      rescue.maxHull,
      rescue.shield,
      rescue.maxShield,
      rescue.remaining,
      rescue.level,
    );
  const services = f.sector?.services;
  a.push(+!!services);
  if (services) {
    list(services.contacts, (c) => a.push(c.id, label(c.name), label(c.role), c.x, c.y, c.hull));
    list(services.outposts, (o) => {
      a.push(o.id, label(o.name), o.x, o.y, +o.active);
      list(o.sockets, (s) => a.push(s.slot, s.x, s.y, s.hull, s.maxHull, s.cost, +s.pending));
    });
    a.push(+!!services.raid);
    if (services.raid)
      a.push(services.raid.site, label(services.raid.state), services.raid.remaining);
    a.push(services.hailing);
    list(services.claimedCaches, (id) => a.push(id));
    const job = services.assignment;
    a.push(label(job.kind), job.progress);
    list(job.targets, (id) => a.push(id));
    a.push(+job.ready, job.credits, job.experience, job.cooldown);
    list(services.knownCaches, (id) => a.push(id));
  }
  const world = f.sector?.world;
  list(world?.territory, (t) => a.push(t.id, label(t.owner), t.capture, +t.contested));
  list(world?.cargo, (c) => a.push(c.id, label(c.kind), c.x, c.y, c.credits, c.towOwner));
  const race = world?.race;
  a.push(+!!race);
  if (race) {
    a.push(label(race.category), label(race.state), race.checkpoint, race.seconds, race.countdown);
    list(race.course, (n) => a.push(n));
    list(race.ghost, (n) => a.push(n));
  }
  host.ArenaSnapshotPacked(a, text);
}
