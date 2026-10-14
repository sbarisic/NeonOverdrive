const fs = require('node:fs'),
  assert = require('node:assert/strict');
const { chromium } = require('../artifacts/browser-check/node_modules/playwright');
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1920, height: 1080 } }),
      errors = [];
    page.on('pageerror', (e) => errors.push(String(e)));
    await page.route('**/main.js*', async (route) => {
      const response = await route.fetch();
      let source = (await response.text()).replaceAll('\r\n', '\n');
      source = source.replace(
        'startNetwork(host, canvas, status);',
        '/* Presentation fixture only. */',
      );
      source = source.replace(
        'const host = exports.ArcadeDodger.Web.BrowserHost;',
        'const host = exports.ArcadeDodger.Web.BrowserHost;window.__fxHost=host;',
      );
      await route.fulfill({ response, body: source });
    });
    await page.goto(process.env.ARENA_URL || 'http://localhost:5094');
    await page.waitForFunction(() => window.__fxHost && document.querySelector('#status').hidden);
    const recording = JSON.parse(
      fs.readFileSync('artifacts/combat-performance/frames-512.json', 'utf8'),
    );
    await page.evaluate(async (base) => {
      const { presentSnapshot } = await import('/snapshot.js');
      const h = window.__fxHost;
      h.ActivateArena();
      h.ArenaHudLayout(1920, 1080);
      h.ArenaWelcome(1);
      const frame = {
        ...base,
        players: [{ ...base.players[0], x: 3000, y: 3000, hull: 3, maxHull: 3, shield: 2, aim: 0 }],
        shots: [],
        beams: [],
        enemies: [],
        mines: [],
        energy: [],
        credits: [],
        moduleDrops: [],
        explosions: [],
        combatEvents: [],
        sector: null,
      };
      window.__sendFx = (tick, events = []) => {
        frame.tick = tick;
        frame.combatEvents = events;
        presentSnapshot(h, frame);
      };
      window.__sendFx(100);
      await new Promise((r) => setTimeout(r, 80));
      window.__sendFx(102);
      await new Promise((r) => setTimeout(r, 80));
      window.__sendFx(104, [
        { id: 1, shot: 3, kind: 1, x: 3090, y: 2980, born: 103, size: 2 },
        {
          id: 2,
          shot: 0,
          kind: 2,
          x: 3190,
          y: 2940,
          born: 103,
          size: 25,
          chassis: 2,
          heading: 0.7,
          scale: 1,
          faction: 'hostile',
          velocityX: 50,
          velocityY: 10,
        },
        {
          id: 3,
          shot: 0,
          kind: 2,
          x: 2810,
          y: 3070,
          born: 103,
          size: 50,
          chassis: 6,
          heading: -1,
          scale: 1.2,
          faction: 'friendly',
          velocityX: -40,
          velocityY: 20,
        },
      ]);
    }, recording.frames[0]);
    await page.waitForTimeout(210);
    await page.screenshot({ path: 'artifacts/browser-check/combat-effects.png' });
    const metrics = await page.evaluate(() =>
      window.__fxHost.ArenaMetrics().split(',').map(Number),
    );
    assert(metrics.every(Number.isFinite));
    assert.deepEqual(errors, []);
    console.log(
      'PASS: combat event packed handoff, renderer and destruction/impact visual fixture',
      metrics,
    );
  } finally {
    await browser.close();
  }
})().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});
