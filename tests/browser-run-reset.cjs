const assert = require('node:assert/strict');
const { chromium } = require('../artifacts/browser-check/node_modules/playwright');
(async () => {
  const browser = await chromium.launch({channel:'msedge',headless:true,args:['--disable-background-timer-throttling','--disable-renderer-backgrounding']});
  try {
    const victim=await browser.newPage({viewport:{width:1280,height:720}}), attacker=await browser.newPage({viewport:{width:1280,height:720}}), errors=[];
    for(const page of [victim,attacker])page.on('pageerror',e=>errors.push(String(e)));
    const origin=process.env.ARENA_URL||'http://localhost:5094', suffix=Date.now().toString(36);
    async function join(page,name,room=''){
      await page.goto(`${origin}/${room?'?room='+room:''}`);
      await page.locator('#nickname').fill(name);await page.getByRole('button',{name:'Join arena',exact:true}).click();
      await page.waitForFunction(()=>document.querySelector('#network-info')?.dataset.snapshot);
    }
    const read=page=>page.locator('#network-info').evaluate(e=>{const snapshot=JSON.parse(e.dataset.snapshot);return {snapshot,self:snapshot.players.find(p=>p.id===+e.dataset.player)};});
    await join(victim,'Run'+suffix);
    const room=await victim.locator('#network-info').getAttribute('data-room');
    await victim.locator('#canvas').focus();await victim.keyboard.press('e');
    await victim.locator('#sector-console').getByRole('button',{name:'Module shop',exact:true}).click();
    const bench=victim.locator('#weapon-bench');await bench.getByRole('tab',{name:'Shop',exact:true}).click();
    await bench.getByRole('button',{name:'Buy Accelerator',exact:true}).click();
    await victim.waitForFunction(()=>JSON.parse(document.querySelector('#network-info').dataset.snapshot).workshop.owned[6]===1);
    await bench.getByRole('tab',{name:'Loadout',exact:true}).click();
    await bench.locator('.module-item[data-module-id="6"]').focus();await victim.keyboard.press('Enter');
    await bench.locator('#weapon-slot-1').focus();await victim.keyboard.press('Enter');
    await bench.getByRole('button',{name:'Apply loadout',exact:true}).click();
    await victim.waitForFunction(()=>JSON.parse(document.querySelector('#network-info').dataset.snapshot).workshop.weapons[0].attachments[0]===6);
    const before=await read(victim);assert.equal(before.snapshot.workshop.credits,40);
    await victim.keyboard.press('Escape');await victim.keyboard.press('e');
    await victim.waitForFunction(()=>JSON.parse(document.querySelector('#network-info').dataset.snapshot).sector.pilot.docked===0);
    await victim.locator('#canvas').focus();await victim.keyboard.down('d');await victim.waitForTimeout(1300);await victim.keyboard.up('d');
    await join(attacker,'Attack'+suffix,room);await attacker.locator('#canvas').focus();
    await attacker.keyboard.down('d');await attacker.waitForTimeout(2800);await attacker.keyboard.up('d');await attacker.waitForTimeout(200);
    const now=await read(attacker), self=now.self, target=now.snapshot.players.find(p=>p.id===before.self.id);
    const box=await attacker.locator('#canvas').boundingBox(), ox=Math.max(0,Math.min(12000-1280,self.x-640)),oy=Math.max(0,Math.min(8000-720,self.y-360));
    const dx=target.x-self.x,dy=target.y-self.y,scale=200/Math.hypot(dx,dy);
    await attacker.mouse.move(box.x+(self.x-ox+dx*scale)/1280*box.width,box.y+(self.y-oy+dy*scale)/720*box.height);
    await attacker.keyboard.down('Space');
    await victim.locator('#arena-respawn').waitFor({state:'visible',timeout:15000});await attacker.keyboard.up('Space');
    await victim.waitForFunction(()=>JSON.parse(document.querySelector('#network-info').dataset.snapshot).workshop.credits===100);
    const dead=await read(victim);assert.equal(dead.self.hull,0);assert.equal(dead.snapshot.workshop.owned[1],4);assert.equal(dead.snapshot.workshop.owned[6]||0,0);
    for(const [i,weapon]of dead.snapshot.workshop.weapons.entries()){
      assert.equal(weapon.emitter,1);assert(weapon.attachments.every(a=>a===0));assert.equal(weapon.enabled,i===0);
    }
    await victim.bringToFront();await victim.locator('#arena-respawn').click();
    await victim.waitForFunction(run=>JSON.parse(document.querySelector('#network-info').dataset.snapshot).players.find(p=>p.id===+document.querySelector('#network-info').dataset.player).runId!==run,before.self.runId);
    const respawn=await read(victim);assert.equal(respawn.self.hull,3);assert.equal(respawn.self.experience,0);assert.deepEqual(respawn.self.skills,[]);
    await victim.keyboard.press('i');await victim.screenshot({path:'artifacts/browser-check/run-reset.png'});
    assert.deepEqual(errors,[]);console.log('PASS: purchased/equipped module, real PvP death, durable starter wallet, cleared inventory/layout, new run identity, healthy respawn, reset skills/XP.');
  }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
