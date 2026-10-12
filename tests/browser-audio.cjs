const { chromium } = require('../artifacts/browser-check/node_modules/playwright');
const assert = require('node:assert/strict');

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage();
    await page.goto(process.env.ARENA_URL || 'http://localhost:5094');
    const results = await page.evaluate(async () => {
      const { createCombatSynth } = await import('./audio-synth.js');
      const results = [];
      for (const sound of [
        'kinetic',
        'laser-shot',
        'rocket',
        'plasma',
        'impact',
        'shield',
        'blast',
        'destruction',
        'saturation',
        'silence',
      ]) {
        const context = new OfflineAudioContext(1, 48000, 48000);
        let started = 0,
          ended = 0,
          buffers = 0;
        for (const method of ['createOscillator', 'createBufferSource']) {
          const original = context[method].bind(context);
          context[method] = () => {
            const source = original(),
              start = source.start.bind(source);
            source.start = (...args) => {
              started++;
              start(...args);
            };
            source.addEventListener('ended', () => ended++);
            return source;
          };
        }
        const createBuffer = context.createBuffer.bind(context);
        context.createBuffer = (...args) => {
          buffers++;
          return createBuffer(...args);
        };
        const synth = createCombatSynth(context);
        if (sound === 'saturation' || sound === 'silence') {
          for (let i = 0; i < 128; i++) synth.fire((i % 4) + 1);
          synth.laser([0, 1, 2, 3]);
          synth.laser([0, 1, 2, 3]); // Repeated snapshots must not create more hums.
          if (sound === 'silence') synth.silence();
        } else if (sound === 'impact' || sound === 'shield') synth.impact(sound === 'shield');
        else if (sound === 'blast' || sound === 'destruction')
          synth.explosion(sound === 'destruction');
        else synth.fire(['kinetic', 'laser-shot', 'rocket', 'plasma'].indexOf(sound) + 1);
        const samples = (await context.startRendering()).getChannelData(0);
        let peak = 0,
          energy = 0,
          tail = 0;
        for (let i = 0; i < samples.length; i++) {
          peak = Math.max(peak, Math.abs(samples[i]));
          energy += samples[i] ** 2;
          if (i > 38400) tail += samples[i] ** 2;
        }
        results.push({
          sound,
          peak,
          rms: Math.sqrt(energy / samples.length),
          tail,
          started,
          ended,
          buffers,
        });
        synth.silence();
      }
      return results;
    });
    for (const result of results) {
      console.log(result);
      assert.equal(result.buffers, 1, 'noise is generated once per context');
      assert(result.peak < 0.9 && Number.isFinite(result.rms), 'mix retains headroom');
      if (result.sound === 'silence') assert.equal(result.peak, 0, 'mute stops all queued voices');
      else assert(result.peak > 0.01 && result.rms > 0.001, 'effect is audible');
      if (['saturation', 'silence'].includes(result.sound))
        assert.equal(result.started, 28, '24 transient voices plus four sustained weapons');
      else {
        assert.equal(result.ended, result.started, 'one-shots release every source');
        assert(result.tail < 0.000001, 'short tails settle between encounters');
      }
    }
    console.log(
      'PASS: real Web Audio synthesis, output headroom, bounded voices, noise reuse and immediate mute.',
    );
  } finally {
    await browser.close();
  }
})().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
