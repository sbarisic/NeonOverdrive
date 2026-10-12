// Feed actual simulation recordings through the WASM renderer; never mutate a live room.
const fs=require('node:fs'),assert=require('node:assert/strict');
const {chromium}=require('../artifacts/browser-check/node_modules/playwright');
(async()=>{
 const browser=await chromium.launch({channel:'msedge',headless:true,args:['--enable-gpu','--use-angle=d3d11']});
 try{
  const page=await browser.newPage({viewport:{width:1920,height:1080}}),errors=[];
  page.on('pageerror',e=>errors.push(String(e)));
  await page.route('**/main.js*',async route=>{
   const response=await route.fetch();let source=(await response.text()).replaceAll('\r\n','\n');
   source=source.replace('startNetwork(host, canvas, status);', '/* Recorded playback owns the arena in this test. */');
   source=source.replace('const host = exports.ArcadeDodger.Web.BrowserHost;','const host = exports.ArcadeDodger.Web.BrowserHost;window.__perfHost=host;window.__frameTimes=[];');
   source=source.replace('const bounds = canvas.getBoundingClientRect();','const started=performance.now();const bounds = canvas.getBoundingClientRect();');
   source=source.replace('} catch (error) {\n      running = false;','window.__frameTimes.push(performance.now()-started);\n    } catch (error) {\n      running = false;');
   await route.fulfill({response,body:source});
  });
  await page.goto(process.env.ARENA_URL||'http://localhost:5094');await page.waitForFunction(()=>window.__perfHost&&document.querySelector('#status').hidden);
  const results=[];
  for(const limit of (process.env.PERF_LIMIT?[+process.env.PERF_LIMIT]:[256,512,1024])){
   const recording=JSON.parse(fs.readFileSync(`artifacts/combat-performance/frames-${limit}.json`,'utf8'));
   recording.frames=recording.frames.slice(0,45);
   const result=await page.evaluate(async ({sector,frames})=>{
    const {presentSnapshot}=await import('/snapshot.js');const h=window.__perfHost;h.ActivateArena();h.ArenaHudLayout(1920,1080);h.ArenaWelcome(1);h.ArenaSector(JSON.stringify(sector));
    window.__frameTimes=[];const stages=[];const receive=[],intervals=[];let last=performance.now(),count=0,done=false;
    function raf(now){intervals.push(now-last);last=now;if(!done)requestAnimationFrame(raf);}requestAnimationFrame(raf);
    for(let lap=0;lap<2;lap++)for(const original of frames){
      const frame=structuredClone(original),offset=lap*1000;frame.tick+=offset;
      for(const s of frame.shots)s.born+=offset;
      for(const x of frame.explosions)x.born+=offset;
      for(const e of frame.combatEvents||[]){e.born+=offset;e.id+=lap*1000000;}
      const json=JSON.stringify(frame),start=performance.now();presentSnapshot(h,frame);receive.push(performance.now()-start);stages.push(h.ArenaMetrics?.().split(',').map(Number)||[]);
      await new Promise(resolve=>setTimeout(resolve,1000/15));count++;
    }
    done=true;const p95=a=>a.slice().sort((a,b)=>a-b)[Math.floor(a.length*.95)];
    return {renderer:document.querySelector("canvas").getContext("webgl2")?.getParameter(7937),stageSamples:stages.slice(-5),frames:count,renderP95:p95(window.__frameTimes.slice(60)),receiveP95:p95(receive.slice(15)),frameIntervalP95:p95(intervals.slice(60)),longFrames:intervals.filter(t=>t>50).length};
   },recording);
   results.push({limit,...result});console.log(results.at(-1));fs.writeFileSync(`artifacts/combat-performance/browser-${limit}.json`,JSON.stringify(result,null,2));await page.screenshot({path:`artifacts/combat-performance/browser-${limit}.png`});
  }
  assert.deepEqual(errors,[]);fs.writeFileSync('artifacts/combat-performance/browser.json',JSON.stringify({browser:browser.version(),viewport:'1920x1080',results},null,2));console.log(results);
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
