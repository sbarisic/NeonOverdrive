import { friendStyles } from './friends.styles.js';
export function createFriendsMenu(element, info, send, onToggle) {
  document.head.append(element('style', friendStyles));
  const opener = element('button', 'Friends [P]');
  opener.type = 'button';
  opener.hidden = true;
  info.append(opener);
  const panel = element('section', '', 'arena-shell');
  panel.id = 'arena-friends';
  panel.hidden = true;
  panel.setAttribute('role', 'dialog');
  panel.setAttribute('aria-modal', 'true');
  panel.setAttribute('aria-labelledby', 'friends-title');
  opener.setAttribute('aria-controls', panel.id);
  opener.setAttribute('aria-expanded', 'false');
  const title = element('h2', 'PILOT ALLIANCES');
  title.id = 'friends-title';
  title.tabIndex = -1;
  const state = element('p', '', 'friend-state');
  state.setAttribute('role', 'status');
  const body = element('div', '', 'friends-body'),
    form = element('form'),
    label = element('label', 'Request by player name'),
    name = element('input');
  name.id = 'friend-name';
  name.maxLength = 16;
  name.pattern = '[A-Za-z0-9 _\\-]+';
  name.required = true;
  label.htmlFor = name.id;
  label.append(name);
  const request = element('button', 'Send request');
  request.type = 'submit';
  form.append(label, request);
  const close = element('button', 'Return to arena [P / Esc]');
  close.type = 'button';
  panel.append(
    title,
    element(
      'p',
      'Friendships become mutual only after the recipient accepts. Friendly shots, explosions and beams pass harmlessly through both pilots.',
      'friend-note',
    ),
    form,
    body,
    state,
    close,
  );
  document.body.append(panel);
  let opened = false,
    available = false,
    lastKey = '',
    pending = 0;
  const key = (s) => s.trim().toUpperCase();
  function action(target, kind) {
    pending = send(target, kind) || 0;
    state.textContent = pending ? 'Waiting for server…' : 'Not connected.';
  }
  form.addEventListener('submit', (e) => {
    e.preventDefault();
    if (name.value.trim()) action(name.value.trim(), 'request');
  });
  function setOpen(value) {
    opened = value && available;
    panel.hidden = !opened;
    info.inert = opened;
    opener.setAttribute('aria-expanded', String(opened));
    onToggle(opened);
    if (opened) title.focus({ preventScroll: true });
  }
  opener.addEventListener('click', () => setOpen(true));
  close.addEventListener('click', () => setOpen(false));
  function section(title, entries) {
    body.append(element('h3', title));
    if (!entries.length) body.append(element('p', 'None yet.', 'friend-note'));
    for (const [name, note, actions] of entries) {
      const row = element('div', '', 'friend-row');
      row.dataset.name = name;
      row.append(element('strong', name), element('span', note, 'friend-note'));
      for (const [caption, kind] of actions) {
        const button = element('button', caption);
        button.type = 'button';
        button.dataset.action = kind;
        button.addEventListener('click', () => action(name, kind));
        row.append(button);
      }
      body.append(row);
    }
  }
  return {
    get open() {
      return opened;
    },
    setOpen,
    update(self, frame, connected) {
      available = connected && !!self;
      opener.hidden = !available;
      if (!available) {
        pending = 0;
        lastKey = '';
        if (opened) setOpen(false);
        return;
      }
      const social = frame.social || { friends: [], incoming: [], outgoing: [] };
      opener.textContent = `Friends [P]${social.incoming.length ? ` +${social.incoming.length}` : ''}`;
      if (pending && social.sequence >= pending) {
        pending = 0;
        state.textContent =
          social.result === 'ok'
            ? 'Saved.'
            : `Request not applied: ${social.result.replaceAll('_', ' ')}.`;
      }
      const present = frame.players.filter((p) => p.connected && key(p.name) !== key(self.name));
      const signature = JSON.stringify([
        social.friends,
        social.incoming,
        social.outgoing,
        present.map((p) => p.name),
      ]);
      if (signature === lastKey) return;
      lastKey = signature;
      const active = document.activeElement,
        focusName = active?.closest('.friend-row')?.dataset.name,
        focusAction = active?.dataset.action;
      body.replaceChildren();
      section(
        'INCOMING REQUESTS',
        social.incoming.map((n) => [
          n,
          'Awaiting your acceptance',
          [
            ['Accept', 'accept'],
            ['Decline', 'decline'],
          ],
        ]),
      );
      section(
        'FRIENDS',
        social.friends.map((n) => [
          n,
          present.some((p) => key(p.name) === key(n)) ? 'In this arena' : 'Offline / another arena',
          [['Remove', 'remove']],
        ]),
      );
      section(
        'SENT REQUESTS',
        social.outgoing.map((n) => [n, 'Waiting for acceptance', [['Cancel', 'cancel']]]),
      );
      const known = new Set([...social.friends, ...social.incoming, ...social.outgoing].map(key));
      section(
        'OTHER PILOTS IN THIS ARENA',
        [...new Map(present.map((p) => [key(p.name), p])).values()]
          .filter((p) => !known.has(key(p.name)))
          .map((p) => [p.name, '', [['Add friend', 'request']]]),
      );
      if (opened && focusName) {
        const next = [...body.querySelectorAll('.friend-row')]
          .find((r) => r.dataset.name === focusName)
          ?.querySelector(`[data-action="${focusAction}"]`);
        (next || title).focus({ preventScroll: true });
      }
    },
    keydown(e) {
      if (!opened) return false;
      if (e.code === 'Escape' || (e.code === 'KeyP' && e.target !== name)) {
        e.preventDefault();
        if (!e.repeat) setOpen(false);
        return true;
      }
      if (e.key === 'Tab') {
        const controls = [...panel.querySelectorAll('input,button')];
        if (
          e.shiftKey &&
          (document.activeElement === controls[0] || !controls.includes(document.activeElement))
        ) {
          e.preventDefault();
          controls.at(-1).focus();
        } else if (
          !e.shiftKey &&
          (document.activeElement === controls.at(-1) || !controls.includes(document.activeElement))
        ) {
          e.preventDefault();
          controls[0].focus();
        }
      }
      return true;
    },
  };
}
