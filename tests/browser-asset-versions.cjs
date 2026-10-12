const assert = require('node:assert/strict');
const {chromium} = require('../artifacts/browser-check/node_modules/playwright');
(async () => {
  const browser = await chromium.launch({channel:'msedge',headless:true});
  try {
    const page = await browser.newPage(), modules = [];
    page.on('request', request => {
      const url = new URL(request.url());
      if (/^\/[^/]+\.js$/.test(url.pathname)) modules.push(url);
    });
    // A stale unversioned skills module must never enter the new module graph.
    await page.route('**/skills.js', route => route.fulfill({contentType:'text/javascript',body:'throw new Error("Stale module loaded");'}));
    const origin = process.env.ARENA_URL || 'http://localhost:5088';
    await page.goto(origin + '/?multiplayer=1');
    await page.locator('#nickname').fill('Cache_' + Date.now().toString(36));
    await page.getByRole('button',{name:'Join arena',exact:true}).click();
    await page.waitForFunction(() => document.querySelector('#network-info')?.dataset.snapshot);
    assert(modules.some(url => url.pathname === '/skills.js'));
    assert(modules.length >= 5);
    assert(modules.every(url => url.searchParams.get('v')));
    assert.equal(new Set(modules.map(url => url.searchParams.get('v'))).size, 1);
    await page.close();

    const broken = await browser.newPage();
    await broken.route('**/skills.js?*', async route => {
      const response = await route.fetch(), source = await response.text();
      assert(source.includes('setOpen,setCatalog,'));
      await route.fulfill({response,body:source.replace('setOpen,setCatalog,','setOpen,')});
    });
    // Reproduce a browser error event after the incompatible welcome handler closes the socket.
    await broken.addInitScript(() => {
      const Native = window.WebSocket;
      window.WebSocket = class extends Native {
        close(...args) { this.dispatchEvent(new Event('error')); return super.close(...args); }
      };
    });
    await broken.goto(origin + '/?multiplayer=1');
    await broken.locator('#nickname').fill('Mismatch_' + Date.now().toString(36));
    await broken.getByRole('button',{name:'Join arena',exact:true}).click();
    await broken.getByText('The game files or server data are incompatible.',{exact:false}).waitFor();
    assert(!(await broken.locator('body').innerText()).includes('A multiplayer server must be running'));
    console.log('PASS: all application modules share a build version, stale unversioned skills bypassed, real join succeeds, incompatible-client error survives subsequent socket error.');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
