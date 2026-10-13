// Presentation styles; inserted by the owning UI component.
export const controlStyles = `
.menu-navigation {
  display: flex;
  flex-wrap: wrap;
  gap: 5px;
  flex: none;
  margin: 8px 0;
  padding-bottom: 9px;
  border-bottom: 1px solid #655b45;
}
.menu-navigation button.ui-action { min-height: 32px; padding: 6px 9px; gap: 6px; }
.menu-navigation button[aria-current="page"] { border-bottom: 3px solid #f1cf80; }
#weapon-bench .workshop-navigation {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 8px 16px;
  border-bottom: 1px solid #655b45;
  padding-bottom: 8px;
}
#weapon-bench .workshop-navigation :is(.menu-navigation, .workshop-tabs) {
  margin: 0;
  padding: 0;
  border: 0;
}

:is(#sector-console, #weapon-bench, #arena-skills, #arena-menu, #arena-friends, #radio-console, #radio-shell, #flight-controls) button.ui-action {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 9px;
  min-height: 34px;
  padding: 7px 11px;
  border: 1px solid #65717a;
  border-top-color: #94a09c;
  border-radius: 2px;
  background: linear-gradient(#263644, #111e2c);
  box-shadow:
    inset 0 0 0 2px #09111a70,
    0 2px 4px #0005;
  color: #dfded3;
  font: 12px/1.3 monospace;
  cursor: pointer;
  white-space: nowrap;
}
:is(#sector-console, #weapon-bench, #arena-skills, #arena-menu, #arena-friends, #radio-console, #radio-shell, #flight-controls) button.ui-action::before {
  content: "";
  width: 17px;
  height: 17px;
  flex: none;
  background: var(--action-icon) center/contain no-repeat;
  opacity: 0.9;
}
:is(#sector-console, #weapon-bench, #arena-skills, #arena-menu, #arena-friends, #radio-console, #radio-shell, #flight-controls)
  button.ui-action[data-variant="primary"] {
  border-color: #bba06a;
  border-top-color: #f0dca3;
  background: linear-gradient(#534832, #2a251e);
  color: #ffe7b1;
}
:is(#sector-console, #weapon-bench, #arena-skills, #arena-menu, #arena-friends, #radio-console, #radio-shell, #flight-controls)
  button.ui-action:hover:not(:disabled) {
  border-color: #efcf83;
  filter: brightness(1.15);
}
:is(#sector-console, #weapon-bench, #arena-skills, #arena-menu, #arena-friends, #radio-console, #radio-shell, #flight-controls) button.ui-action:focus-visible {
  outline: 2px solid #ffe2a0;
  outline-offset: 3px;
}
:is(#sector-console, #weapon-bench, #arena-skills, #arena-menu, #arena-friends, #radio-console, #radio-shell, #flight-controls) button.ui-action:disabled {
  opacity: 0.4;
  cursor: default;
  filter: saturate(0.35);
}
#arena-menu {
  border: 1px solid #988154;
  border-top: 2px solid #d4b575;
  background: linear-gradient(135deg, #202537, #0a1521);
  box-shadow:
    inset 0 0 0 3px #09111b,
    0 8px 40px #0009;
}
#arena-menu h2 {
  font:
    24px Georgia,
    serif;
  letter-spacing: 0.1em;
  color: #ecd59e;
}
#arena-menu .eyebrow {
  color: #9faebc;
  font-size: 10px;
}
#arena-menu .arena-notice {
  color: #dbc18e;
  font-size: 12px;
}
#arena-menu .muted {
  font-size: 12px;
}
#arena-menu label {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  padding: 8px 10px;
  background: #0a1623;
  border: 1px solid #354959;
}
#arena-menu input[type="checkbox"] {
  accent-color: #c9ae71;
  width: 15px;
  height: 15px;
  margin: 0;
}
#arena-menu .actions {
  border-top: 1px solid #655b45;
  padding-top: 12px;
}
#arena-menu .actions button:last-child {
  margin-left: auto;
}
#arena-menu .actions + .muted {
  margin-top: 14px;
  font-size: 11px;
}
#weapon-bench {
  border-radius: 4px;
}
#weapon-bench .workshop-tabs {
  border-bottom: 1px solid #655b45;
  padding-bottom: 8px;
  gap: 6px;
}
#weapon-bench .workshop-tabs button.ui-action {
  min-width: 112px;
  border-bottom: 3px solid transparent;
}
#weapon-bench .workshop-tabs button.ui-action[aria-selected="true"] {
  border-bottom-color: #f1cf80;
  background: linear-gradient(#4a4030, #272a2d);
  color: #ffe3a3;
}
#weapon-bench .bench-actions,
#arena-skills .tree-footer {
  padding: 12px 0 0;
  border-top: 1px solid #655b45;
  min-height: 52px;
}
#weapon-bench .bench-actions button:last-child {
  margin-left: auto;
}
#arena-skills .tree-footer {
  font-size: 12px;
}
#arena-skills .tree-footer button {
  flex-shrink: 0;
}
#arena-skills .tree-nav {
  gap: 5px;
  padding-bottom: 8px;
  border-bottom: 1px solid #655b45;
}
#arena-skills .tree-nav button {
  border: 1px solid #52616a !important;
  border-bottom: 3px solid transparent !important;
  background: linear-gradient(#24313d, #111b29);
  color: #b7c8d1;
  font: 12px monospace;
  padding: 7px 10px;
  min-height: 34px;
}
#arena-skills .tree-nav button[aria-current="true"] {
  border-bottom-color: var(--branch) !important;
  color: var(--branch);
  background: linear-gradient(#30404b, #172230);
}
#arena-skills .tree-nav button:hover {
  color: #ffe1a4;
}
#sector-console[data-mode="station"] {
  width: min(1160px, calc(100vw - 48px));
  height: min(740px, calc(100dvh - 32px));
  padding: 20px;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
#sector-console[data-mode="station"] h2 {
  flex-shrink: 0;
  margin: 0 0 6px;
}
#sector-console[data-mode="station"] > p {
  margin: 6px 0;
}
#sector-console[data-mode="station"] .station-board {
  flex: 1;
  min-height: 0;
  overflow: auto;
  padding-right: 4px;
  margin: 10px 0;
}
#sector-console[data-mode="station"] .contract-card {
  margin: 0;
  padding: 14px;
}
#sector-console[data-mode="station"] .station-caption {
  margin-top: 14px;
}
#sector-console[data-mode="station"] .sector-footer {
  flex-shrink: 0;
}
#sector-console .station-board {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
  margin: 16px 0;
}
#sector-console .contract-card {
  position: relative;
  margin: 0;
  padding: 18px;
  border: 1px solid #425363;
  border-top: 2px solid #786b4f;
  background: linear-gradient(135deg, #172637, #0a1522);
  box-shadow: inset 0 0 0 2px #07111a;
}
#sector-console .contract-card[data-state="active"],
#sector-console .contract-card[data-state="ready"] {
  border-color: #c0a166;
}
#sector-console .contract-card h3 {
  font:
    18px Georgia,
    serif;
  color: #eed8a4;
  margin: 8px 0;
}
#sector-console .contract-card .contract-kind {
  font: 10px monospace;
  letter-spacing: 0.12em;
  color: #82b3be;
}
#sector-console .contract-card .contract-description {
  font: 12px/1.5 monospace;
  color: #a6b8c9;
  min-height: 36px;
  margin: 8px 0;
}
#sector-console .contract-card .contract-reward {
  color: #f1cd80;
  font: 12px monospace;
  margin: 10px 0;
}
#sector-console .contract-card .contract-state {
  font: 11px monospace;
  color: #9cd7b7;
  margin-bottom: 8px;
}
#sector-console .contract-actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}
#sector-console .contract-card .contract-actions button {
  margin: 0;
}
#sector-console .station-caption {
  margin-top: 20px;
  color: #cbbd9b;
  font: 12px monospace;
  letter-spacing: 0.08em;
}
#sector-console .sector-footer {
  border-top: 1px solid #655b45;
  padding-top: 12px;
  display: flex;
  justify-content: flex-end;
}
#sector-console .sector-status {
  font: 12px monospace;
  min-height: 16px;
}
`;
