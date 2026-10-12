export function expandCombat(frame) {
  if(Array.isArray(frame.shots?.[0]))frame.shots=frame.shots.map(s=>({id:s[0],owner:s[1],x:s[2],y:s[3],dx:s[4],dy:s[5],born:s[6],kind:s[7],effects:s[8],modular:!!s[9],parent:s[10],birthX:s[11],birthY:s[12],weapon:s[13],stage:s[14],power:s[15],ancestors:s[16]}));
  if(Array.isArray(frame.beams?.[0]))frame.beams=frame.beams.map(b=>({owner:b[0],segment:b[1],x:b[2],y:b[3],endX:b[4],endY:b[5],effects:b[6],weapon:b[7],stage:b[8],power:b[9]}));
  return frame;
}
// Compact JS -> WASM presentation handoff. The server's JSON remains authoritative.
export function presentSnapshot(host, f) {
  if (!host.ArenaSnapshotPacked) { host.ArenaSnapshot(JSON.stringify(f)); return; }
  const a=[f.v,f.tick],text=[];
  const label=value=>{text.push(value||'');return text.length-1;};
  const list=(items,write)=>{a.push(items?.length||0);for(const item of items||[])write(item);};
  list(f.players,p=>a.push(p.id,label(p.name),p.x,p.y,p.aim,p.hull,p.shield,p.ack,+p.protected,+p.connected,p.dashCooldown,p.experience,p.level,p.dashTicks,p.dashX,p.dashY,p.life,p.skillPoints,p.maxShield,label(p.weapon),label(p.ability),p.maxHull,p.moveSpeed,p.dashMultiplier,p.dashCooldownMax,+p.editing,p.docked));
  list(f.shots,s=>{a.push(s.id,s.owner,s.x,s.y,s.dx,s.dy,s.born,s.kind,s.effects,+s.modular,s.parent,s.birthX,s.birthY,s.weapon,s.stage,s.power??1);list(s.ancestors,id=>a.push(id));});
  const kinds=['rammer','gunner','interceptor','sniper','tender','installation'];
  list(f.enemies,e=>a.push(e.id,kinds.indexOf(e.kind),e.x,e.y,e.aim,e.hull,+e.telegraph,e.effects,e.home,label(e.state),e.level??1,e.radius??20,e.maxHull??3));
  list(f.energy,e=>a.push(e.id,e.x,e.y,e.value,e.owner||0));
  list(f.credits,e=>a.push(e.id,e.x,e.y,e.value,e.owner||0));
  list(f.moduleDrops,e=>a.push(e.id,e.x,e.y,e.module,e.owner));
  list(f.beams,b=>a.push(b.owner,b.segment,b.x,b.y,b.endX,b.endY,b.effects,b.weapon,b.stage,b.power??1));
  list(f.mines,m=>a.push(m.id,m.owner,m.x,m.y,+m.armed,m.power??1));
  list(f.explosions,e=>a.push(e.id,e.x,e.y,e.radius,e.born));
  list(f.sector?.locations,l=>a.push(l.id,l.hull,label(l.state),l.launchTicks));
  list(f.social?.friends,n=>a.push(label(n)));
  host.ArenaSnapshotPacked(a,text);
}
