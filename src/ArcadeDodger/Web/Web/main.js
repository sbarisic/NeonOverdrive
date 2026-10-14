import { createLoadingScreen } from './loading-screen.js';
const canvas = document.getElementById('canvas');
const status = document.getElementById('status');
const loading = createLoadingScreen(status);
const paintLoading = () =>
  new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
try {
  const { dotnet } = await import('./_framework/dotnet.js');
  const { getAssemblyExports, getConfig, runMain } = await dotnet
    .withModuleConfig({ onDownloadResourceProgress: loading.progress })
    .create();
  loading.phase(
    1,
    'Starting flight systems',
    'Preparing the renderer, instruments, and sector interface.',
  );
  await paintLoading();
  const config = getConfig();
  const exports = await getAssemblyExports(config.mainAssemblyName);
  const host = exports.ArcadeDodger.Web.BrowserHost;
  dotnet.instance.Module.canvas = canvas;
  await runMain();
  loading.phase(2, 'Preparing pilot console', 'Getting your multiplayer flight console ready.');
  await paintLoading();
  canvas.focus();
  const { startNetwork } = await import('./network.js');
  startNetwork(host, canvas, status);
  loading.ready();
  let running = true;
  window.addEventListener(
    'pagehide',
    () => {
      if (running) {
        running = false;
        host.Close();
      }
    },
    { once: true },
  );
  function frame() {
    if (!running) return;
    try {
      const bounds = canvas.getBoundingClientRect();
      if (!host.UpdateFrame(bounds.width, bounds.height, window.devicePixelRatio || 1)) {
        running = false;
        host.Close();
        return;
      }
    } catch (error) {
      running = false;
      loading.fail(error, 'Flight systems stopped');
      console.error(error);
      try {
        host.Close();
      } catch {}
      return;
    }
    requestAnimationFrame(frame);
  }
  requestAnimationFrame(frame);
} catch (error) {
  loading.fail(error);
  console.error(error);
}
