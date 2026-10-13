// Static nearest-site cells; ownership changes do not disclose terrain or NPC positions.
export function territoryCells(sector) {
  const sites = sector.locations.filter((l) => [11, 12].includes(l.id) || l.kind === 'wreck');
  return sites.map((site) => {
    let points = [
      [0, 0],
      [sector.width, 0],
      [sector.width, sector.height],
      [0, sector.height],
    ];
    for (const other of sites.filter((s) => s.id !== site.id)) {
      const dx = other.x - site.x,
        dy = other.y - site.y,
        mx = (other.x + site.x) / 2,
        my = (other.y + site.y) / 2;
      const distance = ([x, y]) => (x - mx) * dx + (y - my) * dy,
        next = [];
      points.forEach((a, i) => {
        const b = points[(i + 1) % points.length],
          da = distance(a),
          db = distance(b);
        if (da <= 0) next.push(a);
        if (da <= 0 !== db <= 0) {
          const t = da / (da - db);
          next.push([a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t]);
        }
      });
      points = next;
    }
    return { id: site.id, points };
  });
}
