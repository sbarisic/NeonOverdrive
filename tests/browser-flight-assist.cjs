const assert = require('node:assert/strict');
const { chromium } = require('../artifacts/browser-check/node_modules/playwright');

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    for (const deviceScaleFactor of [1, 2]) {
      const page = await browser.newPage({
        viewport: { width: 1280, height: 720 },
        deviceScaleFactor,
      });
      const errors = [];
      page.on('pageerror', (error) => errors.push(String(error)));
      await page.route('**/main.js*', async (route) => {
        const response = await route.fetch();
        await route.fulfill({
          response,
          body: (await response.text())
            .replace('startNetwork(host, canvas, status);', '/* isolated forecast fixture */')
            .replace(
              'const host = exports.ArcadeDodger.Web.BrowserHost;',
              'const host = exports.ArcadeDodger.Web.BrowserHost; window.host = host;',
            ),
        });
      });
      await page.goto(process.env.ARENA_URL || 'http://127.0.0.1:5126');
      await page.waitForFunction(() => window.host && document.querySelector('#status').hidden);
      await page.evaluate(() => {
        host.ActivateArena();
        host.ArenaWelcome(1);
        window.sector = {
          seed: 1,
          width: 24000,
          height: 16000,
          gravityRange: 600,
          gravityAcceleration: 45,
          locations: [
            {
              id: 1,
              kind: 'planet',
              name: 'Orbit test',
              x: 2000,
              y: 2000,
              radius: 50,
              solid: true,
            },
          ],
          obstacles: [],
        };
        host.ArenaSector(JSON.stringify(sector));
        window.pilot = {
          id: 1,
          name: 'Pilot',
          x: 2150,
          y: 2000,
          aim: 0,
          heading: 0,
          hull: 3,
          shield: 2,
          maxHull: 3,
          maxShield: 2,
          connected: true,
          runId: 'assist',
          velocityX: 0,
          velocityY: Math.sqrt(45 * (1 - 100 / 600) ** 2 * 150),
          moveSpeed: 220,
          acceleration: 110,
          steering: 165,
          turnRate: 2.094,
          throttle: 0,
          detectionRadius: 1200,
          surveyRadius: 1200,
          level: 1,
          activeBays: 1,
          weapons: [{ emitter: 1, attachments: [0, 0, 0, 0, 0, 0, 0, 0], enabled: true }],
          weaponStats: [{ emitter: 1, speed: 650, range: 1300 }],
        };
        window.enemies = [
          {
            id: 10,
            kind: 'scout',
            x: 2350,
            y: 2000,
            velocityX: 0,
            velocityY: 80,
            hull: 3,
            maxHull: 3,
            radius: 18,
            faction: 'hostile',
          },
          {
            id: 11,
            kind: 'scout',
            x: 2400,
            y: 2200,
            velocityX: 0,
            velocityY: 0,
            hull: 3,
            maxHull: 3,
            radius: 18,
            faction: 'hostile',
          },
        ];
        let tick = 1;
        window.feed = () => {
          host.ArenaSnapshot(
            JSON.stringify({
              v: 16,
              type: 'snapshot',
              tick: tick++,
              players: [pilot],
              shots: [],
              enemies,
            }),
          );
          host.ArenaInput(0, 0, 0, false, false, false, false);
        };
        window.feeder = setInterval(feed, 33);
        feed();
        window.assist = () => JSON.parse(host.ArenaFlightAssist());
        window.aimAt = (id) => {
          const contact = JSON.parse(host.ArenaContacts()).find((c) => c.key === 'npc:' + id);
          host.ArenaAim(contact.u, contact.v);
        };
      });
      await page.waitForFunction(() => assist().coast === 'Orbit');
      assert.match(await page.evaluate(() => assist().orbit), /RELEASE W/);
      await page.screenshot({
        path: `artifacts/browser-check/flight-orbit-${deviceScaleFactor}x.png`,
      });
      await page.evaluate(() => aimAt(10));
      await page.waitForFunction(() => assist().target === 10 && assist().y > 2000);
      await page.screenshot({
        path: `artifacts/browser-check/flight-lead-${deviceScaleFactor}x.png`,
      });
      await page.evaluate(() => {
        const prediction = assist();
        const contact = JSON.parse(host.ArenaContacts()).find((c) => c.key === 'npc:10');
        const enemy = enemies.find((e) => e.id === 10);
        const factor = contact.radius / enemy.radius;
        host.ArenaAim(
          contact.u + (prediction.x - enemy.x) * factor,
          contact.v + ((prediction.y - enemy.y) * factor * 1280) / 720,
        );
        enemies[1].x = prediction.x;
        enemies[1].y = prediction.y;
      });
      await page.waitForTimeout(250);
      assert.equal(
        await page.evaluate(() => assist().target),
        10,
        'lead reticle retains its target even near another hull',
      );
      await page.evaluate(() => {
        enemies[1].x = 2400;
        enemies[1].y = 2200;
      });
      await page.evaluate(() => host.ArenaAim(0.95, 0.1));
      await page.waitForTimeout(250);
      assert.equal(await page.evaluate(() => assist().target), 10, 'aiming ahead retains target');
      await page.evaluate(() => aimAt(11));
      await page.waitForFunction(() => assist().target === 11);
      await page.evaluate(() => {
        enemies = enemies.filter((e) => e.id !== 11);
        host.ArenaAim(0.95, 0.1);
      });
      await page.waitForFunction(() => assist().target === 0);
      await page.evaluate(() => {
        pilot.velocityX = -100;
        pilot.velocityY = 0;
      });
      await page.waitForFunction(() => assist().release === 'Collision');
      assert.match(await page.evaluate(() => assist().orbit), /COLLISION/);
      await page.screenshot({
        path: `artifacts/browser-check/flight-assist-${deviceScaleFactor}x.png`,
      });
      await page.evaluate(() => host.ArenaPresentation(true, false));
      await page.waitForFunction(() => assist().orbit === '' && assist().target === 0);
      assert.deepEqual(errors, []);
      await page.close();
    }
    console.log(
      'PASS: full-circuit green coast forecast, collision warning, sticky cursor acquisition/switch/loss, lead solution and modal suppression at DPR 1/2.',
    );
  } finally {
    await browser.close();
  }
})().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
