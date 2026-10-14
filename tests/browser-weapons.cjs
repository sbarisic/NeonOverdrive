// Real server equipment persistence, dormant mods, whole-weapon swaps, recovery and sales.
const assert = require('node:assert/strict');
const { chromium } = require('../artifacts/browser-check/node_modules/playwright');
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1280, height: 720 } }),
      errors = [];
    page.setDefaultTimeout(10000);
    page.setDefaultNavigationTimeout(10000);
    page.on('pageerror', (e) => errors.push(String(e)));
    const nickname = 'Weapon' + Date.now().toString(36);
    await page.goto(process.env.ARENA_URL || 'http://localhost:5094');
    await page.locator('#nickname').fill(nickname);
    await page.getByRole('button', { name: 'Join arena', exact: true }).click();
    await page.waitForFunction(() => document.querySelector('#network-info')?.dataset.snapshot);
    const read = () =>
      page.locator('#network-info').evaluate((el) => {
        const f = JSON.parse(el.dataset.snapshot);
        return { self: f.players.find((p) => p.id === +el.dataset.player), profile: f.workshop };
      });
    await page.locator('#canvas').focus();
    await page.keyboard.press('e');
    if (await page.getByRole('button', { name: 'Dock at Arrival Port', exact: true }).isVisible())
      await page.getByRole('button', { name: 'Dock at Arrival Port', exact: true }).click();
    await page
      .locator('#sector-console')
      .getByRole('button', { name: 'Module shop', exact: true })
      .click();
    const panel = page.locator('#weapon-bench');
    assert.equal(await panel.locator('.shop-card').count(), 28);
    await panel.getByRole('button', { name: 'Buy Accelerator', exact: true }).click();
    await page.waitForFunction(
      () =>
        JSON.parse(document.querySelector('#network-info').dataset.snapshot).workshop.owned[6] ===
        1,
    );
    assert.equal((await read()).profile.credits, 40);
    await panel.getByRole('tab', { name: 'Loadout', exact: true }).click();
    assert.equal(await panel.locator('.weapon-slot').count(), 9);
    assert.equal(await panel.locator('.ship-socket').count(), 6);
    assert(await panel.locator('#weapon-slot-1').isEnabled());
    assert.match(await panel.locator('#weapon-slot-1').innerText(), /Level 2/);
    await panel.locator('.inventory-cell[data-module-id="6"]').click();
    await panel.locator('#weapon-slot-1').click();
    await panel.getByRole('button', { name: 'Apply loadout', exact: true }).click();
    await page.waitForFunction(
      () =>
        JSON.parse(document.querySelector('#network-info').dataset.snapshot).workshop.weapons[0]
          .attachments[0] === 6,
    );
    const equipped = (await read()).profile.inventory.bays[0].weapon;
    assert.match(await panel.locator('.weapon-heading').innerText(), /0 ACTIVE \/ 1 INSTALLED/);
    await panel.locator('#weapon-slot-0').click({ button: 'right' });
    await panel.getByRole('button', { name: 'Apply loadout', exact: true }).click();
    await page.waitForFunction(
      () =>
        JSON.parse(document.querySelector('#network-info').dataset.snapshot).workshop.inventory
          .bays[0].weapon === null,
    );
    const built = panel.locator(`[data-item-key="${equipped}"]`);
    await built.click();
    await panel.locator('#weapon-slot-0').click();
    await panel.getByRole('button', { name: 'Apply loadout', exact: true }).click();
    await page.waitForFunction(
      () =>
        JSON.parse(document.querySelector('#network-info').dataset.snapshot).workshop.weapons[0]
          .attachments[0] === 6,
    );
    assert.equal((await read()).profile.inventory.bays[0].weapon, equipped);
    const before = (await read()).self;
    await page.keyboard.down('w');
    await page.waitForTimeout(200);
    await page.keyboard.up('w');
    assert.equal((await read()).self.x, before.x);
    assert((await read()).self.editing);
    await page.reload();
    await page.getByRole('button', { name: 'Join arena', exact: true }).click();
    await page.waitForFunction(() => document.querySelector('#network-info')?.dataset.snapshot);
    assert.equal((await read()).profile.inventory.bays[0].weapon, equipped);
    assert.equal((await read()).profile.weapons[0].attachments[0], 6);
    await page
      .locator('#sector-console')
      .getByRole('button', { name: 'Arsenal', exact: true })
      .click();
    await panel.locator('#weapon-slot-1').click({ button: 'right' });
    await panel.getByRole('button', { name: 'Apply loadout', exact: true }).click();
    await page.waitForFunction(
      () =>
        JSON.parse(document.querySelector('#network-info').dataset.snapshot).workshop.inventory
          .loose[6] === 1,
    );
    await panel.locator('.inventory-cell[data-module-id="6"]').click();
    await panel.getByRole('button', { name: 'Sell selected', exact: true }).click();
    await page.waitForFunction(
      () =>
        JSON.parse(document.querySelector('#network-info').dataset.snapshot).workshop.credits ===
        55,
    );
    assert.equal((await read()).profile.inventory.loose[6], 0);
    await page.screenshot({ path: 'artifacts/browser-check/equipment-720.png' });
    assert.deepEqual(errors, []);
    console.log(
      'PASS: live weapon identity, dormant mods, whole-build swap, recovery, personal inventory and station sale.',
    );
  } finally {
    await browser.close();
  }
})().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});
