const assert=require('node:assert/strict');
const {chromium}=require('../artifacts/browser-check/node_modules/playwright');
(async()=>{
 const browser=await chromium.launch({channel:'msedge',headless:true});
 try{
  const context=await browser.newContext({viewport:{width:1440,height:1000}}),a=await context.newPage(),b=await context.newPage(),errors=[];
  for(const page of [a,b])page.on('pageerror',e=>errors.push(String(e)));
  const suffix=Date.now().toString(36),url=process.env.ARENA_URL||'http://localhost:5094';
  async function join(page,name,room=''){await page.goto(`${url}/${room?'?room='+room:''}`);await page.locator('#nickname').fill(name);await page.getByRole('button',{name:'Join arena',exact:true}).click();await page.waitForFunction(()=>document.querySelector('#network-info')?.dataset.snapshot);}
  const state=page=>page.locator('#network-info').evaluate(e=>JSON.parse(e.dataset.snapshot));
  await join(a,'PingA'+suffix);const room=await a.locator('#network-info').getAttribute('data-room');await join(b,'PingB'+suffix,room);
  await a.bringToFront();await a.locator('#canvas').focus();await a.keyboard.press('m');const panel=a.locator('#sector-console');
  await panel.getByRole('button',{name:'Arrival Port',exact:true}).click();await panel.getByRole('button',{name:'Ping friends',exact:true}).click();
  await a.waitForFunction(()=>JSON.parse(document.querySelector('#network-info').dataset.snapshot).sector.pilot.pings.length===1);
  assert.equal((await state(b)).sector.pilot.pings.length,0);
  const focused=panel.getByRole('button',{name:'Broadcast to room',exact:true});await focused.focus();await a.waitForTimeout(5200);assert(await focused.evaluate(e=>e===document.activeElement));await focused.click();
  await b.waitForFunction(()=>JSON.parse(document.querySelector('#network-info').dataset.snapshot).sector.pilot.pings.length===1);
  await b.bringToFront();await b.locator('#canvas').focus();await b.keyboard.press('m');await b.locator('#sector-console').getByRole('button',{name:/PingA.*Arrival Port/}).click();
  assert.match(await b.locator('#sector-guide').innerText(),/Arrival Port/);await b.setViewportSize({width:390,height:844});assert(await b.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
  await b.screenshot({path:'artifacts/browser-check/cooperation-mobile.png'});await b.keyboard.press('Escape');await b.keyboard.press('e');
  const card=b.locator('.contract-card').filter({hasText:'INTERDICT'});await card.getByRole('button',{name:'Accept contract',exact:true}).click();
  await b.waitForFunction(()=>JSON.parse(document.querySelector('#network-info').dataset.snapshot).sector.pilot.contract===100);
  await b.reload();await b.getByRole('button',{name:'Join arena',exact:true}).click();await b.waitForFunction(()=>document.querySelector('#network-info')?.dataset.snapshot);
  assert.equal((await state(b)).sector.pilot.contract,100);assert.equal((await state(b)).sector.pilot.docked,10);
  assert.deepEqual(errors,[]);console.log('PASS: private friend pings, explicit room broadcast, stable focus, mobile map, shared waypoint, repeatable patrol contract, docked reconnect.');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
