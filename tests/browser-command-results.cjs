const assert = require('node:assert/strict');
const { chromium } = require('../artifacts/browser-check/node_modules/playwright');
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
    await page.route('**/verification-empty', (route) =>
      route.fulfill({ contentType: 'text/html', body: '<canvas tabindex="0"></canvas>' }),
    );
    await page.goto((process.env.ARENA_URL || 'http://localhost:5105') + '/verification-empty');
    await page.setContent('<canvas tabindex="0"></canvas>');
    await page.evaluate(async () => {
      const { createLivingSector } = await import('/living-sector.js');
      const el = (tag, text = '', cls = '') => {
        const n = document.createElement(tag);
        n.textContent = text;
        n.className = cls;
        return n;
      };
      window.sent = [];
      window.client = createLivingSector(
        el,
        document.querySelector('canvas'),
        {},
        (action, target) => {
          const seq = window.sent.length + 1;
          window.sent.push({ seq, action, target });
          return seq;
        },
      );
      window.pilot = { runId: 'first' };
      window.frame = {
        sector: {
          pilot: { receipts: [] },
          services: { contacts: [], availability: [] },
          world: {
            radio: [],
            stock: [{ id: 9, name: 'Test cargo', price: 50 }],
            contacts: [],
            cargo: [],
            territory: [],
            job: '',
            jobState: '',
            police: '',
            event: '',
          },
        },
      };
      client.update(pilot, frame);
      client.openTarget(0);
    });
    const buy = page.locator('[data-radio-action="convoy_buy:9"]');
    await buy.focus();
    await buy.press('Space');
    const seq = await page.evaluate(() => sent.at(-1).seq);
    assert(await buy.isDisabled());
    await page.evaluate(() => {
      frame.sector.pilot.receipts = [{ sequence: 999, cursor: 1, result: 'ok' }];
      client.update(pilot, frame);
    });
    assert(await buy.isDisabled(), 'unrelated result must not release the purchase');
    await page.evaluate((seq) => {
      frame.sector.pilot.receipts = [{ sequence: seq, cursor: 2, result: 'pending' }];
      client.update(pilot, frame);
    }, seq);
    assert(await buy.isDisabled());
    await page.evaluate((seq) => {
      frame.sector.pilot.receipts = [{ sequence: seq, cursor: 3, result: 'persistence_failed' }];
      client.update(pilot, frame);
    }, seq);
    assert(await buy.isEnabled(), 'matching rejection enables retry');
    await buy.click();
    assert(await buy.isDisabled());
    await page.evaluate(() => {
      pilot.runId = 'second';
      frame.sector.pilot.receipts = [];
      client.update(pilot, frame);
    });
    assert(await buy.isEnabled(), 'fresh run cannot inherit a pending command');
    await buy.click();
    await page.evaluate(() => {
      client.update(null, {});
      client.update(pilot, frame);
      client.openTarget(0);
    });
    assert(await buy.isEnabled(), 'reconnect clears stale pending state');
    console.log(
      'PASS: native keyboard activation, correlated/out-of-order results, pending outcomes, retry, run change and disconnect.',
    );
  } finally {
    await browser.close();
  }
})().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});
