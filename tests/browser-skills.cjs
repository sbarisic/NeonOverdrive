// Real-server smoke: Tab UI, no free purchases, focus and live-room input suppression.
const assert = require('node:assert/strict');
const { chromium } = require('../artifacts/browser-check/node_modules/playwright');
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage({ viewport: {width:1280,height:720} });
    const errors = []; page.on('pageerror', error => errors.push(String(error)));
    await page.route('**/skills.js*',async route=>{const response=await route.fetch(),source=await response.text();assert(source.includes('return {get open()'));await route.fulfill({response,body:source.replace('return {get open()','return window.__skillFixture={get open()')});});
    await page.addInitScript(()=>{
      const Native=window.WebSocket;window.__farm=false;
      window.WebSocket=class extends Native{
        constructor(...args){super(...args);this.addEventListener('message',e=>{const f=JSON.parse(e.data);if(f.type==='welcome'){this.pilot=f.id;const base=f.sector?.locations.find(l=>l.id===20);if(base)this.destination={x:base.x-300,y:base.y};}if(f.type==='snapshot'){this.frame=f;if(window.__holdFrames)e.stopImmediatePropagation();}});}
        send(raw){const m=JSON.parse(raw),f=this.frame,p=f?.players.find(p=>p.id===this.pilot);
          if(window.__farm&&m.type==='input'&&p?.hull>0){
            const nearest=items=>[...(items||[])].sort((a,b)=>Math.hypot(a.x-p.x,a.y-p.y)-Math.hypot(b.x-p.x,b.y-p.y))[0];
            const enemy=nearest(f.enemies?.filter(e=>e.kind!=="installation")),drop=nearest(f.energy),target=drop||enemy||this.destination;
            if(target){const dx=target.x-p.x,dy=target.y-p.y,d=Math.hypot(dx,dy);m.x=dx/Math.max(1,d);m.y=dy/Math.max(1,d);if(!drop&&d<170){m.x*=-1;m.y*=-1;}}
            if(enemy){m.aim=Math.atan2(enemy.y-p.y,enemy.x-p.x);m.fire=true;}m.editing=false;
          }super.send(JSON.stringify(m));
        }
      };
    });
    await page.goto((process.env.ARENA_URL || 'http://localhost:5088') + '/?multiplayer=1');
    await page.locator('#nickname').fill('S_'+Date.now().toString(36));
    await page.getByRole('button', {name:'Join arena',exact:true}).click();
    await page.waitForFunction(() => document.querySelector('#network-info')?.dataset.snapshot);
    await page.locator('#canvas').focus(); await page.keyboard.press('Tab');
    const panel = page.locator('#arena-skills'); await panel.waitFor({state:'visible'});
    assert.equal(await panel.locator('[data-skill]').count(), 69);
    assert.equal(await panel.locator('[data-skill][aria-disabled="true"]').count(), 69);
    assert.equal(await panel.locator('.tree-link').count(), 55);
    await panel.locator('[data-skill="harvest.1"]').focus();
    assert.match(await panel.locator('.tree-detail').innerText(), /Astral Harvest/);
    await panel.getByRole('button',{name:'Collection',exact:true}).click();
    assert(await panel.locator('.tree-scroll').evaluate(el=>el.scrollTop>0));
    assert.equal(await panel.locator('.skill-face svg').count(),69);
    const face=await panel.locator('.skill-face').first().evaluate(el=>({w:el.offsetWidth,h:el.offsetHeight,r:getComputedStyle(el).borderRadius}));assert.equal(face.w,64);assert.equal(face.h,64);assert.equal(face.r,'0px');
    await panel.locator('[data-skill="shield.1"]').focus();
    await page.keyboard.press('Enter'); // no point: no purchase and no disconnect
    await panel.locator('.tree-scroll').evaluate(el => { el.scrollLeft=0; el.scrollTop=0; });
    await panel.locator('h2').focus();
    const read = () => page.locator('#network-info').evaluate(el => {
      const world = JSON.parse(el.dataset.snapshot); return {tick:world.tick,self:world.players.find(p => p.id === +el.dataset.player)};
    });
    const before = await read();
    await page.keyboard.down('KeyW');
    await page.waitForTimeout(300); await page.keyboard.up('KeyW');
    const after = await read(); assert(after.tick > before.tick);
    assert.equal(after.self.x, before.self.x); assert.equal(after.self.y, before.self.y);
    await page.keyboard.press('Tab');
    assert(await panel.evaluate(el => el.contains(document.activeElement)));
    await page.keyboard.press('Escape'); await panel.waitFor({state:'hidden'});
    assert(await page.locator('#canvas').evaluate(el => document.activeElement === el));
    await page.keyboard.press('Tab'); await panel.waitFor({state:'visible'});
    await page.screenshot({path:'artifacts/browser-check/skills.png'});
    // Earn real XP through authoritative combat, then buy two skills through the actual UI.
    await page.keyboard.press('Escape');await page.locator('#canvas').focus();await page.evaluate(()=>window.__farm=true);
    await page.waitForFunction(()=>{const el=document.querySelector('#network-info'),f=JSON.parse(el.dataset.snapshot);return f.players.find(p=>p.id===+el.dataset.player).level>=2;},null,{timeout:60000});
    await page.evaluate(()=>window.__farm=false);await page.keyboard.press('Tab');await panel.waitFor({state:'visible'});
    await panel.locator('[data-skill="shield.1"]').click();
    await page.waitForFunction(()=>document.querySelector('[data-skill="shield.1"]').dataset.state==='learned');
    assert.match(await panel.locator('.tree-receipt').innerText(),/Learned Shield/);
    await panel.getByRole('button',{name:'Fire Rate',exact:true}).click();await panel.locator('[data-skill="rate.1"]').click();
    await page.waitForFunction(()=>document.querySelector('[data-skill="rate.1"]').dataset.state==='learned');
    const learned=await read();assert.equal(learned.self.skillPoints,0);assert.equal(learned.self.maxShield,3);
    const focus=await page.evaluate(()=>document.activeElement.dataset.skill);await page.waitForTimeout(300);assert.equal(await page.evaluate(()=>document.activeElement.dataset.skill),focus);
    await panel.getByRole('button',{name:'Masteries',exact:true}).click();await page.screenshot({path:'artifacts/browser-check/skills-masteries.png'});
    // Isolated presentation fixture for high-level specialization states; real purchase validation is in server tests.
    await page.evaluate(()=>{window.__holdFrames=true;const el=document.querySelector('#network-info'),f=JSON.parse(el.dataset.snapshot),p=f.players.find(p=>p.id===+el.dataset.player);window.__skillFixture.update({...p,level:15,skillPoints:20,skills:['kinetic.1','laser.1']},true);});
    assert.equal(await panel.locator('[data-skill="rocket.1"]').getAttribute('data-state'),'specialization-locked');
    assert.equal(await panel.locator('[data-skill="kinetic.2"]').getAttribute('data-state'),'ready');
    await panel.locator('[data-skill="rocket.1"]').focus();assert.match(await panel.locator('.tree-detail').innerText(),/Two specializations already selected/);
    await page.screenshot({path:'artifacts/browser-check/skills-specializations.png'});
    await page.evaluate(()=>window.__holdFrames=false);
    await page.setViewportSize({width:390,height:844});await panel.getByRole('button',{name:'Fortune',exact:true}).click();
    assert(await panel.evaluate(el=>{const b=el.getBoundingClientRect();return b.x>=0&&b.right<=innerWidth&&b.bottom<=innerHeight;}));
    await page.screenshot({path:'artifacts/browser-check/skills-mobile.png'});await page.keyboard.press('Escape');
    assert.deepEqual(errors, []);
    console.log('PASS: 69 illustrated square tiles, prerequisite links, branch navigation, real XP earned, two accepted skill purchases, receipts, focus across snapshots, mobile layout, locked/no-point purchases, live ticks, neutral movement, keyboard focus/close, no page errors.');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
