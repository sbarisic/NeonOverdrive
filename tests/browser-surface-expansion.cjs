const assert = require('node:assert/strict');
const { chromium } = require('../artifacts/browser-check/node_modules/playwright');
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    const errors = [];
    page.on('pageerror', (e) => errors.push(String(e)));
    await page.addInitScript(() => {
      const Native = window.WebSocket;
      window.WebSocket = class extends Native {
        addEventListener(type, handler, options) {
          if (type !== 'message') return super.addEventListener(type, handler, options);
          return super.addEventListener(
            type,
            (event) => {
              const frame = JSON.parse(event.data);
              if (frame.type === 'welcome') {
                window.__sector = frame.sector;
                window.__pilot = frame.id;
              }
              if (frame.type === 'snapshot') {
                if (frame.sector) window.__lastSector = structuredClone(frame.sector);
                else frame.sector = structuredClone(window.__lastSector);
              }
              if (frame.type === 'snapshot' && window.__surfaceFixture) {
                const planet = window.__sector.locations.find((l) => l.id === 1);
                const self = frame.players.find((p) => p.id === window.__pilot);
                self.x = planet.x + 1470;
                self.y = planet.y;
                self.velocityX = self.velocityY = 0;
                frame.enemies = [
                  {
                    id: 900,
                    kind: 'rover',
                    faction: 'friendly',
                    x: planet.x + 1194,
                    y: planet.y,
                    hull: 2,
                    maxHull: 2,
                    shield: 2,
                    maxShield: 2,
                    surfacePlanet: 1,
                    aim: 0,
                    radius: 13,
                    level: 1,
                    state: 'patrol',
                    home: 80,
                  },
                  {
                    id: 901,
                    kind: 'turret',
                    faction: 'hostile',
                    x: planet.x + 1170,
                    y: planet.y - 225,
                    hull: 4,
                    maxHull: 4,
                    shield: 0,
                    maxShield: 0,
                    surfacePlanet: 1,
                    aim: 0,
                    radius: 16,
                    level: 1,
                    state: 'engage',
                    home: 20,
                  },
                  {
                    id: 902,
                    kind: 'relief',
                    faction: 'friendly',
                    x: planet.x + 1650,
                    y: planet.y - 200,
                    hull: 180,
                    maxHull: 360,
                    shield: 45,
                    maxShield: 180,
                    surfacePlanet: 0,
                    aim: 0,
                    radius: 32,
                    level: 3,
                    state: 'engage',
                    home: 22,
                  },
                ];
                frame.enemies = frame.enemies.map((e) => ({ telegraph: false, effects: 0, ...e }));
                frame.sector.rescue = {
                  id: 1,
                  ship: 902,
                  state: window.__resolved || 'active',
                  x: planet.x + 1650,
                  y: planet.y - 200,
                  hull: 180,
                  maxHull: 360,
                  shield: 45,
                  maxShield: 180,
                  remaining: window.__resolved === 'rescued' ? 0 : 6,
                  level: 3,
                };
              }
              handler(new MessageEvent('message', { data: JSON.stringify(frame) }));
            },
            options,
          );
        }
      };
    });
    await page.goto(process.env.ARENA_URL || 'http://localhost:5094');
    await page.locator('#nickname').fill('Surface' + Date.now().toString(36));
    await page.getByRole('button', { name: 'Join arena', exact: true }).click();
    await page.waitForFunction(() => window.__sector);
    const dimensions = await page.evaluate(() => [
      window.__sector.width,
      window.__sector.height,
      window.__sector.locations.filter((l) => l.solid).length,
    ]);
    assert.deepEqual(dimensions, [24000, 16000, 3]);
    await page.locator('#canvas').focus();
    await page.keyboard.press('i');
    assert(await page.locator('#weapon-slot-1').isEnabled());
    assert.match(await page.locator('#weapon-slot-1').innerText(), /Level 2/);
    await page.keyboard.press('Escape');
    await page.evaluate(() => (window.__surfaceFixture = true));
    await page.waitForFunction(
      () =>
        document.querySelector('#radar-rescue') && !document.querySelector('#radar-rescue').hidden,
    );
    await page.locator('#radar-rescue').click();
    assert.match(await page.locator('#radar-controls').getAttribute('data-waypoint'), /Emergency/);
    assert.match(await page.locator('#sector-guide').innerText(), /6 attackers/);
    await page.waitForTimeout(600);
    await page.screenshot({ path: 'artifacts/browser-check/surface-combat.png' });
    await page.locator('#canvas').focus();
    await page.keyboard.press('m');
    const beacon = page.getByRole('button', {
      name: 'Emergency: save the relief ship',
      exact: true,
    });
    assert.equal(await beacon.count(), 1);
    await beacon.click();
    assert.match(await page.locator('.chart-detail').innerText(), /180\/360 hull/);
    await page
      .locator('.chart-detail')
      .getByRole('button', { name: 'Set waypoint', exact: true })
      .click();
    await page.screenshot({ path: 'artifacts/browser-check/surface-rescue-map.png' });
    await page.evaluate(() => (window.__resolved = 'rescued'));
    await beacon.waitFor({ state: 'detached' });
    assert.equal(await page.locator('#radar-controls').getAttribute('data-waypoint'), '');
    await page.waitForFunction(() =>
      document.querySelector('#sector-guide').textContent.includes('RESCUE COMPLETE'),
    );
    assert.match(await page.locator('#sector-guide').innerText(), /credit and XP pickups/);
    await page.evaluate(() => (window.__resolved = 'failed'));
    await page.waitForFunction(() =>
      document.querySelector('#sector-guide').textContent.includes('RESCUE FAILED'),
    );
    assert.deepEqual(errors, []);
    console.log(
      'PASS: solid world, gated sockets, friendly surface rendering, clickable radio beacons, rescue success/reward guidance and failure cleanup.',
    );
  } finally {
    await browser.close();
  }
})().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});
