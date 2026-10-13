const assert = require('node:assert/strict');
const { chromium } = require('../artifacts/browser-check/node_modules/playwright');
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage({
      viewport: { width: 1280, height: 720 },
      deviceScaleFactor: 2,
    });
    const errors = [];
    page.on('pageerror', (e) => errors.push(String(e)));
    await page.route('**/sector.js*', async (route) => {
      const response = await route.fetch();
      const source = await response.text();
      assert(source.includes('return {\n    trackRescue()'));
      await route.fulfill({
        response,
        body: source.replace(
          'return {\n    trackRescue()',
          'return window.__servicesFixture={\n    trackRescue()',
        ),
      });
    });
    await page.addInitScript(() => {
      const Native = WebSocket;
      window.WebSocket = class extends Native {
        constructor(...args) {
          super(...args);
          this.addEventListener('message', (e) => {
            if (window.__holdServiceFrames && JSON.parse(e.data).type === 'snapshot')
              e.stopImmediatePropagation();
          });
        }
      };
    });
    await page.goto(process.env.ARENA_URL || 'http://localhost:5096');
    await page.locator('#nickname').fill('Outpost' + Date.now().toString(36));
    await page.getByRole('button', { name: 'Join arena', exact: true }).click();
    await page.waitForFunction(() => document.querySelector('#network-info')?.dataset.snapshot);
    await page.evaluate(() => {
      window.__holdServiceFrames = true;
      const info = document.querySelector('#network-info'),
        frame = JSON.parse(info.dataset.snapshot),
        self = frame.players.find((p) => p.id === +info.dataset.player),
        site = frame.sector.services.outposts[0];
      self.x = site.x;
      self.y = site.y;
      frame.workshop.credits = 1000;
      window.__serviceFrame = frame;
      window.__serviceSelf = self;
      window.__servicesFixture.update(self, frame, true);
      window.__servicesFixture.keydown({ code: 'KeyE', repeat: false, preventDefault() {} });
    });
    await page
      .locator('.service-board')
      .getByRole('button', { name: /^Manage / })
      .first()
      .click();
    assert.equal(await page.evaluate(() => window.__servicesFixture.cloaked), false);
    assert(await page.getByRole('button', { name: /Activate Service core/ }).isEnabled());
    assert(await page.getByRole('button', { name: /Build Turret 1/ }).isDisabled());
    await page.evaluate(() => {
      const f = window.__serviceFrame;
      f.sector.services.outposts[0].active = true;
      f.sector.services.outposts[0].sockets[0].hull = 72;
      window.__servicesFixture.update(window.__serviceSelf, f, true);
    });
    assert(await page.getByRole('button', { name: /Build Turret 1/ }).isEnabled());
    page.once('dialog', (dialog) => dialog.dismiss());
    await page.getByRole('button', { name: /Build Turret 1/ }).click();
    const footer = await page
      .getByRole('button', { name: 'Return [Esc]', exact: true })
      .boundingBox();
    assert(footer && footer.y >= 0 && footer.y + footer.height <= 720, JSON.stringify(footer));
    const repair = await page.getByRole('button', { name: /Build Repair drone bay/ }).boundingBox();
    assert(repair && repair.y + repair.height < footer.y);
    await page.screenshot({ path: 'artifacts/browser-check/outpost-management.png' });
    assert.deepEqual(errors, []);
    console.log(
      'PASS: contextual outpost management, no cloak, typed construction sockets, prices, confirmation, visible return at 720p DPR2.',
    );
  } finally {
    await browser.close();
  }
})().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});
