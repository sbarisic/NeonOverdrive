const assert = require('node:assert/strict');
const { chromium } = require('../artifacts/browser-check/node_modules/playwright');
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
    const errors = [];
    page.on('pageerror', (e) => errors.push(String(e)));
    await page.route('**/weapons.js*', async (route) => {
      const response = await route.fetch();
      let source = await response.text();
      source = source
        .replace(/return\s*\{\s*get open\(\)/, 'return window.__bench={get open()')
        .replace(
          /update\(self,\s*connected,\s*profile,\s*services\)\s*\{/,
          'update(self,connected,profile,services){if(window.__hold)return;',
        );
      await route.fulfill({ response, body: source });
    });
    await page.goto(process.env.ARENA_URL || 'http://localhost:5094');
    await page.locator('#nickname').fill('Slots' + Date.now().toString(36));
    await page.getByRole('button', { name: 'Join arena', exact: true }).click();
    await page.waitForFunction(() => document.querySelector('#network-info')?.dataset.snapshot);
    await page.locator('#canvas').focus();
    await page.keyboard.press('i');
    await page.evaluate(() => {
      const info = document.querySelector('#network-info'),
        f = JSON.parse(info.dataset.snapshot);
      const self = f.players.find((p) => p.id === +info.dataset.player),
        profile = structuredClone(f.workshop),
        inventory = profile.inventory;
      inventory.revision++;
      inventory.loose[6] = 5;
      for (let i = 0; i < 3000; i++)
        inventory.weapons.push({
          id: (i + 1).toString(16).padStart(32, '0'),
          emitter: 1,
          attachments: [6, 8, 14, 5, 15, 7, 27, 23],
          tier: 4,
        });
      for (let i = 0; i < 12; i++)
        inventory.gear.push({ id: (5000 + i).toString(16).padStart(32, '0'), kind: i, tier: 4 });
      window.__receive = () => {
        window.__hold = false;
        window.__bench.update(self, true, profile);
        window.__hold = true;
      };
      window.__fixture = profile;
      window.__receive();
    });
    assert(
      (await page.locator('.inventory-cell').count()) < 100,
      'inventory only mounts visible rows',
    );
    await page.getByRole('button', { name: 'Ship gear', exact: true }).click();
    await page.locator('.inventory-cell').filter({ hasText: 'Capacitor' }).click();
    await page.locator('[data-ship-slot="0"]').click();
    assert.match(await page.locator('[data-ship-slot="0"]').innerText(), /Capacitor/);
    await page.getByRole('button', { name: 'Weapons', exact: true }).click();
    await page.locator('.inventory-cell').first().dblclick();
    assert.match(await page.locator('.weapon-heading').innerText(), /STORED BUILD/);
    await page.getByRole('button', { name: 'Full build', exact: true }).click();
    assert.match(await page.locator('.chain-stage').last().innerText(), /Blast Chamber/);
    await page.locator('#weapon-slot-1').click({ button: 'right' });
    await page.evaluate(() => {
      window.__fixture.inventory.revision++;
      window.__fixture.inventory.loose[6]++;
      window.__receive();
    });
    assert.match(await page.locator('[data-ship-slot="0"]').innerText(), /Capacitor/);
    assert.equal(await page.locator('#weapon-slot-1').getAttribute('data-module'), '0');
    assert.doesNotMatch(await page.locator('.bench-status').innerText(), /Reset draft/);
    await page.locator('.inventory-grid').evaluate((el) => {
      el.scrollTop = el.scrollHeight;
    });
    await page.waitForTimeout(100);
    assert((await page.locator('.inventory-cell').count()) < 100);
    await page.screenshot({ path: 'artifacts/browser-check/equipment-inventory-720.png' });
    assert.deepEqual(errors, []);
    console.log(
      'PASS: 3000 stored builds virtualized, typed ship sockets, stored-build editing, full preview and incoming-loot draft merge.',
    );
  } finally {
    await browser.close();
  }
})().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});
