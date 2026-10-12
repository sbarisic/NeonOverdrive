const assert = require('node:assert/strict');
const { chromium } = require('../artifacts/browser-check/node_modules/playwright');
(async () => {
 const browser = await chromium.launch({channel:'msedge',headless:true});
 try {
  for (const deviceScaleFactor of [1, 1.5]) {
   const context = await browser.newContext({deviceScaleFactor});
   await context.route('**/main.js*', async route => {
    const response = await route.fetch(), source = await response.text();
    const anchor = 'const host = exports.ArcadeDodger.Web.BrowserHost;';
    assert(source.includes(anchor));
    await route.fulfill({response,body:source.replace(anchor,anchor+'\nwindow.__viewportHost = host;')});
   });
   const page = await context.newPage(), errors=[];
   page.on('pageerror', e=>errors.push(String(e)));
   await page.goto((process.env.ARENA_URL || 'http://localhost:5088')+'/');
   await page.locator('#nickname').fill('Viewport');
   await page.getByRole('button',{name:'Join arena',exact:true}).click();
   await page.waitForFunction(()=>document.querySelector('#network-info')?.dataset.snapshot);
   // Stay protected during layout checks. Host aim mapping remains testable in-menu.
   await page.locator('#canvas').focus();await page.keyboard.press('i');
   for (const [width,height] of [[2048,906],[2560,720],[1280,720]]) {
    await page.setViewportSize({width,height});
    await page.waitForFunction(({width,height,dpr})=>{const c=document.querySelector('#canvas');return c.width===Math.round(width*dpr)&&c.height===Math.round(height*dpr);},{width,height,dpr:deviceScaleFactor});
    const result = await page.evaluate(()=>{
     const c=document.querySelector('#canvas'), b=c.getBoundingClientRect(), host=window.__viewportHost;
     const [x,y]=host.ArenaPosition().split(',').map(Number);
     const scale=Math.min(c.width/960,c.height/540)*.75,vw=c.width/scale,vh=c.height/scale;
     const ox=Math.max(0,Math.min(x-vw/2,Math.max(0,12000-vw)));
     const oy=Math.max(0,Math.min(y-vh/2,Math.max(0,8000-vh)));
     const errors=[[.05,.2],[.95,.8],[.5,.5]].map(([u,v])=>
      Math.abs(host.ArenaAim(u,v)-Math.atan2(oy+v*vh-y,ox+u*vw-x)));
     return {x:b.x,y:b.y,width:b.width,height:b.height,buffer:[c.width,c.height],errors,
      overflow:document.documentElement.scrollWidth>innerWidth||document.documentElement.scrollHeight>innerHeight};
    });
    assert.equal(result.x,0);assert.equal(result.y,0);assert.equal(result.width,width);assert.equal(result.height,height);
    assert.equal(result.buffer[0],Math.round(width*deviceScaleFactor));
    assert.equal(result.buffer[1],Math.round(height*deviceScaleFactor));
    assert(result.errors.every(e=>e<.0001),JSON.stringify(result));assert(!result.overflow);
    assert(await page.locator('#status').isHidden());
    const hud=await page.locator('#network-info').evaluate(el=>({rect:el.getBoundingClientRect().toJSON(),layout:JSON.parse(el.dataset.layout),buttons:[...el.querySelectorAll('button')].map(b=>b.getBoundingClientRect().toJSON())}));
    const [gx,gy,gw,gh,cell,,fx,fy,fw,fh,mx,,mw]=hud.layout;
    assert(Math.abs(hud.rect.x-gx)<1&&Math.abs(hud.rect.y-gy)<1);
    assert(gx>=fx+11&&gy>=fy+11&&gx+gw<=fx+fw-11&&gy+gh<=fy+fh-11,'grid stays within footer padding');
    assert(Math.abs(mx+mw/2-width/2)<1,'minimap centered');
    assert(hud.buttons.every(b=>b.width>=44&&b.height>=44&&b.x>=gx&&b.y>=gy&&b.right<=gx+gw&&b.bottom<=gy+gh),'command buttons contained');

    if(width===2048){
     await page.keyboard.press('Escape');await page.waitForTimeout(100);
     await page.screenshot({path:`artifacts/browser-check/full-window-dpr${deviceScaleFactor}.png`});
     await page.keyboard.press('i');
    }
   }
   await page.setViewportSize({width:1280,height:720});await page.mouse.move(800,200);
   await page.keyboard.press('Escape');await page.setViewportSize({width:1600,height:800});
   await page.waitForTimeout(250);
   const aiming = await page.evaluate(()=>{
    const info=document.querySelector('#network-info'), frame=JSON.parse(info.dataset.snapshot);
    return {actual:frame.players.find(p=>p.id===+info.dataset.player).aim,expected:window.__viewportHost.ArenaAim(.5,.25)};
   });
   assert(Math.abs(aiming.actual-aiming.expected)<.02,`stationary cursor after resize: ${JSON.stringify(aiming)}`);
   assert.deepEqual(errors,[]);await context.close();
  }
  console.log('PASS: full-window canvas and matching backing buffer at three desktop sizes and two pixel densities; accurate world aiming after resize; no scrolling or page errors.');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
