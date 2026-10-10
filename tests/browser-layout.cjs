// Prerequisites: npm install --prefix artifacts/browser-check playwright
// Build browser project, run serve-browser.ps1, then: node tests/browser-layout.cjs
const assert = require('node:assert/strict');
const { chromium } = require('../artifacts/browser-check/node_modules/playwright');
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    for (const deviceScaleFactor of [1, 1.5]) {
      const page = await browser.newPage({ viewport: { width: 1760, height: 974 }, deviceScaleFactor });
      const errors = [];
      page.on('pageerror', e => errors.push(e.message));
      await page.goto('http://localhost:54955');
      await page.waitForFunction(() => document.getElementById('status').hidden);
      for (const [width, height] of [[1760,974], [2781,1238], [1280,720], [800,900]]) {
        await page.setViewportSize({ width, height });
        await page.waitForTimeout(250);
        const result = await page.evaluate(() => {
          const canvas = document.getElementById('canvas');
          const gl = canvas.getContext('webgl');
          return { buffer: [canvas.width, canvas.height], viewport: Array.from(gl.getParameter(gl.VIEWPORT)),
            rect: canvas.getBoundingClientRect().toJSON(),
            scroll: [document.documentElement.scrollWidth, document.documentElement.scrollHeight] };
        });
        assert.deepEqual(result.buffer, [1920,1080]);
        assert.deepEqual(result.viewport, [0,0,1920,1080]);
        const r = result.rect;
        assert(r.left >= -1 && r.top >= -1 && r.right <= width+1 && r.bottom <= height+1);
        assert(Math.abs(r.width/r.height - 16/9) < 0.001);
        assert(result.scroll[0] <= width && result.scroll[1] <= height);
        assert.deepEqual(errors, []);
        console.log(`PASS ${width}x${height}, DPR ${deviceScaleFactor}: fixed buffer, full viewport, no overflow`);
      }
      await page.setViewportSize({width:1760,height:974});
      await page.keyboard.press('Enter');
      await page.waitForTimeout(250);
      await page.screenshot({path:`artifacts/browser-check/gameplay-${deviceScaleFactor}.png`});
      await page.keyboard.press('p');
      await page.waitForTimeout(100);
      await page.screenshot({path:`artifacts/browser-check/paused-${deviceScaleFactor}.png`});
      assert.deepEqual(errors, []);
      await page.close();
    }
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
