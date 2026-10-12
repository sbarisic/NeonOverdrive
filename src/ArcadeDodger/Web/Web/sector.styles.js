// Presentation styles; inserted by the owning UI component.
export const stationStyles = `
#sector-console {
  width: min(1500px, calc(100vw - 32px));
  max-height: calc(100dvh - 32px);
  border-color: #c5a66c;
  background: linear-gradient(135deg, #202034, #081320);
  z-index: 9;
}
#sector-console h2 {
  color: #edd39b;
  font-family: Georgia, serif;
}
#sector-console .sector-map {
  position: relative;
  width: 100%;
}
#sector-console canvas {
  display: block;
}
#sector-console .contract-card {
  border: 1px solid #536071;
  padding: 12px;
  margin: 10px 0;
  background: #071321;
}
#sector-console .contract-card button {
  margin: 8px 8px 0 0;
}
#sector-console .sector-toolbar {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}
#sector-console .sector-status {
  color: #f2ce81;
  min-height: 24px;
}
#sector-guide {
  position: fixed;
  top: var(--sector-guide-top, 190px);
  left: 18px;
  max-width: calc(100vw - 36px);
  padding: 8px 12px;
  background: #071321d9;
  border-left: 2px solid #dfbd76;
  color: #f2d699;
  font: 16px/1.5 monospace;
  pointer-events: none;
  z-index: 3;
  white-space: pre-line;
}
`;
