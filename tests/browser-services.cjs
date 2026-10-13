const assert = require('node:assert/strict');
const { chromium } = require('../artifacts/browser-check/node_modules/playwright');
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
    const errors = [];
    page.on('pageerror', (e) => errors.push(String(e)));
    await page.goto(process.env.ARENA_URL || 'http://localhost:5096');
    await page.locator('#nickname').fill('Contact' + Date.now().toString(36));
    await page.getByRole('button', { name: 'Join arena', exact: true }).click();
    await page.waitForFunction(() => document.querySelector('#network-info')?.dataset.snapshot);
    const state = () =>
      page.locator('#network-info').evaluate((e) => JSON.parse(e.dataset.snapshot));
    assert.equal((await state()).v, 12);
    assert.equal((await state()).sector.services.contacts.length, 3);
    const ownId = +(await page.locator('#network-info').getAttribute('data-player'));
    assert.equal((await state()).players.find((p) => p.id === ownId).playthrough, 1);
    await page.locator('#canvas').focus();
    await page.keyboard.press('e');
    const trader = (await state()).sector.services.contacts.find((c) => c.role === 'trader');
    await page
      .locator('.service-board')
      .getByRole('button', { name: 'Hail ' + trader.name, exact: true })
      .click();
    await page.getByRole('button', { name: 'Chat', exact: true }).waitFor();
    await page.waitForFunction(() => {
      const f = JSON.parse(document.querySelector('#network-info').dataset.snapshot);
      const own = f.players.find(
        (p) => p.id === +document.querySelector('#network-info').dataset.player,
      );
      return f.sector.services.hailing > 0 && own.editing && own.throttle === 0;
    });
    const before = await page.locator('.service-board').innerText();
    await page.getByRole('button', { name: 'Chat', exact: true }).click();
    assert.notEqual(await page.locator('.service-board').innerText(), before);
    const close = await page
      .getByRole('button', { name: 'Return [Esc]', exact: true })
      .boundingBox();
    assert(close && close.y + close.height <= 720);
    await page.screenshot({ path: 'artifacts/browser-check/contact-dialogue.png' });
    await page.getByRole('button', { name: 'Trade modules', exact: true }).click();
    await page
      .locator('#weapon-bench')
      .getByRole('button', { name: 'Buy Accelerator', exact: true })
      .click();
    await page.waitForFunction(() => {
      const f = JSON.parse(document.querySelector('#network-info').dataset.snapshot);
      return f.workshop.credits === 40 && f.sector.services.hailing > 0;
    });
    await page.keyboard.press('Escape');
    await page.waitForFunction(
      () =>
        !JSON.parse(document.querySelector('#network-info').dataset.snapshot).sector.services
          .hailing,
    );
    await page.locator('#canvas').focus();
    await page.keyboard.press('e');
    await page
      .locator('.service-board')
      .getByRole('button', { name: 'Hail ' + trader.name, exact: true })
      .click();
    await page.getByRole('button', { name: 'Goodbye', exact: true }).waitFor();
    await page.getByRole('button', { name: 'Goodbye', exact: true }).click();
    await page.waitForFunction(
      () =>
        !JSON.parse(document.querySelector('#network-info').dataset.snapshot).sector.services
          .hailing,
    );
    await page.locator('#canvas').focus();
    await page.keyboard.press('i');
    const colors = await page
      .locator('.ship-socket')
      .evaluateAll((nodes) => nodes.map((n) => getComputedStyle(n).borderTopColor));
    assert.equal(new Set(colors).size, 6);
    await page.screenshot({ path: 'artifacts/browser-check/equipment-colors.png' });
    await page.keyboard.press('Escape');
    await page.keyboard.press('m');
    assert((await page.locator('.map-location[data-kind="contact"]').count()) >= 3);
    assert.deepEqual(errors, []);
    console.log(
      'PASS: v12, playthrough, contextual hailing, authored chat, cloak/exit, desktop footer, six gear colors, roaming map contacts.',
    );
  } finally {
    await browser.close();
  }
})().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});
