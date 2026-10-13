import { control } from './interface.js';

export function createServicePanel(el, panel, send, shop, navigate) {
  const root = el('div', '', 'service-board');
  panel.insertBefore(root, panel.lastElementChild);
  const style = el('style');
  style.textContent = `#sector-console[data-mode=outpost] { display:flex; flex-direction:column; height:min(740px, calc(100dvh - 32px)); overflow:hidden; }
#sector-console[data-mode=outpost] > :not(.service-board) { flex-shrink:0; }
.service-board { min-height:0; flex:1; max-height:calc(100vh - 240px); overflow:auto; padding:4px 12px 12px 0; }
.service-board button { margin:4px 8px 4px 0; } .service-board p { margin:10px 0; }
.outpost-schematic { float:left; width:220px; margin:8px 16px 8px 0 !important; }
#sector-console[data-mode=outpost] .sector-status:empty { display:none; }
.outpost-sockets { display:grid; grid-template-columns:1fr 1fr; gap:8px; }
.outpost-socket { border:1px solid #446176; background:#071321; padding:8px 12px; }`;
  document.head.append(style);
  let previous = '',
    chatter = 0;
  const greetings = {
    trader: [
      'Keep your engines warm and your invoices tidy. Looking for a new firing pattern?',
      'My cargo is reliable. My route? That depends on who is shooting at it.',
    ],
    explorer: [
      'Those rocks hide more than a good ambush. I pay for clean survey data.',
      'A slow approach and a quiet scanner beat a fast funeral.',
    ],
    bounty: [
      'Factories keep sending ships. Help thin their patrols and I will make it worth your time.',
      'Every wreck was somebody who thought they had the better angle.',
    ],
  };
  return {
    render(mode, target, choices, frame, actionPending) {
      root.hidden = !['contact', 'outpost', 'chooser'].includes(mode);
      if (root.hidden) {
        previous = '';
        return;
      }
      const services = frame.sector?.services,
        job = services?.assignment;
      const key = JSON.stringify([
        mode,
        target,
        choices,
        services,
        frame.workshop?.credits,
        actionPending,
        chatter,
      ]);
      if (previous === key) return;
      previous = key;
      const focus = document.activeElement?.dataset.serviceAction;
      root.replaceChildren();
      const add = (label, icon, action, id, disabled = false) => {
        const button = el('button', label);
        control(button, icon);
        button.dataset.serviceAction = `${action}:${id}`;
        button.disabled = !!actionPending || disabled;
        button.onclick = () => send(action, id);
        root.append(button);
        return button;
      };
      if (mode === 'chooser') {
        root.append(el('p', 'Choose a nearby ship or service.'));
        for (const choice of choices) add(choice.name, 'locate', choice.action, choice.id);
      } else if (mode === 'contact') {
        const contact = services?.contacts.find((c) => c.id === services.hailing);
        if (!contact) {
          root.append(el('p', 'Opening channel…'));
          return;
        }
        root.append(el('h3', `${contact.name} · ${contact.role.toUpperCase()}`));
        root.append(el('p', greetings[contact.role][chatter % 2]));
        const talk = add('Chat', 'friends', 'chat', contact.id);
        talk.onclick = () => {
          chatter++;
          previous = '';
          this.render(mode, target, choices, frame, actionPending);
        };
        if (contact.role === 'trader') {
          const trade = add('Trade modules', 'shop', 'trade', contact.id);
          trade.onclick = shop;
        } else
          add(
            contact.role === 'explorer'
              ? 'Work: search 3 caches · 40 credits / 30 XP'
              : 'Work: defeat 3 patrol ships · 60 credits / 45 XP',
            'contract',
            'job_accept',
            contact.id,
            !!job?.kind || job?.cooldown > 0,
          );
        if (job?.kind)
          root.append(
            el(
              'p',
              `${job.kind.toUpperCase()} · ${Math.min(3, job.progress)}/3 · ${job.ready ? 'Ready to claim' : 'In progress'}`,
            ),
          );
        if (job?.cooldown) root.append(el('p', `Next assignment in ${job.cooldown}s`));
        if (job?.ready)
          add(
            `Claim assignment · ${job.credits} credits / ${job.experience} XP`,
            'apply',
            'job_claim',
            0,
          );
        for (const id of (job?.targets || []).filter(
          (id) => !services.claimedCaches.includes(id),
        )) {
          const locate = add(`Locate cache ${id - 99}`, 'locate', 'locate_cache', id);
          locate.onclick = () => navigate(id);
        }
        add('Goodbye', 'exit', 'goodbye', 0);
      } else {
        const site = services?.outposts.find((s) => s.id === target);
        if (!site) return;
        root.append(el('h3', `${site.name} · ${site.active ? 'SHARED OUTPOST' : 'INACTIVE'}`));
        root.append(
          el(
            'p',
            `${frame.workshop?.credits || 0} credits · Live combat area — no sanctuary. Repairs reach 350 units.`,
          ),
        );
        const schematic = el('div', '', 'outpost-schematic');
        schematic.style.cssText =
          'position:relative;height:160px;max-width:420px;border:1px solid #446176;margin:12px 0';
        for (const socket of site.sockets) {
          const mark = el('span', ['CORE', 'T1', 'T2', 'BOT'][socket.slot]);
          mark.style.cssText = `position:absolute;left:${[40, 12, 70, 40][socket.slot]}%;top:${[25, 55, 55, 75][socket.slot]}%;color:${socket.hull ? '#65e6be' : '#71818f'}`;
          schematic.append(mark);
        }
        root.append(schematic);
        const sockets = el('div', '', 'outpost-sockets');
        root.append(sockets);
        for (const socket of site.sockets) {
          const card = el('div', '', 'outpost-socket');
          sockets.append(card);
          const name = ['Service core', 'Turret 1', 'Turret 2', 'Repair drone bay'][socket.slot];
          card.append(
            el(
              'p',
              `${name} · ${socket.hull}/${socket.maxHull} hull${socket.pending ? ' · Transaction pending' : ''}`,
            ),
          );
          const action = ['activate', 'turret_one', 'turret_two', 'repair_bot'][socket.slot];
          const build = add(
            `${socket.slot ? 'Build' : 'Activate'} ${name} · ${socket.cost} credits`,
            'apply',
            action,
            site.id,
            socket.hull > 0 ||
              socket.pending ||
              (socket.slot > 0 && !site.active) ||
              (frame.workshop?.credits || 0) < socket.cost,
          );
          card.append(build);
          build.onclick = () => {
            if (confirm(`${name}: spend ${socket.cost} credits for this shared outpost?`))
              send(action, site.id);
          };
        }
      }
      if (focus)
        [...root.querySelectorAll('button')]
          .find((b) => b.dataset.serviceAction === focus)
          ?.focus({ preventScroll: true });
    },
  };
}
