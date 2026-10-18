const assert = require('node:assert/strict');
const { chromium } = require('../artifacts/browser-check/node_modules/playwright');
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage({
        viewport: { width: 1280, height: 720 },
        deviceScaleFactor: +(process.env.FLIGHT_DPR || 1),
      }),
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
    await page.keyboard.down('q');
    await page.waitForTimeout(1000);
    await page.keyboard.up('q');
    await page.waitForTimeout(150);
    const released = await read();
    assert(released.throttle > 0.4 && released.throttle < 0.65);
    await page.waitForTimeout(300);
    const held = await read();
    assert(Math.abs(held.throttle - released.throttle) < 0.02);
    assert(Math.hypot(held.x - released.x, held.y - released.y) > 5);
    await page.keyboard.down('w');
    await page.waitForTimeout(250);
    assert(Math.abs((await read()).throttle - held.throttle) < 0.02, 'W preserves saved throttle');
    assert.match(await page.locator('#flight-throttle').textContent(), /100%.*saved/);
    await page.keyboard.up('w');
    await page.waitForTimeout(100);
    assert(
      Math.abs((await read()).throttle - held.throttle) < 0.02,
      'W release restores saved throttle',
    );
    assert(await page.locator('#flight-throttle').isHidden());
    await page.keyboard.down('e');
    await page.waitForTimeout(1300);
    await page.keyboard.up('e');
    await page.waitForTimeout(1100);
    const coasting = await read();
    assert.equal(coasting.throttle, 0);
    assert(Math.hypot(coasting.velocityX, coasting.velocityY) > 20);
    await page.keyboard.down('d');
    await page.waitForTimeout(750);
    await page.keyboard.up('d');
    const rotated = await read();
    assert(Math.abs(rotated.velocityX - coasting.velocityX) < 1);
    assert(Math.abs(rotated.velocityY - coasting.velocityY) < 1);
    await page.keyboard.down('s');
    await page.waitForTimeout(100);
    assert.equal((await read()).braking, true);
    await page.waitForTimeout(850);
    await page.keyboard.up('s');
    await page.waitForTimeout(100);
    const stopped = await read();
    assert.equal(stopped.throttle, 0);
    assert.equal(stopped.velocityX, 0);
    assert.equal(stopped.velocityY, 0);
    assert.equal(stopped.braking, false);
    // Pointer hold, release outside the button, and keyboard activation all release safely.
    const brake = page.locator('#flight-brake');
    const box = await brake.boundingBox();
    assert(box && box.width > 50 && box.height >= 30);
    const layout = JSON.parse(await page.locator('#network-info').getAttribute('data-layout'));
    assert(box.x >= layout[6] && box.x + box.width <= layout[6] + layout[8]);
    assert(box.y >= layout[7] && box.y + box.height <= layout[7] + layout[9]);
    await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
    await page.mouse.down();
    await page.waitForTimeout(100);
    assert.equal((await read()).braking, true);
    await page.mouse.move(box.x + box.width + 100, box.y - 100);
    await page.mouse.up();
    await page.waitForTimeout(100);
    assert.equal((await read()).braking, false);
    await brake.focus();
    await page.keyboard.down('Space');
    await page.waitForTimeout(100);
    assert.equal((await read()).braking, true);
    await page.keyboard.up('Space');
    await page.waitForTimeout(100);
    assert.equal((await read()).braking, false);
    await page.locator('#canvas').focus();
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
