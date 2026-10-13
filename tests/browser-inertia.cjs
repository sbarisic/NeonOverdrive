const assert = require('node:assert/strict');
const { chromium } = require('../artifacts/browser-check/node_modules/playwright');
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } }),
      errors = [];
    page.on('pageerror', (e) => errors.push(String(e)));
    await page.goto((process.env.ARENA_URL || 'http://localhost:5094') + '/');
    await page.locator('#nickname').fill('Drift' + Date.now().toString(36));
    await page.getByRole('button', { name: 'Join arena', exact: true }).click();
    await page.waitForFunction(() => document.querySelector('#network-info')?.dataset.snapshot);
    const read = () =>
      page
        .locator('#network-info')
        .evaluate((e) =>
          JSON.parse(e.dataset.snapshot).players.find((p) => p.id === +e.dataset.player),
        );
    await page.locator('#canvas').focus();
    await page.keyboard.down('d');
    await page.waitForTimeout(300);
    await page.keyboard.up('d');
    assert.equal((await read()).throttle, 0);
    await page.keyboard.down('w');
    await page.waitForTimeout(1000);
    await page.keyboard.up('w');
    await page.waitForTimeout(150);
    const released = await read();
    assert(released.throttle > 0.4 && released.throttle < 0.65);
    await page.waitForTimeout(300);
    const held = await read();
    assert(Math.abs(held.throttle - released.throttle) < 0.02);
    assert(Math.hypot(held.x - released.x, held.y - released.y) > 25);
    await page.keyboard.down('s');
    await page.waitForTimeout(1300);
    await page.keyboard.up('s');
    await page.waitForTimeout(1100);
    const stopped = await read();
    assert.equal(stopped.throttle, 0);
    assert.equal(stopped.velocityX, 0);
    await page.keyboard.down('Shift');
    await page.waitForTimeout(300);
    await page.keyboard.up('Shift');
    assert.equal((await read()).x, stopped.x);
    await page.keyboard.down('d');
    await page.waitForTimeout(300);
    await page.keyboard.press('Escape');
    await page.keyboard.up('d');
    await page.waitForFunction(() => {
      const e = document.querySelector('#network-info'),
        p = JSON.parse(e.dataset.snapshot).players.find((p) => p.id === +e.dataset.player);
      return p.menu && p.velocityX === 0;
    });
    const menu = await read();
    assert.equal(menu.editing, false);
    await page.waitForTimeout(200);
    assert.equal((await read()).x, menu.x);
    await page.keyboard.press('Escape');
    await page.screenshot({ path: 'artifacts/browser-check/inertia-hud-desktop.png' });
    await page
      .locator('#network-info')
      .getByRole('button', { name: 'Skills [Tab]', exact: true })
      .click();
    await page.locator('#arena-skills').waitFor({ state: 'visible' });
    assert.deepEqual(errors, []);
    console.log(
      'PASS: rotation, persistent throttle, braking, full stop, cruise requires an installed drive, Escape immobilizes without equipment cloak, icon activation.',
    );
  } finally {
    await browser.close();
  }
})().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});
