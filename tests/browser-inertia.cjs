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
    await page.keyboard.down('a');
    await page.waitForTimeout(500);
    await page.keyboard.up('a');
    const released = await read();
    assert(released.velocityX < -290);
    await page.waitForTimeout(220);
    const coast = await read();
    assert(coast.x < released.x - 30);
    assert(coast.velocityX > released.velocityX && coast.velocityX < -100);
    await page.waitForTimeout(1200);
    const stopped = await read();
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
      'PASS: acceleration, release coasting, full stop, Shift has no action, Escape immobilizes without equipment cloak, icon activation.',
    );
  } finally {
    await browser.close();
  }
})().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});
