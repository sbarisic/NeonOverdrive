// Real-server acceptance: delay incoming WebSocket messages, not their payloads.
// Prerequisites match browser-presentation.cjs. Test-only main.js instrumentation
// exposes the actual C# host; production does not gain a window-global control API.
const assert = require('node:assert/strict');
const { chromium } = require('../artifacts/browser-check/node_modules/playwright');
(async () => {
  const browser = await chromium.launch({
    channel: 'msedge',
    headless: true,
    args: ['--disable-background-timer-throttling', '--disable-renderer-backgrounding'],
  });
  const errors = [];
  try {
    const context = await browser.newContext({ viewport: { width: 1280, height: 720 } });
    await context.route('**/main.js*', async (route) => {
      const response = await route.fetch();
      const source = await response.text();
      const marker = 'const host = exports.ArcadeDodger.Web.BrowserHost;';
      assert(source.includes(marker), 'host instrumentation anchor must exist');
      await route.fulfill({
        response,
        body: source.replace(marker, marker + '\n window.__predictionHost = host;'),
      });
    });
    await context.addInitScript(() => {
      const NativeSocket = window.WebSocket;
      window.WebSocket = class extends NativeSocket {
        addEventListener(type, listener, options) {
          if (type !== 'message') return super.addEventListener(type, listener, options);
          return super.addEventListener(
            type,
            (event) => {
              setTimeout(() => {
                if (this.readyState === NativeSocket.OPEN) {
                  if (typeof listener === 'function') listener.call(this, event);
                  else listener.handleEvent(event);
                }
              }, 300);
            },
            options,
          );
        }
      };
    });
    const page = await context.newPage();
    page.on('pageerror', (error) => errors.push(String(error)));
    await page.goto((process.env.ARENA_URL || 'http://localhost:5088') + '/');
    await page.locator('#nickname').fill('Prediction');
    await page.locator('#join-form button[type=submit]').click();
    await page.waitForFunction(() => document.querySelector('#network-info')?.dataset.snapshot);
    await page.locator('#canvas').focus();
    await page.waitForTimeout(500);
    // ArenaPosition must report the actual C# rendered local pilot, not JS intent.
    const read = () =>
      page.evaluate(() => {
        const info = document.querySelector('#network-info');
        const world = JSON.parse(info.dataset.snapshot);
        const own = world.players.find((player) => player.id === Number(info.dataset.player));
        const position = window.__predictionHost.ArenaPosition().split(',').map(Number);
        return { x: position[0], y: position[1], serverX: own.x, hull: own.hull, tick: world.tick };
      });
    const before = await read();
    assert(before.hull > 0 && Number.isFinite(before.x), 'live rendered pilot required');
    await page.keyboard.down('a');
    await page.waitForTimeout(120);
    const immediate = await read();
    assert(
      immediate.x < before.x - 3,
      `local movement must precede delayed server snapshot: ${JSON.stringify({ before, immediate })}`,
    );
    assert(
      Math.abs(immediate.serverX - before.serverX) < 1,
      'authoritative snapshot should still precede input during inbound delay',
    );
    await page.waitForTimeout(450);
    await page.keyboard.up('a');
    await page.waitForTimeout(1500);
    const settled = await read();
    assert(
      Math.abs(settled.x - settled.serverX) < 12,
      `prediction must settle to server: ${JSON.stringify(settled)}`,
    );
    await page.keyboard.press('Escape');
    await page.getByRole('button', { name: /^Resume$/ }).waitFor();
    const menuStart = await read();
    await page.keyboard.down('d');
    await page.waitForTimeout(150);
    const menuEnd = await read();
    await page.keyboard.up('d');
    assert(Math.abs(menuEnd.x - menuStart.x) < 3, 'menu must suppress predicted movement too');
    assert.deepEqual(errors, []);
    console.log(
      JSON.stringify({
        result: 'PASS',
        inboundDelayMs: 300,
        before,
        immediate,
        settled,
        checks: [
          'actual rendered local movement before delayed authority',
          'settles to authority',
          'menu suppresses prediction',
        ],
        limits: [
          'Fixed inbound delay, not a jitter/endurance benchmark.',
          'Does not measure remote interpolation or shot latency.',
        ],
      }),
    );
  } finally {
    await browser.close();
  }
})().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
