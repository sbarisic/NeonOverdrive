import { branches, colors, skillIcon, treeCss } from './skill-definitions.js';
export function createSkillTree(element,info,buySkill,onToggle){
 document.head.append(element('style',treeCss));
 const opener=element('button','Skills [Tab]');opener.hidden=true;opener.type='button';info.append(opener);
 const panel=element('section','','arena-shell');panel.id='arena-skills';panel.hidden=true;panel.setAttribute('role','dialog');panel.setAttribute('aria-modal','true');panel.setAttribute('aria-labelledby','arena-skills-title');
 opener.setAttribute('aria-controls',panel.id);opener.setAttribute('aria-expanded','false');
 const title=element('h2','PILOT ASCENSION');title.id='arena-skills-title';title.tabIndex=-1;
 const points=element('p'),heading=element('div','','tree-heading');heading.append(title,points);
 const nav=element('nav','','tree-nav');nav.setAttribute('aria-label','Skill branches');
 const scroll=element('div','','tree-scroll');scroll.setAttribute('aria-label','Skill tree. Scroll vertically through branches and horizontally through tiers.');
 const detail=element('div','','tree-detail');detail.id='skill-detail';const detailName=element('strong'),description=element('p'),requirement=element('p'),comparison=element('p');detail.append(detailName,description,comparison,requirement);
 const receipt=element('div','','tree-receipt');receipt.setAttribute('role','status');
 const close=element('button','Return to arena [Esc]');close.type='button';const footer=element('div','','tree-footer');footer.append(element('span','2 points per level · Reset on death · Two masteries · EQUIPMENT CLOAK'),close);
 panel.append(heading,nav,element('p','Select a branch above · Hover or focus for details · Click or press Enter to learn','tree-help'),scroll,detail,receipt,footer);document.body.append(panel);
 let catalog=[],byId=new Map(),buttons=new Map(),positions=new Map(),links=[],self=null,owned=new Set(),opened=false,available=false,selected='',pending=null,lastReceipt=0,life=-1;
 const roman=['','I','II','III','IV','V','VI'];
 function state(d){if(owned.has(d.id))return 'learned';if((self?.level||1)<d.minLevel)return 'level-locked';if(d.prerequisite&&!owned.has(d.prerequisite))return 'prerequisite-locked';if(d.mastery&&d.tier===1&&new Set(catalog.filter(s=>s.mastery&&owned.has(s.id)).map(s=>s.mastery)).size>=2)return 'specialization-locked';return available&&(self?.skillPoints||0)>0?'ready':'no points';}
 const reasons={'learned':'LEARNED','ready':'AVAILABLE','level-locked':'LEVEL REQUIRED','prerequisite-locked':'PREREQUISITE REQUIRED','specialization-locked':'Two specializations already selected','no points':'NO SKILL POINTS'};
 function describe(id){const d=byId.get(id);if(!d)return;selected=id;detailName.textContent=d.name;description.textContent=d.description;
 const previous=catalog.filter(s=>s.family===d.family&&owned.has(s.id)).sort((a,b)=>b.tier-a.tier)[0];
 comparison.textContent=owned.has(id)?'Current: '+d.description:`Current: ${previous?.description||'No bonus from this skill family.'} → ${d.description}`;
 requirement.textContent=`${reasons[state(d)]} · ${d.cost} point · Level ${d.minLevel}${d.prerequisite?' · Requires '+byId.get(d.prerequisite).name:''}${d.mastery?' · '+d.mastery+' specialization':''}`;
 }
 function redraw(){points.textContent=`LEVEL ${self?.level||1} / ${self?.skillPoints||0} POINTS AVAILABLE`;opener.textContent=`Skills [Tab]${self?.skillPoints?' +'+self.skillPoints:''}`;
 for(const d of catalog){const b=buttons.get(d.id),s=state(d);b.dataset.state=s;b.setAttribute('aria-disabled',String(s!=='ready'||pending!==null));b.setAttribute('aria-label',`${d.name}: ${reasons[s]}, level ${d.minLevel}`);b.querySelector('.skill-level').textContent=s==='learned'?'OWNED':`L${d.minLevel}`;}
 for(const l of links)l.path.classList.toggle('unlocked',owned.has(l.parent));describe(selected);
 }
 function setCatalog(data){catalog=data;byId=new Map(data.map(d=>[d.id,d]));buttons.clear();positions.clear();links=[];scroll.replaceChildren();nav.replaceChildren();let offset=0;
 branches.forEach((branch,bi)=>{const defs=data.filter(d=>d.branch===branch),families=[...new Set(defs.map(d=>d.family))],section=element('section','','tree-section');section.style.setProperty('--branch',colors[bi]);section.style.height=`${55+families.length*108}px`;section.append(element('h3',branch.toUpperCase()));
 const svg=document.createElementNS('http://www.w3.org/2000/svg','svg');svg.classList.add('tree-links');svg.setAttribute('aria-hidden','true');section.append(svg);
 const jump=element('button',branch);jump.type='button';jump.addEventListener('click',()=>{scroll.scrollTo({top:section.offsetTop-scroll.firstElementChild.offsetTop,left:0});buttons.get(defs[0].id)?.focus({preventScroll:true});});nav.append(jump);
 const local=new Map();for(const d of defs){const x=d.prerequisite&&local.has(d.prerequisite)?local.get(d.prerequisite).x+120:18,y=45+families.indexOf(d.family)*108;local.set(d.id,{x,y});positions.set(d.id,{x,y:y+offset});
 const b=element('button','','skill-node');b.type='button';b.dataset.skill=d.id;b.style.left=x+'px';b.style.top=y+'px';b.setAttribute('aria-describedby',detail.id);
 const face=element('span','','skill-face');face.append(skillIcon(d.icon,colors[bi],d.tier),element('span',roman[d.tier]||String(d.tier),'skill-tier'),element('span',`L${d.minLevel}`,'skill-level'));const pips=element('span','','skill-pips');for(let n=0;n<d.tier;n++)pips.append(element('i'));face.append(pips);b.append(face,element('span',d.name,'skill-name'));
 b.addEventListener('mouseenter',()=>describe(d.id));b.addEventListener('focus',()=>describe(d.id));b.addEventListener('click',()=>{describe(d.id);if(state(d)==='ready'&&!pending){pending=d.id;receipt.textContent='Learning '+d.name+'…';buySkill(d.id);redraw();}});buttons.set(d.id,b);section.append(b);}
 for(const d of defs)if(d.prerequisite&&local.has(d.prerequisite)){const a=local.get(d.prerequisite),b=local.get(d.id),path=document.createElementNS(svg.namespaceURI,'path');path.setAttribute('d',`M${a.x+88} ${a.y+32} C${(a.x+b.x)/2+56} ${a.y+32},${(a.x+b.x)/2+56} ${b.y+32},${b.x+24} ${b.y+32}`);path.setAttribute('class','tree-link');svg.append(path);links.push({path,parent:d.prerequisite});}
 scroll.append(section);offset+=55+families.length*108;
 });selected=data[0]?.id||'';redraw();}
 function setOpen(value){opened=value&&available;panel.hidden=!opened;opener.setAttribute('aria-expanded',String(opened));info.inert=opened;onToggle(opened);if(opened)title.focus({preventScroll:true});}
 opener.addEventListener('click',()=>setOpen(true));close.addEventListener('click',()=>setOpen(false));
 return {get open(){return opened;},setOpen,setCatalog,
 update(player,connected,action){self=player;available=connected&&!!player&&player.hull>0;opener.hidden=!connected;opener.disabled=!available;owned=new Set(player?.skills||[]);
 if(!connected||player?.life!==life){pending=null;lastReceipt=0;receipt.textContent='';life=player?.life;}
 if(action&&action.sequence>lastReceipt){lastReceipt=action.sequence;pending=null;receipt.textContent=action.result==='ok'?`Learned ${byId.get(action.skill)?.name||action.skill}`:`Could not learn: ${action.result.replaceAll('_',' ')}`;}
 redraw();if(!available&&opened)setOpen(false);},
 keydown(e){if(!opened)return false;if(e.code==='Escape'){e.preventDefault();if(!e.repeat)setOpen(false);}
 if(e.key==='Tab'){const controls=[...panel.querySelectorAll('button')];if(e.shiftKey&&(document.activeElement===controls[0]||!controls.includes(document.activeElement))){e.preventDefault();controls.at(-1).focus();}else if(!e.shiftKey&&document.activeElement===controls.at(-1)){e.preventDefault();controls[0].focus();}}
 if(['ArrowLeft','ArrowRight','ArrowUp','ArrowDown'].includes(e.key)){e.preventDefault();const id=document.activeElement?.dataset.skill;if(!id){buttons.values().next().value?.focus();return true;}const a=positions.get(id),horizontal=['ArrowLeft','ArrowRight'].includes(e.key),sign=['ArrowLeft','ArrowUp'].includes(e.key)?-1:1;const candidates=[...positions].map(([id,p])=>({id,dx:p.x-a.x,dy:p.y-a.y})).filter(p=>(horizontal?p.dx:p.dy)*sign>0);candidates.sort((a,b)=>(horizontal?Math.abs(a.dx)+Math.abs(a.dy)*4:Math.abs(a.dy)+Math.abs(a.dx)*4)-(horizontal?Math.abs(b.dx)+Math.abs(b.dy)*4:Math.abs(b.dy)+Math.abs(b.dx)*4));const target=buttons.get(candidates[0]?.id);target?.focus({preventScroll:true});target?.scrollIntoView({block:'nearest',inline:'nearest'});}return true;}
 };
}
