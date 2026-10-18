const assert = require('node:assert/strict');
const { chromium } = require('../artifacts/browser-check/node_modules/playwright');

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  const origin = process.env.ARENA_URL || 'http://localhost:5098';
  try {
    for (const density of process.env.TEST_DPR ? [+process.env.TEST_DPR] : [1, 2]) {
      const page = await browser.newPage({
        viewport: { width: 1280, height: 720 },
        deviceScaleFactor: density,
      });
      const errors = [];
      page.on('pageerror', (e) => errors.push(String(e)));
      await page.addInitScript(() => {
        const send = WebSocket.prototype.send;
        WebSocket.prototype.send = function (text) {
          const input = JSON.parse(text);
          const info = document.querySelector('#network-info');
          if (window.stageAutopilot && input.type === 'input' && info?.dataset.snapshot) {
            const frame = JSON.parse(info.dataset.snapshot),
              p = frame.players.find((p) => p.id === +info.dataset.player),
              race = frame.sector.world.race;
            if (race?.state === 'staging') {
              // Fly a clear departure dogleg around the named contacts at Arrival.
              window.stageRoute ||= [
                [p.x, p.y - 200],
                [race.stageX, race.stageY - 200],
                [race.stageX, race.stageY],
              ];
              if (
                window.stageRoute.length > 1 &&
                Math.hypot(window.stageRoute[0][0] - p.x, window.stageRoute[0][1] - p.y) < 18
              )
                window.stageRoute.shift();
              const dx = window.stageRoute[0][0] - p.x,
                dy = window.stageRoute[0][1] - p.y,
                distance = Math.hypot(dx, dy);
              const speed = Math.min(65, Math.sqrt(60 * Math.max(0, distance - 8)));
              const cx = (dx / Math.max(0.01, distance)) * speed - (p.velocityX || 0),
                cy = (dy / Math.max(0.01, distance)) * speed - (p.velocityY || 0);
              input.brake = distance < 15;
              input.aim =
                input.brake && window.stageRoute.length === 1
                  ? race.stageHeading
                  : Math.atan2(cy, cx);
              input.cursorSteering = true;
              input.x = 0;
              const angle = Math.atan2(
                Math.sin(input.aim - (p.heading || 0)),
                Math.cos(input.aim - (p.heading || 0)),
              );
              input.brake ||=
                Math.hypot(p.velocityX || 0, p.velocityY || 0) > speed + 5 ||
                (Math.abs(angle) > 0.6 && Math.hypot(p.velocityX || 0, p.velocityY || 0) > 20);
              const throttle =
                !input.brake && Math.abs(angle) < 0.45
                  ? Math.min(1, (2 * Math.hypot(cx, cy)) / 110)
                  : 0;
              input.y = Math.max(-1, Math.min(1, ((p.throttle || 0) - throttle) * 60));
              input.fire = input.menu = input.editing = false;
            }
          }
          return send.call(this, JSON.stringify(input));
        };
      });
      await page.goto(origin);
      await page.locator('#nickname').fill('Fleet' + density + Date.now().toString(36));
      await page.getByRole('button', { name: 'Join arena', exact: true }).click();
      await page.waitForFunction(() => document.querySelector('#network-info')?.dataset.snapshot);
      const read = () =>
        page.locator('#network-info').evaluate((e) => {
          const frame = JSON.parse(e.dataset.snapshot);
          return { frame, self: frame.players.find((p) => p.id === +e.dataset.player) };
        });
      const initial = await read();
      assert.equal(initial.frame.v, 16);
      assert.equal(initial.self.chassis, 0);
      assert.equal(initial.self.moveSpeed, 220);
      assert.equal(initial.frame.sector.world.territory.length, 8);
      await page.locator('#radio-toggle').click();
      const panel = page.locator('#radio-console');
      await panel.waitFor({ state: 'visible' });
      const contact = initial.frame.sector.world.contacts.find((c) => c.role === 'trader');
      assert(contact);
      await panel.getByLabel('Radio contact').selectOption(String(contact.id));
      await page.waitForFunction(
        (id) =>
          JSON.parse(document.querySelector('#network-info').dataset.snapshot).sector.world
            .conversation === id,
        contact.id,
      );
      assert.equal((await read()).self.editing, false, 'remote radio grants no cloak');
      const originalTrader = initial.frame.sector.services.contacts.find(
        (c) => c.role === 'trader',
      );
      await panel.getByLabel('Radio contact').selectOption(String(originalTrader.id));
      await panel.getByRole('button', { name: 'Close-range trade', exact: true }).click();
      await panel.getByRole('button', { name: 'Open module shop', exact: true }).waitFor();
      await page.waitForFunction(() => {
        const node = document.querySelector('#network-info');
        const frame = JSON.parse(node.dataset.snapshot);
        return frame.players.find((p) => p.id === +node.dataset.player).editing;
      });
      await panel.getByRole('button', { name: 'Hail / banter', exact: true }).click();
      await page.waitForFunction(() => {
        const node = document.querySelector('#network-info');
        const frame = JSON.parse(node.dataset.snapshot);
        return (
          !frame.sector.services.hailing &&
          !frame.players.find((p) => p.id === +node.dataset.player).editing
        );
      });
      await page.evaluate(() => (window.stageAutopilot = true));
      await panel.getByRole('button', { name: 'Sprint', exact: true }).click();
      await page
        .waitForFunction(
          () =>
            JSON.parse(document.querySelector('#network-info').dataset.snapshot).sector.world.race
              ?.state === 'countdown',
          null,
          { timeout: 65000 },
        )
        .catch(async (error) => {
          console.log(JSON.stringify(await read()));
          throw error;
        });
      await page.evaluate(() => (window.stageAutopilot = false));
      const staged = await read();
      await page.locator('#canvas').focus();
      await page.keyboard.down('w');
      await page.waitForTimeout(700);
      await page.keyboard.up('w');
      const held = await read();
      assert.equal(held.self.x, staged.self.x);
      assert.equal(held.self.y, staged.self.y);
      assert.equal(held.self.throttle, 0);
      assert(held.self.protected);
      await panel.getByRole('button', { name: 'Leave race', exact: true }).click();
      await page.waitForFunction(
        () =>
          JSON.parse(document.querySelector('#network-info').dataset.snapshot).sector.world.race
            ?.state === 'abandoned',
      );
      await page.screenshot({ path: `artifacts/browser-check/living-sector-${density}x.png` });
      const bounds = await panel.boundingBox();
      assert(bounds.x >= 0 && bounds.x + bounds.width <= 1280 && bounds.y + bounds.height <= 720);
      assert.deepEqual(errors, []);
      await page.close();
    }
    console.log(
      'PASS: v16 catalogs, keyboard contact selection, nonmodal radio, synchronized race staging, cancellation, bounded desktop panel at 1x/2x density.',
    );
  } finally {
    await browser.close();
  }
})().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});
