// Presentation styles; inserted by the owning UI component.
export const skillTreeStyles = `
#arena-skills {
  position: fixed;
  inset: 2vh 2vw;
  max-width: none;
  width: auto;
  max-height: none;
  transform: none;
  padding: 18px;
  display: flex;
  flex-direction: column;
  gap: 10px;
  z-index: 7;
  border: 2px solid #806844;
  border-radius: 4px;
  background: linear-gradient(130deg, #1b2530, #090f19 65%);
  box-shadow:
    0 0 0 100vmax #02050be8,
    inset 0 0 0 2px #d8bd7330;
  color: #ede4cf;
}
#arena-skills[hidden] {
  display: none;
}
#arena-skills h2 {
  margin: 0;
  color: #f2d698;
  font:
    24px Georgia,
    serif;
  letter-spacing: 3px;
}
#arena-skills .tree-heading {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
}
#arena-skills .tree-heading p {
  margin: 0;
  color: #f2d698;
  font-size: 12px;
}
#arena-skills .tree-help {
  margin: 0;
  font-size: 12px;
  color: #aebbc9;
}
.tree-nav {
  display: flex;
  gap: 5px;
  overflow: auto;
  flex-shrink: 0;
}
.tree-nav button {
  font-size: 11px;
  padding: 8px 10px;
  white-space: nowrap;
  border-color: #596574 !important;
}
#arena-skills .tree-scroll {
  overflow: auto;
  min-height: 110px;
  flex: 1;
  border: 1px solid #51616d;
  background: radial-gradient(circle at 1px 1px, #8197ac18 1px, transparent 2px) 0 0/22px 22px;
  scrollbar-color: #8d7a53 #111923;
}
.tree-section {
  position: relative;
  width: 1040px;
  box-sizing: border-box;
  border-bottom: 1px solid #405363;
  padding-top: 1px;
  background: linear-gradient(
    100deg,
    color-mix(in srgb, var(--branch) 5%, transparent),
    transparent 80%
  );
}
#arena-skills h3 {
  margin: 12px 18px;
  font:
    14px Georgia,
    serif;
  letter-spacing: 2px;
  color: var(--branch);
}
.tree-links {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  pointer-events: none;
}
.tree-link {
  fill: none;
  stroke: #526474;
  stroke-width: 2;
}
.tree-link.unlocked {
  stroke: var(--branch);
  opacity: 0.8;
}
#arena-skills .skill-node {
  position: absolute;
  width: 112px;
  height: 100px;
  padding: 0;
  margin: 0;
  border: 0;
  background: none;
  color: #bac6d0;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
  cursor: pointer;
  font: 11px/1.2 monospace;
}
.skill-face {
  position: relative;
  display: block;
  width: 64px;
  height: 64px;
  box-sizing: border-box;
  border: 3px ridge #67747f;
  background: radial-gradient(
    ellipse at 30% 10%,
    color-mix(in srgb, var(--branch) 28%, #24323d),
    #0a1527 75%
  );
  box-shadow:
    inset 0 0 12px #000,
    0 3px 6px #0009;
}
.skill-face svg {
  width: 100%;
  height: 100%;
  padding: 4px;
  box-sizing: border-box;
  filter: drop-shadow(0 1px 2px #000);
}
.skill-tier {
  position: absolute;
  bottom: 1px;
  right: 3px;
  font: bold 10px Georgia;
  color: #fff0be;
  text-shadow: 1px 1px #000;
}
.skill-level {
  position: absolute;
  top: -7px;
  right: -10px;
  font: 9px monospace;
  padding: 2px 3px;
  background: #17232e;
  border: 1px solid #6b7783;
  color: #ddd;
}
.skill-name {
  max-width: 112px;
}
.skill-pips {
  position: absolute;
  left: 2px;
  bottom: 2px;
  display: flex;
  gap: 2px;
}
.skill-pips i {
  width: 3px;
  height: 4px;
  background: var(--branch);
}
#arena-skills .skill-node[data-state="learned"] .skill-face {
  border-color: #8ef0b5;
  background: radial-gradient(ellipse at 30% 20%, #39644d, #0b201e 75%);
}
#arena-skills .skill-node[data-state="ready"] .skill-face {
  border-color: #ffe09d;
  box-shadow:
    0 0 13px #f2cf6744,
    inset 0 0 10px #eac95a22;
}
#arena-skills .skill-node[data-state*="locked"] .skill-face {
  filter: saturate(0.45);
  opacity: 0.6;
}
#arena-skills .skill-node:focus-visible {
  outline: 2px solid #fff0b5;
  outline-offset: 3px;
}
#arena-skills .skill-node:hover .skill-face {
  filter: brightness(1.3);
}
#arena-skills .tree-detail {
  border-top: 1px solid #66707a;
  padding-top: 8px;
  min-height: 76px;
  font-size: 12px;
  color: #bccbd7;
}
.tree-detail strong {
  font: 17px Georgia;
  color: #f1d69b;
}
.tree-detail p {
  margin: 5px 0;
}
.tree-receipt {
  color: #a6f0bd;
  min-height: 14px;
}
.tree-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 10px;
  font-size: 10px;
  color: #9dafbe;
}
`;
