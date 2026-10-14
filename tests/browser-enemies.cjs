const { chromium } = require('../artifacts/browser-check/node_modules/playwright');
const assert = require('node:assert/strict');
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  const errors = [];
  try {
    const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
    page.on('pageerror', (e) => errors.push(String(e)));
    await page.route('**/main.js*', async (route) => {
      const response = await route.fetch();
      await route.fulfill({
        response,
        body: (await response.text()).replace(
          'const host = exports.ArcadeDodger.Web.BrowserHost;',
          'const host = exports.ArcadeDodger.Web.BrowserHost;window.__routeHost=host;',
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
            if (frame.type === 'welcome') {
              this.pilot = frame.id;
              const base = frame.sector.locations.find((l) => l.id === 20);
              this.destination = { x: base.x + 350, y: base.y };
            }
            if (frame.type === 'snapshot') this.frame = frame;
          });
        }
        send(raw) {
          const input = JSON.parse(raw),
            p = this.frame?.players.find((p) => p.id === this.pilot);
          if (input.type === 'input' && p && this.destination) {
            const route = window.__routeHost?.ArenaRoute(
              p.x,
              p.y,
              this.destination.x,
              this.destination.y,
            );
            const next = route
              ? JSON.parse(route).find((q) => Math.hypot(q.x - p.x, q.y - p.y) > 60) ||
                this.destination
              : this.destination;
            const dx = next.x - p.x,
              dy = next.y - p.y,
              length = Math.max(1, Math.hypot(dx, dy));
            const desiredSpeed = Math.min(180, Math.sqrt(140 * Math.max(0, length - 45)));
            const ax = (dx / length) * desiredSpeed - p.velocityX,
              ay = (dy / length) * desiredSpeed - p.velocityY;
            const heading = Math.atan2(ay, ax);
            const error = Math.atan2(Math.sin(heading - p.heading), Math.cos(heading - p.heading));
            input.x = Math.max(-1, Math.min(1, error * 3));
            const throttle = Math.abs(error) > 0.6 ? 0 : Math.min(1, Math.hypot(ax, ay) / 90);
            input.y = Math.max(-1, Math.min(1, (p.throttle - throttle) * 8));
            input.brake = length < 50;
          }
          super.send(JSON.stringify(input));
        }
      };
    });
    await page.goto((process.env.ARENA_URL || 'http://localhost:5088') + '/');
    await page.locator('#nickname').fill('NPC_' + Date.now().toString(36));
    await page.locator('#join-form button').click();
    await page.waitForFunction(() => document.querySelector('#network-info')?.dataset.snapshot);
    await page.locator('#canvas').focus();
    await page.waitForFunction(
      () => {
        const state = JSON.parse(document.querySelector('#network-info')?.dataset.snapshot || '{}');
        return (
          state.enemies?.some((e) => e.kind === 'rammer') &&
          state.enemies?.some((e) => e.kind === 'gunner')
        );
      },
      null,
      { timeout: 60000 },
    );
    await page.waitForFunction(
      () =>
        JSON.parse(document.querySelector('#network-info').dataset.snapshot).shots.some(
          (s) => s.owner < 0 && s.born > 0,
        ),
      null,
      { timeout: 10000 },
    );
    const state = await page
      .locator('#network-info')
      .evaluate((e) => JSON.parse(e.dataset.snapshot));
    assert(state.enemies.length <= 192);
    assert(state.shots.every((s) => Number.isFinite(s.dx) && Number.isFinite(s.dy) && s.born > 0));
    await page.screenshot({ path: 'artifacts/browser-check/multiplayer-enemies.png' });
    assert.deepEqual(errors, []);
    console.log(
      JSON.stringify({
        result: 'PASS',
        tick: state.tick,
        enemies: state.enemies,
        checks: [
          'WASM startup',
          'rammer and gunner snapshots',
          'hostile shot birth/velocity',
          'no page errors',
        ],
      }),
    );
  } finally {
    await browser.close();
  }
})().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});
