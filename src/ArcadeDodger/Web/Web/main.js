import { dotnet } from './_framework/dotnet.js';
const canvas = document.getElementById('canvas');
const status = document.getElementById('status');
try {
  const { getAssemblyExports, getConfig, runMain } = await dotnet.create();
  const config = getConfig();
  const exports = await getAssemblyExports(config.mainAssemblyName);
  const host = exports.ArcadeDodger.Web.BrowserHost;
  dotnet.instance.Module.canvas = canvas;
  await runMain();
  status.hidden = true;
  canvas.focus();
  if (new URLSearchParams(location.search).has('multiplayer')) {
    const { startNetwork } = await import('./network.js');
    startNetwork(host, canvas, status);
  }
  let running = true;
  window.addEventListener('pagehide', () => {
    if (running) { running = false; host.Close(); }
  }, { once: true });
  function frame() {
    if (!running) return;
    try {
      const bounds = canvas.getBoundingClientRect();
      if (!host.UpdateFrame(bounds.width, bounds.height)) { running = false; host.Close(); return; }
    } catch (error) {
      running = false;
      status.hidden = false;
      status.textContent = `Game stopped: ${error}`;
      console.error(error);
      try { host.Close(); } catch {}
      return;
    }
    requestAnimationFrame(frame);
  }
  requestAnimationFrame(frame);
} catch (error) {
  status.hidden = false;
  status.textContent = `Could not start game: ${error}`;
  console.error(error);
}
