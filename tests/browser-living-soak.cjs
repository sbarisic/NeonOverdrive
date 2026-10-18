const assert = require('node:assert/strict');
const { chromium } = require('../artifacts/browser-check/node_modules/playwright');

(async () => {
  const browser = await chromium.launch({
    channel: 'msedge',
    headless: true,
    args: ['--disable-background-timer-throttling', '--disable-renderer-backgrounding'],
  });
  try {
    const origin = process.env.ARENA_URL || 'http://localhost:5098';
    const pages = [],
      errors = [];
    let room;
    const read = (page) =>
      page.locator('#network-info').evaluate((e) => {
        const frame = JSON.parse(e.dataset.snapshot);
        return { frame, self: frame.players.find((p) => p.id === +e.dataset.player) };
      });
    for (let i = 0; i < 3; i++) {
      const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
      page.on('pageerror', (e) => errors.push(String(e)));
      await page.goto(origin + (room ? '/?room=' + room : '/'));
      await page.locator('#nickname').fill('Soak' + i + Date.now().toString(36));
      await page.getByRole('button', { name: 'Join arena', exact: true }).click();
      await page.waitForFunction(() => document.querySelector('#network-info')?.dataset.snapshot);
      room = await page.locator('#network-info').getAttribute('data-room');
      pages.push(page);
    }
    const start = (await read(pages[0])).frame.tick;
    for (let sample = 0; sample < 12; sample++) {
      for (const page of pages) {
        if ((await read(page)).self.hull <= 0) {
          await page.locator('#arena-respawn').click();
          await page.waitForFunction(() => {
            const e = document.querySelector('#network-info');
            return (
              JSON.parse(e.dataset.snapshot).players.find((p) => p.id === +e.dataset.player)?.hull >
              0
            );
          });
        }
        await page.locator('#canvas').focus();
        await page.keyboard.down('w');
        await page.waitForTimeout(120);
        await page.keyboard.up('w');
        await page.keyboard.down('d');
        await page.waitForTimeout(180);
        await page.keyboard.up('d');
        await page.keyboard.down('Space');
        await page.waitForTimeout(250);
        await page.keyboard.up('Space');
        const { frame, self } = await read(page);
        assert.equal(frame.v, 16);
        assert(Number.isFinite(self.x) && Number.isFinite(self.y));
        assert(frame.enemies.length <= 192 && frame.shots.length <= 512);
        assert(frame.sector.world.radio.length <= 32);
      }
      if (sample === 5) {
        const page = pages[2],
          before = await read(page);
        if (before.self.hull > 0) {
          await page.reload();
          await page.getByRole('button', { name: 'Join arena', exact: true }).click();
          await page.waitForFunction(
            () => document.querySelector('#network-info')?.dataset.snapshot,
          );
          const recovered = await read(page);
          if (recovered.self.hull > 0) assert.equal(recovered.self.runId, before.self.runId);
        }
      }
      await pages[0].waitForTimeout(8500);
    }
    assert((await read(pages[0])).frame.tick > start + 90 * 30);
    assert.deepEqual(errors, []);
    console.log(
      'PASS: three desktop clients, two minutes of flight/fire, bounded snapshots, recovery and no browser exceptions.',
    );
  } finally {
    await browser.close();
  }
})().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});
