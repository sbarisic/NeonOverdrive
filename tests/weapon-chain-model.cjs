const assert = require('node:assert/strict'),
  fs = require('node:fs'),
  vm = require('node:vm');
const sandbox = { color: (id) => String(id) };
vm.createContext(sandbox);
vm.runInContext(
  fs
    .readFileSync('src/ArcadeDodger/Web/Web/weapon-chain-model.js', 'utf8')
    .replace(/^import .*;\r?\n/m, '')
    .replace('export function', 'function'),
  sandbox,
);
const definitions = new Map([
  [11, { value: 2, radius: 0.22 }],
  [12, { value: 3, radius: 0.3 }],
  [13, { value: 6, radius: 0.6 }],
  [5, { value: 2 }],
]);
function model(attachments, emitter = 1) {
  return sandbox.buildChainModel({ emitter, attachments }, [], definitions);
}
const typical = model([21, 9, 14, 9, 12, 13, 0, 0]);
assert.equal(typical.branches, 18);
assert.equal(typical.pathCount, 18);
assert.equal(typical.stages[4].angle, 0.075);
assert.equal(typical.stages[5].angle, 0.15);
assert.equal(typical.levels.length, 10);
assert.equal(typical.levels[5].length, 1, 'split starts after the 25% checkpoint');
assert.equal(typical.levels[6].length, 3);
assert.equal(typical.levels[7].length, 18);
const dense = model(Array(8).fill(13));
assert.equal(dense.branches, 6 ** 4);
assert.equal(dense.pathCount, 64);
assert(dense.links.length <= 9 * 64);
for (const level of dense.levels) {
  assert(level.length <= 64);
  assert(
    Math.abs(level.reduce((sum, n) => sum + n.y, 0) / level.length - 0.5) < 1e-9,
    'both sides are represented symmetrically',
  );
}
for (const link of dense.links) {
  assert(link.y1 >= 0 && link.y1 <= 1 && link.y2 >= 0 && link.y2 <= 1);
  assert(Number.isFinite(link.power));
}
const terminal = model([11, 26, 13, 5, 0, 0, 0, 0]);
assert.equal(terminal.branches, 2);
assert.equal(terminal.damage, 0.5);
assert.equal(terminal.levels.length, 3);
assert.equal(terminal.stages[2].reachable, false);
const beam = model([5, 0, 11, 0, 0, 0, 0, 0], 2);
assert.equal(beam.baseDamage, 4);
assert.equal(beam.stages[0].damage, 8);
assert.equal(beam.damage, 4);
assert.equal(beam.unit, 'DPS');
console.log(
  'PASS: checkpoint ordering, damage, spread, terminal stages and symmetric bounded dense paths.',
);

// Bloom shortens downstream range: later checkpoint slots are genuinely unreachable.
assert.equal(dense.stages[4].reachable, false);
for (let level = 1; level <= 4; level++) {
  const converter = 25 + 32 * (level - 1),
    accelerator = 6 + 32 * (level - 1);
  const strength = 1 + 0.25 * (level - 1);
  definitions.set(converter, { name: 'Plasma Converter', value: 0, level });
  definitions.set(accelerator, { value: 1 + 0.25 * strength, level });
  const plasma = model([accelerator, 0, converter, 0, 0, 0, 0, 0]);
  assert.equal(plasma.projectile, 'Plasma');
  assert.equal(plasma.outputSpeed, 300 * (1 + 0.25 * strength));
  assert.equal(plasma.stages[2].radius, 45 * strength);
  assert.notEqual(plasma.links[2].color, plasma.links[3].color);
  assert.equal(plasma.damage, 1, 'conversion does not multiply inherited damage');
}
console.log('PASS: conversion speed, splash tiers, visual checkpoint and inherited damage.');

if (process.env.NEON_PREVIEW_FIXTURES) {
  const authority = JSON.parse(fs.readFileSync(process.env.NEON_PREVIEW_FIXTURES, 'utf8'));
  const catalog = new Map(authority.definitions.map((d) => [d.id, d]));
  for (const fixture of authority.fixtures) {
    const actual = sandbox.buildChainModel(fixture.weapon, authority.stats, catalog);
    const stage = actual.stages[0];
    for (const key of ['damage', 'range', 'speed', 'radius', 'branches']) {
      assert(
        Math.abs(stage[key] - fixture[key]) < 0.001,
        `Component ${fixture.weapon.attachments[0]} ${key}: preview ${stage[key]}, server ${fixture[key]}`,
      );
    }
    assert.equal(stage.projectile, fixture.kind);
  }
  console.log(
    `PASS: ${authority.fixtures.length} authoritative module/tier fixtures match the preview.`,
  );
}
