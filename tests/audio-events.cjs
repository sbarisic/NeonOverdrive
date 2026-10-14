const assert = require('node:assert/strict'),
  fs = require('node:fs'),
  vm = require('node:vm');
const calls = [],
  listeners = {},
  visibility = {};
const synth = Object.fromEntries(
  ['fire', 'impact', 'explosion', 'tone', 'laser', 'silence', 'stopHum'].map((name) => [
    name,
    (...args) => calls.push({ name, args }),
  ]),
);
const sandbox = {
  createCombatSynth: () => synth,
  window: {
    AudioContext: class {
      state = 'running';
    },
    addEventListener(type, fn) {
      listeners[type] = fn;
    },
  },
  document: {
    hidden: false,
    addEventListener(type, fn) {
      visibility[type] = fn;
    },
  },
  setTimeout() {
    return 1;
  },
  clearTimeout() {},
};
vm.createContext(sandbox);
vm.runInContext(
  fs
    .readFileSync('src/ArcadeDodger/Web/Web/audio.js', 'utf8')
    .replace(/^import .*;\r?\n/m, '')
    .replace('export function', 'function') + '\nthis.audio=createArenaAudio();',
  sandbox,
);
listeners.pointerdown();
const self = {
  id: 1,
  life: 0,
  hull: 3,
  shield: 2,
  x: 0,
  y: 0,
  skills: [],
  weapons: [],
  level: 1,
  experience: 0,
};
const frame = (tick, shots = [], beams = [], extra = {}) => ({
  tick,
  players: [{ ...self }],
  shots,
  beams,
  enemies: [],
  ...extra,
});
const observe = (...args) => sandbox.audio.observe(frame(...args), 1);
const count = (name) => calls.filter((c) => c.name === name).length;
const last = (name) => calls.filter((c) => c.name === name).at(-1);
observe(1);
observe(3, [{ id: 1, owner: 1, kind: 1, parent: 0 }]);
assert.equal(count('fire'), 1);
observe(5, [
  { id: 2, owner: 1, kind: 1, parent: 1 },
  { id: 3, owner: 1, kind: 1, parent: 1 },
]);
assert.equal(count('fire'), 1, 'split descendants stay silent');
observe(7, [], [{ owner: 1 }, { owner: 1 }, { owner: 1 }]);
assert.equal(last('laser').args[0].length, 1, 'forks share one sustained voice');
observe(
  9,
  [],
  [0, 1, 2, 3].flatMap((weapon) => [
    { owner: 1, weapon },
    { owner: 1, weapon },
  ]),
);
assert.equal(last('laser').args[0].length, 4);
observe(11, [], [{ owner: 1, weapon: 2 }]);
assert.deepEqual(Array.from(last('laser').args[0]), [2]);
const shots = [0, 1, 2, 3].map((weapon) => ({
  id: 10 + weapon,
  owner: 1,
  weapon,
  kind: weapon + 1,
  parent: 0,
}));
observe(13, shots);
assert.equal(count('fire'), 5, 'each weapon gets its own attack');
observe(15, shots);
assert.equal(count('fire'), 5, 'existing projectiles do not replay');
sandbox.audio.setMuted(true);
assert.equal(count('silence'), 1, 'mute cancels tails and hums');
observe(17, [{ id: 20, owner: 1, kind: 1 }]);
assert.equal(count('fire'), 5);
sandbox.audio.setMuted(false);
observe(19, [{ id: 20, owner: 1, kind: 1 }]);
assert.equal(count('fire'), 5, 'unmute does not replay muted shots');
const explosions = [{ id: 1, x: 0, y: 0 }],
  combatEvents = [{ id: 1, kind: 2, x: 0, y: 0 }];
observe(21, [], [], { explosions, combatEvents });
assert.equal(count('explosion'), 1, 'destruction and blast coalesce');
assert.equal(last('explosion').args[0], true);
observe(23, [], [], { explosions, combatEvents });
assert.equal(count('explosion'), 1, 'events do not replay');
observe(25, [], [], { enemies: [{ id: 1, x: 0, y: 0, hull: 2 }] });
observe(27, [], [], { enemies: [] });
assert.equal(count('explosion'), 1, 'enemy relevance exits are silent');
const impacts = [
  { id: 2, kind: 1, x: 30, y: 0, material: 'shield', weaponKind: 1 },
  { id: 3, kind: 3, x: 50, y: 0, material: 'hull', weaponKind: 4 },
  { id: 4, kind: 1, x: 2000, y: 0, material: 'rock', weaponKind: 1 },
];
observe(28, [], [], { combatEvents: impacts });
assert.equal(count('impact'), 2, 'nearby shield and piercing events sound, distant impacts do not');
assert.equal(last('impact').args[1], 4);
observe(29, [], [], { combatEvents: impacts });
assert.equal(count('impact'), 2, 'repeated snapshots cannot replay impacts');
sandbox.document.hidden = true;
visibility.visibilitychange();
assert.equal(count('silence'), 2);
observe(29, [{ id: 30, owner: 1, kind: 1 }]);
assert.equal(count('fire'), 5);
sandbox.document.hidden = false;
sandbox.audio.reset();
assert.equal(count('silence'), 3);
observe(1);
observe(3, [{ id: 1, owner: 1, kind: 1 }]);
assert.equal(count('fire'), 6, 'reset clears per-weapon cadence history');
console.log(
  'PASS: silent splits, independent weapons, laser grouping, event deduplication, mute, visibility and reset.',
);
