// Exercise the visible service controls now that E adjusts throttle.
async function interact(page) {
  const docked = await page
    .locator('#network-info')
    .evaluate((info) => JSON.parse(info.dataset.snapshot).sector.pilot.docked);
  if (docked) {
    const undock = page.getByRole('button', { name: 'Undock [Esc]', exact: true });
    if (!(await undock.isVisible()))
      await page.getByRole('button', { name: 'Station services', exact: true }).click();
    await undock.click();
  } else {
    await page.getByRole('button', { name: /^(Hail \/ Services|Dock|Interact)$/ }).click();
  }
}

module.exports = { interact };
