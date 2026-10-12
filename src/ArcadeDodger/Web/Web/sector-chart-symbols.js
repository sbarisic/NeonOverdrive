export const names = {
  planet: 'Planet',
  station: 'Friendly station',
  base: 'Hostile installation',
  wreck: 'Salvage wreck',
  beacon: 'Survey beacon',
  corridor: 'Arrival corridor',
  pilot: 'Friendly pilot',
};
export const colors = {
  planet: '#9ec7e0',
  station: '#77e6ae',
  base: '#f87971',
  wreck: '#c0a777',
  beacon: '#eedb7a',
  corridor: '#ff9477',
  pilot: '#79e9ce',
};
export const paths = {
  planet: '<circle cx="12" cy="12" r="7"/><path d="M3 16L21 8"/>',
  station: '<path d="M12 2L21 7V17L12 22L3 17V7Z M7 12H17 M12 7V17"/>',
  base: '<path d="M12 2L23 21H1Z M8 10L16 18 M16 10L8 18"/>',
  wreck: '<path d="M4 4L18 7L21 17L11 21L3 13Z M9 4L11 10L7 14L12 21"/>',
  beacon: '<path d="M12 2L20 12L12 22L4 12Z M12 7V17 M8 12H16"/>',
  corridor: '<path d="M3 5L11 12L3 19 M12 5L20 12L12 19"/>',
  pilot: '<path d="M12 2L21 21L12 16L3 21Z"/>',
};

names.rescue = 'Emergency radio';
colors.rescue = '#ffd569';
paths.rescue =
  '<path d="M12 8v13M7 21h10M8 6a6 6 0 0 0 0 8M16 6a6 6 0 0 1 0 8M4 3a10 10 0 0 0 0 14M20 3a10 10 0 0 1 0 14"/><circle cx="12" cy="10" r="2"/>';
names.outpost = 'Allied outpost';
colors.outpost = '#65e6be';
paths.outpost = paths.base;
