// Presentation fixture through the real WASM renderer. Combat/DPS uses model tests.
const assert=require('node:assert/strict');
const {chromium}=require('../artifacts/browser-check/node_modules/playwright');
(async()=>{
 const browser=await chromium.launch({channel:'msedge',headless:true});
 try{
  const context=await browser.newContext({viewport:{width:1280,height:720}}),errors=[];
  await context.route('**/main.js*',async route=>{
   const response=await route.fetch(),source=await response.text(),marker='const host = exports.ArcadeDodger.Web.BrowserHost;';
   assert(source.includes(marker));await route.fulfill({response,body:source.replace(marker,marker+'\nwindow.__beamHost=host;')});
  });
  const page=await context.newPage();page.on('pageerror',e=>errors.push(String(e)));
  await page.goto((process.env.ARENA_URL||'http://localhost:5088')+'/');
  await page.locator('#nickname').fill('BeamFixture');await page.getByRole('button',{name:'Join arena',exact:true}).click();
  await page.waitForFunction(()=>document.querySelector('#network-info')?.dataset.snapshot);
  await page.evaluate(()=>{
   const info=document.querySelector('#network-info'),snapshot=JSON.parse(info.dataset.snapshot),id=+info.dataset.player;
   const own=snapshot.players.find(p=>p.id===id);
   Object.assign(own,{x:1000,y:800,aim:0,level:7,weapon:'laser',weapons:[{emitter:2,attachments:[5,0,11,0,0,0,0,0],enabled:true},...Array.from({length:3},()=>({emitter:1,attachments:Array(8).fill(0),enabled:false}))],editing:false});
   Object.assign(snapshot,{tick:1000000,players:[own],shots:[],enemies:[],energy:[],explosions:[],beams:[
    {owner:id,segment:0,x:1000,y:800,endX:1065,endY:800,effects:0,power:1},
    {owner:id,segment:1,x:1065,y:800,endX:1195,endY:800,effects:0,power:2},
    {owner:id,segment:2,x:1195,y:800,endX:1345,endY:752,effects:0},
    {owner:id,segment:3,x:1195,y:800,endX:1345,endY:848,effects:0}]});
   window.__beamHost.ActivateArena();window.__beamHost.ArenaWelcome(id);window.__beamHost.ArenaSnapshot(JSON.stringify(snapshot));
  });
  await page.waitForTimeout(300);await page.mouse.move(1000,360);await page.waitForTimeout(100);
  await page.screenshot({path:'artifacts/browser-check/beams.png'});
  assert.deepEqual(errors,[]);assert(await page.locator('#status').isHidden());
  console.log('PASS: continuous forked-beam presentation fixture renders through WASM without page errors; screenshot saved for visual inspection.');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
