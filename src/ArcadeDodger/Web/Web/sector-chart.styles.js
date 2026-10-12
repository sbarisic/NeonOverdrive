// Presentation styles; inserted by the owning UI component.
export const chartStyles = `
#sector-console .sector-map {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 260px;
  aspect-ratio: auto;
  height: min(66vh, 760px);
  min-height: 330px;
  border: 1px solid #31485d;
  background: #07111e;
}
#sector-console h2 {
  font-size: 26px;
  letter-spacing: 0.12em;
  margin-bottom: 8px;
}
#sector-console .chart-view {
  position: relative;
  overflow: hidden;
  min-width: 0;
  background: #070f1c;
  cursor: grab;
}
#sector-console .chart-view:active {
  cursor: grabbing;
}
#sector-console .chart-view canvas {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
}
#sector-console .chart-tools {
  position: absolute;
  left: 12px;
  top: 12px;
  display: flex;
  gap: 6px;
  z-index: 3;
}
#sector-console .chart-tools button {
  font-size: 11px;
  padding: 6px 10px;
  min-height: 30px;
  background: #0b1b2be8;
  border-color: #375063;
}
#sector-console .chart-help {
  position: absolute;
  bottom: 8px;
  left: 12px;
  color: #93a6b9;
  font-size: 10px;
  pointer-events: none;
}
#sector-console .chart-detail {
  padding: 18px;
  border-left: 1px solid #31485d;
  overflow: auto;
  background: linear-gradient(#122033, #0b1320);
  font-size: 12px;
}
#sector-console .chart-detail h3 {
  color: #efddb3;
  font: 18px/1.3 monospace;
  margin: 8px 0 16px;
  overflow-wrap: anywhere;
}
#sector-console .chart-detail p {
  color: #aab8ca;
  line-height: 1.6;
  font-size: 12px;
}
#sector-console .chart-detail button {
  width: 100%;
  padding: 8px;
  margin-top: 7px;
  font-size: 12px;
  min-height: 34px;
}
#sector-console .chart-category {
  color: #6fd9e4;
  font-size: 10px;
  letter-spacing: 0.12em;
  text-transform: uppercase;
}
#sector-console .chart-detail .chart-primary {
  border-color: #dabb75;
  color: #f3d99b;
  background: #3a3022;
}
#sector-console .chart-filters {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  border-top: 1px solid #31485d;
  margin-top: 16px;
  padding-top: 10px;
}
#sector-console .chart-filters button {
  width: auto;
  font-size: 10px;
  padding: 5px 7px;
  min-height: 26px;
  color: #79909e;
  border-color: #344353;
}
#sector-console .chart-filters button[aria-pressed="true"] {
  color: #bce4ee;
  border-color: #648496;
}
#sector-console .map-location {
  position: absolute;
  transform: translate(-50%, -50%);
  width: 26px;
  height: 26px;
  min-height: 0;
  padding: 3px;
  max-width: none;
  border: 0;
  background: #081423aa;
  color: var(--marker);
  border-radius: 50%;
  cursor: pointer;
  z-index: 1;
}
#sector-console .map-location svg {
  display: block;
  width: 100%;
  height: 100%;
  fill: none;
  stroke: currentColor;
  stroke-width: 1.7;
  filter: drop-shadow(0 0 3px #07111e);
}
#sector-console .map-location:hover,
#sector-console .map-location:focus-visible,
#sector-console .map-location[aria-pressed="true"] {
  outline: 1px solid #f3d99b;
  background: #203044;
  z-index: 2;
}
#sector-console .map-location .planet-name {
  position: absolute;
  left: 50%;
  top: -20px;
  transform: translateX(-50%);
  font: 11px monospace;
  white-space: nowrap;
  color: #dce8ed;
  text-shadow: 0 2px 3px #000;
  pointer-events: none;
}
#sector-console .chart-player {
  position: absolute;
  width: 14px;
  height: 14px;
  transform: translate(-50%, -50%);
  border: 2px solid #b8fff3;
  border-radius: 50%;
  box-shadow: 0 0 12px #53ebce;
  pointer-events: none;
  z-index: 2;
  background: radial-gradient(#fff 0 25%, transparent 35%);
}
#sector-console .chart-context {
  position: absolute;
  z-index: 5;
  width: 175px;
  padding: 5px;
  background: #101e2f;
  border: 1px solid #bda774;
  box-shadow: 0 8px 25px #0009;
}
#sector-console .chart-context button {
  display: block;
  width: 100%;
  min-height: 30px;
  font-size: 11px;
  padding: 6px;
  text-align: left;
  border: 0;
}
`;
