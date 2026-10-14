// Shared module glyphs and palette. Economy and effects come from the server.
export const glyph = (id) =>
  [
    '◇',
    '▰',
    '╱',
    '➤',
    '◉',
    '◆',
    '»',
    '↔',
    '➶',
    '⌖',
    'ϟ',
    '⋔',
    'Ψ',
    '✺',
    '◎',
    '↱',
    '⌁',
    '♨',
    '❄',
    '☢',
    '◈',
    'ϟ',
    '⊙',
    '✹',
    '◌',
    '◉',
    '✥',
    '♡',
    '¤',
  ][id % 32] || '◇';
export const color = (component) => {
  const id = component % 32;
  return id === 25
    ? '#c390f5'
    : id === 17
      ? '#ff9958'
      : id === 19
        ? '#c3ef78'
        : id === 18
          ? '#9edbff'
          : id === 20
            ? '#b5ff67'
            : id === 21
              ? '#c792ff'
              : id <= 4
                ? ['', '#dfc585', '#73e7f1', '#ffa26a', '#c390f5'][id]
                : '#ebce86';
};
