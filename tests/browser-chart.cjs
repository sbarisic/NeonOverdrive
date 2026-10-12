const assert = require('node:assert/strict');
const { chromium } = require('../artifacts/browser-check/node_modules/playwright');
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } }),
      errors = [];
    page.on('pageerror', (e) => errors.push(String(e)));
    await page.goto((process.env.ARENA_URL || 'http://localhost:5094') + '/');
    await page.locator('#nickname').fill('Chart' + Date.now().toString(36));
    await page.getByRole('button', { name: 'Join arena', exact: true }).click();
    await page.waitForFunction(() => document.querySelector('#network-info')?.dataset.snapshot);
    await page.locator('#canvas').focus();
    await page.keyboard.press('m');
    const panel = page.locator('#sector-console'),
      detail = panel.locator('.chart-detail');
    await panel.getByRole('button', { name: 'Arrival Port', exact: true }).click();
    assert.match(await detail.innerText(), /Arrival Port/);
    assert.match(await detail.innerText(), /WAYPOINT · None/);
    await detail.getByRole('button', { name: 'Set waypoint', exact: true }).click();
    assert.match(await page.locator('#sector-guide').innerText(), /Arrival Port/);
    await panel.getByRole('button', { name: 'Cinder Hostile Array', exact: true }).click();
    assert.match(await detail.innerText(), /Level 2/);
    assert.match(await detail.innerText(), /80 credits/);
    assert.match(await page.locator('#sector-guide').innerText(), /Arrival Port/);
    await page.screenshot({ path: 'artifacts/browser-check/chart-desktop.png' });
    await panel.getByRole('button', { name: 'Cinder Hostile Array', exact: true }).press('Enter');
    assert.match(await page.locator('#sector-guide').innerText(), /Cinder Hostile Array/);
    await panel
      .getByRole('button', { name: 'Arrival Port', exact: true })
      .click({ button: 'right' });
    await panel
      .locator('.chart-context')
      .getByRole('button', { name: 'Set waypoint', exact: true })
      .click();
    assert.match(await page.locator('#sector-guide').innerText(), /Arrival Port/);
    await panel.getByRole('button', { name: 'Zoom in', exact: true }).click();
    assert.equal(await panel.getByRole('button', { name: '125%', exact: true }).count(), 1);
    await panel.getByRole('button', { name: 'Fit sector', exact: true }).click();
    await panel
      .locator('.chart-filters')
      .getByRole('button', { name: 'Salvage', exact: true })
      .click();
    assert.equal(await panel.locator('.map-location[aria-label^="Salvage"]:visible').count(), 0);
    await panel.locator('.chart-view').focus();
    const before = await panel
      .getByRole('button', { name: 'Arrival Port', exact: true })
      .boundingBox();
    await page.keyboard.press('d');
    const after = await panel
      .getByRole('button', { name: 'Arrival Port', exact: true })
      .boundingBox();
    assert(after.x < before.x);
    await panel.getByRole('button', { name: 'Fit sector', exact: true }).click();
    await page.setViewportSize({ width: 1280, height: 720 });
    await page.waitForTimeout(200);
    assert(await panel.locator('.chart-detail').isVisible());
    await page.screenshot({ path: 'artifacts/browser-check/chart-720p.png' });
    await detail.getByRole('button', { name: 'Clear waypoint', exact: true }).click();
    assert.match(await detail.innerText(), /WAYPOINT · None/);
    assert.deepEqual(errors, []);
    console.log(
      'PASS: inspection, explicit/keyboard/context waypoints, regional details, zoom, pan, filters, clearing, desktop layouts, no page errors.',
    );
  } finally {
    await browser.close();
  }
})().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});
