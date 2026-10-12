// Real two-client request/accept/rejoin path; persistent store is selected by the test server.
const assert=require('node:assert/strict');
const {chromium}=require('../artifacts/browser-check/node_modules/playwright');
(async()=>{
 const browser=await chromium.launch({channel:'msedge',headless:true,args:['--disable-background-timer-throttling','--disable-renderer-backgrounding']});
 try{
  const aContext=await browser.newContext(),bContext=await browser.newContext();
  const a=await aContext.newPage(),b=await bContext.newPage(),errors=[];
  for(const p of [a,b])p.on('pageerror',e=>errors.push(String(e)));
  const suffix=Date.now().toString(36),alice=`A_${suffix}`,bob=`B_${suffix}`,url=process.env.ARENA_URL||'http://localhost:5088';
  const join=async(p,n,address)=>{await p.goto(address);await p.locator('#nickname').fill(n);await p.getByRole('button',{name:'Join arena',exact:true}).click();await p.waitForFunction(()=>document.querySelector('#network-info')?.dataset.snapshot);};
  const social=async p=>p.locator('#network-info').evaluate(el=>JSON.parse(el.dataset.snapshot).social);
  await join(a,alice,url+'/');const room=await a.locator('#network-info').getAttribute('data-room');
  await a.locator('#canvas').focus();await a.keyboard.press('p');await a.locator('#arena-friends').waitFor({state:'visible'});
  await join(b,bob,`${url}/?room=${room}`);
  await a.bringToFront();await a.locator('#friend-name').fill(bob);await a.getByRole('button',{name:'Send request',exact:true}).click();
  await a.waitForFunction(n=>JSON.parse(document.querySelector('#network-info').dataset.snapshot).social.outgoing.includes(n),bob);
  assert.deepEqual((await social(a)).friends,[]);
  await b.bringToFront();await b.locator('#canvas').focus();await b.keyboard.press('p');
  await b.locator('#arena-friends .friend-row').filter({hasText:alice}).getByRole('button',{name:'Accept',exact:true}).click();
  await b.waitForFunction(n=>JSON.parse(document.querySelector('#network-info').dataset.snapshot).social.friends.includes(n),alice);
  await a.waitForFunction(n=>JSON.parse(document.querySelector('#network-info').dataset.snapshot).social.friends.includes(n),bob);
  assert.deepEqual((await social(a)).outgoing,[]);assert.deepEqual((await social(b)).incoming,[]);
  await b.screenshot({path:'artifacts/browser-check/friends.png'});
  await a.bringToFront();await a.reload();await a.locator('#nickname').waitFor();assert.equal(await a.locator('#nickname').inputValue(),alice);
  await a.getByRole('button',{name:'Join arena',exact:true}).click();await a.waitForFunction(n=>document.querySelector('#network-info')?.dataset.snapshot&&JSON.parse(document.querySelector('#network-info').dataset.snapshot).social.friends.includes(n),bob);
  await a.locator('#canvas').focus();await a.keyboard.press('p');await a.locator('#arena-friends').waitFor({state:'visible'});
  await a.waitForFunction(()=>{const el=document.querySelector('#network-info');return JSON.parse(el.dataset.snapshot).players.find(p=>p.id===+el.dataset.player).editing;});
  await a.setViewportSize({width:390,height:844});assert(await a.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
  await a.screenshot({path:'artifacts/browser-check/friends-mobile.png'});
  await a.locator('#arena-friends .friend-row').filter({hasText:bob}).getByRole('button',{name:'Remove',exact:true}).click();
  await a.waitForFunction(()=>JSON.parse(document.querySelector('#network-info').dataset.snapshot).social.friends.length===0);
  await b.waitForFunction(()=>JSON.parse(document.querySelector('#network-info').dataset.snapshot).social.friends.length===0);
  await a.keyboard.press('Escape');await a.locator('#arena-friends').waitFor({state:'hidden'});assert(await a.locator('#canvas').evaluate(el=>document.activeElement===el));
  assert.deepEqual(errors,[]);console.log('PASS: request is pending until recipient accepts, mutual friendships, name remembered after reload, friendship restored on rejoin, removal updates both clients, P/escape/focus, narrow layout, no page errors.');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
