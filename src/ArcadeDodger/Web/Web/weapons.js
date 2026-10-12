// The server supplies names, prices, parameters, and descriptions. This module owns presentation only.
export function createWeaponBench(element, info, equip, buy, onToggle) {
  document.head.append(element('style', `
    #weapon-bench{position:fixed;inset:4vh 4vw;padding:22px;z-index:8;border:1px solid #9d875a;border-radius:14px;background:radial-gradient(ellipse at 50% 0,#282641,#0b101d 75%);box-shadow:0 0 0 100vmax #02050bd9,0 0 65px #80704733;color:#e9e3d4;display:flex;flex-direction:column;gap:12px}
    #weapon-bench h2{font:26px Georgia,serif;letter-spacing:5px;color:#e8cf90;margin:0}
    #weapon-bench p{margin:0}#weapon-bench .forge-heading{display:flex;justify-content:space-between;align-items:center;gap:12px}
    #weapon-bench .forge-level{font-size:12px;color:#9fdbc7;white-space:nowrap}
    #weapon-bench .forge-body{overflow:auto;min-height:100px;padding:6px 4px;display:flex;flex-direction:column;gap:16px;scrollbar-color:#a08a59 #101627}
    #weapon-bench .forge-label{font-size:11px;color:#c6b482;letter-spacing:2px;display:flex;justify-content:space-between;margin:0 0 10px}
    #weapon-bench .forge-label span{color:#98a4ba;letter-spacing:0}
    #weapon-bench .weapon-scroll{overflow-x:auto;padding:4px 4px 12px;scrollbar-color:#a08a59 #101627}
    #weapon-bench .weapon-row{display:grid;grid-template-columns:repeat(9,minmax(76px,1fr));gap:12px;min-width:810px}
    #weapon-bench .weapon-slot{position:relative;padding:9px 4px 7px;min-height:118px;border:1px solid #685d47;border-radius:5px;background:linear-gradient(135deg,#262633,#0b1120);color:#9d9581;touch-action:none;user-select:none;box-shadow:inset 0 0 0 3px #090e18,inset 0 0 18px #0008;cursor:pointer}
    #weapon-bench .weapon-slot::after{content:'›';position:absolute;right:-11px;top:40px;color:#aa9663;font:22px Georgia}
    #weapon-bench .weapon-slot:last-child::after{display:none}
    #weapon-bench .slot-index{position:absolute;top:7px;left:9px;font-size:10px;color:#bda978}
    #weapon-bench .slot-icon{display:block;font:36px/1 Georgia,serif;margin:17px 0 9px;color:var(--part);text-shadow:0 0 20px var(--part)}
    #weapon-bench .slot-name{display:block;font-size:11px;line-height:1.3;min-height:29px;white-space:normal;color:#cec5af}
    #weapon-bench .weapon-slot[data-module="0"]{border-style:dashed;background:radial-gradient(circle,#191f2d,#0a101a)}
    #weapon-bench .weapon-slot[data-module="0"] .slot-icon{opacity:.35;text-shadow:none}
    #weapon-bench .weapon-slot[data-module="0"] .slot-name{color:#6f778c}
    #weapon-bench button[data-selected="true"]{outline:2px solid #f3d68b;outline-offset:3px;background:#363446}
    #weapon-bench .weapon-slot[data-drop="valid"],#weapon-bench .module-catalog[data-drop="valid"]{outline:2px solid #8ff0c7;outline-offset:2px;box-shadow:0 0 24px #62edb833}
    #weapon-bench .weapon-slot[data-drop="invalid"]{outline:2px solid #f77e8c;outline-offset:2px}
    #weapon-bench .module-catalog{display:grid;grid-template-columns:repeat(7,minmax(70px,1fr));gap:10px;padding:8px;border:1px solid #4e4650;background:#090f1c88;border-radius:6px}
    #weapon-bench .module-item{padding:8px 3px;border:1px solid #6e6149;border-radius:4px;background:radial-gradient(circle at 50% 25%,#393346,#121827);color:var(--part);touch-action:none;user-select:none;min-height:87px;cursor:grab;position:relative}
    #weapon-bench .module-glyph{display:block;font:28px/1 Georgia,serif;margin-bottom:7px;text-shadow:0 0 15px var(--part)}
    #weapon-bench .module-name{display:block;font-size:11px;line-height:1.25;color:#e1d7be}
    #weapon-bench .module-level{position:absolute;top:5px;right:6px;font-size:9px;color:#b5a688}
    #weapon-bench .module-item[aria-disabled="true"]{filter:saturate(.25);opacity:.4;cursor:help}
    #weapon-bench .module-item:hover,#weapon-bench .weapon-slot:hover{border-color:#ddc681}
    #weapon-bench .bench-detail{border-left:2px solid #ac9462;padding:7px 12px;min-height:68px;background:#121929;font-size:12px;color:#b5c0d1}
    #weapon-bench .bench-detail strong{display:block;font:17px Georgia,serif;color:#f0d69b;margin-bottom:5px}
    #weapon-bench .weapon-preview{font-size:11px;line-height:1.5;color:#bdba9d;margin-top:8px}
    #weapon-bench .bench-actions{display:flex;gap:10px;align-items:center;flex-wrap:wrap;border-top:1px solid #5f533c;padding-top:12px}
    #weapon-bench .bench-actions button{font-size:12px;border-color:#a68c54;background:#252333;color:#ecd59e;border-radius:3px;padding:9px 13px}
    #weapon-bench .bench-actions button:disabled{cursor:default}
    #weapon-bench .bench-status{color:#a4dfca;font-size:12px;min-height:17px}
    #weapon-bench .forge-help{color:#a9b0c3;font-size:12px;line-height:1.5}
    .module-drag-ghost{position:fixed;z-index:20;pointer-events:none;width:64px;height:64px;display:grid;place-items:center;border:2px solid #ebce86;border-radius:5px;background:#1e2436e8;box-shadow:0 8px 30px #000a;font:36px Georgia;color:var(--part);transform:translate(-50%,-50%)}
    @media(max-width:700px){#weapon-bench{inset:2vh 3vw;padding:14px}#weapon-bench h2{font-size:20px;letter-spacing:2px}#weapon-bench .module-catalog{grid-template-columns:repeat(4,minmax(58px,1fr))}#weapon-bench .forge-heading{align-items:start;flex-direction:column;gap:6px}}
    @media(max-height:650px){#weapon-bench{inset:2vh 3vw;padding:12px;gap:8px}#weapon-bench h2{font-size:20px}#weapon-bench .bench-actions{padding-top:8px}}
  `));

  document.head.append(element('style', `
    #weapon-bench .forge-body{flex:1}#weapon-bench .workshop-tabs{display:flex;gap:8px}#weapon-bench .workshop-tabs button[aria-selected=true]{background:#554532;color:#fff0bd;border-color:#ffe3a0}
    #weapon-bench .workshop-tab[hidden]{display:none}#weapon-bench .shop-filters{display:flex;gap:8px;flex-wrap:wrap;margin:8px 0 14px}
    #weapon-bench .shop-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(205px,1fr));gap:12px}#weapon-bench .shop-card{padding:12px;background:#111727;border:1px solid #685d47;border-radius:6px;display:flex;flex-direction:column;gap:8px}
    #weapon-bench .shop-card p{font-size:12px;color:#b8c4d2;min-height:52px}#weapon-bench .shop-card button{margin-top:auto;font-size:12px}#weapon-bench .shop-card canvas{width:100%;height:52px;background:#080e19}
    #weapon-bench .weapon-heading{display:flex;align-items:center;justify-content:space-between;margin:16px 0 8px;font-size:13px;color:#e6d6ac}#weapon-bench .weapon-heading button{font-size:12px;padding:6px 10px}
    #weapon-bench .chain-preview{width:100%;height:130px;background:#080e19;border:1px solid #383545}#weapon-bench .preview-values{font-size:11px;line-height:1.7;color:#b9c4cf;white-space:normal}
    #weapon-bench .wallet{font-size:16px;color:#ffda70}#weapon-bench [data-locked=true] .weapon-heading{color:#9293a3}#weapon-bench .module-count{display:block;font-size:10px;color:#a8c3b1;margin-top:5px}
    #weapon-bench .workshop-tabs button{padding:8px 20px;font-size:14px}#weapon-bench .shop-filters button{padding:6px 10px;font-size:11px}
    #weapon-bench .weapon-selector{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:8px;margin-bottom:12px}
    #weapon-bench .weapon-selector button{padding:9px 6px;font-size:13px;border-color:#655b48;background:#111927;color:#d9cfb6;min-width:0}
    #weapon-bench .weapon-selector button span{display:block;font-size:10px;margin-top:4px;color:#aebaca}
    #weapon-bench .weapon-selector button[aria-selected=true]{border-color:#ffe3a0;background:#51432f;color:#fff0bd}
    #weapon-bench .weapon-selector button[data-drop=valid]{outline:2px solid #8ff0c7;outline-offset:2px}
    #weapon-bench .module-item[aria-disabled=true]{opacity:.45;cursor:help}
  `));
  const opener=element('button','Weapons [I]');opener.type='button';opener.hidden=true;info.append(opener);
  const panel=element('section','','arena-shell');panel.id='weapon-bench';panel.hidden=true;panel.setAttribute('role','dialog');panel.setAttribute('aria-modal','true');panel.setAttribute('aria-labelledby','weapon-title');
  opener.setAttribute('aria-controls',panel.id);opener.setAttribute('aria-expanded','false');
  const title=element('h2','MODULAR ARSENAL');title.id='weapon-title';title.tabIndex=-1;
  const wallet=element('span','100 CREDITS','wallet'),levelLabel=element('span','','forge-level'),heading=element('div','','forge-heading');heading.append(title,wallet,levelLabel);
  const tabs=element('div','','workshop-tabs');tabs.setAttribute('role','tablist');
  const shopTab=element('button','Shop'),loadoutTab=element('button','Loadout');tabs.append(shopTab,loadoutTab);
  const body=element('div','','forge-body'),shop=element('section','','workshop-tab'),loadout=element('section','','workshop-tab');shop.id='workshop-shop';loadout.id='workshop-loadout';
  for(const [button,section] of [[shopTab,shop],[loadoutTab,loadout]]){button.type='button';button.id=section.id+'-tab';button.setAttribute('role','tab');button.setAttribute('aria-controls',section.id);section.setAttribute('role','tabpanel');section.setAttribute('aria-labelledby',button.id);}
  const filters=element('div','','shop-filters'),shopGrid=element('div','','shop-grid');shop.append(filters,shopGrid);
  const rows=[],slots=[],items=[],cards=[],weaponButtons=[];
  const weaponSelector=element('div','','weapon-selector');weaponSelector.setAttribute('role','tablist');weaponSelector.setAttribute('aria-label','Select weapon to edit');loadout.append(weaponSelector);
  const detail=element('div','','bench-detail');detail.id='module-detail';const detailName=element('strong','Build your fleet'),detailText=element('p','Choose an emitter, then place attachments at fixed distance checkpoints.');detail.append(detailName,detailText);
  const preview=element('canvas','','chain-preview');preview.width=1000;preview.height=150;preview.setAttribute('aria-label','Live weapon chain preview');
  const previewText=element('div','','preview-values');previewText.setAttribute('aria-live','polite');
  const inventory=element('div','','module-catalog');inventory.setAttribute('aria-label','Owned inventory. Drop an equipped module here to unequip it.');
  const state=element('p','','bench-status');state.setAttribute('role','status');
  const apply=element('button','Apply loadout'),reset=element('button','Reset draft'),clear=element('button','Clear selected'),close=element('button','Return to arena [I / Esc]');
  const actions=element('div','','bench-actions');actions.append(apply,reset,clear,close);for(const b of [apply,reset,clear,close])b.type='button';
  body.append(shop,loadout);panel.append(heading,tabs,element('p','Choose a weapon to edit. To move a part, drop it on another weapon tab or select it, switch weapons, then choose a socket.','forge-help'),body,detail,state,actions);document.body.append(panel);
  let docked=false,opened=false,available=false,level=1,stats=[],unlocked=[true,false,false,false],credits=0,owned=Array(29).fill(0),catalog=[],definitions=new Map(),unlock=[1,5,10,15];
  const starter=()=>Array.from({length:4},()=>({emitter:1,attachments:Array(8).fill(0),enabled:true}));
  let saved=starter(),draft=starter(),selected=null,drag=null,ghost=null,suppressClick=false,pending=0,pendingKind='',aliveLife=null,weapon=0,filter='All',tab='loadout',animation=0;
  const clone=w=>w.map(x=>({...x,attachments:[...x.attachments]}));
  const part=i=>i%9===0?draft[Math.floor(i/9)].emitter:draft[Math.floor(i/9)].attachments[i%9-1];
  function setPart(i,id){if(i%9===0)draft[Math.floor(i/9)].emitter=id;else draft[Math.floor(i/9)].attachments[i%9-1]=id;}
  const accepts=(slot,id)=>id===0||(slot%9===0?id>=1&&id<=4:id>=5&&id<=28);
  const used=id=>draft.reduce((n,w)=>n+(w.emitter===id?1:0)+w.attachments.filter(x=>x===id).length,0);
  const spare=id=>(owned[id]||0)-used(id);
  const name=id=>definitions.get(id)?.name||'Empty';
  const glyph=id=>['◇','▰','╱','➤','◉','◆','»','↔','➶','⌖','ϟ','⋔','Ψ','✺','◎','↱','⌁','♨','❄','☢','◈','ϟ','⊙','✹','◌','◉','✥','♡','¤'][id]||'◇';
  const color=id=>id===17?'#ff9958':id===19?'#c3ef78':id===18?'#9edbff':id===20?'#b5ff67':id===21?'#c792ff':id<=4?['','#dfc585','#73e7f1','#ffa26a','#c390f5'][id]:'#ebce86';
  function describe(id,slot=-1){const d=definitions.get(id);if(slot>=0)weapon=Math.floor(slot/9);detailName.textContent=d?.name||(slot%9===0?'Emitter socket':'Empty checkpoint');detailText.textContent=d?d.description+(draft[weapon].emitter===2&&d.laserDescription?' Laser: '+d.laserDescription:''): 'Drag an owned copy here. Effects activate after travelling to this checkpoint.';}
  function switchTab(value){cancelDrag();tab=value;shop.hidden=value!=='shop';loadout.hidden=value!=='loadout';shopTab.setAttribute('aria-selected',String(value==='shop'));loadoutTab.setAttribute('aria-selected',String(value==='loadout'));shopTab.tabIndex=value==='shop'?0:-1;loadoutTab.tabIndex=value==='loadout'?0:-1;render();}
  shopTab.addEventListener('click',()=>switchTab('shop'));loadoutTab.addEventListener('click',()=>switchTab('loadout'));
  tabs.addEventListener('keydown',e=>{if(['ArrowLeft','ArrowRight'].includes(e.key)){e.preventDefault();switchTab(tab==='shop'?'loadout':'shop');(tab==='shop'?shopTab:loadoutTab).focus();}});
  function selectWeapon(index){cancelDrag();weapon=index;describe(part(index*9),index*9);render();}
  weaponSelector.addEventListener('keydown',e=>{if(['ArrowLeft','ArrowRight','Home','End'].includes(e.key)){e.preventDefault();const index=e.key==='Home'?0:e.key==='End'?3:(weapon+(e.key==='ArrowLeft'?3:1))%4;selectWeapon(index);weaponButtons[index].focus();}});
  function render(){
    wallet.textContent=`${credits} CREDITS${docked?"":" · Dock at a station to purchase"}`;levelLabel.textContent=`LEVEL ${level} · ${unlocked.filter(Boolean).length}/4 WEAPONS`;
    slots.forEach((slot,i)=>{const id=part(i);slot.dataset.module=id;slot.style.setProperty('--part',color(id));slot.querySelector('.slot-icon').textContent=glyph(id);slot.querySelector('.slot-name').textContent=id?name(id):i%9===0?'Emitter':'Empty';slot.setAttribute('aria-label',`Weapon ${Math.floor(i/9)+1}, ${i%9===0?'emitter':i%9*5+'%'}: ${name(id)}`);slot.dataset.selected=String(selected?.slot===i);});
    rows.forEach((row,i)=>{row.section.hidden=i!==weapon;weaponButtons[i].setAttribute('aria-selected',String(i===weapon));weaponButtons[i].tabIndex=i===weapon?0:-1;weaponButtons[i].querySelector('span').textContent=!unlocked[i]?'Locked · L'+unlock[i]:draft[i].enabled?'Enabled':'Disabled';row.section.dataset.locked=String(!unlocked[i]);row.label.textContent=`WEAPON ${i+1} · ${!unlocked[i]?'REQUIRES BAY '+(i+1)+' · LEVEL '+unlock[i]:'READY'}`;row.toggle.textContent=draft[i].enabled?'Enabled':'Disabled';row.toggle.setAttribute('aria-pressed',String(draft[i].enabled));});
    items.forEach(item=>{const id=+item.dataset.moduleId;item.hidden=(owned[id]||0)===0;item.querySelector('.module-count').textContent=`${Math.max(0,spare(id))} available / ${owned[id]||0} owned`;item.setAttribute('aria-disabled',String(spare(id)<=0));item.dataset.selected=String(selected?.slot===-1&&selected.module===id);});
    cards.forEach(card=>{const d=definitions.get(+card.dataset.moduleId);card.hidden=filter!=='All'&&filter!==d.category;card.querySelector('.owned-count').textContent=`${owned[d.id]||0} owned · ${Math.max(0,spare(d.id))} available`;const b=card.querySelector('button');b.disabled=!docked||!!pending||d.id===1||credits<d.price;card.querySelector('p').textContent=d.description+(draft[weapon].emitter===2&&d.laserDescription?' Laser: '+d.laserDescription:'');});
    apply.disabled=!!pending||draft.some(w=>w.emitter<0)||catalog.some(d=>spare(d.id)<0);reset.disabled=!!pending;clear.disabled=!!pending||!selected||selected.slot<0;
    preview.setAttribute('aria-label',`Live preview for weapon ${weapon+1}`);previewText.textContent=explain(draft[weapon]);
  }
  function explain(w){const effective=stats.find(s=>s.emitter===w.emitter);let damage=effective?.damage||(w.emitter===2?4:w.emitter>=3?2:1),branches=1,angle=1,terminal=false;return `WEAPON ${weapon+1} · ${name(w.emitter)} · ${damage.toFixed(2)} ${w.emitter===2?'DPS':'damage'} · Range ${Math.round(effective?.range||1300)}${w.emitter===2?'':' · '+(30/(effective?.interval||7)).toFixed(2)+' shots/sec'} · `+w.attachments.map((id,i)=>{if(!id)return '';if(terminal)return `${(i+1)*5}% ${name(id)}: unreachable`;const d=definitions.get(id);if(id===5)damage*=d?.value||2;if(id===9)angle*=.5;if([11,12,13].includes(id)){branches*=d.value;damage/=d.value;}if(id===26)terminal=true;return `${(i+1)*5}% ${name(id)}: ${damage.toFixed(2).replace(/\.?0+$/,'')} ${w.emitter===2?'DPS':'damage'}/branch, ${branches} branches${[11,12,13].includes(id)?', ±'+(d.radius*angle).toFixed(2)+' rad':''}`;}).filter(Boolean).join(' → ');}
  function changed(){state.textContent='Draft changed · Apply loadout to save · CLOAK ACTIVE';render();}
  function remove(slot){if(pending)return;setPart(slot,0);selected=null;changed();}
  function place(source,target){
    if(pending)return;
    if(!accepts(target,source.module)||(source.slot>=0&&!accepts(source.slot,part(target)))){state.textContent='Emitters go in emitter sockets; attachments go in percentage boxes.';return;}
    if(source.slot<0&&spare(source.module)<=0&&part(target)!==source.module){state.textContent='No spare copy. Buy another or move an equipped one.';return;}
    if(source.slot>=0){const old=part(target);setPart(target,source.module);setPart(source.slot,old);}else setPart(target,source.module);
    selected=null;describe(part(target),target);changed();
  }
  function choose(source){if(!opened||pending)return;if(suppressClick){suppressClick=false;return;}describe(source.module,source.slot);if(source.slot<0){if(spare(source.module)<=0){state.textContent='All copies are equipped, or none are owned. Buy a copy in Shop.';return;}selected=source;}else if(selected)place(selected,source.slot);else selected=source.module?source:null;render();}
  function clearDrop(){[...slots,...weaponButtons].forEach(s=>delete s.dataset.drop);delete inventory.dataset.drop;}
  function cancelDrag(){const old=drag;drag=null;if(old?.node.hasPointerCapture(old.pointerId))old.node.releasePointerCapture(old.pointerId);ghost?.remove();ghost=null;clearDrop();}
  function bind(node,source){
    node.addEventListener('pointerdown',e=>{suppressClick=false;if(e.button!==0||!opened||pending)return;const from=source();if(!from.module||(from.slot<0&&spare(from.module)<=0))return;e.preventDefault();node.focus({preventScroll:true});drag={...from,node,pointerId:e.pointerId,x:e.clientX,y:e.clientY,moving:false};node.setPointerCapture(e.pointerId);});
    node.addEventListener('lostpointercapture',()=>{if(drag?.node===node)cancelDrag();});
    node.addEventListener('click',e=>{if(e.detail===0)suppressClick=false;choose(source());});
    node.addEventListener('pointerenter',()=>{if(!drag){const s=source();describe(s.module,s.slot);}});
    node.addEventListener('focus',()=>{const s=source();describe(s.module,s.slot);render();});
  }
  for(let w=0;w<4;w++){
    const section=element('section'),head=element('div','','weapon-heading'),label=element('strong'),toggle=element('button','Enabled');toggle.type='button';toggle.setAttribute('aria-label',`Enable weapon ${w+1}`);toggle.addEventListener('click',()=>{if(!pending){draft[w].enabled=!draft[w].enabled;weapon=w;changed();}});head.append(label,toggle);
    section.id=`weapon-chain-${w}`;section.setAttribute('role','tabpanel');const select=element('button',`Weapon ${w+1}`);select.type='button';select.id=`weapon-select-${w}`;select.dataset.weapon=String(w);select.setAttribute('role','tab');select.setAttribute('aria-controls',section.id);section.setAttribute('aria-labelledby',select.id);select.append(element('span'));select.addEventListener('click',()=>selectWeapon(w));weaponButtons.push(select);weaponSelector.append(select);
    const scroll=element('div','','weapon-scroll'),row=element('div','','weapon-row');scroll.append(row);section.append(head,scroll);loadout.append(section);rows.push({section,label,toggle,scroll});
    for(let stage=0;stage<9;stage++){
      const i=w*9+stage,slot=element('button','','weapon-slot');slot.id=`weapon-slot-${i}`;slot.type='button';slot.dataset.slot=i;slot.setAttribute('aria-describedby',detail.id);slot.append(element('span',stage===0?'EMITTER':stage*5+'%','slot-index'),element('span','','slot-icon'),element('span','','slot-name'));bind(slot,()=>({slot:i,module:part(i)}));slot.addEventListener('contextmenu',e=>{e.preventDefault();cancelDrag();remove(i);});slots.push(slot);row.append(slot);
    }
  }
  loadout.append(preview,previewText,element('h3','OWNED INVENTORY'),inventory);
  panel.addEventListener('pointermove',e=>{
    if(!drag||e.pointerId!==drag.pointerId)return;if(!drag.moving&&Math.hypot(e.clientX-drag.x,e.clientY-drag.y)<6)return;drag.moving=true;e.preventDefault();
    if(!ghost){ghost=element('div',glyph(drag.module),'module-drag-ghost');ghost.style.setProperty('--part',color(drag.module));ghost.setAttribute('aria-hidden','true');document.body.append(ghost);}ghost.style.left=e.clientX+'px';ghost.style.top=e.clientY+'px';clearDrop();
    const target=document.elementFromPoint(e.clientX,e.clientY)?.closest('[data-slot],[data-weapon],.module-catalog');if(target?.dataset.slot!==undefined)target.dataset.drop=accepts(+target.dataset.slot,drag.module)&&(drag.slot<0||accepts(drag.slot,part(+target.dataset.slot)))?'valid':'invalid';else if(target===inventory&&drag.slot>=0)inventory.dataset.drop='valid';else if(target?.dataset.weapon!==undefined&&drag.slot>=0)target.dataset.drop='valid';
    for(const {scroll} of rows){const b=scroll.getBoundingClientRect();if(e.clientY>=b.top&&e.clientY<=b.bottom){if(e.clientX>b.right-28)scroll.scrollLeft+=18;else if(e.clientX<b.left+28)scroll.scrollLeft-=18;}}
    const bounds=body.getBoundingClientRect();if(e.clientY<bounds.top+35)body.scrollTop-=15;else if(e.clientY>bounds.bottom-35)body.scrollTop+=15;
  });
  panel.addEventListener('pointerup',e=>{if(!drag||e.pointerId!==drag.pointerId)return;const source=drag,target=document.elementFromPoint(e.clientX,e.clientY)?.closest('[data-slot],[data-weapon],.module-catalog');if(source.moving){suppressClick=true;e.preventDefault();if(target?.dataset.slot!==undefined)place(source,+target.dataset.slot);else if(target===inventory&&source.slot>=0)remove(source.slot);else if(target?.dataset.weapon!==undefined&&source.slot>=0)place(source,+target.dataset.weapon*9+source.slot%9);}cancelDrag();});
  panel.addEventListener('pointercancel',cancelDrag);window.addEventListener('blur',cancelDrag);document.addEventListener('visibilitychange',()=>{if(document.hidden)cancelDrag();});
  function setCatalog(data,levels){
    catalog=data;definitions=new Map(data.map(d=>[d.id,d]));unlock=levels||unlock;inventory.replaceChildren();shopGrid.replaceChildren();filters.replaceChildren();items.length=0;cards.length=0;
    for(const category of ['All','Emitter','Damage','Trajectory','Elemental','Utility']){const b=element('button',category);b.type='button';b.addEventListener('click',()=>{filter=category;render();});filters.append(b);}
    for(const d of catalog){
      const item=element('button','','module-item');item.type='button';item.dataset.moduleId=d.id;item.style.setProperty('--part',color(d.id));item.append(element('span',glyph(d.id),'module-glyph'),element('span',d.name,'module-name'),element('span','','module-count'));bind(item,()=>({slot:-1,module:d.id}));items.push(item);inventory.append(item);
      const card=element('article','','shop-card');card.dataset.moduleId=d.id;const animation=element('canvas');animation.width=280;animation.height=60;animation.setAttribute('aria-label',d.name+' effect preview');const purchase=element('button',d.id===1?'Starter equipment':`Buy · ${d.price} credits`);purchase.type='button';purchase.setAttribute('aria-label',`Buy ${d.name}`);purchase.addEventListener('click',()=>{if(pending)return;pending=buy(d.id)||0;pendingKind='buy';state.textContent=pending?'Buying…':'Not connected.';render();});card.append(element('strong',d.name),animation,element('p',d.description),element('span','','owned-count'),purchase);cards.push(card);shopGrid.append(card);
    }render();
  }
  function drawChain(canvas,w,time,mini=false){
    const ctx=canvas.getContext('2d'),width=canvas.width,height=canvas.height;ctx.clearRect(0,0,width,height);let rays=[{y:height/2,angle:0,power:(stats.find(s=>s.emitter===w.emitter)?.damage||(w.emitter===2?4:w.emitter>=3?2:1))/(w.emitter===2?4:w.emitter>=3?2:1),color:color(w.emitter)}],spread=1,last=10,terminal=false;
    const points=mini?[.32]:[.05,.1,.15,.2,.25,.3,.35,.4];
    function segment(end){for(const ray of rays){const y=ray.y+Math.tan(ray.angle)*(end-last);ctx.strokeStyle=ray.color;ctx.lineWidth=Math.min(14,2*Math.sqrt(ray.power));ctx.beginPath();ctx.moveTo(last,ray.y);ctx.lineTo(end,y);ctx.stroke();if(w.emitter!==2){const f=(time*.5)%1;ctx.fillStyle='#fff';ctx.fillRect(last+(end-last)*f-2,ray.y+(y-ray.y)*f-2,4,4);}ray.y=y;}last=end;}
    for(let i=0;i<w.attachments.length;i++){const id=w.attachments[i];const x=10+(width-25)*(points[i]??.5);segment(x);if(!mini){ctx.fillStyle='#78889b';ctx.font='10px monospace';ctx.fillText((i+1)*5+'%',x-8,height-7);}if(!id)continue;
      if(id===5)rays.forEach(r=>r.power*=2);if(id===9)spread*=.5;
      if([11,12,13].includes(id)){const d=definitions.get(id);if(d)rays=rays.flatMap(r=>Array.from({length:d.value},(_,n)=>({...r,angle:r.angle+(-d.radius+2*d.radius*n/(d.value-1))*spread,power:r.power/d.value}))).slice(0,32);}
      if([17,18,19,20,21,25].includes(id))rays.forEach(r=>r.color=color(id));
      if(id===26){for(const r of rays){ctx.strokeStyle='#ffcf6e';ctx.strokeRect(x-5,r.y-5,10,10);}terminal=true;break;}
      if(mini&&![5,9,11,12,13,17,18,19,20,21,25].includes(id)){ctx.fillStyle=color(id);ctx.font='20px serif';ctx.fillText(glyph(id),x+5,height/2-5);}
    }
    if(!terminal)segment(width-10);
  }
  function animate(t){if(!opened)return;if(tab==='loadout')drawChain(preview,draft[weapon],t/1000);else cards.forEach(card=>{if(!card.hidden){const id=+card.dataset.moduleId;drawChain(card.querySelector('canvas'),{emitter:id<=4?id:draft[weapon].emitter||2,attachments:id<=4?[]:[id]},t/1000,true);}});animation=requestAnimationFrame(animate);}
  function restore(){cancelDrag();selected=null;draft=clone(saved);describe(draft[weapon].emitter,weapon*9);render();}
  function setOpen(value){opened=value&&available;panel.hidden=!opened;info.inert=opened;opener.setAttribute('aria-expanded',String(opened));cancelDrag();suppressClick=false;cancelAnimationFrame(animation);if(opened){restore();state.textContent='CLOAK ACTIVE · Invincible and hidden from aliens';animation=requestAnimationFrame(animate);}onToggle(opened);if(opened)title.focus({preventScroll:true});}
  opener.addEventListener('click',()=>setOpen(true));close.addEventListener('click',()=>setOpen(false));reset.addEventListener('click',restore);clear.addEventListener('click',()=>{if(selected?.slot>=0)remove(selected.slot);});
  apply.addEventListener('click',()=>{if(pending)return;pending=equip(clone(draft))||0;pendingKind='equip';state.textContent=pending?'Saving loadout…':'Not connected.';render();});
  switchTab('loadout');
  return {get open(){return opened;},setOpen,setCatalog,openShop(){setOpen(true);switchTab("shop");},
    update(self,connected,profile){
      available=connected&&!!self&&self.hull>0;opener.hidden=!connected;opener.disabled=!available;docked=!!self?.docked;level=self?.level||1;stats=self?.weaponStats||[];unlocked=self?.unlockedWeapons||[true,false,false,false];
      if(profile){credits=profile.credits;owned=profile.owned;saved=clone(profile.weapons);}
      const life=self?`${self.id}:${self.life}`:null;if(life!==aliveLife){aliveLife=life;pending=0;restore();}
      if(pending&&profile?.sequence>=pending&&profile.result!=="pending"){const was=pendingKind;pending=0;state.textContent=profile.result==='ok'?(was==='buy'?'Module purchased ✓':'Loadout equipped ✓')+' · CLOAK ACTIVE':`Not applied: ${profile.result.replaceAll('_',' ')}.`;}
      render();if(!available&&opened)setOpen(false);
    },
    keydown(e){if(!opened)return false;if(e.code==='Escape'||e.code==='KeyI'){e.preventDefault();if(!e.repeat)setOpen(false);return true;}
      if(['Delete','Backspace'].includes(e.code)){const slot=document.activeElement?.dataset.slot;e.preventDefault();if(slot!==undefined)remove(+slot);}
      if(['ArrowLeft','ArrowRight','ArrowUp','ArrowDown'].includes(e.code)&&document.activeElement?.dataset.slot!==undefined){e.preventDefault();const delta=e.code==='ArrowLeft'?-1:e.code==='ArrowRight'?1:e.code==='ArrowUp'?-9:9;const current=+document.activeElement.dataset.slot;const next=Math.max(delta===1||delta===-1?weapon*9:0,Math.min(delta===1||delta===-1?weapon*9+8:35,current+delta));if(Math.floor(next/9)!==weapon)selectWeapon(Math.floor(next/9));slots[next].focus({preventScroll:true});slots[next].scrollIntoView({block:'nearest',inline:'nearest'});}
      if(e.key==='Tab'){const controls=[...panel.querySelectorAll('button:not(:disabled)')].filter(n=>!n.closest('[hidden]')&&n.tabIndex>=0);if(e.shiftKey&&(document.activeElement===controls[0]||!controls.includes(document.activeElement))){e.preventDefault();controls.at(-1).focus();}else if(!e.shiftKey&&(document.activeElement===controls.at(-1)||!controls.includes(document.activeElement))){e.preventDefault();controls[0].focus();}}
      if(e.code==='Space'&&e.repeat)e.preventDefault();return true;
    }
  };
}
