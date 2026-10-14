const assert = require('node:assert/strict');
const { chromium } = require('../artifacts/browser-check/node_modules/playwright');
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    for (const dpr of [1, 2]) {
      const page = await browser.newPage({
        viewport: { width: 1280, height: 720 },
        deviceScaleFactor: dpr,
      });
      const errors = [];
      page.on('pageerror', (e) => errors.push(String(e)));
      await page.goto(process.env.ARENA_URL || 'http://localhost:5098');
      await page.waitForFunction(() => !!document.querySelector('#crt-setting'));
      assert.equal(await page.locator('#crt-setting').inputValue(), 'subtle');
      for (const mode of ['off', 'enhanced', 'subtle', 'off']) {
        await page.locator('#crt-setting').evaluate((e, mode) => {
          e.value = mode;
          e.dispatchEvent(new Event('change'));
        }, mode);
        assert.equal(await page.locator('body').getAttribute('data-crt'), mode);
        assert.equal(await page.evaluate(() => localStorage.getItem('neon-crt')), mode);
      }
      await page.reload();
      await page.waitForFunction(() => !!document.querySelector('#crt-setting'));
      assert.equal(await page.locator('#crt-setting').inputValue(), 'off');
      await page.evaluate(async () => {
        const { control } = await import('/interface.js');
        const { createContextActions } = await import('/context-actions.js');
        window.testActions = [];
        window.testContext = createContextActions(
          (tag, text = '') => Object.assign(document.createElement(tag), { textContent: text }),
          (item) => window.testActions.push(item.id),
        );
        window.testContext.update(
          [
            { id: 1, action: 'dock', label: 'Dock: Arrival', icon: 'station' },
            { id: 2, action: 'hail', label: 'Hail: Courier', icon: 'friends' },
          ],
          true,
        );
        document.body.classList.remove('modal-open');
        for (const [name, icon] of [
          ['close', 'exit'],
          ['apply', 'apply'],
          ['purchase', 'shop'],
        ]) {
          const b = document.createElement('button');
          b.id = 'tone-' + name;
          control(b, icon);
          document.body.append(b);
        }
      });
      const b = page.locator('#context-actions button').first(),
        box = await b.boundingBox();
      assert(box && box.x >= 0 && box.y + box.height <= 720);
      await page.mouse.move(box.x + 10, box.y + 10);
      await page.mouse.down();
      await b.evaluate((e) => (window.originalNode = e));
      await page.evaluate(() =>
        window.testContext.update(
          [
            { id: 2, action: 'hail', label: 'Hail: Courier', icon: 'friends' },
            { id: 1, action: 'dock', label: 'Dock: Arrival', icon: 'station' },
          ],
          true,
        ),
      );
      assert(await b.evaluate((e) => e === window.originalNode));
      await page.mouse.up();
      assert.deepEqual(await page.evaluate(() => window.testActions), [1]);
      const colors = await page
        .locator('[id^="tone-"]')
        .evaluateAll((items) => items.map((e) => getComputedStyle(e).color));
      assert.equal(new Set(colors).size, 3);
      await page.screenshot({ path: `artifacts/browser-check/crt-context-${dpr}x.png` });
      assert.deepEqual(errors, []);
      await page.close();
    }
    console.log(
      'PASS: CRT default/settings/persistence, semantic colors, stable contextual click during reordered snapshots, 720p DPR1/2.',
    );
  } finally {
    await browser.close();
  }
})().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});
