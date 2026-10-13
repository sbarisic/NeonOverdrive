import { controlStyles } from './interface.styles.js';
// Shared controls for desktop game panels; gameplay icons keep their own styling.
const paths = {
  brake: 'M5 4H9V20H5Z M15 4H19V20H15Z',
  pilot: 'M12 3A4 4 0 1 1 11.9 3 M4 21V18A8 6 0 0 1 20 18V21Z',
  friends: 'M8 3A4 4 0 1 1 7.9 3 M2 21V18Q8 10 14 18V21Z M17 4A3 3 0 1 1 16.9 4 M17 14Q23 14 23 21',
  map: 'M2 5L8 2L16 6L22 3V20L16 23L8 19L2 22Z M8 2V19 M16 6V23',
  station: 'M12 4L19 8V16L12 20L5 16V8Z M12 1V4 M12 20V23 M1 12H5 M19 12H23 M9 12H15 M12 9V15',
  strength: 'M4 20L17 3L21 7L7 21Z M5 14L11 20',
  rate: 'M4 6H16 M4 12H20 M4 18H16 M16 3L21 6L16 9',
  mastery: 'M12 2L15 8L22 9L17 14L18 22L12 18L6 22L7 14L2 9L9 8Z',
  mobility: 'M12 3L21 21L12 17L3 21Z M7 8L3 13 M17 8L21 13',
  fortune: 'M6 3H18L22 12L18 21H6L2 12Z M12 7V17 M8 12H16',
  collection: 'M4 4V14A8 8 0 0 0 20 14V4H15V14A3 3 0 0 1 9 14V4Z',
  exit: 'M9 4H4V20H9 M12 7L17 12L12 17 M8 12H21',
  apply: 'M4 12L9 17L20 6',
  reset: 'M5 8A8 8 0 1 1 4 16 M5 3V9H11',
  clear: 'M5 6H19 M9 6V3H15V6 M7 6L8 21H16L17 6',
  shop: 'M3 9L5 3H19L21 9 M3 9H21V13H3Z M5 13V21H19V13',
  loadout: 'M3 3H10V10H3Z M14 3H21V10H14Z M3 14H10V21H3Z M14 14H21V21H14Z',
  repair: 'M12 3L20 7V13L12 21L4 13V7Z M8 12H16 M12 8V16',
  locate: 'M12 2V6 M12 18V22 M2 12H6 M18 12H22 M12 7A5 5 0 1 1 11.9 7',
  contract: 'M6 3H18V21H6Z M9 8H15 M9 12H15 M9 16H13',
};
let installed = false;
export function control(button, icon, variant = 'secondary') {
  button.classList.add('ui-action');
  button.dataset.variant = variant;
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="#dcc791" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"><path d="${paths[icon] || paths.contract}"/></svg>`;
  button.style.setProperty('--action-icon', `url("data:image/svg+xml,${encodeURIComponent(svg)}")`);
  if (installed) return;
  installed = true;
  const style = document.createElement('style');
  style.textContent = controlStyles;
  document.head.append(style);
}
