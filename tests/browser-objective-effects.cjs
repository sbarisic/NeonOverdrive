const fs = require('node:fs'),
  assert = require('node:assert/strict');
const { chromium } = require('../artifacts/browser-check/node_modules/playwright');
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    const errors = [];
    page.on('pageerror', (e) => errors.push(String(e)));
    await page.route('**/main.js*', async (route) => {
      const response = await route.fetch();
      const source = (await response.text())
        .replace('startNetwork(host, canvas, status);', '/* Isolated rendering fixture. */')
        .replace(
          'const host = exports.ArcadeDodger.Web.BrowserHost;',
          'const host = exports.ArcadeDodger.Web.BrowserHost; window.__host = host;',
        );
      await route.fulfill({ response, body: source });
    });
    await page.goto(process.env.ARENA_URL || 'http://localhost:5094');
    await page.waitForFunction(() => window.__host && document.querySelector('#status').hidden);
    const base = JSON.parse(fs.readFileSync('artifacts/combat-performance/frames-512.json', 'utf8'))
      .frames[0];
    await page.evaluate(async (base) => {
      const { presentSnapshot } = await import('/snapshot.js');
      const host = window.__host;
      host.ActivateArena();
      host.ArenaWelcome(1);
      host.ArenaHudLayout(1440, 900);
      host.ArenaSector(
        JSON.stringify({
          seed: 123,
          width: 12000,
          height: 8000,
          obstacles: [],
          locations: [
            { id: 50, kind: 'beacon', name: 'Orbital Survey 1', x: 3000, y: 2950, radius: 18 },
            { id: 10, kind: 'station', name: 'Arrival Port', x: 2660, y: 3060, radius: 58 },
            { id: 20, kind: 'base', name: 'Hostile Array', x: 3340, y: 3060, radius: 85 },
          ],
        }),
      );
      const frame = {
        ...base,
        v: 12,
        players: [
          {
            ...base.players[0],
            id: 1,
            name: 'Survey pilot',
            x: 3000,
            y: 3060,
            velocityX: 0,
            velocityY: 0,
            hull: 3,
            shield: 2,
            level: 1,
          },
        ],
        shots: [],
        enemies: [],
        energy: [],
        credits: [],
        beams: [],
        mines: [],
        explosions: [],
        moduleDrops: [],
        combatEvents: [],
        sector: {
          locations: [{ id: 20, hull: 120, state: 'active', launchTicks: 0 }],
          pilot: { interactionTarget: 50, interaction: 0.6 },
        },
      };
      window.__fixture = async (target, progress, reduced = false) => {
        frame.sector.pilot = { interactionTarget: target, interaction: progress };
        host.ArenaPresentation(false, reduced);
        for (let i = 0; i < 3; i++) {
          frame.tick += 2;
          presentSnapshot(host, frame);
          await new Promise((r) => setTimeout(r, 80));
        }
      };
      await window.__fixture(50, 0.6);
    }, base);
    await page.screenshot({ path: 'artifacts/browser-check/objective-scanning.png' });
    const active = await page.locator('#canvas').screenshot();
    await page.evaluate(() => window.__fixture(0, 0, true));
    await page.screenshot({ path: 'artifacts/browser-check/objective-idle.png' });
    assert.notDeepEqual(await page.locator('#canvas').screenshot(), active);
    assert.deepEqual(errors, []);
    console.log(
      'PASS: packed scan target/progress handoff and enlarged station cores render in WASM; cancellation and reduced-effects screenshots captured.',
    );
  } finally {
    await browser.close();
  }
})().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});
