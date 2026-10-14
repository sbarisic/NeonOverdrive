export function installCrt(menu, host) {
  const style = document.createElement('style');
  style.textContent = `
body{--crt-glow:3px}
body[data-crt=off]{--crt-glow:0px}
body[data-crt=enhanced]{--crt-glow:6px}
.ui-action[data-tone=danger]{--action-color:#ff727c}
.ui-action[data-tone=success]{--action-color:#68efa0}
.ui-action[data-tone=purchase]{--action-color:#ffe078}
.ui-action[data-tone=special]{--action-color:#ef8aff}
.ui-action[data-tone=info]{--action-color:#82b8ff}
.ui-action[data-tone=action]{--action-color:#68eee8}
body .ui-action[data-tone]{color:var(--action-color)!important;background:linear-gradient(#172633,#091521);border-color:color-mix(in srgb,var(--action-color) 60%,#10212d)!important;border-left:3px solid var(--action-color);text-shadow:0 0 var(--crt-glow) color-mix(in srgb,var(--action-color) 30%,transparent)}
body .ui-action[data-tone]:hover:not(:disabled){background:color-mix(in srgb,var(--action-color) 15%,#10212d)}
body .ui-action[data-tone]:focus-visible{outline:2px solid var(--action-color);outline-offset:2px}
body .ui-action[data-tone]:disabled{opacity:.45;text-shadow:none}
body:not([data-crt=off]) :is(.arena-panel,.arena-surface){background-image:repeating-linear-gradient(0deg,#00000008 0px,#00000008 1px,transparent 1px,transparent 4px),linear-gradient(120deg,#12212dee,#0b101bee);box-shadow:inset 0 0 35px #07111c,0 0 12px #68eee815}
:is(.arena-panel,.arena-surface) h2{color:#7ee9f0;text-shadow:0 0 var(--crt-glow) #68eee870}
#crt-setting{margin:8px;color:#d7edf4;background:#101c28;padding:5px;border:1px solid #82b8ff}
#context-actions button.ui-action{position:relative;display:block;font-size:11px;min-height:27px;padding:3px 5px 3px 23px;min-width:0;max-width:100%;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;text-align:left}
#context-actions button.ui-action::before{position:absolute;left:4px;top:5px;width:14px;height:14px}
#context-actions{position:fixed;z-index:5;display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr);gap:3px;box-sizing:border-box}
#context-actions[hidden],.modal-open #context-actions{display:none}
#context-more{position:absolute;bottom:100%;left:0;width:100%;max-height:240px;overflow:auto;background:#081522;border:1px solid #68eee8;padding:5px;box-sizing:border-box}
#context-more button{display:block;width:100%;margin:3px 0}
#context-status{position:absolute;bottom:calc(100% + 6px);left:0;max-width:300px;padding:6px;background:#091522;color:#ffe078;border:1px solid #ffe078;font:11px monospace;pointer-events:none}
`;
  document.head.append(style);
  const label = document.createElement('label'),
    select = document.createElement('select');
  select.id = 'crt-setting';
  label.textContent = 'CRT display ';
  for (const value of ['off', 'subtle', 'enhanced']) {
    const option = document.createElement('option');
    option.value = value;
    option.textContent = value[0].toUpperCase() + value.slice(1);
    select.append(option);
  }
  let saved;
  try {
    saved = localStorage.getItem('neon-crt');
  } catch {}
  select.value = ['off', 'subtle', 'enhanced'].includes(saved) ? saved : 'subtle';
  const update = () => {
    document.body.dataset.crt = select.value;
    host.ArenaCrt?.(['off', 'subtle', 'enhanced'].indexOf(select.value));
    try {
      localStorage.setItem('neon-crt', select.value);
    } catch {}
  };
  select.onchange = update;
  label.append(select);
  menu.insertBefore(label, menu.querySelector('.actions'));
  update();
}
