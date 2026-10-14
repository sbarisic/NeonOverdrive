const assert = require('node:assert/strict');
const { chromium } = require('../artifacts/browser-check/node_modules/playwright');
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    const errors = [];
    page.on('pageerror', (e) => errors.push(String(e)));
    await page.goto(process.env.ARENA_URL || 'http://localhost:5094');
    await page.locator('#nickname').fill('Menu' + Date.now().toString(36));
    await page.getByRole('button', { name: 'Join arena', exact: true }).click();
    await page.waitForFunction(() => document.querySelector('#network-info')?.dataset.snapshot);
    await page.getByRole('button', { name: 'Menu [Esc]', exact: true }).click();
    const panels = {
      pilot: '#arena-menu',
      skills: '#arena-skills',
      arsenal: '#weapon-bench',
      alliances: '#arena-friends',
      map: '#sector-console',
      station: '#sector-console',
    };
    let active = 'pilot';
    async function navigate(next) {
      await page.locator(`${panels[active]} .menu-navigation [data-menu="${next}"]`).click();
      active = next;
      assert.equal(await page.locator('[role="dialog"]:visible').count(), 1);
      assert.equal(
        await page
          .locator(`${panels[next]} .menu-navigation [data-menu="${next}"]`)
          .getAttribute('aria-current'),
        'page',
      );
      await page.waitForTimeout(150);
      assert(await page.locator(panels[next]).evaluate((p) => p.contains(document.activeElement)));
    }
    for (const size of [
      { width: 1440, height: 900 },
      { width: 1280, height: 720 },
    ]) {
      await page.setViewportSize(size);
      for (const next of ['skills', 'arsenal', 'alliances', 'map', 'pilot']) {
        await navigate(next);
        const nav = page.locator(`${panels[next]} .menu-navigation`);
        assert.equal(await nav.locator('button').count(), 6);
        assert(await nav.locator('[data-menu="station"]').isDisabled());
        const bounds = await nav.boundingBox();
        assert(bounds.x >= 0 && bounds.y >= 0 && bounds.x + bounds.width <= size.width);
      }
      await navigate('alliances');
      const send = page.getByRole('button', { name: 'Send request', exact: true });
      assert(await send.evaluate((b) => b.classList.contains('ui-action')));
      assert((await send.boundingBox()).height <= 38);
      await page.screenshot({ path: `artifacts/browser-check/menu-alliances-${size.width}.png` });
      await navigate('pilot');
    }
    const resume = page.getByRole('button', { name: 'Resume', exact: true });
    await resume.focus();
    await resume.press('Space');
    await page.locator('#arena-menu').waitFor({ state: 'hidden' });
    assert(
      !(await page
        .locator('#network-info')
        .evaluate((e) =>
          JSON.parse(e.dataset.snapshot).shots.some((s) => s.owner === +e.dataset.player),
        )),
      'focused Space must not fire',
    );
    await page.keyboard.press('e');
    const dock = page.getByRole('button', { name: 'Dock at Arrival Port', exact: true });
    if (await dock.isVisible()) await dock.click();
    await page.locator('#sector-console[data-mode="station"]').waitFor({ state: 'visible' });
    active = 'station';
    for (const next of ['arsenal', 'skills', 'alliances', 'map', 'pilot', 'station'])
      await navigate(next);
    await page.screenshot({ path: 'artifacts/browser-check/menu-station.png' });
    await page
      .locator('#sector-console')
      .getByRole('button', { name: 'Undock [E / Esc]', exact: true })
      .click();
    await page.locator('#sector-console').waitFor({ state: 'hidden' });
    assert.deepEqual(errors, []);
    console.log(
      'PASS: all menus connected by icon buttons, single modal, focus stability, desktop layouts, docked station access and styled alliance controls.',
    );
  } finally {
    await browser.close();
  }
})().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});
