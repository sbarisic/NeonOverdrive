import { createCombatSynth } from './audio-synth.js';

// Audio follows authoritative events. Split descendants and relevance exits are silent.
export function createArenaAudio() {
  let context,
    synth,
    muted = false,
    previous = null,
    humTimeout,
    lastEvent = 0;
  const lastShots = new Map();
  function silence() {
    clearTimeout(humTimeout);
    synth?.silence();
  }
  document.addEventListener('visibilitychange', () => {
    if (document.hidden) silence();
  });
  const unlock = () => {
    try {
      if (!context) {
        context = new (window.AudioContext || window.webkitAudioContext)();
        synth = createCombatSynth(context);
      }
      if (context.state === 'suspended') context.resume().catch(() => {});
    } catch {}
  };
  window.addEventListener('pointerdown', unlock);
  window.addEventListener('keydown', unlock);
  return {
    setMuted(value) {
      muted = value;
      if (value) silence();
    },
    reset() {
      previous = null;
      lastEvent = 0;
      lastShots.clear();
      silence();
    },
    observe(frame, id) {
      const self = frame.players.find((p) => p.id === id);
      if (muted || !synth || context.state !== 'running' || document.hidden) {
        lastEvent = Math.max(lastEvent, ...(frame.combatEvents || []).map((e) => e.id));
        previous = frame;
        return;
      }
      synth.laser(
        self && self.hull > 0 && !self.editing
          ? [
              ...new Set(
                (frame.beams || []).filter((b) => b.owner === id).map((b) => b.weapon || 0),
              ),
            ]
          : [],
      );
      clearTimeout(humTimeout);
      humTimeout = setTimeout(() => synth.stopHum(), 300);
      if (previous && frame.tick > previous.tick && frame.tick - previous.tick < 30) {
        const old = previous.players.find((p) => p.id === id),
          oldShots = new Set(previous.shots.map((s) => s.id));
        // One attack per weapon per snapshot, regardless of branch count or packet batching.
        for (const shot of frame.shots) {
          const weapon = shot.weapon || 0;
          if (
            shot.owner !== id ||
            shot.parent ||
            oldShots.has(shot.id) ||
            frame.tick - (lastShots.get(weapon) ?? -Infinity) < 2
          )
            continue;
          lastShots.set(weapon, frame.tick);
          synth.fire(shot.kind);
        }
        if (self && old && self.life === old.life) {
          if (self.hull <= 0 && old.hull > 0) synth.explosion(true);

          if (self.experience > old.experience) synth.tone(1040, 1560, 0.085, 'sine', 0.035);
          if (self.level > old.level)
            for (let i = 0; i < 4; i++)
              synth.tone(440 * 2 ** (i / 3), 440 * 2 ** (i / 3), 0.2, 'triangle', 0.045, i * 0.075);
          if (
            JSON.stringify(self.skills) !== JSON.stringify(old.skills) ||
            JSON.stringify(self.weapons) !== JSON.stringify(old.weapons)
          )
            synth.tone(550, 1100, 0.13, 'triangle', 0.045);
        }
        const oldExplosions = new Set((previous.explosions || []).map((e) => e.id)),
          oldEvents = new Set((previous.combatEvents || []).map((e) => e.id)),
          nearby = (e) => self && Math.hypot(e.x - self.x, e.y - self.y) < 800,
          destruction = (frame.combatEvents || []).some(
            (e) => e.kind === 2 && !oldEvents.has(e.id) && nearby(e),
          ),
          blast = (frame.explosions || []).some((e) => !oldExplosions.has(e.id) && nearby(e));
        // Coalesce simultaneous explosions: preserve weight without stacking loud tails.
        if (destruction || blast) synth.explosion(destruction);
        const impactGroups = new Set();
        for (const e of frame.combatEvents || []) {
          if (e.id <= lastEvent) continue;
          lastEvent = e.id;
          if (!self || ![1, 3, 4].includes(e.kind)) continue;
          const distance = Math.hypot(e.x - self.x, e.y - self.y);
          const group = `${e.material}:${e.weaponKind}:${Math.floor(e.x / 120)}:${Math.floor(e.y / 120)}`;
          if (distance >= 900 || impactGroups.has(group) || impactGroups.size >= 4) continue;
          impactGroups.add(group);
          synth.impact(
            e.material === 'shield',
            e.weaponKind || 1,
            e.material || 'hull',
            (1 - distance / 900) ** 2,
            (e.x - self.x) / 600,
          );
        }
      }
      lastEvent = Math.max(lastEvent, ...(frame.combatEvents || []).map((e) => e.id));
      previous = frame;
    },
  };
}
