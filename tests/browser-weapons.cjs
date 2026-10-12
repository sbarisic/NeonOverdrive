// Real v3 server, persisted wallet, WASM renderer and physical pointer actions.
const assert=require('node:assert/strict');
const {chromium}=require('../artifacts/browser-check/node_modules/playwright');
(async()=>{
 const browser=await chromium.launch({channel:'msedge',headless:true});
 try{
  const page=await browser.newPage({viewport:{width:1440,height:1000}}),errors=[];
  page.on('pageerror',e=>errors.push(String(e)));
  await page.route('**/main.js*',async route=>{const response=await route.fetch(),source=await response.text();await route.fulfill({response,body:source.replace('const host = exports.ArcadeDodger.Web.BrowserHost;','const host = exports.ArcadeDodger.Web.BrowserHost;window.__weaponHost=host;')});});
  await page.addInitScript(()=>{const NativeAudio=window.AudioContext;window.__tones=0;window.AudioContext=class extends NativeAudio{createOscillator(){window.__tones++;return super.createOscillator();}};});
  const nickname='W_'+Date.now().toString(36);
  await page.goto((process.env.ARENA_URL||'http://localhost:5088')+'/');await page.locator('#nickname').fill(nickname);await page.getByRole('button',{name:'Join arena',exact:true}).click();
  await page.waitForFunction(()=>document.querySelector('#network-info')?.dataset.snapshot);
  const read=()=>page.locator('#network-info').evaluate(el=>{const f=JSON.parse(el.dataset.snapshot);return {self:f.players.find(p=>p.id===+el.dataset.player),profile:f.workshop};});
  await page.locator('#canvas').focus();await page.keyboard.press('e');await page.locator('#sector-console').getByRole('button',{name:'Module shop',exact:true}).click();await page.locator('#weapon-bench').getByRole('tab',{name:'Loadout',exact:true}).click();const panel=page.locator('#weapon-bench');await panel.waitFor({state:'visible'});
  assert.equal(await panel.locator('.weapon-slot').count(),36);assert.equal(await panel.locator('.weapon-slot:visible').count(),9);assert.equal(await panel.locator('.weapon-selector [role=tab]').count(),4);assert.equal(await panel.locator('select').count(),0);assert.equal((await read()).profile.credits,100);
  await panel.getByRole('tab',{name:'Shop',exact:true}).click();assert.equal(await panel.locator('.shop-card').count(),28);
  await page.screenshot({path:'artifacts/browser-check/workshop-shop.png'});
  assert(await panel.getByRole('button',{name:'Buy Laser emitter',exact:true}).isDisabled());
  await panel.getByRole('button',{name:'Buy Accelerator',exact:true}).click();
  await page.waitForFunction(()=>JSON.parse(document.querySelector('#network-info').dataset.snapshot).workshop.owned[6]===1);
  assert.equal((await read()).profile.credits,40);assert.match(await panel.locator('.bench-status').innerText(),/purchased/);
  await panel.getByRole('tab',{name:'Loadout',exact:true}).click();
  const slot=i=>panel.locator('#weapon-slot-'+i),part=id=>panel.locator('.module-item[data-module-id="'+id+'"]');
  async function drag(source,target,hold=false){
   await source.scrollIntoViewIfNeeded();const a=await source.boundingBox();await page.mouse.move(a.x+a.width/2,a.y+a.height/2);await page.mouse.down();await page.mouse.move(a.x+a.width/2+8,a.y+a.height/2,{steps:3});
   await target.scrollIntoViewIfNeeded();const b=await target.boundingBox();await page.mouse.move(b.x+b.width/2,b.y+b.height/2,{steps:12});
   if(hold){assert.equal(await page.locator('.module-drag-ghost').count(),1);await source.evaluate(el=>window.__dragSource=el);await page.waitForTimeout(300);assert(await source.evaluate(el=>el===window.__dragSource));}
   await page.mouse.up();assert.equal(await page.locator('.module-drag-ghost').count(),0);
  }
  await drag(part(6),slot(1),true);assert.equal(await slot(1).getAttribute('data-module'),'6');
  await drag(slot(1),panel.locator('#weapon-select-1'));assert.equal(await panel.locator('.weapon-slot:visible').count(),9);assert.match(await panel.locator('.preview-values').innerText(),/WEAPON 2/);assert.equal(await slot(1).getAttribute('data-module'),'0');assert.equal(await slot(10).getAttribute('data-module'),'6');
  await panel.getByRole('button',{name:'Apply loadout',exact:true}).click();
  await page.waitForFunction(()=>JSON.parse(document.querySelector('#network-info').dataset.snapshot).workshop.weapons[1].attachments[0]===6);
  assert.equal((await read()).profile.weapons[1].enabled,false);assert.equal((await read()).self.level,1);
  await drag(slot(10),slot(9));assert.equal(await slot(0).getAttribute('data-module'),'1');assert.equal(await slot(10).getAttribute('data-module'),'6');
  await drag(slot(10),panel.locator('.module-catalog'));assert.equal(await slot(10).getAttribute('data-module'),'0');
  await part(6).focus();await page.keyboard.press('Enter');await panel.locator('#weapon-select-0').click();await slot(1).focus();await page.keyboard.press('Enter');assert.equal(await slot(1).getAttribute('data-module'),'6');
  await panel.getByRole('button',{name:'Apply loadout',exact:true}).click();
  await page.waitForFunction(()=>JSON.parse(document.querySelector('#network-info').dataset.snapshot).workshop.weapons[0].attachments[0]===6);
  await panel.locator('#weapon-select-0').focus();await page.keyboard.press('ArrowRight');assert.equal(await panel.locator('#weapon-select-1').getAttribute('aria-selected'),'true');await page.waitForTimeout(200);assert.equal(await panel.locator('#weapon-select-1').getAttribute('aria-selected'),'true');await page.keyboard.press('ArrowLeft');assert.equal(await panel.locator('.weapon-slot:visible').count(),9);
  const before=await read();await page.keyboard.down('w');await page.keyboard.down('Space');await page.waitForTimeout(2200);await page.keyboard.up('Space');await page.keyboard.up('w');
  const after=await read();assert.equal(after.self.x,before.self.x);assert.equal(after.self.y,before.self.y);assert(after.self.protected&&after.self.editing);
  await slot(0).scrollIntoViewIfNeeded();await page.screenshot({path:'artifacts/browser-check/workshop-loadout.png'});
  const origin=await slot(0).boundingBox();await page.mouse.move(origin.x+origin.width/2,origin.y+origin.height/2);await page.mouse.down();await page.mouse.move(origin.x+origin.width/2+10,origin.y+origin.height/2+10,{steps:5});
  assert.equal(await page.locator('.module-drag-ghost').count(),1);await page.keyboard.press('i');await page.mouse.up();assert.equal(await page.locator('.module-drag-ghost').count(),0);assert(await page.locator('#canvas').evaluate(el=>document.activeElement===el));
  await page.setViewportSize({width:1440,height:1000});await page.keyboard.press('i');await slot(1).click({button:'right'});assert.equal(await slot(1).getAttribute('data-module'),'0');await page.keyboard.press('Escape');await page.keyboard.press('i');assert.equal(await slot(1).getAttribute('data-module'),'6');
  await page.keyboard.press('Escape');await page.reload();assert.equal(await page.locator('#nickname').inputValue(),nickname);await page.getByRole('button',{name:'Join arena',exact:true}).click();await page.waitForFunction(()=>document.querySelector('#network-info')?.dataset.snapshot);
  assert.equal((await read()).profile.credits,40);assert.equal((await read()).profile.weapons[0].attachments[0],6);
  await page.locator('#canvas').focus();if((await read()).self?.docked){await page.keyboard.press('e');await page.waitForFunction(()=>JSON.parse(document.querySelector('#network-info').dataset.snapshot).players.find(p=>p.id===+document.querySelector('#network-info').dataset.player).docked===0);await page.locator('#sector-console').waitFor({state:'hidden'});}await page.keyboard.down('d');await page.waitForTimeout(1000);await page.keyboard.up('d');await page.keyboard.down('Space');await page.waitForTimeout(300);await page.keyboard.up('Space');assert(await page.evaluate(()=>window.__tones>0));
  await page.keyboard.press('Escape');await page.getByRole('checkbox',{name:'Sound effects',exact:true}).uncheck();const tones=await page.evaluate(()=>window.__tones);await page.getByRole('button',{name:'Resume',exact:true}).click();await page.keyboard.down('Space');await page.waitForTimeout(300);await page.keyboard.up('Space');assert.equal(await page.evaluate(()=>window.__tones),tones);
  assert.deepEqual(errors,[]);console.log('PASS: 28 shop cards, server-confirmed purchase, credits/ownership, one visible chain, four weapon selectors, matching preview, retained selection across snapshots, 36 saved sockets, cross-weapon drag/drop across snapshots, invalid drops, locked-row reservations, keyboard placement, atomic Apply, draft discard, reconnect persistence, cloak, drag cancellation, sound/mute, no page errors.');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
