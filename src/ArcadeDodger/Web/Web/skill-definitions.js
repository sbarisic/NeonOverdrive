import { skillTreeStyles } from './skill-definitions.styles.js';
// Illustrations are presentation only; all skill rules arrive from the server catalog.
export const branches = [
  'Defense',
  'Strength',
  'Fire Rate',
  'Masteries',
  'Mobility',
  'Fortune',
  'Collection',
];
export const colors = [
  '#7bded9',
  '#f3a365',
  '#efda82',
  '#94bfff',
  '#cf9dfa',
  '#79d5ff',
  '#f9d375',
  '#9fe7a6',
];
const art = {
  shield: '<path d="M32 10 49 17 46 38 32 52 18 38 15 17Z"/><path d="M32 17V44M22 25H42"/>',
  recharge:
    '<path d="M42 15A20 20 0 1 0 51 37M42 7V20H55"/><path d="M34 19 24 34H34L29 46 42 29H32Z"/>',
  flux: '<path d="M16 21Q24 10 32 21T48 21M16 32Q24 21 32 32T48 32M16 43Q24 32 32 43T48 43"/>',
  siphon:
    '<path d="M32 12 47 18 44 38 32 50 20 38 17 18ZM8 33H26M21 27 27 33 21 39M56 33H38M43 27 37 33 43 39"/>',
  hull: '<path d="M15 17 32 10 49 17V46L32 54 15 46ZM15 17 32 28 49 17M32 28V54"/><path d="M22 22V41M42 22V41"/>',
  repair: '<path d="M17 14H47V50H17Z"/><path d="M27 22H37V28H43V38H37V44H27V38H21V28H27Z"/>',
  strength: '<path d="M16 49 43 14 51 21 24 56ZM20 35 36 48M36 12 39 5M51 31 59 30M27 18 22 12"/>',
  dynamics: '<path d="M12 43 39 16 48 16 48 25 21 52ZM11 22H26M6 32H18M38 40H55M46 31H60"/>',
  range:
    '<circle cx="32" cy="32" r="18"/><circle cx="32" cy="32" r="9"/><path d="M32 5V24M32 40V59M5 32H24M40 32H59"/>',
  rate: '<path d="M32 9 22 30H34L25 54 47 26H35L43 9ZM10 22H18M7 33H16M11 44H20"/>',
  bay: '<path d="M10 12H54V52H10ZM32 12V52M10 32H54"/><path d="M17 19H25V25H17ZM39 39H47V45H39Z"/>',
  kinetic:
    '<path d="M20 49 13 42 38 17Q49 6 55 9 58 15 47 26ZM20 35 29 44M7 53 17 43M7 42 12 37M23 56 28 51"/>',
  laser: '<path d="M8 49 19 56 30 43 19 32ZM25 38 52 11M31 42 57 16M44 9 58 23M49 5V28M38 16H61"/>',
  rocket:
    '<path d="M22 40Q20 20 49 10 51 36 32 44ZM23 30 11 33 10 46 22 42M34 41 32 54 44 50 46 36M16 47 8 56M22 51 17 58"/><circle cx="38" cy="23" r="5"/>',
  plasma:
    '<circle cx="32" cy="32" r="11"/><ellipse cx="32" cy="32" rx="25" ry="12" transform="rotate(45 32 32)"/><ellipse cx="32" cy="32" rx="25" ry="12" transform="rotate(-45 32 32)"/>',
  thruster: '<path d="M20 9H44V34H20ZM25 34 20 53 32 45 44 53 39 34M26 16H38V27H26Z"/>',
  vector:
    '<path d="M32 8V54M8 32H56M24 17 32 8 40 17M24 45 32 54 40 45M17 24 8 32 17 40M47 24 56 32 47 40"/>',
  slipstream: '<path d="M8 17Q25 30 51 17M5 30Q24 43 54 30M8 43Q26 56 51 43"/>',
  cruise: '<circle cx="43" cy="39" r="12"/><path d="M33 36 8 8 24 37M40 27 30 6M30 45 7 31"/>',
  impact: '<path d="M32 9 49 17 45 39 32 53 19 39 15 17ZM23 20V38M32 17V43M41 20V38"/>',
  fortune:
    '<ellipse cx="32" cy="18" rx="21" ry="8"/><path d="M11 18V43C11 55 53 55 53 43V18M11 30C11 42 53 42 53 30M32 15V21"/>',
  luck: '<rect x="12" y="12" width="40" height="40" rx="5"/><circle cx="22" cy="22" r="3"/><circle cx="42" cy="22" r="3"/><circle cx="32" cy="32" r="3"/><circle cx="22" cy="42" r="3"/><circle cx="42" cy="42" r="3"/>',
  luckyfind:
    '<path d="m32 7 7 16 18 2-14 12 4 18-15-9-15 9 4-18L7 25l18-2Z"/><circle cx="32" cy="32" r="7"/>',
  collector:
    '<path d="M14 12V35C14 59 50 59 50 35V12H39V35C39 44 25 44 25 35V12ZM14 24H25M39 24H50M29 16H35M32 13V19"/>',
  harvest: '<path d="m32 9 18 23-18 23L14 32ZM14 32H50M32 9V55M8 17 13 20M51 44 58 48"/>',
  fieldrepair: '<path d="M10 15H54V53H10ZM24 15V9H40V15M27 23H37V30H44V40H37V47H27V40H20V30H27Z"/>',
};
export function skillIcon(key, color, tier) {
  const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
  svg.setAttribute('viewBox', '0 0 64 64');
  svg.setAttribute('aria-hidden', 'true');
  svg.innerHTML = `<g fill="${color}22" stroke="${color}" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round">${art[key] || art.strength}</g>`;
  return svg;
}
export const treeCss = skillTreeStyles;
