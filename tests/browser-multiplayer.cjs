const { chromium } = require('../artifacts/browser-check/node_modules/playwright');
const assert = require('node:assert/strict');
(async () => {
  const browser = await chromium.launch({
    channel: 'msedge',
    headless: true,
    args: ['--disable-background-timer-throttling', '--disable-renderer-backgrounding'],
  });
  const errors = [];
  try {
    const a = await browser.newPage({ viewport: { width: 1280, height: 720 } });
    const b = await browser.newPage({ viewport: { width: 1280, height: 720 } });
    for (const p of [a, b]) p.on('pageerror', (e) => errors.push(String(e)));
    const base = process.env.ARENA_URL || 'http://localhost:5088';
    await a.goto(base + '/');
    await a.locator('#nickname').waitFor();
    assert.equal(await a.getByRole('link', { name: 'Single-player', exact: true }).count(), 0);
    await a.locator('#nickname').fill('Alpha' + Date.now().toString(36));
    await a.locator('#join-form button').click();
    await a.waitForFunction(() => document.querySelector('#network-info')?.dataset.snapshot);
    const room = await a.locator('#network-info').getAttribute('data-room');
    await b.goto(base + '/?room=' + room);
    await b.locator('#nickname').fill('Beta' + Date.now().toString(36));
    await b.locator('#join-form button').click();
    await b.waitForFunction(
      () =>
        JSON.parse(document.querySelector('#network-info')?.dataset.snapshot || '{}').players
          ?.length >= 2,
    );
    const read = (p) =>
      p.locator('#network-info').evaluate((e) => ({
        id: Number(e.dataset.player),
        snapshot: JSON.parse(e.dataset.snapshot),
      }));
    const initial = await read(a),
      second = await read(b);
    assert.notEqual(initial.id, second.id);
    // Both pilots leave the sanctuary before testing hostile player damage.
    await b.bringToFront();
    await b.locator('#canvas').focus();
    await b.keyboard.down('d');
    await b.waitForTimeout(750);
    await b.keyboard.up('d');
    await b.keyboard.down('w');
    await b.waitForTimeout(3200);
    await b.keyboard.up('w');
    await b.keyboard.down('s');
    await b.waitForTimeout(2100);
    await b.keyboard.up('s');
    await a.bringToFront();
    await a.locator('#canvas').focus();
    await a.keyboard.down('d');
    await a.waitForTimeout(750);
    await a.keyboard.up('d');
    await a.keyboard.down('w');
    await a.waitForTimeout(4500);
    await a.keyboard.up('w');
    await a.keyboard.down('s');
    await a.waitForTimeout(2100);
    await a.keyboard.up('s');
    await a.waitForTimeout(1200);
    const moved = await read(b);
    assert(
      moved.snapshot.players.find((p) => p.id === initial.id).y >
        initial.snapshot.players.find((p) => p.id === initial.id).y + 500,
    );
    const now = await read(a),
      self = now.snapshot.players.find((p) => p.id === initial.id),
      target = now.snapshot.players.find((p) => p.id === second.id);
    assert(!self.protected && !target.protected, 'both pilots must leave sanctuary before PvP');
    const ox = Math.max(0, Math.min(24000 - 1280, self.x - 640)),
      oy = Math.max(0, Math.min(16000 - 720, self.y - 360));
    const box = await a.locator('#canvas').boundingBox();
    // Aim toward Beta after both pilots have stopped outside the station body and sanctuary.
    const dx = target.x - self.x,
      dy = target.y - self.y,
      scale = 200 / Math.hypot(dx, dy);
    await a.mouse.move(
      box.x + ((self.x - ox + dx * scale) / 1280) * box.width,
      box.y + ((self.y - oy + dy * scale) / 720) * box.height,
    );
    await a.keyboard.down('Space');
    console.log(
      'PvP positions',
      JSON.stringify({ self: [self.x, self.y], target: [target.x, target.y] }),
    );
    await b.waitForFunction(
      (id) =>
        JSON.parse(document.querySelector('#network-info').dataset.snapshot).shots.some(
          (s) => s.owner === id,
        ),
      initial.id,
    );
    await b.waitForFunction(
      (id) =>
        JSON.parse(document.querySelector('#network-info').dataset.snapshot).players.find(
          (p) => p.id === id,
        ).shield < 2,
      second.id,
      { timeout: 10000 },
    );
    await a.keyboard.up('Space');
    const hit = await read(b);
    await a.screenshot({ path: 'artifacts/browser-check/multiplayer-alpha.png' });
    await b.screenshot({ path: 'artifacts/browser-check/multiplayer-beta.png' });
    assert.deepEqual(errors, []);
    console.log(
      JSON.stringify({
        result: 'PASS',
        room,
        players: [initial.id, second.id],
        victim: hit.snapshot.players.find((p) => p.id === second.id),
        checks: [
          'independent sessions',
          'same room',
          'remote movement',
          'remote shots',
          'server-confirmed damage',
          'no page errors',
        ],
      }),
    );
  } finally {
    await browser.close();
  }
})().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});
