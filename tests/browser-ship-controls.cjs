const assert = require('node:assert/strict');
const { chromium } = require('../artifacts/browser-check/node_modules/playwright');
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    for (const dpr of [1, 2]) {
      const page = await browser.newPage({
        viewport: { width: 1280, height: 720 },
        deviceScaleFactor: dpr,
      });
      const errors = [];
      page.on('pageerror', (e) => errors.push(String(e)));
      await page.route('**/main.js*', async (route) => {
        const response = await route.fetch();
        await route.fulfill({
          response,
          body: (await response.text()).replace(
            'const host = exports.ArcadeDodger.Web.BrowserHost;',
            'const host = exports.ArcadeDodger.Web.BrowserHost; window.testHost=host;',
          ),
        });
      });
      await page.addInitScript(() => {
        window.sent = [];
        const original = WebSocket.prototype.send;
        WebSocket.prototype.send = function (text) {
          try {
            window.sent.push(JSON.parse(text));
            if (window.sent.length > 300) window.sent.shift();
          } catch {}
          return original.call(this, text);
        };
      });
      await page.goto(process.env.ARENA_URL || 'http://localhost:5098');
      await page.locator('#nickname').fill('Control' + dpr + Date.now().toString(36));
      await page.getByRole('button', { name: 'Join arena', exact: true }).click();
      await page.waitForFunction(() => document.querySelector('#network-info')?.dataset.snapshot);
      const read = () =>
        page.evaluate(() => {
          const n = document.querySelector('#network-info'),
            f = JSON.parse(n.dataset.snapshot);
          return f.players.find((p) => p.id === +n.dataset.player);
        });
      await page.locator('#canvas').focus();
      await page.mouse.move(640, 250);
      await page.mouse.down({ button: 'right' });
      await page.waitForTimeout(500);
      await page.mouse.up({ button: 'right' });
      await page.waitForTimeout(100);
      const steered = await read();
      assert(steered.heading < -0.3);
      assert.equal(steered.throttle, 0);
      assert.equal(steered.inertia, 1);
      await page.waitForTimeout(150);
      assert(Math.abs((await read()).heading - steered.heading) < 0.01);
      assert(await page.evaluate(() => window.sent.some((m) => m.cursorSteering)));
      await page.evaluate(() => (window.sent.length = 0));
      await page.mouse.click(800, 310);
      await page.waitForTimeout(90);
      assert(
        !(await page.evaluate(() => window.sent.some((m) => m.fire))),
        'short click must not fire',
      );
      await page.mouse.down();
      await page.waitForTimeout(300);
      assert(await page.evaluate(() => window.sent.some((m) => m.fire)));
      await page.mouse.up();
      // Chorded mouse presses do not produce a second pointerdown. Exercise
      // actual browser mouse events in both orders and each partial release.
      for (const firstButton of ['right', 'left']) {
        const secondButton = firstButton === 'right' ? 'left' : 'right';
        await page.mouse.down({ button: firstButton });
        await page.mouse.down({ button: secondButton });
        await page.waitForTimeout(250);
        assert(
          await page.evaluate(() => window.sent.at(-1).fire && window.sent.at(-1).cursorSteering),
        );
        await page.mouse.up({ button: firstButton });
        await page.waitForTimeout(60);
        const one = await page.evaluate(() => window.sent.at(-1));
        assert.equal(one.fire, secondButton === 'left');
        assert.equal(one.cursorSteering, secondButton === 'right');
        await page.mouse.up({ button: secondButton });
        await page.waitForTimeout(50);
        assert(
          await page.evaluate(() => !window.sent.at(-1).fire && !window.sent.at(-1).cursorSteering),
        );
      }
      await page.mouse.down({ button: 'left' });
      await page.mouse.down({ button: 'right' });
      await page.waitForTimeout(220);
      await page.evaluate(() => window.dispatchEvent(new Event('blur')));
      await page.mouse.move(810, 320);
      await page.waitForTimeout(70);
      assert(
        await page.evaluate(() => !window.sent.at(-1).fire && !window.sent.at(-1).cursorSteering),
      );
      await page.mouse.up({ button: 'left' });
      await page.mouse.up({ button: 'right' });
      // Keyboard controls remain independent, and cancelling a chord cannot revive it.
      await page.locator('#canvas').focus();
      await page.mouse.move(800, 310);
      await page.mouse.down({ button: 'left' });
      await page.waitForTimeout(220);
      await page.mouse.down({ button: 'right' });
      assert(
        await page.evaluate(() => window.sent.at(-1).fire),
        'RMB does not restart the firing threshold',
      );
      await page.keyboard.down('w');
      await page.keyboard.down('a');
      await page.waitForTimeout(80);
      assert(
        await page.evaluate(
          () =>
            window.sent.at(-1).x === -1 && window.sent.at(-1).y === -1 && window.sent.at(-1).fire,
        ),
      );
      await page.keyboard.up('w');
      await page.keyboard.up('a');
      await page.keyboard.down('x');
      await page.waitForTimeout(80);
      assert(
        await page.evaluate(
          () =>
            window.sent.at(-1).brake &&
            window.sent.at(-1).fire &&
            window.sent.at(-1).cursorSteering,
        ),
      );
      await page.keyboard.up('x');
      await page.keyboard.down('Space');
      await page.mouse.up({ button: 'left' });
      await page.waitForTimeout(60);
      assert(await page.evaluate(() => window.sent.at(-1).fire), 'Space survives LMB release');
      await page.keyboard.up('Space');
      await page.evaluate(() =>
        document.querySelector('#canvas').dispatchEvent(new PointerEvent('pointercancel')),
      );
      await page.mouse.move(820, 320);
      assert(
        await page.evaluate(() => !window.sent.at(-1).fire && !window.sent.at(-1).cursorSteering),
      );
      await page.mouse.move(-20, -20);
      await page.mouse.up({ button: 'right' });
      await page.mouse.move(800, 310);
      assert(
        await page.evaluate(() => !window.sent.at(-1).fire && !window.sent.at(-1).cursorSteering),
      );
      assert((await page.locator('#context-actions button').count()) > 0);
      await page.waitForFunction(
        () => document.querySelectorAll('#ship-contacts button').length > 0,
      );
      const radioContact = await page.evaluate(() => {
        const f = JSON.parse(document.querySelector('#network-info').dataset.snapshot);
        const contacts = JSON.parse(window.testHost.ArenaContacts());
        return { world: f.sector.world.contacts, contacts };
      });
      for (const contact of radioContact.world || []) {
        const visible = radioContact.contacts.find((c) => c.key === 'npc:' + contact.id);
        if (visible) assert(visible.radio, `Missing radio action for ${contact.name}`);
      }
      const first = page.locator('#ship-contacts button').first();
      const key = await first.getAttribute('data-key');
      await page.locator(`#ship-contacts button[data-key="${key}"]`).click();
      await page.locator('#target-details').waitFor({ state: 'visible' });
      assert(/^(npc|player):/.test(key));
      const c = await page.evaluate(
        (key) => JSON.parse(window.testHost.ArenaContacts()).find((c) => c.key === key),
        key,
      );
      await page.keyboard.press('Escape');
      await page.locator('#target-details').waitFor({ state: 'hidden' });
      assert(await page.locator('#arena-menu').isHidden());
      const layout = await page.evaluate(() =>
        JSON.parse(document.querySelector('#network-info').dataset.layout),
      );
      await page.mouse.click(
        layout[10] + c.radarU * layout[12],
        layout[11] + c.radarV * layout[13],
      );
      if (await page.locator('#target-chooser').isVisible())
        await page.locator('#target-chooser button').first().click();
      await page.locator('#target-details').waitFor({ state: 'visible' });
      await page.locator('#radio-toggle').click();
      const close = page.locator('#radio-console button').filter({ hasText: 'Close' }).first();
      await close.evaluate((e) => (window.closeNode = e));
      await page.waitForTimeout(300);
      assert(await close.evaluate((e) => e === window.closeNode), 'snapshot preserves Close node');
      await page.keyboard.press('Tab'); // Skills owns the foreground, radio and feed must disappear.
      await page.locator('#arena-skills').waitFor({ state: 'visible' });
      assert(await page.locator('#radio-console').isHidden());
      assert(await page.locator('#radio-feed').isHidden());
      assert(await page.locator('#target-details').isHidden());
      await page.keyboard.press('Escape');
      assert(await page.locator('#radio-console').isHidden());
      await page.locator('#radio-toggle').click();
      await page.keyboard.press('Escape');
      assert(await page.locator('#radio-console').isHidden());
      await page.waitForTimeout(400);
      assert(await page.locator('#radio-console').isHidden());
      // An actual mouse press must survive a service snapshot arriving before release.
      await page.evaluate(async () => {
        const { createServicePanel } = await import('./sector-services.js');
        const el = (tag, text = '', cls = '') => {
          const n = document.createElement(tag);
          n.textContent = text;
          n.className = cls;
          return n;
        };
        const panel = el('section');
        panel.id = 'test-service';
        panel.style.cssText =
          'position:fixed;inset:120px 350px 180px 300px;background:#101827;z-index:100';
        panel.append(el('footer'));
        document.body.append(panel);
        window.testCalls = [];
        window.serviceTest = createServicePanel(
          el,
          panel,
          (...args) => window.testCalls.push(args),
          () => {},
          () => {},
        );
        window.serviceFrame = {
          sector: { services: { contacts: [], outposts: [], assignment: {}, availability: [] } },
          workshop: { credits: 100 },
        };
        window.renderService = () =>
          window.serviceTest.render(
            'chooser',
            0,
            [{ id: 42, action: 'manage', name: 'Manage test outpost' }],
            window.serviceFrame,
            99,
          );
        window.renderService();
      });
      const manage = page.locator('#test-service button');
      assert(await manage.isEnabled(), 'local navigation remains enabled during economic save');
      const box = await manage.boundingBox();
      await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
      await page.mouse.down();
      await manage.evaluate((e) => (window.pressedNode = e));
      await page.evaluate(() => {
        window.serviceFrame.workshop.credits = 90;
        window.renderService();
      });
      assert(await manage.evaluate((e) => e === window.pressedNode));
      await page.mouse.up();
      assert.deepEqual(await page.evaluate(() => window.testCalls), [['manage', 42]]);
      await page.evaluate(() => document.querySelector('#test-service').remove());
      await page.screenshot({ path: `artifacts/browser-check/ship-controls-${dpr}x.png` });
      assert.deepEqual(errors, []);
      await page.close();
    }
    console.log(
      'PASS: v14 cursor steering, click/hold intent, selection/list/minimap, radio lifecycle, menu layering, snapshot-safe controls at DPR 1/2.',
    );
  } finally {
    await browser.close();
  }
})().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});
