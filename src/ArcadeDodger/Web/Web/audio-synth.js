// Short, layered transients provide punch without raising the entire mix.
// All voices share one output bus and a cached noise buffer; branches add no voices.
export function createCombatSynth(context) {
  const output = context.createGain(),
    compressor = context.createDynamicsCompressor(),
    voices = new Set(),
    hums = new Map(),
    noiseBuffer = context.createBuffer(1, Math.ceil(context.sampleRate * 0.6), context.sampleRate);
  output.gain.value = 0.7;
  compressor.threshold.value = -18;
  compressor.knee.value = 12;
  compressor.ratio.value = 5;
  compressor.attack.value = 0.003;
  compressor.release.value = 0.12;
  output.connect(compressor);
  compressor.connect(context.destination);
  const samples = noiseBuffer.getChannelData(0);
  let seed = 0x41c64e6d;
  for (let i = 0; i < samples.length; i++) {
    seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
    samples[i] = (seed / 0x100000000) * 2 - 1;
  }

  function voice(source, duration, volume, delay = 0, filter = null) {
    const gain = context.createGain(),
      at = context.currentTime + delay;
    gain.gain.setValueAtTime(0, at);
    gain.gain.linearRampToValueAtTime(volume, at + 0.002);
    gain.gain.exponentialRampToValueAtTime(0.0001, at + duration);
    if (filter) {
      source.connect(filter);
      filter.connect(gain);
    } else source.connect(gain);
    gain.connect(output);
    const entry = { source, gain, filter };
    voices.add(entry);
    source.onended = () => {
      source.disconnect();
      filter?.disconnect();
      gain.disconnect();
      voices.delete(entry);
    };
    source.start(at);
    source.stop(at + duration + 0.015);
  }
  function tone(start, end, duration = 0.1, type = 'sine', volume = 0.05, delay = 0) {
    if (voices.size >= 24) return;
    const source = context.createOscillator(),
      at = context.currentTime + delay;
    source.type = type;
    source.frequency.setValueAtTime(start, at);
    source.frequency.exponentialRampToValueAtTime(Math.max(20, end), at + duration);
    voice(source, duration, volume, delay);
  }
  function noise(frequency, duration, volume, type = 'bandpass') {
    if (voices.size >= 24) return;
    const source = context.createBufferSource(),
      filter = context.createBiquadFilter();
    source.buffer = noiseBuffer;
    filter.type = type;
    filter.frequency.setValueAtTime(frequency, context.currentTime);
    filter.frequency.exponentialRampToValueAtTime(frequency * 0.45, context.currentTime + duration);
    filter.Q.value = 0.7;
    voice(source, duration, volume, 0, filter);
  }
  function stopHum() {
    for (const hum of hums.values()) {
      hum.source.stop();
      hum.source.disconnect();
      hum.gain.disconnect();
    }
    hums.clear();
  }
  return {
    tone,
    fire(kind) {
      if (kind === 3) {
        // Rocket: a heavy ignition thump and an airy exhaust tail.
        tone(145, 38, 0.22, 'sine', 0.16);
        noise(950, 0.19, 0.14, 'lowpass');
      } else if (kind === 4) {
        // Plasma: rounded electrical discharge with a bright initial snap.
        tone(430, 85, 0.16, 'triangle', 0.105);
        noise(3200, 0.035, 0.08);
      } else if (kind === 2) {
        tone(1200, 420, 0.09, 'sine', 0.06);
        noise(4200, 0.025, 0.055);
      } else {
        // Kinetic: brief mechanical crack over a tight low-frequency body.
        tone(190, 55, 0.085, 'triangle', 0.13);
        noise(2400, 0.045, 0.16);
      }
    },
    impact(shield = false) {
      tone(shield ? 720 : 210, shield ? 260 : 65, 0.095, 'triangle', 0.08);
      noise(shield ? 3800 : 1800, 0.045, 0.08);
    },
    explosion(destruction = false) {
      tone(destruction ? 105 : 145, 28, destruction ? 0.42 : 0.24, 'sine', 0.18);
      noise(1500, destruction ? 0.48 : 0.26, 0.23, 'lowpass');
      if (destruction) tone(230, 45, 0.13, 'triangle', 0.065);
    },
    laser(weapons) {
      const active = new Set(weapons);
      for (const [id, hum] of hums) {
        if (active.has(id)) continue;
        hum.source.stop();
        hum.source.disconnect();
        hum.gain.disconnect();
        hums.delete(id);
      }
      for (const id of active) {
        if (hums.has(id)) continue;
        const source = context.createOscillator(),
          gain = context.createGain();
        source.type = 'triangle';
        source.frequency.value = 115 + id * 9;
        gain.gain.value = 0;
        source.connect(gain);
        gain.connect(output);
        source.start();
        hums.set(id, { source, gain });
      }
      for (const hum of hums.values())
        hum.gain.gain.setTargetAtTime(0.035 / Math.sqrt(hums.size), context.currentTime, 0.025);
    },
    stopHum,
    silence() {
      stopHum();
      for (const { source, gain, filter } of voices) {
        source.stop();
        source.disconnect();
        filter?.disconnect();
        gain.disconnect();
      }
      voices.clear();
    },
  };
}
