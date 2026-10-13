const assert = require('node:assert/strict');
const { chromium } = require('../artifacts/browser-check/node_modules/playwright');
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } }),
      errors = [];
    page.on('pageerror', (e) => errors.push(String(e)));
    await page.goto((process.env.ARENA_URL || 'http://localhost:5088') + '/');
    const name = 'Sector_' + Date.now().toString(36);
    await page.locator('#nickname').fill(name);
    await page.getByRole('button', { name: 'Join arena', exact: true }).click();
    await page.waitForFunction(() => document.querySelector('#network-info')?.dataset.snapshot);
    const read = () =>
      page.locator('#network-info').evaluate((e) => {
        const f = JSON.parse(e.dataset.snapshot);
        return { f, p: f.players.find((p) => p.id === +e.dataset.player), s: f.sector.pilot };
      });
    await page.locator('#canvas').focus();
    await page.keyboard.press('i');
    const bench = page.locator('#weapon-bench');
    await bench.getByRole('tab', { name: 'Shop', exact: true }).click();
    assert(await bench.getByRole('button', { name: 'Buy Accelerator', exact: true }).isDisabled());
    assert.match(await bench.innerText(), /Dock or hail a trader/);
    await page.keyboard.press('Escape');
    await page.keyboard.press('e');
    if (await page.getByRole('button', { name: 'Dock at Arrival Port', exact: true }).isVisible())
      await page.getByRole('button', { name: 'Dock at Arrival Port', exact: true }).click();
    const panel = page.locator('#sector-console');
    await panel.waitFor({ state: 'visible' });
    await page.waitForFunction(
      () =>
        JSON.parse(document.querySelector('#network-info').dataset.snapshot).sector.pilot.docked ===
        10,
    );
    assert.equal(await panel.locator('.contract-card:visible').count(), 4);
    await panel.getByRole('button', { name: 'Accept contract', exact: true }).first().click();
    await page.waitForFunction(
      () =>
        JSON.parse(document.querySelector('#network-info').dataset.snapshot).sector.pilot
          .contract === 1,
    );
    await panel.getByRole('button', { name: 'Module shop', exact: true }).click();
    await bench.waitFor({ state: 'visible' });
    await bench.getByRole('button', { name: 'Buy Accelerator', exact: true }).click();
    await page.waitForFunction(
      () =>
        JSON.parse(document.querySelector('#network-info').dataset.snapshot).workshop.credits ===
        40,
    );
    await page.keyboard.press('Escape');
    await page.keyboard.press('m');
    await panel.waitFor({ state: 'visible' });
    assert.equal(
      await panel.locator('.map-location:visible').count(),
      await panel.locator('.map-location:not([data-kind=cache])').count(),
    );
    await panel.getByRole('button', { name: 'Arrival Port', exact: true }).click();
    await panel.getByRole('button', { name: 'Set waypoint', exact: true }).click();
    assert.match(await page.locator('#sector-guide').innerText(), /Arrival Port/);
    const focus = await page.evaluate(() => document.activeElement.textContent);
    await page.waitForTimeout(250);
    assert.equal(await page.evaluate(() => document.activeElement.textContent), focus);
    await page.screenshot({ path: 'artifacts/browser-check/sector-map.png' });
    await page.keyboard.press('Escape');
    await page.setViewportSize({ width: 1440, height: 1000 });
    await page.keyboard.press('e');
    if (await page.getByRole('button', { name: 'Dock at Arrival Port', exact: true }).isVisible())
      await page.getByRole('button', { name: 'Dock at Arrival Port', exact: true }).click();
    await page.waitForFunction(
      () =>
        JSON.parse(document.querySelector('#network-info').dataset.snapshot).sector.pilot.docked ===
        0,
    );
    await page.locator('#canvas').focus();
    await page.keyboard.down('w');
    await page.waitForTimeout(2900);
    await page.keyboard.up('w');
    const outside = await read();
    assert(outside.p.x > 1900);
    assert(!outside.p.protected);
    assert(outside.s.cells.length > 20);
    await page.screenshot({ path: 'artifacts/browser-check/sector-planet.png' });
    await page.keyboard.press('i');
    await page.waitForFunction(() => {
      const info = document.querySelector('#network-info');
      const pilot = JSON.parse(info.dataset.snapshot).players.find(
        (p) => p.id === +info.dataset.player,
      );
      return pilot.editing && pilot.velocityX === 0 && pilot.velocityY === 0;
    });
    const before = await read();
    await page.keyboard.down('w');
    await page.keyboard.down('Space');
    await page.waitForTimeout(300);
    await page.keyboard.up('w');
    await page.keyboard.up('Space');
    const cloaked = await read();
    assert(cloaked.p.editing && cloaked.p.protected);
    assert.equal(cloaked.p.x, before.p.x);
    await page.keyboard.press('Escape');
    const room = await page.locator('#network-info').getAttribute('data-room');
    await page.goto((process.env.ARENA_URL || 'http://localhost:5088') + '/?room=' + room);
    await page.waitForTimeout(6200);
    await page.getByRole('button', { name: 'Join arena', exact: true }).click();
    await page.waitForFunction(() => document.querySelector('#network-info')?.dataset.snapshot);
    const rejoined = await read();
    assert.equal(rejoined.s.contract, 1);
    assert.equal(rejoined.f.workshop.credits, 40);
    assert(rejoined.s.cells.length >= outside.s.cells.length);
    assert(Math.hypot(rejoined.p.x - before.p.x, rejoined.p.y - before.p.y) < 50);
    assert.deepEqual(errors, []);
    console.log(
      'PASS: station-only shop, E docking, purchase receipt, contract acceptance, M waypoint map, snapshot-stable focus, undocking and exploration, equipment cloak, room rejoin persistence, planet render, no page errors.',
    );
  } finally {
    await browser.close();
  }
})().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});
