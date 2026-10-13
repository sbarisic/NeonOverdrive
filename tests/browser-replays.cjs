const assert = require('node:assert/strict');
const { chromium } = require('../artifacts/browser-check/node_modules/playwright');

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
    const errors = [],
      leaves = [];
    page.on('pageerror', (e) => errors.push(String(e)));
    page.on('websocket', (socket) => {
      let sent = 0,
        acknowledged = 0;
      socket.on('framesent', ({ payload }) => {
        const message = JSON.parse(String(payload));
        if (message.type === 'leave') sent = message.seq;
      });
      socket.on('framereceived', ({ payload }) => {
        const message = JSON.parse(String(payload));
        if (message.leaveSequence) acknowledged = message.leaveSequence;
      });
      socket.on('close', () => {
        if (sent) leaves.push({ sent, acknowledged });
      });
    });
    const origin = process.env.ARENA_URL || 'http://localhost:5094';
    const name = 'Replay' + Date.now().toString(36);
    const join = async () => {
      await page.getByRole('button', { name: 'Join arena', exact: true }).click();
      await page.waitForFunction(() => document.querySelector('#network-info')?.dataset.snapshot);
    };
    const read = () =>
      page.locator('#network-info').evaluate((el) => {
        const f = JSON.parse(el.dataset.snapshot);
        return { pilot: f.players.find((p) => p.id === +el.dataset.player), profile: f.workshop };
      });
    await page.goto(origin);
    await page.locator('#nickname').fill(name);
    await join();
    const first = await read();
    await page.locator('#canvas').focus();
    await page.keyboard.press('e');
    if (await page.getByRole('button', { name: 'Dock at Arrival Port', exact: true }).isVisible())
      await page.getByRole('button', { name: 'Dock at Arrival Port', exact: true }).click();
    await page
      .locator('#sector-console')
      .getByRole('button', { name: 'Module shop', exact: true })
      .click();
    await page.locator('#weapon-bench').getByRole('tab', { name: 'Shop', exact: true }).click();
    await page.getByRole('button', { name: 'Buy Accelerator', exact: true }).click();
    await page.waitForFunction(
      () =>
        JSON.parse(document.querySelector('#network-info').dataset.snapshot).workshop.credits ===
        40,
    );
    await page.reload();
    await join();
    const recovered = await read();
    assert.equal(recovered.pilot.runId, first.pilot.runId);
    assert.equal(recovered.profile.credits, 40);
    assert.equal(recovered.profile.owned[6], 1);
    if (recovered.pilot.docked) {
      await page.locator('#canvas').focus();
      await page.keyboard.press('e');
      if (await page.getByRole('button', { name: 'Dock at Arrival Port', exact: true }).isVisible())
        await page.getByRole('button', { name: 'Dock at Arrival Port', exact: true }).click();
      await page.locator('#sector-console').waitFor({ state: 'hidden' });
    }
    for (let n = 0; n < 2; n++) {
      const before = await read();
      await page.locator('#canvas').focus();
      await page.keyboard.press('Escape');
      assert.equal(
        await page.locator('#replay-score').innerText(),
        'Replays: 0 · Starting credits: 100',
      );
      // Verify the focus trap after the single-player button removal.
      await page.getByRole('button', { name: 'Leave', exact: true }).focus();
      await page.keyboard.press('Tab');
      assert(
        await page
          .locator('#arena-menu .menu-navigation [data-menu="pilot"]')
          .evaluate((e) => e === document.activeElement),
      );
      await page.screenshot({ path: 'artifacts/browser-check/replay-menu.png' });
      await page.getByRole('button', { name: 'Leave', exact: true }).click();
      await page
        .getByRole('button', { name: 'Join arena', exact: true })
        .waitFor({ state: 'visible' });
      await page.waitForTimeout(100);
      assert.equal(leaves.length, n + 1);
      assert.equal(leaves[n].acknowledged, leaves[n].sent);
      await join();
      const fresh = await read();
      assert.notEqual(fresh.pilot.runId, before.pilot.runId);
      assert.equal(fresh.pilot.level, 1);
      assert.equal(fresh.profile.credits, 100);
      assert.equal(fresh.profile.replayCount, 0);
      assert.equal(fresh.profile.owned[1], 4);
      assert.equal(fresh.profile.owned[6], 0);
      assert.deepEqual(fresh.pilot.skills, []);
      assert(
        fresh.profile.weapons.every(
          (w, i) =>
            w.emitter === 1 && w.enabled === (i === 0) && w.attachments.every((x) => x === 0),
        ),
      );
    }
    assert.deepEqual(errors, []);
    console.log(
      'PASS: refresh preserves purchases and run; Leave is acknowledged before close; repeated rejoin resets without farming replay credits; menu score and focus work.',
    );
  } finally {
    await browser.close();
  }
})().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});
