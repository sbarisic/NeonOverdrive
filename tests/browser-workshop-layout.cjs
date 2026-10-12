const { chromium } = require('../artifacts/browser-check/node_modules/playwright');
const assert = require('node:assert/strict');
(async () => {
  const b = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const scale = Number(process.env.TEST_DPR || 1),
      p = await b.newPage({ viewport: { width: 1920, height: 1080 }, deviceScaleFactor: scale }),
      errors = [];
    p.on('pageerror', (e) => errors.push(String(e)));
    await p.route('**/weapons.js*', async (route) => {
      const response = await route.fetch();
      let s = await response.text();
      s = s
        .replace(/return\s*\{\s*get open\(\)/, 'return window.__benchFixture={get open()')
        .replace(
          /update\(self,\s*connected,\s*profile\)\s*\{/,
          'update(self,connected,profile){if(window.__holdWorkshop)return;',
        );
      await route.fulfill({ response, body: s });
    });
    await p.goto((process.env.ARENA_URL || 'http://localhost:5094') + '/');
    await p.locator('#nickname').fill('Dense' + Date.now().toString(36));
    await p.getByRole('button', { name: 'Join arena', exact: true }).click();
    await p.waitForFunction(() => document.querySelector('#network-info')?.dataset.snapshot);
    await p.locator('#canvas').focus();
    await p.keyboard.press('i');
    await p.evaluate(() => {
      const e = document.querySelector('#network-info'),
        f = JSON.parse(e.dataset.snapshot),
        s = f.players.find((p) => p.id === +e.dataset.player);
      const profile = structuredClone(f.workshop);
      profile.owned = Array(29).fill(10);
      window.__setPreview = (attachments) => {
        profile.weapons[0].attachments = attachments;
        s.life += 1; // Reload the local fixture draft without writing server equipment.
        window.__holdWorkshop = false;
        window.__benchFixture.update(s, true, profile);
        window.__holdWorkshop = true;
      };
      window.__setPreview([21, 9, 14, 9, 12, 13, 0, 0]);
    });
    for (const [width, height] of [
      [1920, 1080],
      [1280, 720],
    ]) {
      await p.setViewportSize({ width, height });
      await p.waitForTimeout(150);
      const metrics = await p.locator('#weapon-bench').evaluate((el) => {
        const body = el.querySelector('.forge-body');
        return {
          scroll: body.scrollHeight - body.clientHeight,
          preview: el.querySelector('.chain-preview').getBoundingClientRect().height,
          previewWidth: el.querySelector('.chain-preview').clientWidth,
          pixelWidth: el.querySelector('.chain-preview').width,
          overflow: el.scrollWidth - el.clientWidth,
          slots: [...el.querySelectorAll('#weapon-chain-0 .weapon-slot')].map(
            (s) => s.getBoundingClientRect().height,
          ),
          inventory: [...el.querySelectorAll('.module-item')].map(
            (s) => s.getBoundingClientRect().height,
          ),
        };
      });
      console.log(width, height, metrics);
      assert(metrics.slots.every((h) => h <= 74));
      assert(metrics.inventory.every((h) => h <= 48));
      assert(metrics.preview >= 166 && metrics.preview <= 210);
      assert(metrics.previewWidth < 800, 'diagram is not stretched across the menu');
      assert.equal(metrics.pixelWidth, Math.round(metrics.previewWidth * scale));
      assert(metrics.overflow <= 2);
      assert(metrics.scroll <= 2, 'main menu stays in view, including inventory and footer');
      await p.screenshot({ path: `artifacts/browser-check/dense-workshop-${height}.png` });
    }
    assert.match(await p.locator('.chain-summary').innerText(), /18 branches/);
    await p.locator('.chain-stage').nth(4).click();
    assert.equal(await p.evaluate(() => document.activeElement.id), 'weapon-slot-5');
    await p.evaluate(() => window.__setPreview(Array(8).fill(13)));
    assert.match(await p.locator('.chain-note').innerText(), /64 representative paths/);
    assert.match(await p.locator('.chain-summary').innerText(), /1,679,616 branches/);
    await p.screenshot({ path: 'artifacts/browser-check/dense-forks-720.png' });
    await p.evaluate(() => window.__setPreview([11, 26, 13, 5, 0, 0, 0, 0]));
    assert.equal(await p.locator('.chain-stage[data-reachable="false"]').count(), 6);
    await p.screenshot({ path: 'artifacts/browser-check/terminal-forks-720.png' });
    await p.getByRole('tab', { name: 'Shop', exact: true }).click();
    await p.screenshot({ path: 'artifacts/browser-check/compact-shop.png' });
    await p.keyboard.press('Escape');
    await p.keyboard.press('Escape');
    await p.locator('#arena-menu').waitFor({ state: 'visible' });
    await p.screenshot({ path: 'artifacts/browser-check/compact-menu.png' });
    assert.deepEqual(errors, []);
    console.log(
      'PASS: populated compact inventory, slots, shop and Escape menu, no browser errors.',
    );
  } finally {
    await b.close();
  }
})().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});
