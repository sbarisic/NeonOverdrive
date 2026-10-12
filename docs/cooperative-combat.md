# Cooperative combat and run economy — protocol v5

This release changes browser multiplayer. Single-player and the 69-skill catalog are unchanged.

## Playing

- Death starts a new economic run: 100 credits, four kinetic emitters, empty attachment chains, and only weapon 1 enabled. Skills and XP reset. Existing profiles keep their purchases until their next death.
- Refreshing or reconnecting resumes the same pilot, including damage, skills, equipment, and cooldowns. A disconnected ship remains exposed for five seconds. Changing rooms cannot bypass that body or its death reset.
- Live run checkpoints are saved every second and on disconnect. After a restart or expired sector, the last saved run resumes at the arrival port. An abrupt process failure can lose progression since the last checkpoint; acknowledged purchases and payouts are already durable.
- Drops eject outside solid wrecks and slide against obstacles. Gold pickups contain credits, cyan pickups contain XP, and square MOD pickups grant an owned module copy. Equipment screens, docking, death, and disconnection prevent collection.
- The map's **Ping friends** shares a selected landmark with mutual friends. **Broadcast to room** explicitly shares it with everyone. Sharing reveals the marker without revealing surrounding terrain. Signals last 30 seconds, with a five-second sending cooldown.
- Select a friend and use **Defend ally** to mark that pilot and the threatening patrol. Its waypoint follows the ally while their ship remains visible in player state. Pings never fire weapons or alter friendship/PvP rules.
- Stations now offer **Patrol Interdiction**: contribute at least one damage to each of three ships from the designated corridor, return for 60 credits and 45 XP, and wait five minutes before repeating. This supplies objectives after permanent installation destruction.

## Enemies and rewards

Enemy levels follow their source region, not player level. Corridors inherit their destination's level.

| Region / level | Size | Hull multiplier | Ordinary hit damage |
|---|---:|---:|---:|
| Inhabited / 1 | 0.65× | 0.6× | 0.25 |
| Volcanic / 2 | 0.80× | 1× | 0.50 |
| Gas giant / 3 | 0.95× | 1.5× | 0.75 |
| Shattered / 4 | 1.10× | 2× | 1 |

Hull rounds up once. Damage accumulates fractionally. Displayed size and collision radius agree. Installations retain 120 hull and their existing immunity to displacement, slow, and stun.

- **Interceptor:** level 2+, fast flanking attacks, two-shot bursts, and retreating turns.
- **Siege Sniper:** level 3+, a one-second aim warning followed by a fast shot at twice regional damage. Cover blocks it.
- **Repair Tender:** level 2+, restores another living ship at 0.5 hull/second within 250 units and line of sight. It cannot repair itself or installations.

Ship XP and credits scale by level. Each pilot contributing at least one actual damage to an installation receives personal pickups worth 40 × level credits and 30 × level XP, plus a 50% chance of one uniformly selected shop module. Status damage counts; the final hit is irrelevant. Salvage Beacon modifies installation credit value before collection skills. Fortune/Luck affect credits, Harvest affects XP, and contract payouts stay fixed.

Personal rewards belong to the contributing run. Death invalidates unclaimed rewards; disconnection preserves them until expiry. Pending writes reserve pickups. Failed credit writes retain the original player/pickup payout, even if income skills change before retry. Overflow personal drops wait for pickup capacity until their original expiry.

## Runtime and compatibility

- Production limits: **512 projectiles, 192 enemies, 1,536 beam segments, 128 mines**, with existing per-pilot beam/mine limits. XP, credits, and module pickups each have a 128-entity visible-world limit.
- Projectile and beam emitters rotate pilot/weapon priority. Split saturation preserves the parent and never charges inventory. Ancestry indexing preserves continuous forks and silent descendants.
- Nearby combat snapshots use a 1,400-unit interest radius. Remote players retain social/radar information without sending their full equipment and skill arrays.
- Socket senders serialize selected snapshots outside the simulation loop. Private inventory, social, and sector-session state is revisioned and omitted when unchanged. Deltas are computed after the bounded send queue selects a frame; skipped frames cannot lose updates.
- Wire v5 packs shots as `[id, owner, x, y, dx, dy, born, kind, effects, modular, parent, birthX, birthY, weapon, stage, power, ancestors]` and beams as `[owner, segment, x, y, endX, endY, effects, weapon, stage, power]`. `snapshot.js` expands them for UI/audio consumers, then passes bounded numeric arrays directly to WASM for presentation. Legacy JSON fixture input remains available.
- The welcome message supplies enemy definitions. Sector commands add `ping`, `broadcast`, and `defend`; snapshots add enemy level/radius/max hull, personal pickup ownership, module drops, run identity, contribution markers, cooldowns, and pings. Older protocols receive refresh-required handling.
- Profile changes run through one ordered writer. Wallet changes and death resets publish only after atomic file replacement succeeds. Respawn waits for a successful reset; the reset's previous run ID prevents duplicate starter grants.

## Verification and performance

Reproduce server measurements with `NEON_PERF_OUTPUT` set to an artifact directory, then run the `CombatPerformanceTests` filter in Release. The test records 24 pilots with four extreme chains, actual simulation snapshots, per-viewer encoding, persistence completion, allocations, bytes, and playback. It includes 300 warm-up ticks and 600 measured ticks at 256/96, 512/192, and 1,024/256 projectile/enemy limits.

Run `node tests/browser-combat-performance.cjs` against the built server using `ARENA_URL`. It feeds recorded combat through the actual WASM renderer at 1920×1080; it is a presentation fixture, not a WAN latency test. Reports and screenshots are written under `artifacts/combat-performance`.

Measured on CARPPC, Ryzen 5 3600XT, .NET 10.0.12, Edge:

| 512-projectile / 192-enemy scenario | 95th percentile |
|---|---:|
| Server simulation | 8.3 ms |
| Full server test iteration, including serialization and queued persistence | 16.7 ms |
| Browser frame work | 6.2 ms |
| Browser snapshot handoff | 4.2 ms |

The initial JSON-to-WASM fixture took about 496 ms per snapshot at this load. The packed handoff removes that second JSON parse; indexed playback and cached beam transforms remove repeated per-frame searches. The 1,024-projectile stress recording measured 8.5 ms frame work and 7.4 ms handoff; that higher count remains a test configuration.

These measurements are local acceptance results, not a guarantee for every device. Run the complete .NET suite and the multiplayer, sector, cooperation, run-reset, weapon, skills, friends, prediction, viewport, presentation, beam, enemy, and cache-version browser checks after changing protocol or simulation behavior.
