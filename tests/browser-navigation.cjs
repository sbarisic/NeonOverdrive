const assert = require('node:assert/strict');
const { chromium } = require('../artifacts/browser-check/node_modules/playwright');
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    for (const dpr of [1, 2]) {
      const page = await browser.newPage({
        viewport: { width: 1280, height: 720 },
        deviceScaleFactor: dpr,
      });
      const errors = [];
      page.on('pageerror', (e) => errors.push(String(e)));
      await page.route('**/main.js*', async (route) => {
        const response = await route.fetch();
        await route.fulfill({
          response,
          body: (await response.text()).replace(
            'const host = exports.ArcadeDodger.Web.BrowserHost;',
            'const host = exports.ArcadeDodger.Web.BrowserHost;window.__navigationHost=host;',
          ),
        });
      });
      await page.addInitScript(() => {
        const Native = window.WebSocket;
        window.WebSocket = class extends Native {
          constructor(...args) {
            super(...args);
            this.addEventListener('message', (e) => {
              const frame = JSON.parse(e.data);
              if (frame.type === 'welcome') window.__navigationSector = frame.sector;
            });
          }
        };
      });
      await page.goto(process.env.ARENA_URL || 'http://localhost:5088');
      await page.locator('#nickname').fill('Route' + dpr + Date.now().toString(36));
      await page.getByRole('button', { name: 'Join arena', exact: true }).click();
      await page.waitForFunction(
        () =>
          document.querySelector('#network-info')?.dataset.snapshot && window.__navigationSector,
      );
      await page.waitForTimeout(1500);
      const result = await page.evaluate(async () => {
        const h = window.__navigationHost,
          sector = window.__navigationSector;
        const info = document.querySelector('#network-info');
        const self = JSON.parse(info.dataset.snapshot).players.find(
          (p) => p.id === +info.dataset.player,
        );
        const calls = [],
          frames = [],
          routes = [];
        // Blocked targets require approach selection and several planet detours across the expanded sector.
        for (const target of sector.locations.filter((l) =>
          ['planet', 'station', 'base'].includes(l.kind),
        )) {
          const began = performance.now();
          let path = [];
          while ((!path.length || h.ArenaRoutePending()) && performance.now() - began < 10000) {
            await new Promise(requestAnimationFrame);
            const start = performance.now();
            path = JSON.parse(h.ArenaRoute(self.x, self.y, target.x, target.y));
            calls.push(performance.now() - start);
            frames.push(+h.ArenaMetrics().split(',')[2]);
          }
          if (!path.length || h.ArenaRoutePending()) throw new Error('No route to ' + target.name);
          routes.push({ name: target.name, ms: performance.now() - began, points: path.length });
        }
        calls.sort((a, b) => a - b);
        frames.sort((a, b) => a - b);
        return {
          routes,
          callMax: calls.at(-1),
          frameP95: frames[Math.floor(frames.length * 0.95)],
          frameMax: frames.at(-1),
          canvas: {
            width: document.querySelector('#canvas').width,
            height: document.querySelector('#canvas').height,
          },
        };
      });
      assert(result.callMax < 20, JSON.stringify(result));
      assert(result.frameP95 < 20, JSON.stringify(result));
      assert.equal(result.canvas.width, 1280 * dpr);
      assert.equal(result.canvas.height, 720 * dpr);
      assert.deepEqual(errors, []);
      await page.screenshot({ path: `artifacts/navigation-font-dpr${dpr}.png` });
      console.log(JSON.stringify({ dpr, ...result }));
      await page.close();
    }
  } finally {
    await browser.close();
  }
})().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
