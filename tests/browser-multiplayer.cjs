const { chromium } = require('../artifacts/browser-check/node_modules/playwright');
const assert = require('node:assert/strict');
(async () => {
  const browser = await chromium.launch({channel:'msedge',headless:true,args:['--disable-background-timer-throttling','--disable-renderer-backgrounding']});
  const errors=[];
  try {
    const a=await browser.newPage({viewport:{width:1280,height:720}});
    const b=await browser.newPage({viewport:{width:1280,height:720}});
    for(const p of [a,b]) p.on('pageerror',e=>errors.push(String(e)));
    const base=process.env.ARENA_URL || 'http://localhost:5088';
    await a.goto(base+'/?multiplayer=1');
    await a.locator('#nickname').fill('Alpha'); await a.locator('#join-form button').click();
    await a.waitForFunction(()=>document.querySelector('#network-info')?.dataset.snapshot);
    const room=await a.locator('#network-info').getAttribute('data-room');
    await b.goto(base+'/?multiplayer=1&room='+room);
    await b.locator('#nickname').fill('Beta'); await b.locator('#join-form button').click();
    await b.waitForFunction(()=>JSON.parse(document.querySelector('#network-info')?.dataset.snapshot || '{}').players?.length===2);
    const read=p=>p.locator('#network-info').evaluate(e=>({id:Number(e.dataset.player),snapshot:JSON.parse(e.dataset.snapshot)}));
    const initial=await read(a), second=await read(b);
    assert.notEqual(initial.id,second.id);
    await a.bringToFront();
    await a.keyboard.down('a'); await a.waitForTimeout(7600); await a.keyboard.up('a');
    await a.waitForTimeout(200);
    const moved=await read(b);
    assert(moved.snapshot.players.find(p=>p.id===initial.id).x < initial.snapshot.players.find(p=>p.id===initial.id).x-1200);
    const now=await read(a), self=now.snapshot.players.find(p=>p.id===initial.id), target=now.snapshot.players.find(p=>p.id===second.id);
    const ox=Math.max(0,Math.min(2880-960,self.x-480)), oy=Math.max(0,Math.min(1620-540,self.y-270));
    const box=await a.locator('#canvas').boundingBox();
    // Aim left towards Beta, even when Beta lies outside the local viewport.
    const dx=target.x-self.x, dy=target.y-self.y, scale=200/Math.hypot(dx,dy);
    await a.mouse.move(box.x+(self.x-ox+dx*scale)/960*box.width,box.y+(self.y-oy+dy*scale)/540*box.height);
    await a.keyboard.down('Space');
    await b.waitForFunction(id=>JSON.parse(document.querySelector('#network-info').dataset.snapshot).shots.some(s=>s.owner===id),initial.id);
    await b.waitForFunction(id=>JSON.parse(document.querySelector('#network-info').dataset.snapshot).players.find(p=>p.id===id).shield<2,second.id,{timeout:10000});
    await a.keyboard.up('Space');
    const hit=await read(b);
    await a.screenshot({path:'artifacts/browser-check/multiplayer-alpha.png'});
    await b.screenshot({path:'artifacts/browser-check/multiplayer-beta.png'});
    assert.deepEqual(errors,[]);
    console.log(JSON.stringify({result:'PASS',room,players:[initial.id,second.id],victim:hit.snapshot.players.find(p=>p.id===second.id),checks:['independent sessions','same room','remote movement','remote shots','server-confirmed damage','no page errors']}));
  } finally {await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
