import { glyph, color } from './weapon-art.js';

export function drawChain(canvas, w, time, stats, definitions, mini = false) {
  const ctx = canvas.getContext('2d'),
    width = canvas.width,
    height = canvas.height;
  ctx.clearRect(0, 0, width, height);
  let rays = [
      {
        y: height / 2,
        angle: 0,
        power:
          (stats.find((s) => s.emitter === w.emitter)?.damage ||
            (w.emitter === 2 ? 4 : w.emitter >= 3 ? 2 : 1)) /
          (w.emitter === 2 ? 4 : w.emitter >= 3 ? 2 : 1),
        color: color(w.emitter),
      },
    ],
    spread = 1,
    last = 10,
    terminal = false;
  const points = mini ? [0.32] : [0.05, 0.1, 0.15, 0.2, 0.25, 0.3, 0.35, 0.4];
  function segment(end) {
    for (const ray of rays) {
      const y = ray.y + Math.tan(ray.angle) * (end - last);
      ctx.strokeStyle = ray.color;
      ctx.lineWidth = Math.min(14, 2 * Math.sqrt(ray.power));
      ctx.beginPath();
      ctx.moveTo(last, ray.y);
      ctx.lineTo(end, y);
      ctx.stroke();
      if (w.emitter !== 2) {
        const f = (time * 0.5) % 1;
        ctx.fillStyle = '#fff';
        ctx.fillRect(last + (end - last) * f - 2, ray.y + (y - ray.y) * f - 2, 4, 4);
      }
      ray.y = y;
    }
    last = end;
  }
  for (let i = 0; i < w.attachments.length; i++) {
    const id = w.attachments[i];
    const x = 10 + (width - 25) * (points[i] ?? 0.5);
    segment(x);
    if (!mini) {
      ctx.fillStyle = '#78889b';
      ctx.font = '10px monospace';
      ctx.fillText((i + 1) * 5 + '%', x - 8, height - 7);
    }
    if (!id) continue;
    if (id === 5) rays.forEach((r) => (r.power *= 2));
    if (id === 9) spread *= 0.5;
    if ([11, 12, 13].includes(id)) {
      const d = definitions.get(id);
      if (d)
        rays = rays
          .flatMap((r) =>
            Array.from({ length: d.value }, (_, n) => ({
              ...r,
              angle: r.angle + (-d.radius + (2 * d.radius * n) / (d.value - 1)) * spread,
              power: r.power / d.value,
            })),
          )
          .slice(0, 32);
    }
    if ([17, 18, 19, 20, 21, 25].includes(id)) rays.forEach((r) => (r.color = color(id)));
    if (id === 26) {
      for (const r of rays) {
        ctx.strokeStyle = '#ffcf6e';
        ctx.strokeRect(x - 5, r.y - 5, 10, 10);
      }
      terminal = true;
      break;
    }
    if (mini && ![5, 9, 11, 12, 13, 17, 18, 19, 20, 21, 25].includes(id)) {
      ctx.fillStyle = color(id);
      ctx.font = '20px serif';
      ctx.fillText(glyph(id), x + 5, height / 2 - 5);
    }
  }
  if (!terminal) segment(width - 10);
}
