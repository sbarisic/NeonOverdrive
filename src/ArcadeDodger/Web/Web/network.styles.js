// Presentation styles; inserted by the owning UI component.
export const shellStyles = `
.modal-open #sector-guide,.modal-open #radio-feed,.modal-open #world-status,.modal-open .credit-payout {display:none!important;}
.arena-shell {
  color: #ebf0fa;
  font:
    16px/1.4 ui-monospace,
    Consolas,
    monospace;
  box-sizing: border-box;
}
.arena-shell * {
  box-sizing: border-box;
}
.arena-shell[hidden],
.arena-shell [hidden] {
  display: none !important;
}
.arena-panel {
  position: fixed;
  left: 50%;
  top: 50%;
  transform: translate(-50%, -50%);
  width: min(560px, calc(100vw - 32px));
  max-height: calc(100dvh - 90px);
  overflow: auto;
  padding: 24px;
  background: rgba(4, 14, 22, 0.97);
  border: 1px solid #287084;
  border-top: 3px solid #41f5eb;
  box-shadow:
    0 0 0 6px #060817a0,
    0 0 36px #41f5eb18;
  z-index: 5;
}
.arena-shell h1,
.arena-shell h2 {
  margin: 0 0 12px;
  color: #41f5eb;
  font-size: clamp(24px, 4vw, 34px);
  line-height: 1.1;
  letter-spacing: 0.08em;
}
.arena-shell p {
  margin: 10px 0;
}
.arena-shell .eyebrow {
  color: #a29aff;
  font-size: 12px;
  letter-spacing: 0.18em;
}
.arena-shell .muted {
  color: #a5b8cd;
  font-size: 14px;
}
.arena-shell label {
  display: block;
  margin: 12px 0 6px;
}
.arena-shell input:not([type="checkbox"]) {
  display: block;
  width: 100%;
  margin: 6px 0;
  padding: 10px;
  background: #060817;
  color: #ebf0fa;
  border: 1px solid #287084;
  font: inherit;
}
.arena-shell button,
.arena-shell .action {
  display: inline-block;
  cursor: pointer;
  border: 1px solid #41f5eb;
  background: #0b2833;
  color: #41f5eb;
  padding: 10px 16px;
  font: inherit;
  text-decoration: none;
  min-height: 44px;
}
.arena-shell a {
  color: #41f5eb;
  overflow-wrap: anywhere;
}
.arena-shell button:hover,
.arena-shell .action:hover {
  background: #153c48;
}
.arena-shell :focus-visible {
  outline: 3px solid #ffcd69;
  outline-offset: 3px;
}
.arena-shell button:disabled {
  cursor: wait;
  opacity: 0.6;
}
.arena-shell .actions {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  margin-top: 16px;
  align-items: center;
}
#network-info {
  position: fixed;
  bottom: 12px;
  right: 18px;
  padding: 5px;
  background: #07111bed;
  border: 2px ridge #85744e;
  z-index: 6;
  display: grid;
  grid-template-columns: repeat(3, var(--command-size, 48px));
  gap: 5px;
  box-shadow: inset 0 0 14px #000;
}
#network-info > span,
#network-info > a {
  display: none;
}
#network-info:not(:has(button:not([hidden]))) {
  display: none;
}
#network-info button {
  position: relative;
  width: var(--command-size, 48px);
  height: var(--command-size, 48px);
  min-height: 44px;
  padding: 0;
  font-size: 0;
  border: 3px ridge #7b806e;
  background: radial-gradient(ellipse at 30% 15%, #425252, #111e2b 70%);
  box-shadow: inset 0 0 8px #000;
  color: #ffe5a1;
}
#network-info button::before {
  content: "";
  position: absolute;
  inset: 3px 3px 10px;
  background: var(--command-icon) center/contain no-repeat;
  filter: drop-shadow(0 2px 2px #000);
}
#network-info button::after {
  content: attr(data-hotkey);
  position: absolute;
  right: 2px;
  bottom: 0;
  font: bold 10px/1.2 monospace;
  color: #ffe5a1;
  text-shadow: 1px 1px #000;
}
#network-info button:hover,
#network-info button:focus-visible {
  border-color: #f7d780;
  background-color: #425347;
  box-shadow: 0 0 9px #e6c37970;
}
#arena-respawn {
  position: fixed;
  left: 50%;
  top: 65%;
  transform: translateX(-50%);
  max-width: calc(100vw - 32px);
  z-index: 4;
  padding: 14px 24px;
  background: #041622;
  color: #41f5eb;
  border: 1px solid #41f5eb;
  cursor: pointer;
}
.credit-payout {
  position: fixed;
  top: 30%;
  left: 50%;
  transform: translateX(-50%);
  color: #ffdd79;
  text-shadow: 0 2px 4px #000;
  font: bold 22px monospace;
  pointer-events: none;
  z-index: 8;
  background: #171208bb;
  padding: 10px 16px;
  border: 1px solid #baa35b;
}
.arena-notice {
  color: #ffcd69;
  font-size: 14px;
  overflow-wrap: anywhere;
}
`;
