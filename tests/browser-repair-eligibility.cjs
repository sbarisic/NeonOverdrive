const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const { chromium } = require('../artifacts/browser-check/node_modules/playwright');

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    for (const deviceScaleFactor of [1, 2]) {
      const page = await browser.newPage({
        viewport: { width: 1280, height: 720 },
        deviceScaleFactor,
      });
      const errors = [];
      page.on('pageerror', (error) => errors.push(String(error)));
      // Serve the actual source modules without touching a running game or profile store.
      await page.route('http://repair.test/**', async (route) => {
        const file = new URL(route.request().url()).pathname;
        if (file === '/')
          return route.fulfill({ contentType: 'text/html', body: '<main id="info"></main>' });
        assert.match(file, /^\/[a-z0-9.-]+\.js$/);
        await route.fulfill({
          contentType: 'text/javascript',
          body: await fs.readFile(
            path.join(__dirname, '../src/ArcadeDodger/Web/Web', file.slice(1)),
          ),
        });
      });
      await page.goto('http://repair.test/');
      await page.evaluate(async () => {
        const { createSectorConsole } = await import('/sector.js');
        const el = (tag, text = '', cls = '') => {
          const node = document.createElement(tag);
          node.textContent = text;
          node.className = cls;
          return node;
        };
        window.sent = [];
        window.client = createSectorConsole(
          el,
          document.querySelector('#info'),
          (action, target) => {
            sent.push({ action, target });
            return sent.length;
          },
          () => {},
          () => {},
        );
        client.setup(
          {
            seed: 1,
            width: 24000,
            height: 16000,
            locations: [{ id: 10, kind: 'station', name: 'Arrival', x: 1000, y: 1000 }],
            obstacles: [],
          },
          [],
        );
        window.pilot = { id: 1, runId: 'repair-test', hull: 1, x: 1000, y: 1000 };
        window.frame = {
          workshop: { credits: 0 },
          sector: {
            pilot: { docked: 10, repairCost: 20, receipts: [] },
            services: {
              contacts: [],
              outposts: [],
              availability: [{ target: 0, action: 'repair', reason: 'insufficient_credits' }],
            },
          },
        };
        window.refresh = (reason) => {
          frame.sector.services.availability[0].reason = reason;
          client.update(pilot, frame, true);
        };
        refresh('insufficient_credits');
      });
      const repair = page.getByRole('button', { name: 'Repair hull · 20 credits', exact: true });
      assert(await repair.isDisabled(), 'unaffordable repair must be disabled');
      assert.match(await repair.getAttribute('title'), /credits/i);
      await page.evaluate(() => {
        frame.workshop.credits = 20;
        refresh('busy');
      });
      assert(await repair.isDisabled(), 'a transaction in another panel must block repair');
      assert.match(await repair.getAttribute('title'), /wait|saving|transaction/i);
      await page.evaluate(() => refresh(''));
      assert(await repair.isEnabled(), 'repair becomes available when the server permits it');
      assert.equal(await repair.getAttribute('title'), '');
      await repair.click();
      assert.deepEqual(await page.evaluate(() => sent.map((s) => s.action)), ['repair']);
      assert(await repair.isDisabled(), 'local pending command still blocks repeated repairs');
      await page.evaluate(() => {
        frame.sector.pilot.receipts = [{ sequence: 1, cursor: 1, result: 'insufficient_credits' }];
        refresh('insufficient_credits');
      });
      assert(await repair.isDisabled(), 'rejection must not override authoritative eligibility');
      assert.deepEqual(errors, []);
      await page.close();
    }
    console.log(
      'PASS: repair eligibility, wallet rejection, cross-panel busy state and pending commands at DPR 1/2.',
    );
  } finally {
    await browser.close();
  }
})().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
