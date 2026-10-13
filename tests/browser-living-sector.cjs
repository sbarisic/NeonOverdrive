const assert = require('node:assert/strict');
const { chromium } = require('../artifacts/browser-check/node_modules/playwright');

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  const origin = process.env.ARENA_URL || 'http://localhost:5098';
  try {
    for (const density of [1, 2]) {
      const page = await browser.newPage({
        viewport: { width: 1280, height: 720 },
        deviceScaleFactor: density,
      });
      const errors = [];
      page.on('pageerror', (e) => errors.push(String(e)));
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
      assert.equal(initial.frame.v, 12);
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
      await panel.getByRole('button', { name: 'Sprint', exact: true }).click();
      await page.waitForFunction(
        () =>
          JSON.parse(document.querySelector('#network-info').dataset.snapshot).sector.world.race
            ?.state === 'countdown',
      );
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
      'PASS: v12 catalogs, keyboard contact selection, nonmodal radio, synchronized race staging, cancellation, bounded desktop panel at 1x/2x density.',
    );
  } finally {
    await browser.close();
  }
})().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});
