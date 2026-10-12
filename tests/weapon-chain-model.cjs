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
assert.equal(dense.branches, 6 ** 8);
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
