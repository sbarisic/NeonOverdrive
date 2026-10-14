// Runtime-reported resource counts are genuine progress for that phase, not a simulated overall percentage.
export function createLoadingScreen(root = document.getElementById('status')) {
  const title = root.querySelector('[data-loading-title]');
  const detail = root.querySelector('[data-loading-detail]');
  const bar = root.querySelector('[role="progressbar"]');
  const fill = root.querySelector('[data-loading-fill]');
  const count = root.querySelector('[data-loading-count]');
  const progressLabel = root.querySelector('[data-loading-label]');
  const steps = [...root.querySelectorAll('[data-loading-step]')];
  const retry = root.querySelector('[data-loading-retry]');
  let stage = 0;
  retry.addEventListener('click', () => window.location.reload());

  function phase(index, heading, message) {
    stage = index;
    root.dataset.phase = String(index);
    title.textContent = heading;
    detail.textContent = message;
    count.textContent = 'In progress';
    progressLabel.textContent = index === 0 ? 'Resource loading' : 'Launch sequence';
    bar.removeAttribute('aria-valuenow');
    bar.removeAttribute('aria-valuetext');
    bar.setAttribute('aria-label', heading);
    fill.style.removeProperty('width');
    steps.forEach((step, i) => {
      step.dataset.state = i < index ? 'done' : i === index ? 'active' : 'waiting';
      if (i === index) step.setAttribute('aria-current', 'step');
      else step.removeAttribute('aria-current');
    });
  }

  return {
    phase,
    progress(loaded, total) {
      if (stage !== 0 || !Number.isFinite(total) || total <= 0) return;
      const done = Math.max(0, Math.min(total, loaded));
      const percent = Math.floor((done / total) * 100);
      title.textContent = done === total ? 'Preparing flight systems' : 'Loading flight systems';
      detail.textContent = `${done} of ${total} runtime resources received`;
      count.textContent = `${percent}%`;
      bar.setAttribute('aria-valuenow', String(percent));
      bar.setAttribute('aria-valuetext', `${done} of ${total} runtime resources received`);
      fill.style.width = `${percent}%`;
    },
    ready() {
      root.hidden = true;
    },
    fail(error, heading = 'Launch interrupted') {
      root.hidden = false;
      root.dataset.failed = 'true';
      title.textContent = heading;
      detail.textContent = String(error?.message || error);
      bar.hidden = true;
      count.textContent = 'Unable to continue';
      retry.hidden = false;
      retry.focus();
    },
  };
}
