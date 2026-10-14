import { control } from './interface.js';
import { reconcile } from './stable-dom.js';
import { actionMessage } from './action-messages.js';

export function createContextActions(el, execute) {
  const root = el('section');
  root.id = 'context-actions';
  root.hidden = true;
  root.setAttribute('aria-label', 'Nearby actions');
  document.body.append(root);
  let notice = '',
    noticeUntil = 0;
  let expanded = false,
    current = [],
    pressed = false;
  root.addEventListener('pointerdown', () => {
    pressed = true;
  });
  const release = () => {
    pressed = false;
    setTimeout(render, 0);
  };
  window.addEventListener('pointerup', release);
  window.addEventListener('pointercancel', release);
  window.addEventListener('blur', release);
  root.addEventListener('keydown', (e) => {
    if (['Space', 'Enter'].includes(e.code)) e.stopPropagation();
    if (e.code === 'Escape' && expanded) {
      e.stopPropagation();
      expanded = false;
      render();
    }
  });
  function render() {
    if (pressed) return;
    const draft = el('div');
    function add(item, parent) {
      const button = el('button', item.label);
      button.type = 'button';
      button.dataset.contextAction = `${item.action}:${item.id}`;
      button.dataset.key = button.dataset.contextAction;
      control(button, item.icon || 'locate');
      button.title = item.reason ? actionMessage(item.reason) : item.label;
      button.disabled = !!item.reason || !!item.pending;
      button.onclick = () => execute(item);
      parent.append(button);
    }
    current.slice(0, 4).forEach((i) => add(i, draft));
    if (current.length > 4) {
      const more = el('button', expanded ? 'Less' : `More (${current.length - 4})`);
      more.dataset.contextAction = 'more';
      more.dataset.key = 'more';
      more.setAttribute('aria-expanded', String(expanded));
      control(more, 'loadout');
      more.style.cssText = 'position:absolute;right:0;bottom:100%';
      more.onclick = () => {
        expanded = !expanded;
        render();
      };
      draft.append(more);
      if (expanded) {
        const overflow = el('div');
        overflow.id = 'context-more';
        current.slice(4).forEach((i) => add(i, overflow));
        draft.append(overflow);
      }
    }
    if (notice && performance.now() < noticeUntil) {
      const status = el('div', notice);
      status.id = 'context-status';
      status.setAttribute('role', 'status');
      draft.append(status);
    }
    reconcile(root, draft);
  }
  return {
    report(message) {
      notice = message;
      noticeUntil = performance.now() + 5000;
      render();
    },
    update(items, alive) {
      current = items;
      root.hidden = !alive || (!items.length && performance.now() >= noticeUntil);
      const layout = JSON.parse(document.querySelector('#network-info')?.dataset.layout || '[]');
      // Reserve the centered radar and its independent contact list.
      if (layout.length) {
        root.style.left = `${layout[6] + 16}px`;
        root.style.top = `${layout[7] + 51}px`;
        root.style.width = `${Math.max(130, layout[10] - 216 - layout[6] - 16)}px`;
      } else {
        root.style.left = '20px';
        root.style.bottom = '18px';
        root.style.width = '240px';
      }
      if (!alive) expanded = false;
      render();
    },
  };
}
