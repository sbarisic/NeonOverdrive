// Reconcile live controls in place: snapshots must not replace a pressed/focused button.
export function reconcile(parent, source) {
  const key = (n) =>
    n.nodeType === 1
      ? n.tagName +
        ':' +
        (n.dataset.serviceAction ||
          n.dataset.radioAction ||
          n.dataset.key ||
          n.id ||
          (['BUTTON', 'SELECT'].includes(n.tagName)
            ? n.getAttribute('aria-label') || n.textContent
            : ''))
      : '#text';
  const used = new Set();
  const old = [...parent.childNodes];
  let reconcileIndex = 0;
  for (const fresh of [...source.childNodes]) {
    const existing = old.find((n) => !used.has(n) && key(n) === key(fresh));
    const node = existing || fresh;
    if (existing) {
      used.add(existing);
      if (node.nodeType === 3) {
        if (node.textContent !== fresh.textContent) node.textContent = fresh.textContent;
      } else {
        for (const a of [...node.attributes])
          if (!fresh.hasAttribute(a.name)) node.removeAttribute(a.name);
        for (const a of fresh.attributes)
          if (node.getAttribute(a.name) !== a.value) node.setAttribute(a.name, a.value);
        node.onclick = fresh.onclick;
        const value = fresh.value;
        node.onchange = fresh.onchange
          ? (event) => {
              fresh.value = node.value;
              fresh.onchange(event);
            }
          : null;
        reconcile(node, fresh);
        if (node.tagName === 'SELECT') node.value = value;
      }
    }
    const before = parent.childNodes[reconcileIndex] || null;
    if (before !== node) parent.insertBefore(node, before);
    reconcileIndex++;
  }
  for (const n of old) if (!used.has(n) && n.parentNode === parent) n.remove();
}
