// Presentation styles; inserted by the owning UI component.
export const loadoutStyles = `
#weapon-bench {
  position: fixed;
  inset: 4vh 4vw;
  padding: 22px;
  z-index: 8;
  border: 1px solid #9d875a;
  border-radius: 14px;
  background: radial-gradient(ellipse at 50% 0, #282641, #0b101d 75%);
  box-shadow:
    0 0 0 100vmax #02050bd9,
    0 0 65px #80704733;
  color: #e9e3d4;
  display: flex;
  flex-direction: column;
  gap: 12px;
}
#weapon-bench h2 {
  font:
    26px Georgia,
    serif;
  letter-spacing: 5px;
  color: #e8cf90;
  margin: 0;
}
#weapon-bench p {
  margin: 0;
}
#weapon-bench .forge-heading {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
}
#weapon-bench .forge-level {
  font-size: 12px;
  color: #9fdbc7;
  white-space: nowrap;
}
#weapon-bench .forge-body {
  overflow: auto;
  min-height: 100px;
  padding: 6px 4px;
  display: flex;
  flex-direction: column;
  gap: 16px;
  scrollbar-color: #a08a59 #101627;
}
#weapon-bench .forge-label {
  font-size: 11px;
  color: #c6b482;
  letter-spacing: 2px;
  display: flex;
  justify-content: space-between;
  margin: 0 0 10px;
}
#weapon-bench .forge-label span {
  color: #98a4ba;
  letter-spacing: 0;
}
#weapon-bench .weapon-scroll {
  overflow-x: auto;
  padding: 4px 4px 12px;
  scrollbar-color: #a08a59 #101627;
}
#weapon-bench .weapon-row {
  display: grid;
  grid-template-columns: repeat(9, minmax(76px, 1fr));
  gap: 12px;
  min-width: 810px;
}
#weapon-bench .weapon-slot {
  position: relative;
  padding: 9px 4px 7px;
  min-height: 118px;
  border: 1px solid #685d47;
  border-radius: 5px;
  background: linear-gradient(135deg, #262633, #0b1120);
  color: #9d9581;
  touch-action: none;
  user-select: none;
  box-shadow:
    inset 0 0 0 3px #090e18,
    inset 0 0 18px #0008;
  cursor: pointer;
}
#weapon-bench .weapon-slot::after {
  content: "›";
  position: absolute;
  right: -11px;
  top: 40px;
  color: #aa9663;
  font: 22px Georgia;
}
#weapon-bench .weapon-slot:last-child::after {
  display: none;
}
#weapon-bench .slot-index {
  position: absolute;
  top: 7px;
  left: 9px;
  font-size: 10px;
  color: #bda978;
}
#weapon-bench .slot-icon {
  display: block;
  font:
    36px/1 Georgia,
    serif;
  margin: 17px 0 9px;
  color: var(--part);
  text-shadow: 0 0 20px var(--part);
}
#weapon-bench .slot-name {
  display: block;
  font-size: 11px;
  line-height: 1.3;
  min-height: 29px;
  white-space: normal;
  color: #cec5af;
}
#weapon-bench .weapon-slot[data-module="0"] {
  border-style: dashed;
  background: radial-gradient(circle, #191f2d, #0a101a);
}
#weapon-bench .weapon-slot[data-module="0"] .slot-icon {
  opacity: 0.35;
  text-shadow: none;
}
#weapon-bench .weapon-slot[data-module="0"] .slot-name {
  color: #6f778c;
}
#weapon-bench button[data-selected="true"] {
  outline: 2px solid #f3d68b;
  outline-offset: 3px;
  background: #363446;
}
#weapon-bench .weapon-slot[data-drop="valid"],
#weapon-bench .module-catalog[data-drop="valid"] {
  outline: 2px solid #8ff0c7;
  outline-offset: 2px;
  box-shadow: 0 0 24px #62edb833;
}
#weapon-bench .weapon-slot[data-drop="invalid"] {
  outline: 2px solid #f77e8c;
  outline-offset: 2px;
}
#weapon-bench .module-catalog {
  display: grid;
  grid-template-columns: repeat(7, minmax(70px, 1fr));
  gap: 10px;
  padding: 8px;
  border: 1px solid #4e4650;
  background: #090f1c88;
  border-radius: 6px;
}
#weapon-bench .module-item {
  padding: 8px 3px;
  border: 1px solid #6e6149;
  border-radius: 4px;
  background: radial-gradient(circle at 50% 25%, #393346, #121827);
  color: var(--part);
  touch-action: none;
  user-select: none;
  min-height: 87px;
  cursor: grab;
  position: relative;
}
#weapon-bench .module-glyph {
  display: block;
  font:
    28px/1 Georgia,
    serif;
  margin-bottom: 7px;
  text-shadow: 0 0 15px var(--part);
}
#weapon-bench .module-name {
  display: block;
  font-size: 11px;
  line-height: 1.25;
  color: #e1d7be;
}
#weapon-bench .module-level {
  position: absolute;
  top: 5px;
  right: 6px;
  font-size: 9px;
  color: #b5a688;
}
#weapon-bench .module-item[aria-disabled="true"] {
  filter: saturate(0.25);
  opacity: 0.4;
  cursor: help;
}
#weapon-bench .module-item:hover,
#weapon-bench .weapon-slot:hover {
  border-color: #ddc681;
}
#weapon-bench .bench-detail {
  border-left: 2px solid #ac9462;
  padding: 7px 12px;
  min-height: 68px;
  background: #121929;
  font-size: 12px;
  color: #b5c0d1;
}
#weapon-bench .bench-detail strong {
  display: block;
  font:
    17px Georgia,
    serif;
  color: #f0d69b;
  margin-bottom: 5px;
}
#weapon-bench .weapon-preview {
  font-size: 11px;
  line-height: 1.5;
  color: #bdba9d;
  margin-top: 8px;
}
#weapon-bench .bench-actions {
  display: flex;
  gap: 10px;
  align-items: center;
  flex-wrap: wrap;
  border-top: 1px solid #5f533c;
  padding-top: 12px;
}
#weapon-bench .bench-actions button {
  font-size: 12px;
  border-color: #a68c54;
  background: #252333;
  color: #ecd59e;
  border-radius: 3px;
  padding: 9px 13px;
}
#weapon-bench .bench-actions button:disabled {
  cursor: default;
}
#weapon-bench .bench-status {
  color: #a4dfca;
  font-size: 12px;
  min-height: 17px;
}
#weapon-bench .forge-help {
  color: #a9b0c3;
  font-size: 12px;
  line-height: 1.5;
}
.module-drag-ghost {
  position: fixed;
  z-index: 20;
  pointer-events: none;
  width: 64px;
  height: 64px;
  display: grid;
  place-items: center;
  border: 2px solid #ebce86;
  border-radius: 5px;
  background: #1e2436e8;
  box-shadow: 0 8px 30px #000a;
  font: 36px Georgia;
  color: var(--part);
  transform: translate(-50%, -50%);
}
`;
export const shopStyles = `
#weapon-bench .forge-body {
  flex: 1;
}
#weapon-bench .workshop-tabs {
  display: flex;
  gap: 8px;
}
#weapon-bench .workshop-tabs button[aria-selected="true"] {
  background: #554532;
  color: #fff0bd;
  border-color: #ffe3a0;
}
#weapon-bench .workshop-tab[hidden] {
  display: none;
}
#weapon-bench .shop-filters {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
  margin: 8px 0 14px;
}
#weapon-bench .shop-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(205px, 1fr));
  gap: 12px;
}
#weapon-bench .shop-card {
  padding: 12px;
  background: #111727;
  border: 1px solid #685d47;
  border-radius: 6px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}
#weapon-bench .shop-card p {
  font-size: 12px;
  color: #b8c4d2;
  min-height: 52px;
}
#weapon-bench .shop-card button {
  margin-top: auto;
  font-size: 12px;
}
#weapon-bench .shop-card canvas {
  width: 100%;
  height: 52px;
  background: #080e19;
}
#weapon-bench .weapon-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin: 16px 0 8px;
  font-size: 13px;
  color: #e6d6ac;
}
#weapon-bench .weapon-heading button {
  font-size: 12px;
  padding: 6px 10px;
}
#weapon-bench .chain-preview {
  width: 100%;
  height: 130px;
  background: #080e19;
  border: 1px solid #383545;
}
#weapon-bench .preview-values {
  font-size: 11px;
  line-height: 1.7;
  color: #b9c4cf;
  white-space: normal;
}
#weapon-bench .wallet {
  font-size: 16px;
  color: #ffda70;
}
#weapon-bench [data-locked="true"] .weapon-heading {
  color: #9293a3;
}
#weapon-bench .module-count {
  display: block;
  font-size: 10px;
  color: #a8c3b1;
  margin-top: 5px;
}
#weapon-bench .workshop-tabs button {
  padding: 8px 20px;
  font-size: 14px;
}
#weapon-bench .shop-filters button {
  padding: 6px 10px;
  font-size: 11px;
}
#weapon-bench .weapon-selector {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 8px;
  margin-bottom: 12px;
}
#weapon-bench .weapon-selector button {
  padding: 9px 6px;
  font-size: 13px;
  border-color: #655b48;
  background: #111927;
  color: #d9cfb6;
  min-width: 0;
}
#weapon-bench .weapon-selector button span {
  display: block;
  font-size: 10px;
  margin-top: 4px;
  color: #aebaca;
}
#weapon-bench .weapon-selector button[aria-selected="true"] {
  border-color: #ffe3a0;
  background: #51432f;
  color: #fff0bd;
}
#weapon-bench .weapon-selector button[data-drop="valid"] {
  outline: 2px solid #8ff0c7;
  outline-offset: 2px;
}
#weapon-bench .module-item[aria-disabled="true"] {
  opacity: 0.45;
  cursor: help;
}
`;
export const compactStyles = `
#weapon-bench {
  inset: 2vh 2vw;
  padding: 16px;
  gap: 8px;
}
#weapon-bench h2 {
  font-size: 23px;
  letter-spacing: 3px;
}
#weapon-bench .forge-body {
  min-height: 0;
  gap: 8px;
  padding: 3px 4px;
}
#weapon-bench .forge-help {
  font-size: 11px;
  line-height: 1.35;
}
#weapon-bench .weapon-selector {
  display: flex;
  gap: 6px;
  margin-bottom: 6px;
}
#weapon-bench .weapon-selector button {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 5px 10px;
  min-height: 30px;
  font-size: 11px;
}
#weapon-bench .weapon-selector button span {
  display: inline;
  margin: 0;
  font-size: 10px;
}
#weapon-bench .weapon-heading {
  margin: 0 0 3px;
  min-height: 28px;
}
#weapon-bench .weapon-heading button.ui-action {
  min-height: 28px;
  padding: 3px 8px;
  font-size: 11px;
}
#weapon-bench .weapon-scroll {
  padding: 3px 3px 7px;
}
#weapon-bench .weapon-row {
  gap: 8px;
  min-width: 810px;
}
#weapon-bench .weapon-slot {
  min-height: 72px;
  height: 72px;
  padding: 3px 4px;
}
#weapon-bench .slot-index {
  top: 4px;
  left: 6px;
  font-size: 9px;
}
#weapon-bench .slot-icon {
  font-size: 24px;
  margin: 14px 0 2px;
}
#weapon-bench .slot-name {
  font-size: 10px;
  min-height: 24px;
  line-height: 1.2;
}
#weapon-bench .weapon-slot::after {
  top: 24px;
  right: -8px;
  font-size: 17px;
}
#weapon-bench .chain-panel {
  display: grid;
  grid-template-columns: minmax(0, 1.25fr) minmax(380px, 1fr);
  gap: 16px;
  max-width: 1320px;
  margin: 10px auto;
  padding: 10px;
  border: 1px solid #424252;
  background: #080e19;
}
#weapon-bench .chain-diagram { min-width: 0; }
#weapon-bench .chain-caption {
  display: flex;
  justify-content: space-between;
  gap: 10px;
  font-size: 10px;
  color: #e6d6ac;
  min-height: 26px;
}
#weapon-bench .chain-note { color: #a5afc1; text-align: right; }
#weapon-bench .chain-preview {
  width: 100%;
  height: clamp(166px, 19vh, 210px);
  display: block;
  border: 0;
}
#weapon-bench .preview-values {
  font-size: 11px;
  line-height: 1.3;
  margin: 0;
  min-width: 0;
}
#weapon-bench .chain-summary {
  display: flex;
  flex-direction: column;
  gap: 3px;
  padding-bottom: 8px;
}
#weapon-bench .chain-summary strong { color: #e6d6ac; }
#weapon-bench .chain-summary b { color: #82d8d0; font-weight: normal; }
#weapon-bench .chain-stages { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 4px; }
#weapon-bench .chain-stage {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 4px 6px;
  margin: 0;
  min-height: 34px;
  border: 1px solid #384353;
  background: #111b29;
  text-align: left;
  color: #c5cddd;
  font-size: 11px;
}
#weapon-bench .chain-stage:hover, #weapon-bench .chain-stage:focus-visible { border-color: #d9bb70; background: #222838; }
#weapon-bench .chain-checkpoint { color: #dfc480; font-size: 10px; }
#weapon-bench .chain-stage strong { display: block; font-size: 11px; font-weight: normal; }
#weapon-bench .chain-stage small { display: block; font-size: 10px; color: #a5b6c3; }
#weapon-bench .chain-stage[data-empty="true"] { background: transparent; }
#weapon-bench .chain-stage[data-reachable="false"] { border-style: dashed; color: #9696a7; }
@media (max-height: 850px) {
  #weapon-bench .loadout-lower {
    display: grid;
    grid-template-columns: minmax(0, 1fr) 290px;
    gap: 10px;
    height: 232px;
    margin: 8px 0;
  }
  #weapon-bench .chain-panel {
    grid-template-columns: minmax(0, 1fr) 340px;
    gap: 8px;
    padding: 8px;
    margin: 0;
    min-width: 0;
  }
  #weapon-bench .chain-caption { flex-direction: column; gap: 2px; }
  #weapon-bench .chain-note { text-align: left; }
  #weapon-bench .chain-preview { height: 176px; }
  #weapon-bench .chain-stage { gap: 4px; padding: 3px 4px; }
  #weapon-bench .chain-stage small { font-size: 9px; }
  #weapon-bench .owned-panel { display: flex; flex-direction: column; min-height: 0; }
  #weapon-bench .owned-panel .module-catalog {
    overflow-y: auto;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    min-height: 0;
  }
}
#weapon-bench #workshop-loadout h3 {
  font-size: 11px;
  letter-spacing: 0.08em;
  margin: 8px 0 6px;
  color: #b9c5d0;
}
#weapon-bench .module-catalog {
  grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
  gap: 5px;
  padding: 5px;
}
#weapon-bench .module-item {
  display: grid;
  grid-template-columns: 26px 1fr;
  grid-template-rows: auto auto;
  align-items: center;
  text-align: left;
  min-height: 42px;
  padding: 5px 6px;
}
#weapon-bench .module-glyph {
  grid-row: 1/3;
  font-size: 22px;
  margin: 0;
}
#weapon-bench .module-name {
  font-size: 10px;
  line-height: 1.2;
}
#weapon-bench .module-count {
  font-size: 9px;
  line-height: 11px;
  color: #9caebb;
  margin-top: 0;
  white-space: nowrap;
}
#weapon-bench .bench-detail {
  min-height: 42px;
  padding: 5px 10px;
  font-size: 11px;
}
#weapon-bench .bench-detail strong {
  font-size: 15px;
  margin-bottom: 3px;
}
#weapon-bench .bench-status {
  font-size: 11px;
  min-height: 14px;
}
#weapon-bench .shop-filters {
  gap: 5px;
  margin: 2px 0 8px;
}
#weapon-bench .shop-filters button.ui-action {
  min-height: 28px;
  padding: 4px 8px;
  font-size: 11px;
}
#weapon-bench .shop-filters button[aria-pressed="true"] {
  border-bottom: 2px solid #eccb81;
  color: #ffe3a1;
}
#weapon-bench .shop-grid {
  grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
  gap: 8px;
}
#weapon-bench .shop-card {
  padding: 9px;
  gap: 6px;
  border-radius: 3px;
  min-width: 0;
}
#weapon-bench .shop-card strong {
  font-size: 13px;
}
#weapon-bench .shop-card p {
  font-size: 11px;
  line-height: 1.4;
  min-height: 46px;
}
#weapon-bench .shop-card canvas {
  height: 36px;
}
#weapon-bench .shop-card .owned-count {
  font-size: 10px;
  color: #b2c2cc;
}
#weapon-bench .shop-card button.ui-action {
  min-height: 30px;
  padding: 5px 8px;
  font-size: 11px;
}
`;
