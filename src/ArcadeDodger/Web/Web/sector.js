export function createSectorConsole(el, info, send, changed, openShop) {
  let definition, contracts=[], self, state, frame, mode='', waypoint=null, lastDock=0, lastReceipt=0, pending=0;
  const style=el('style');style.textContent=`
  #sector-console{width:min(1000px,calc(100vw - 24px));max-height:calc(100dvh - 32px);border-color:#c5a66c;background:linear-gradient(135deg,#202034,#081320);z-index:9}
  #sector-console h2{color:#edd39b;font-family:Georgia,serif}#sector-console .sector-map{position:relative;width:100%;aspect-ratio:3/2;background:#030811;border:1px solid #475268}
  #sector-console canvas{width:100%;height:100%;display:block}#sector-console .map-location{position:absolute;transform:translate(-50%,-50%);padding:3px 6px;min-height:30px;font-size:11px;max-width:125px;background:#102331ef}
  #sector-console .contract-card{border:1px solid #536071;padding:12px;margin:10px 0;background:#071321}#sector-console .contract-card button{margin:8px 8px 0 0}
  #sector-console .sector-toolbar{display:flex;gap:8px;flex-wrap:wrap}#sector-console .sector-status{color:#f2ce81;min-height:24px}
  #sector-guide{position:fixed;top:var(--sector-guide-top,190px);left:18px;max-width:calc(100vw - 36px);padding:8px 12px;background:#071321d9;border-left:2px solid #dfbd76;color:#f2d699;font:12px monospace;pointer-events:none;z-index:3;white-space:pre-line}
  @media(max-width:600px){#sector-console .map-location{font-size:9px;max-width:85px;padding:2px}#sector-guide{font-size:10px}}
  `;document.head.append(style);
  const opener=el('button','Map [M]'),dockButton=el('button','Dock [E]');opener.type=dockButton.type='button';info.append(opener,dockButton);opener.hidden=dockButton.hidden=true;
  const panel=el('section','','arena-shell arena-panel');panel.id='sector-console';panel.hidden=true;panel.setAttribute('role','dialog');panel.setAttribute('aria-modal','true');panel.setAttribute('aria-label','Sector navigation and station services');
  const heading=el('h2'),summary=el('p'),toolbar=el('div','','sector-toolbar'),map=el('div','','sector-map'),canvas=el('canvas');canvas.width=1200;canvas.height=800;canvas.setAttribute('aria-label','Explored sector map');map.append(canvas);
  const shop=el('button','Module shop'),repair=el('button','Repair hull'),claim=el('button','Claim reward'),board=el('div'),status=el('p','','sector-status'),close=el('button','Return [Esc]');status.setAttribute('role','status');toolbar.append(shop,repair,claim);panel.append(heading,summary,toolbar,map,board,status,close);document.body.append(panel);
  const guide=el('div');guide.id='sector-guide';guide.hidden=true;document.body.append(guide);
  const markers=new Map(),cards=new Map();
  const pingTools=el('div','','sector-toolbar'),share=el('button','Ping friends'),broadcast=el('button','Broadcast to room'),defend=el('button','Defend ally'),received=el('div','','sector-toolbar');
  panel.insertBefore(pingTools,map);const allySelect=el('select');allySelect.setAttribute('aria-label','Ally to defend');pingTools.append(share,broadcast,allySelect,defend);panel.insertBefore(received,board);
  share.onclick=()=>waypoint&&action('ping',waypoint.id);broadcast.onclick=()=>waypoint&&action('broadcast',waypoint.id);
  defend.onclick=()=>{const ally=frame?.players.find(p=>p.id===+allySelect.value);if(ally)action('defend',ally.id);};
  const pingButtons=new Map();
  function setOpen(next){if(!self||self.hull<=0)next='';mode=next;panel.hidden=!mode;render();changed(!!mode);if(mode)close.focus();}
  function action(action,target=0){if(pending)return;pending=send(action,target)||0;status.textContent=pending?'Waiting for station confirmation…':'Disconnected.';render();}
  function nearby(){if(!definition||!self)return null;return definition.locations.filter(l=>['station','wreck','beacon'].includes(l.kind)&&Math.hypot(l.x-self.x,l.y-self.y)<(l.kind==='station'?180:100)).sort((a,b)=>Math.hypot(a.x-self.x,a.y-self.y)-Math.hypot(b.x-self.x,b.y-self.y))[0];}
  function interact(){if(state?.docked){action('undock');return;}const l=nearby();if(l)action(l.kind==='station'?'dock':'interact',l.id);}
  opener.onclick=()=>setOpen(mode==='map'?'':'map');dockButton.onclick=()=>state?.docked?setOpen('station'):interact();shop.onclick=()=>{setOpen('');openShop();};repair.onclick=()=>action('repair');claim.onclick=()=>action('claim');close.onclick=()=>mode==='station'?action('undock'):setOpen('');
  function drawMap(){
    const ctx=canvas.getContext('2d');ctx.fillStyle='#030811';ctx.fillRect(0,0,1200,800);ctx.fillStyle='#142337';for(const cell of state?.cells||[])ctx.fillRect(cell%60*20,Math.floor(cell/60)*20,20,20);
    for(const l of definition?.locations||[]){if(!state?.discovered.includes(l.id))continue;const x=l.x/10,y=l.y/10;
      if(l.kind==='planet'){ctx.fillStyle=({inhabited:'#28556b',volcanic:'#794331',gas:'#565074',shattered:'#535865'})[l.biome];ctx.beginPath();ctx.arc(x,y,l.radius/10,0,Math.PI*2);ctx.fill();}
      if(l.kind==='cluster'){ctx.fillStyle='#7c879c';ctx.fillRect(x-3,y-3,6,6);}
    }
    if(self){ctx.fillStyle='#ffffff';ctx.beginPath();ctx.arc(self.x/10,self.y/10,5,0,Math.PI*2);ctx.fill();}
    if(waypoint){ctx.strokeStyle='#f6d27b';ctx.beginPath();ctx.moveTo(self.x/10,self.y/10);ctx.lineTo(waypoint.x/10,waypoint.y/10);ctx.stroke();}
  }
  function render(){
    if(!self)return;
    const allies=(frame?.players||[]).filter(p=>p.id!==self.id&&p.hull>0&&p.connected&&frame.social?.friends.some(n=>n.toLowerCase()===p.name.toLowerCase()));
    const allyKey=allies.map(p=>p.id+':'+p.name).join(',');if(allySelect.dataset.key!==allyKey){const old=allySelect.value;allySelect.replaceChildren(...allies.map(p=>{const option=el('option',p.name);option.value=p.id;return option;}));if(allies.some(p=>String(p.id)===old))allySelect.value=old;allySelect.dataset.key=allyKey;}defend.disabled=!!pending||!allies.length;
    const docked=state?.docked||0;const location=definition?.locations.find(l=>l.id===docked);const active=contracts.find(c=>c.id===state?.contract);
    heading.textContent=mode==='map'?'SECTOR CHART':location?.name||'STATION';summary.textContent=mode==='map'?`Sector ${definition?.seed} · Select a discovered destination to set a waypoint.`:`Docked · Sanctuary active · ${frame?.workshop?.credits||0} credits`;
    map.hidden=pingTools.hidden=mode!=='map';share.disabled=broadcast.disabled=!!pending||!waypoint||!state?.discovered.includes(waypoint.id);toolbar.hidden=board.hidden=mode!=='station';close.textContent=mode==='station'?'Undock [E / Esc]':'Return [Esc]';
    repair.textContent=`Repair hull · ${state?.repairCost||0} credits`;repair.disabled=!!pending||!state?.repairCost||!docked;claim.hidden=!active;claim.disabled=!!pending||!state?.ready||active?.station!==docked;shop.disabled=!docked;
    for(const [id,{card,button,text}] of cards){const c=contracts.find(c=>c.id===id),destroyed=frame?.sector?.locations?.find(l=>l.id===c.target)?.state==='destroyed';card.hidden=c.station!==docked||(c.kind==='suppress'&&destroyed&&state?.contract!==id);button.disabled=!!pending||!!state?.contract||(state?.completed.includes(id)||state?.coolingContracts?.includes(id));button.textContent=state?.coolingContracts?.includes(id)?'Cooling down':state?.completed.includes(id)?'Completed':state?.contract===id?'Active':'Accept contract';text.textContent=`${c.kind.toUpperCase()} · ${c.credits} credits + ${c.experience} XP${state?.contract===id?' · '+(state.ready?'Return to issuing station':c.kind==='survey'?`${state.progress.length}/3 beacons`:c.kind==='recover'?'Retrieve marked wreck data':c.kind==='interdict'?`${state.progress.length}/3 corridor ships`:'Damage and destroy the marked array'):''}`;}
    for(const [id,b] of markers){const l=definition.locations.find(l=>l.id===id),destroyed=frame?.sector?.locations?.find(s=>s.id===id)?.state==='destroyed';b.hidden=!state?.discovered.includes(id);b.textContent=l.name+(l.kind==='base'?(destroyed?' · Cleared':' · Danger'):l.kind==='corridor'?' · Danger':'');b.setAttribute('aria-pressed',String(waypoint?.id===id));}
    if(mode==='map')drawMap();
    const near=nearby();dockButton.textContent=docked?'Station services':near?`${near.kind==='station'?'Dock':'Interact'} [E]`:'Dock [E]';dockButton.disabled=!docked&&!near;
    let text=docked?'DOCKED · E to undock':near?`E · ${near.kind==='station'?'Dock at':'Interact with'} ${near.name}`:'';
    if(waypoint?.pilot){const ally=frame.players.find(p=>p.id===waypoint.pilot);if(ally){waypoint.x=ally.x;waypoint.y=ally.y;}}
    if(waypoint&&self){const dx=waypoint.x-self.x,dy=waypoint.y-self.y;const compass=['E','SE','S','SW','W','NW','N','NE'][(Math.round(Math.atan2(dy,dx)/(Math.PI/4))+8)%8];text+=`\n${compass} → ${waypoint.name} · ${Math.round(Math.hypot(dx,dy))} units`;}
    if(active)text+=`\n${active.kind.toUpperCase()} · ${state.ready?'Return to station':active.kind==='survey'?`${state.progress.length}/3 beacons`:active.kind==='recover'?'Retrieve wreck data':active.kind==='interdict'?`${state.progress.length}/3 corridor ships`:'Destroy marked array'}`;
    if(state?.interaction>0)text+=`\nScanning ${Math.round(state.interaction*100)}% · Stay still`;
    if(state?.pings?.length)text+='\nShared signal from '+state.pings.at(-1).name+' · M to locate';
    if(state?.lootNotice)text+='\n'+state.lootNotice;
    if(state?.contributions?.length)text+='\nInstallation participation secured';
    for(const [id,b] of pingButtons)if(!state?.pings?.some(p=>p.id===id)){b.remove();pingButtons.delete(id);}
    for(const ping of state?.pings||[])if(!pingButtons.has(ping.id)){
      const l=definition.locations.find(l=>l.id===ping.location),ally=frame.players.find(p=>p.id===ping.pilot),patrol=definition.locations.find(l=>l.id===ping.patrol),b=el('button',`${ping.name}: ${ping.pilot?'Defend '+(ally?.name||'ally')+' · '+(patrol?.name||'patrol'):l?.name||'Shared location'}`);
      b.onclick=()=>{waypoint={id:ping.location,x:ping.x,y:ping.y,pilot:ping.pilot,name:ping.pilot?'Defend '+(ally?.name||'ally'):l?.name||'Shared location'};setOpen('map');};received.append(b);pingButtons.set(ping.id,b);
    }
    guide.textContent=text.trim();guide.hidden=!text.trim();
  }
  return {
    get open(){return !!mode;},setOpen,
    setup(sector,catalog){definition=sector;contracts=catalog||[];markers.forEach(b=>b.remove());markers.clear();board.replaceChildren();cards.clear();waypoint=null;
      for(const l of sector?.locations||[]){if(['thermal','cluster'].includes(l.kind))continue;const b=el('button',l.name,'map-location');b.type='button';b.style.left=`${l.x/sector.width*100}%`;b.style.top=`${l.y/sector.height*100}%`;b.onclick=()=>{waypoint=l;render();};map.append(b);markers.set(l.id,b);}
      for(const c of contracts){const card=el('article','','contract-card'),text=el('p'),button=el('button','Accept contract'),locate=el('button','Locate objective');button.onclick=()=>action('accept',c.id);locate.onclick=()=>{const l=definition.locations.find(l=>l.id===(c.kind==='survey'?50+(state?.progress.length||0):c.target));waypoint=l;setOpen('map');};card.append(text,button,locate);board.append(card);cards.set(c.id,{card,text,button});}
    },
    update(p,snapshot,connected){self=p;frame=snapshot;state=snapshot?.sector?.pilot;opener.hidden=dockButton.hidden=!connected;
      if(!connected){lastDock=0;pending=0;lastReceipt=0;setOpen('');guide.hidden=true;return;}
      if(state?.receipt&&state.receipt.sequence>lastReceipt&&state.receipt.result!=="pending"){lastReceipt=state.receipt.sequence;pending=0;status.textContent=state.receipt.result==='ok'?'Confirmed.':state.receipt.result.replaceAll('_',' ');}
      if((state?.docked||0)!==lastDock){lastDock=state?.docked||0;setOpen(lastDock?'station':'');}
      if(p?.hull<=0&&mode)setOpen('');render();
    },
    keydown(e){if(!self||self.hull<=0)return false;if(mode&&e.code==='Escape'){e.preventDefault();if(!e.repeat)mode==='station'?action('undock'):setOpen('');return true;}
      if(e.code==='KeyM'){e.preventDefault();if(!e.repeat)setOpen(mode==='map'?'':'map');return true;}
      if(e.code==='KeyE'){e.preventDefault();if(!e.repeat)interact();return true;}
      if(mode&&e.key==='Tab'){const buttons=[...panel.querySelectorAll('button:not(:disabled)')].filter(b=>!b.closest('[hidden]'));const i=buttons.indexOf(document.activeElement);if(e.shiftKey&&i<=0){e.preventDefault();buttons.at(-1)?.focus();}else if(!e.shiftKey&&i===buttons.length-1){e.preventDefault();buttons[0]?.focus();}return true;}
      return !!mode;
    }
  };
}
