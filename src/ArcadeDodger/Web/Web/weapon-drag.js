import { glyph, color } from './weapon-art.js';

// Owns pointer capture, drag feedback, auto-scroll and click suppression.
export function createModuleDrag({
  element,
  panel,
  body,
  inventory,
  slots,
  weaponButtons,
  rows,
  accepts,
  part,
  spare,
  place,
  remove,
  describe,
  render,
  choose,
  canInteract,
}) {
  let drag = null,
    ghost = null,
    suppressClick = false;
  function clearDrop() {
    [...slots, ...weaponButtons].forEach((s) => delete s.dataset.drop);
    delete inventory.dataset.drop;
  }
  function cancelDrag() {
    const old = drag;
    drag = null;
    if (old?.node.hasPointerCapture(old.pointerId)) old.node.releasePointerCapture(old.pointerId);
    ghost?.remove();
    ghost = null;
    clearDrop();
  }
  function bind(node, source) {
    node.addEventListener('pointerdown', (e) => {
      suppressClick = false;
      if (e.button !== 0 || !canInteract()) return;
      const from = source();
      if (!from.module || (from.slot < 0 && spare(from.module) <= 0)) return;
      e.preventDefault();
      node.focus({ preventScroll: true });
      drag = { ...from, node, pointerId: e.pointerId, x: e.clientX, y: e.clientY, moving: false };
      node.setPointerCapture(e.pointerId);
    });
    node.addEventListener('lostpointercapture', () => {
      if (drag?.node === node) cancelDrag();
    });
    node.addEventListener('click', (e) => {
      if (e.detail === 0) suppressClick = false;
      choose(source());
    });
    node.addEventListener('pointerenter', () => {
      if (!drag) {
        const s = source();
        describe(s.module, s.slot);
      }
    });
    node.addEventListener('focus', () => {
      const s = source();
      describe(s.module, s.slot);
      render();
    });
  }
  panel.addEventListener('pointermove', (e) => {
    if (!drag || e.pointerId !== drag.pointerId) return;
    if (!drag.moving && Math.hypot(e.clientX - drag.x, e.clientY - drag.y) < 6) return;
    drag.moving = true;
    e.preventDefault();
    if (!ghost) {
      ghost = element('div', glyph(drag.module), 'module-drag-ghost');
      ghost.style.setProperty('--part', color(drag.module));
      ghost.setAttribute('aria-hidden', 'true');
      document.body.append(ghost);
    }
    ghost.style.left = e.clientX + 'px';
    ghost.style.top = e.clientY + 'px';
    clearDrop();
    const target = document
      .elementFromPoint(e.clientX, e.clientY)
      ?.closest('[data-slot],[data-weapon],.module-catalog');
    if (target?.dataset.slot !== undefined)
      target.dataset.drop =
        accepts(+target.dataset.slot, drag.module) &&
        (drag.slot < 0 || accepts(drag.slot, part(+target.dataset.slot)))
          ? 'valid'
          : 'invalid';
    else if (target === inventory && drag.slot >= 0) inventory.dataset.drop = 'valid';
    else if (target?.dataset.weapon !== undefined && drag.slot >= 0) target.dataset.drop = 'valid';
    for (const { scroll } of rows) {
      const b = scroll.getBoundingClientRect();
      if (e.clientY >= b.top && e.clientY <= b.bottom) {
        if (e.clientX > b.right - 28) scroll.scrollLeft += 18;
        else if (e.clientX < b.left + 28) scroll.scrollLeft -= 18;
      }
    }
    const bounds = body.getBoundingClientRect();
    if (e.clientY < bounds.top + 35) body.scrollTop -= 15;
    else if (e.clientY > bounds.bottom - 35) body.scrollTop += 15;
  });
  panel.addEventListener('pointerup', (e) => {
    if (!drag || e.pointerId !== drag.pointerId) return;
    const source = drag,
      target = document
        .elementFromPoint(e.clientX, e.clientY)
        ?.closest('[data-slot],[data-weapon],.module-catalog');
    if (source.moving) {
      suppressClick = true;
      e.preventDefault();
      if (target?.dataset.slot !== undefined) place(source, +target.dataset.slot);
      else if (target === inventory && source.slot >= 0) remove(source.slot);
      else if (target?.dataset.weapon !== undefined && source.slot >= 0)
        place(source, +target.dataset.weapon * 9 + (source.slot % 9));
    }
    cancelDrag();
  });
  panel.addEventListener('pointercancel', cancelDrag);
  window.addEventListener('blur', cancelDrag);
  document.addEventListener('visibilitychange', () => {
    if (document.hidden) cancelDrag();
  });
  return {
    bind,
    cancelDrag,
    consumeClick() {
      const suppressed = suppressClick;
      suppressClick = false;
      return suppressed;
    },
    resetClick() {
      suppressClick = false;
    },
  };
}
