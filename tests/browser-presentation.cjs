// Prerequisites: the built WASM AppBundle served by the multiplayer server on the
// same origin (including /ws), and Playwright in artifacts/browser-check/node_modules.
// Run with Node; ARENA_URL (or BROWSER_URL) overrides http://localhost:5088.
// This script does not start/build a server. It uses real WebSocket traffic, not mocks.
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const { chromium } = require('../artifacts/browser-check/node_modules/playwright');

const output = path.resolve('artifacts/browser-check/presentation');
const timeout = 15000;
const sizes = [[1760, 974], [1280, 720], [800, 900], [640, 480]];
const checks = [];

async function snapshot(page) {
  return page.locator('#network-info').evaluate(element => ({
    id: Number(element.dataset.player),
    tick: Number(element.dataset.tick),
    world: JSON.parse(element.dataset.snapshot)
  }));
}

async function waitForTicks(page, tick, count = 4) {
  await page.waitForFunction(({ tick, count }) =>
    Number(document.querySelector('#network-info')?.dataset.tick) >= tick + count,
  { tick, count }, { timeout });
}

async function inputMarker(page) {
  return page.evaluate(() => window.__presentationProbe.count);
}

async function waitForInputs(page, marker, predicate) {
  await page.waitForFunction(({ marker, predicate }) => {
    const frames = window.__presentationProbe.inputs.filter(frame => frame.index > marker);
    if (predicate === 'neutral') return frames.length >= 6;
    return frames.some(frame => frame.message.x !== 0 && frame.message.fire === true);
  }, { marker, predicate }, { timeout: 5000 });
  return page.evaluate(marker => window.__presentationProbe.inputs
    .filter(frame => frame.index > marker).map(frame => frame.message), marker);
}

async function assertContained(page, selector, label) {
  const result = await page.locator(selector).evaluate(element => {
    const visible = node => {
      const style = getComputedStyle(node);
      const box = node.getBoundingClientRect();
      return style.display !== 'none' && style.visibility !== 'hidden' && box.width > 0 && box.height > 0;
    };
    return {
      boxes: [element, ...element.querySelectorAll('input,button,a,label')]
        .filter(visible).map(node => ({ tag: node.tagName, ...node.getBoundingClientRect().toJSON() })),
      viewport: [innerWidth, innerHeight],
      scroll: [document.documentElement.scrollWidth, document.documentElement.scrollHeight]
    };
  });
  assert(result.boxes.length > 0, `${label}: no visible content`);
  for (const box of result.boxes) {
    assert(box.left >= -1 && box.top >= -1 && box.right <= result.viewport[0] + 1 &&
      box.bottom <= result.viewport[1] + 1, `${label}: clipped ${box.tag}: ${JSON.stringify(box)}`);
  }
  assert(result.scroll[0] <= result.viewport[0] + 1 && result.scroll[1] <= result.viewport[1] + 1,
    `${label}: page overflow`);
}

async function join(page, nickname) {
  await page.locator('#nickname').fill(nickname);
  await page.locator('#join-form button[type="submit"]').click();
  await page.waitForFunction(() => {
    const info = document.querySelector('#network-info');
    if (!info?.dataset.snapshot || !info.dataset.player) return false;
    const snapshot = JSON.parse(info.dataset.snapshot);
    return snapshot.players?.some(player => player.id === Number(info.dataset.player));
  }, null, { timeout });
  await page.locator('#join-form').waitFor({ state: 'hidden' });
  const state = await snapshot(page);
  await waitForTicks(page, state.tick);
  return state;
}

async function effectsState(control) {
  return control.evaluate(element => {
    if (element instanceof HTMLInputElement && element.type === 'checkbox') return String(element.checked);
    return element.getAttribute('aria-pressed') ?? element.getAttribute('aria-checked') ?? element.textContent.trim();
  });
}

(async () => {
  await fs.mkdir(output, { recursive: true });
  const browser = await chromium.launch({
    channel: 'msedge', headless: true,
    args: ['--disable-background-timer-throttling', '--disable-renderer-backgrounding']
  });
  const errors = [];
  let currentPage;
  try {
    for (const deviceScaleFactor of [1, 1.5]) {
      const context = await browser.newContext({ viewport: { width: 1280, height: 720 }, deviceScaleFactor });
      try {
        // Observe sends only; never replace payloads, synthesize snapshots, or bypass host code.
        // Retain a bounded ring so a failed test cannot accumulate unlimited diagnostics.
        await context.addInitScript(() => {
          const probe = window.__presentationProbe = { sockets: [], inputs: [], count: 0 };
          const originalSend = WebSocket.prototype.send;
          WebSocket.prototype.send = function (data) {
            if (!probe.sockets.includes(this)) probe.sockets.push(this);
            try {
              const message = JSON.parse(data);
              if (message.type === 'input') {
                probe.inputs.push({ index: ++probe.count, message });
                if (probe.inputs.length > 240) probe.inputs.shift();
              }
            } catch { /* Non-JSON traffic is not an input frame. */ }
            return originalSend.call(this, data);
          };
        });
        const page = currentPage = await context.newPage();
        page.setDefaultTimeout(timeout);
        page.setDefaultNavigationTimeout(60000);
        page.on('pageerror', error => errors.push(error.message));
        const url = new URL(process.env.ARENA_URL || process.env.BROWSER_URL || 'http://localhost:5088');
        url.searchParams.set('multiplayer', '1');
        // Do not inherit a shared room from an override: each run should begin in a fresh available room.
        url.searchParams.delete('room');
        await page.goto(url.href);
        await page.locator('#join-form').waitFor({ state: 'visible', timeout: 60000 });
        for (const [width, height] of sizes) {
          await page.setViewportSize({ width, height });
          await page.waitForTimeout(150); // Allow responsive CSS/layout to settle, not gameplay timing.
          await assertContained(page, '#join-form', `join ${width}x${height} DPR ${deviceScaleFactor}`);
          assert(await page.locator('#nickname').isEditable(), 'nickname must remain usable');
          await page.screenshot({ path: path.join(output, `join-${width}x${height}-dpr${deviceScaleFactor}.png`) });
          checks.push(`join containment ${width}x${height} DPR ${deviceScaleFactor}`);
        }
        await page.setViewportSize({ width: 1280, height: 720 });
        const initial = await join(page, `Present${deviceScaleFactor === 1 ? 'One' : 'Two'}`);
        await page.locator('#canvas').focus();
        // Exercise the advertised primary mouse path independently of Space.
        const canvasBox = await page.locator('#canvas').boundingBox();
        await page.mouse.move(canvasBox.x + canvasBox.width * .8, canvasBox.y + canvasBox.height * .5);
        const mouseMarker = await inputMarker(page);
        await page.keyboard.down('d');
        await page.mouse.down();
        await waitForInputs(page, mouseMarker, 'active');
        await page.mouse.up();
        await page.keyboard.up('d');
        const activeMarker = await inputMarker(page);
        await page.keyboard.down('d');
        await page.keyboard.down('Space');
        await waitForInputs(page, activeMarker, 'active');
        await page.keyboard.press('Escape');
        const resume = page.getByRole('button', { name: /^resume(?: arena| game)?$/i });
        await resume.waitFor({ state: 'visible' });
        await page.keyboard.up('d');
        await page.keyboard.up('Space');

        // Re-press input while the menu is visible: clearing held keys once on opening is insufficient.
        const menuMarker = await inputMarker(page);
        const beforeMenu = await snapshot(page);
        await page.keyboard.down('d');
        await page.keyboard.down('Space');
        await page.keyboard.down('Shift');
        await page.mouse.down();
        const neutral = await waitForInputs(page, menuMarker, 'neutral');
        for (const frame of neutral) {
          assert.equal(frame.x, 0, 'menu must suppress horizontal input');
          assert.equal(frame.y, 0, 'menu must suppress vertical input');
          assert.equal(frame.fire, false, 'menu must suppress firing');
          assert.equal(frame.dash, false, 'menu must suppress dash');
        }
        await waitForTicks(page, beforeMenu.tick);
        await page.keyboard.up('d');
        await page.keyboard.up('Space');
        await page.keyboard.up('Shift');
        await page.mouse.up();
        for (const [width, height] of sizes) {
          await page.setViewportSize({ width, height });
          await page.waitForTimeout(150);
          await assertContained(page, '#arena-menu', `menu ${width}x${height} DPR ${deviceScaleFactor}`);
          await assertContained(page, '#network-info', `transport ${width}x${height} DPR ${deviceScaleFactor}`);
          const clearOfLoadout = await page.evaluate(() => {
            const canvas = document.querySelector('#canvas').getBoundingClientRect();
            const info = document.querySelector('#network-info').getBoundingClientRect();
            return info.bottom <= canvas.bottom - Math.min(canvas.width / 960, canvas.height / 540) * 74 + 1;
          });
          assert(clearOfLoadout, 'transport controls must not overlap canvas loadout');
          await page.screenshot({ path: path.join(output, `menu-${width}x${height}-dpr${deviceScaleFactor}.png`) });
          checks.push(`menu/transport containment and loadout clearance ${width}x${height} DPR ${deviceScaleFactor}`);
        }
        await page.setViewportSize({ width: 1280, height: 720 });
        await page.screenshot({ path: path.join(output, `live-menu-dpr${deviceScaleFactor}.png`) });
        checks.push(`primary mouse fire active and blocked by menu DPR ${deviceScaleFactor}`);
        checks.push(`Escape suppresses input/fire/dash while authoritative snapshots tick DPR ${deviceScaleFactor}`);

        // Accept a semantic checkbox/switch or a stateful button. A constant label with no
        // checked/pressed state is deliberately not enough evidence that the setting changed.
        const effects = page.getByRole('checkbox', { name: /reduced effects/i })
          .or(page.getByRole('switch', { name: /reduced effects/i }))
          .or(page.getByRole('button', { name: /reduced effects|effects:/i }));
        await effects.waitFor({ state: 'visible' });
        const fullState = await effectsState(effects);
        await effects.click();
        const reducedState = await effectsState(effects);
        assert.notEqual(reducedState, fullState, 'effects toggle must expose changed state');
        await page.screenshot({ path: path.join(output, `reduced-effects-dpr${deviceScaleFactor}.png`) });
        await effects.click();
        assert.equal(await effectsState(effects), fullState, 'effects toggle must restore original state');
        await resume.click();
        await resume.waitFor({ state: 'hidden' });
        const resumeMarker = await inputMarker(page);
        await page.keyboard.down('a');
        await page.keyboard.down('Space');
        await waitForInputs(page, resumeMarker, 'active');
        await page.keyboard.up('a');
        await page.keyboard.up('Space');
        await waitForTicks(page, (await snapshot(page)).tick);
        await page.screenshot({ path: path.join(output, `resumed-dpr${deviceScaleFactor}.png`) });
        checks.push(`effects state round-trip and Resume restores live input DPR ${deviceScaleFactor}`);

        // A deliberate real transport close tests the disconnect/retry UI without depending
        // on enemy damage/death timing or pretending a server error was received.
        await page.evaluate(() => {
          const sockets = window.__presentationProbe.sockets.filter(socket => socket.readyState === WebSocket.OPEN);
          if (sockets.length !== 1) throw new Error(`Expected one active arena socket, found ${sockets.length}`);
          sockets[0].close(1000, 'presentation acceptance disconnect');
        });
        await page.waitForFunction(() => /disconnected|connection (?:lost|closed|failed)|network error/i.test(document.body.innerText),
          null, { timeout });
        await page.screenshot({ path: path.join(output, `disconnected-dpr${deviceScaleFactor}.png`) });
        // Retry may restore the join form directly, or expose an explicit Retry/Reconnect action.
        if (!(await page.locator('#join-form').isVisible())) {
          await page.getByRole('button', { name: /^(?:retry|reconnect|join again)(?: arena)?$/i }).click();
        }
        await page.locator('#join-form').waitFor({ state: 'visible' });
        await join(page, `Retry${deviceScaleFactor === 1 ? 'One' : 'Two'}`);
        await page.keyboard.press('Escape');
        await resume.waitFor({ state: 'visible' });
        await page.getByRole('button', { name: /^leave(?: arena| game)?$/i }).click();
        await page.locator('#join-form').waitFor({ state: 'visible' });
        await page.waitForFunction(() => window.__presentationProbe.sockets.every(socket =>
          socket.readyState === WebSocket.CLOSED || socket.readyState === WebSocket.CLOSING), null, { timeout });
        await assertContained(page, '#join-form', 'leave returns usable join form');
        await page.screenshot({ path: path.join(output, `left-dpr${deviceScaleFactor}.png`) });
        checks.push(`real disconnect, retry join and intentional Leave DPR ${deviceScaleFactor}`);
        assert.deepEqual(errors, [], 'no unhandled page errors');
        assert(Number.isFinite(initial.id) && initial.id > 0, 'server assigned player ID');
      } catch (error) {
        if (currentPage && !currentPage.isClosed()) {
          await currentPage.screenshot({ path: path.join(output, `failure-dpr${deviceScaleFactor}.png`) }).catch(() => {});
        }
        throw error;
      } finally {
        await context.close();
        currentPage = undefined;
      }
    }
    console.log(JSON.stringify({ result: 'PASS', checks, screenshots: output,
      limits: ['Effects checked through exposed UI state, not pixel parity.',
        'Input suppression checked on real outbound frames; no death/respawn or load/capacity claim.'] }));
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
