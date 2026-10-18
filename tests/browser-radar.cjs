const assert = require('node:assert/strict');
const { interact } = require('./browser-service-actions.cjs');
const { chromium } = require('../artifacts/browser-check/node_modules/playwright');
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } }),
      errors = [];
    page.on('pageerror', (e) => errors.push(String(e)));
    await page.route('**/sector.js*', async (route) => {
      const response = await route.fetch();
      let s = (await response.text()).replaceAll('\r\n', '\n');
      s = s
        .replace(
          '  return {\n    trackRescue()',
          '  return window.__sectorFixture={\n    trackRescue()',
        )
        .replace(
          /update\(p,\s*snapshot,\s*connected\)\s*\{/,
          'update(p,snapshot,connected){if(window.__holdSector)return;',
        );
      await route.fulfill({ response, body: s });
    });
    await page.goto((process.env.ARENA_URL || 'http://localhost:5094') + '/');
    await page.locator('#nickname').fill('Radar' + Date.now().toString(36));
    await page.getByRole('button', { name: 'Join arena', exact: true }).click();
    await page.waitForFunction(() => document.querySelector('#network-info')?.dataset.snapshot);
    await page.locator('#canvas').focus();
    await interact(page);
    if (await page.getByRole('button', { name: 'Dock at Arrival Port', exact: true }).isVisible())
      await page.getByRole('button', { name: 'Dock at Arrival Port', exact: true }).click();
    await page
      .locator('#sector-console')
      .getByRole('button', { name: 'Accept contract', exact: true })
      .first()
      .click();
    await page.waitForFunction(
      () => document.querySelector('#radar-controls').dataset.waypoint === 'Orbital Survey 1',
    );
    await interact(page);
    if (await page.getByRole('button', { name: 'Dock at Arrival Port', exact: true }).isVisible())
      await page.getByRole('button', { name: 'Dock at Arrival Port', exact: true }).click();
    await page.locator('#sector-console').waitFor({ state: 'hidden' });
    assert.equal(
      await page.locator('#sector-guide').evaluate((e) => getComputedStyle(e).fontSize),
      '16px',
    );
    const zoom = () => page.locator('#radar-controls').getAttribute('data-zoom');
    await page.getByRole('button', { name: 'Minimap zoom in', exact: true }).click();
    assert.equal(await zoom(), '2');
    await page.getByRole('button', { name: 'Minimap zoom out', exact: true }).click();
    assert.equal(await zoom(), '1');
    await page.screenshot({ path: 'artifacts/browser-check/local-radar.png' });
    // Exercise client guidance for authoritative survey transitions, including non-sequential scans.
    const transition = async (progress, ready) =>
      page.evaluate(
        ({ progress, ready }) => {
          window.__holdSector = false;
          const e = document.querySelector('#network-info'),
            f = JSON.parse(e.dataset.snapshot),
            p = f.players.find((p) => p.id === +e.dataset.player);
          f.sector.pilot.contract = 1;
          f.sector.pilot.progress = progress;
          f.sector.pilot.ready = ready;
          window.__sectorFixture.update(p, f, true);
          window.__holdSector = true;
        },
        { progress, ready },
      );
    await transition([50], false);
    assert.equal(
      await page.locator('#radar-controls').getAttribute('data-waypoint'),
      'Orbital Survey 2',
    );
    assert.match(
      await page.locator('#sector-guide').innerText(),
      /1\/3 scanned — next: Orbital Survey 2/,
    );
    await transition([50, 52], false);
    assert.equal(
      await page.locator('#radar-controls').getAttribute('data-waypoint'),
      'Orbital Survey 2',
    );
    await transition([50, 51, 52], true);
    assert.equal(
      await page.locator('#radar-controls').getAttribute('data-waypoint'),
      'Arrival Port',
    );
    assert.match(
      await page.locator('#sector-guide').innerText(),
      /Complete — dock at Arrival Port and claim reward/,
    );
    assert.deepEqual(errors, []);
    console.log(
      'PASS: larger guide, accepted contract waypoint, radar zoom, next unscanned objective, return/claim instructions, no browser errors.',
    );
  } finally {
    await browser.close();
  }
})().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});
