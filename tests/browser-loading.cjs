const assert = require('node:assert/strict');
const { chromium } = require('../artifacts/browser-check/node_modules/playwright');
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  const url = process.env.ARENA_URL || 'http://localhost:5098';
  try {
    for (const dpr of [1, 2]) {
      const page = await browser.newPage({
        viewport: { width: 1280, height: 720 },
        deviceScaleFactor: dpr,
        reducedMotion: dpr === 2 ? 'reduce' : 'no-preference',
      });
      const errors = [];
      page.on('pageerror', (e) => errors.push(String(e)));
      await page.addInitScript(() => {
        window.launchStates = [];
        new MutationObserver(() => {
          const root = document.querySelector('#status');
          if (!root) return;
          const bar = root.querySelector('[role="progressbar"]');
          const state = {
            phase: root.dataset.phase,
            value: bar?.getAttribute('aria-valuenow'),
            text: bar?.getAttribute('aria-valuetext'),
            hidden: root.hidden,
          };
          if (JSON.stringify(window.launchStates.at(-1)) !== JSON.stringify(state))
            window.launchStates.push(state);
        }).observe(document, { subtree: true, attributes: true, childList: true });
      });
      let release;
      const gate = new Promise((resolve) => (release = resolve));
      await page.route('**/_framework/dotnet.js', async (route) => {
        await gate;
        await route.continue();
      });
      await page.goto(url, { waitUntil: 'commit' });
      await page.locator('.launch-card').waitFor();
      const bounds = await page.locator('.launch-card').boundingBox();
      assert(
        bounds.x >= 0 &&
          bounds.y >= 0 &&
          bounds.x + bounds.width <= 1280 &&
          bounds.y + bounds.height <= 720,
      );
      assert.equal(await page.locator('[role="progressbar"]').getAttribute('aria-valuenow'), null);
      if (dpr === 2)
        assert.equal(
          await page.locator('.launch-orbit').evaluate((e) => getComputedStyle(e).animationName),
          'none',
        );
      await page.screenshot({ path: `artifacts/loading-screen-dpr${dpr}.png` });
      release();
      await page.locator('#status').waitFor({ state: 'hidden', timeout: 60000 });
      const states = await page.evaluate(() => window.launchStates);
      assert(
        states.some(
          (s) => s.phase === '0' && s.value !== null && s.text.includes('runtime resources'),
        ),
        JSON.stringify(states),
      );
      assert(states.some((s) => s.phase === '1' && s.value === null));
      assert(states.some((s) => s.phase === '2' && s.value === null));
      await page.locator('#nickname').fill('Launch' + dpr + Date.now().toString(36));
      await page.getByRole('button', { name: 'Join arena', exact: true }).click();
      await page.waitForFunction(() => document.querySelector('#network-info')?.dataset.snapshot);
      assert.deepEqual(errors, []);
      console.log(JSON.stringify({ dpr, states }));
      await page.close();
    }
    const broken = await browser.newPage({ viewport: { width: 1280, height: 720 } });
    await broken.route('**/_framework/dotnet.js', (route) => route.abort());
    await broken.goto(url);
    await broken.getByText('Launch interrupted', { exact: true }).waitFor();
    assert(
      await broken.locator('[data-loading-retry]').evaluate((e) => e === document.activeElement),
    );
    assert(await broken.locator('[role="progressbar"]').isHidden());
    await broken.screenshot({ path: 'artifacts/loading-screen-error.png' });
    await broken.unroute('**/_framework/dotnet.js');
    await broken.getByRole('button', { name: 'Retry launch' }).press('Enter');
    await broken.locator('#status').waitFor({ state: 'hidden', timeout: 60000 });
    await broken.locator('#nickname').waitFor();
    await broken.reload();
    await broken.locator('#status').waitFor({ state: 'hidden', timeout: 60000 });
    console.log(
      'PASS: first paint, real resource progress, startup phases, DPR 1/2, reduced motion, real join, failed runtime download, keyboard retry, and warm reload.',
    );
  } finally {
    await browser.close();
  }
})().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
