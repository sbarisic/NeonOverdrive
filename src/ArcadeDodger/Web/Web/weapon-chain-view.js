import { buildChainModel } from './weapon-chain-model.js';

const amount = (value) =>
  value >= 1000
    ? value.toLocaleString('en-US', { maximumFractionDigits: 1 })
    : value >= 0.01
      ? Number(value.toFixed(2)).toString()
      : value.toExponential(1);

export function createChainPreview(element, onStage) {
  const root = element('section', '', 'chain-panel'),
    diagram = element('div', '', 'chain-diagram'),
    heading = element('div', '', 'chain-caption'),
    canvas = element('canvas', '', 'chain-preview'),
    note = element('span', '', 'chain-note'),
    details = element('div', '', 'preview-values'),
    summary = element('div', '', 'chain-summary'),
    stages = element('div', '', 'chain-stages');
  heading.append(element('strong', 'FIRING PATTERN'), note);
  canvas.setAttribute('role', 'img');
  diagram.append(heading, canvas);
  details.append(summary, stages);
  root.append(diagram, details);
  const rows = Array.from({ length: 8 }, (_, i) => {
    const button = element('button', '', 'chain-stage'),
      checkpoint = element('span', `${(i + 1) * 5}%`, 'chain-checkpoint'),
      text = element('span'),
      name = element('strong'),
      result = element('small');
    button.type = 'button';
    text.append(name, result);
    button.append(checkpoint, text);
    button.addEventListener('click', () => onStage(i));
    stages.append(button);
    return { button, name, result };
  });
  let model,
    revision = '';
  return {
    root,
    update(weapon, index, stats, definitions) {
      const next = JSON.stringify([weapon, index, stats, [...definitions.values()]]);
      if (revision === next) return;
      revision = next;
      model = buildChainModel(weapon, stats, definitions);
      summary.replaceChildren(
        element('strong', `WEAPON ${index + 1} · ${model.emitter}`),
        element(
          'span',
          `${amount(model.baseDamage)} base ${model.unit} · ${Math.round(model.range)} range${model.laser ? '' : ` · ${amount(model.rate)} shots/s`}`,
        ),
        element(
          'b',
          `${amount(model.branches)} ${model.terminal ? 'mines' : 'branches'} · ${amount(model.damage)} ${model.unit} each`,
        ),
      );
      note.textContent =
        model.branches > model.pathCount
          ? `${model.pathCount} representative paths · ${amount(model.branches)} total`
          : 'Fork schematic · angles in details';
      canvas.setAttribute(
        'aria-label',
        `Weapon ${index + 1}: ${model.branches} branches, ${amount(model.damage)} ${model.unit} each. ${model.terminal ? 'Chain ends at Mine Layer.' : 'Output tail shortened for clarity.'}`,
      );
      for (const [i, stage] of model.stages.entries()) {
        const row = rows[i];
        row.name.textContent = stage.name;
        row.result.textContent = !stage.reachable
          ? 'Unreachable'
          : !stage.id
            ? 'Pass through'
            : `${amount(stage.damage)} × ${amount(stage.branches)}${stage.angle !== null ? ` · ±${Number(stage.angle.toPrecision(3))} rad` : ''}`;
        row.button.dataset.reachable = String(stage.reachable);
        row.button.dataset.empty = String(!stage.id);
        row.button.title = `${(i + 1) * 5}% · ${stage.name}: ${row.result.textContent} (${model.unit} per branch × branch count). Select checkpoint.`;
      }
    },
    draw(time) {
      if (!model) return;
      const width = canvas.clientWidth,
        height = canvas.clientHeight;
      if (!width || !height) return;
      const dpr = window.devicePixelRatio || 1,
        pixelWidth = Math.round(width * dpr),
        pixelHeight = Math.round(height * dpr);
      if (canvas.width !== pixelWidth || canvas.height !== pixelHeight) {
        canvas.width = pixelWidth;
        canvas.height = pixelHeight;
      }
      const ctx = canvas.getContext('2d');
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
      ctx.clearRect(0, 0, width, height);
      const x = (level) => 24 + (level * (width - 60)) / 9,
        y = (position) => 34 + position * (height - 56);
      ctx.font = '11px monospace';
      ctx.textAlign = 'center';
      for (let i = 1; i <= 8; i++) {
        const stage = model.stages[i - 1];
        ctx.fillStyle = stage.reachable ? '#b2b5c8' : '#646779';
        ctx.fillText(`${i * 5}%`, x(i), 16);
        ctx.strokeStyle = '#a9bdd112';
        ctx.beginPath();
        ctx.moveTo(x(i), 24);
        ctx.lineTo(x(i), height - 12);
        ctx.stroke();
      }
      for (const [index, link] of model.links.entries()) {
        const x1 = x(link.from),
          x2 = x(link.from + 1),
          y1 = y(link.y1),
          y2 = y(link.y2),
          mid = (x1 + x2) / 2;
        ctx.strokeStyle = link.color;
        ctx.globalAlpha = 0.65;
        ctx.lineWidth = Math.min(5, 0.8 + Math.sqrt(link.power));
        ctx.beginPath();
        ctx.moveTo(x1, y1);
        ctx.bezierCurveTo(mid, y1, mid, y2, x2, y2);
        ctx.stroke();
        if (!model.laser) {
          // Stagger path lights so a dense fork does not become a solid white wall.
          const t = (time * 0.65 - link.from * 0.09 + ((index * 0.173) % 1) + 2) % 1,
            u = 1 - t,
            px = u ** 3 * x1 + 3 * u * t * mid + t ** 3 * x2,
            py = u ** 3 * y1 + 3 * u ** 2 * t * y1 + 3 * u * t ** 2 * y2 + t ** 3 * y2;
          ctx.globalAlpha = 0.9;
          ctx.fillStyle = '#fff3ce';
          ctx.fillRect(px - 1.3, py - 1, 2.6, 2);
        }
      }
      ctx.globalAlpha = 1;
      ctx.fillStyle = '#edd18c';
      ctx.beginPath();
      ctx.arc(x(0), y(0.5), 3, 0, Math.PI * 2);
      ctx.fill();
      if (model.terminal) {
        ctx.strokeStyle = '#ffcf6e';
        ctx.lineWidth = 1.5;
        for (const node of model.levels.at(-1))
          ctx.strokeRect(x(model.levels.length - 1) - 3, y(node.y) - 3, 6, 6);
      }
    },
  };
}
