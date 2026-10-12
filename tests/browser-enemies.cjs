const { chromium } = require('../artifacts/browser-check/node_modules/playwright');
const assert = require('node:assert/strict');
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  const errors = [];
  try {
    const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
    page.on('pageerror', (e) => errors.push(String(e)));
    await page.goto((process.env.ARENA_URL || 'http://localhost:5088') + '/');
    await page.locator('#nickname').fill('NPCCheck');
    await page.locator('#join-form button').click();
    await page.waitForFunction(() => document.querySelector('#network-info')?.dataset.snapshot);
    await page.locator('#canvas').focus();
    await page.keyboard.down('d');
    await page.waitForTimeout(5200);
    await page.keyboard.up('d');
    await page.waitForFunction(
      () => {
        const state = JSON.parse(document.querySelector('#network-info')?.dataset.snapshot || '{}');
        return (
          state.enemies?.some((e) => e.kind === 'rammer') &&
          state.enemies?.some((e) => e.kind === 'gunner')
        );
      },
      null,
      { timeout: 20000 },
    );
    await page.waitForFunction(
      () =>
        JSON.parse(document.querySelector('#network-info').dataset.snapshot).shots.some(
          (s) => s.owner < 0 && s.born > 0,
        ),
      null,
      { timeout: 10000 },
    );
    const state = await page
      .locator('#network-info')
      .evaluate((e) => JSON.parse(e.dataset.snapshot));
    assert(state.enemies.length <= 192);
    assert(state.shots.every((s) => Number.isFinite(s.dx) && Number.isFinite(s.dy) && s.born > 0));
    await page.screenshot({ path: 'artifacts/browser-check/multiplayer-enemies.png' });
    assert.deepEqual(errors, []);
    console.log(
      JSON.stringify({
        result: 'PASS',
        tick: state.tick,
        enemies: state.enemies,
        checks: [
          'WASM startup',
          'rammer and gunner snapshots',
          'hostile shot birth/velocity',
          'no page errors',
        ],
      }),
    );
  } finally {
    await browser.close();
  }
})().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});
