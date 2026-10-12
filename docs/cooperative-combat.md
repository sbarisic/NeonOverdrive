# Cooperative combat and run economy — protocol v9

The game is desktop WebAssembly multiplayer only. Protocol v8 adds level-gated weapon sockets, solid planets, surface factions, and relief-ship rescues. The skill catalog has 66 unlocks: the three purchasable Weapon Bay nodes have been removed and refunded. See [sector rules](inhabited-sector.md) and [weapon progression](multiplayer-skills.md).

## Playing

- Every fresh run resets level, XP, skills, health, equipment, and wallet. Restore four kinetic emitters, empty attachment chains, and only weapon 1 enabled. Starting credits replace the previous wallet: **100 + 5 × ReplayCount**.
- Only death increases the permanent replay count. Death resets are atomic and guarded by the previous run ID; retries and respawn reuse that reset. Fresh joins use the arrival port; death respawn uses the last visited station.
- Refresh or unexpected disconnect resumes the existing pilot for **strictly less than 60 seconds** from the first disconnect, including damage, progression, purchases, and cooldowns. The disconnected body remains exposed for five seconds. Repeated disconnect notifications cannot extend recovery.
- Explicit Leave uses a sequenced command and server acknowledgement, disables recovery immediately, and adds no replay. Expired recovery, server restart, and sector expiration start fresh without increasing the counter. Friendships and remembered names persist; same-sector discovery and contracts follow their existing death rules.
- Fresh admission and respawn wait for durable reset success. Failed saves allow retry without partial changes. Pending purchases, rewards, and checkpoints are guarded by run identity. Private profiles include replay count and starting allowance; v7 and older clients must refresh.
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

Reproduce server measurements with `NEON_PERF_OUTPUT` set to an artifact directory and `NEON_PERF_ENFORCE=1`, then run the `CombatPerformanceTests` filter in Release. The test records 24 pilots with four extreme chains, actual simulation snapshots, per-viewer encoding, persistence completion, allocations, bytes, and playback. It includes 300 warm-up ticks and 600 measured ticks at 256/96, 512/192, and 1,024/256 projectile/enemy limits. Stop other test servers before measuring; the enforcement flag asserts the production 25 ms end-to-end gate. The browser fixture asserts the production 20 ms frame-work and frame-interval gates.

Run `node tests/browser-combat-performance.cjs` against the Release browser bundle using `ARENA_URL`. It feeds recorded, interest-filtered viewer packets through JSON expansion, private-state merging, packed handoff, and the actual WASM renderer at 1920×1080. This is a local transport/presentation fixture, not a WAN latency test. Reports and screenshots are written under `artifacts/combat-performance`. Set `PERF_LIMIT=512` for production or `1024` for stress. Set `PERF_FULL_WORLD=1` separately to bypass the server's normal interest filtering; that intentionally over-delivers distant combat and is not the production network path.

Remeasured with the expanded v8 world and mixed NPC population on CARPPC, Ryzen 5 3600XT, .NET 10.0.12, Edge (2026-10-12):

| 512-projectile / 192-enemy scenario | 95th percentile |
|---|---:|
| Server simulation | 7.0 ms |
| Full server test iteration, including serialization and queued persistence | 13.5 ms |
| Browser frame work | 6.4 ms |
| Browser packet processing and snapshot handoff | 4.0 ms |
| Browser frame interval, including packet-processing contention | 8.4 ms |

The 1,024-projectile / 256-NPC viewer stress recording measured 8.9 ms frame work, 6.6 ms packet processing, and 12.6 ms frame intervals. Both viewer recordings had zero intervals above 50 ms. The separate unfiltered whole-room stress at production population measured 12 ms frame work and roughly 21–25 ms frame intervals; production uses nearby-entity filtering. Limits remain 192 combined combat NPCs and 512 projectiles.

These measurements are local acceptance results, not a guarantee for every device. Run the complete .NET suite and the multiplayer, sector, cooperation, run-reset, weapon, skills, friends, prediction, viewport, presentation, beam, enemy, and cache-version browser checks after changing protocol or simulation behavior.

## Inertial movement and HUD update (v6)

The server and prediction share acceleration (1,200 units/s²), counter-steering (1,800 units/s²), and passive deceleration (300 units/s²). Maximum base speed stays 300 units/s. Releasing WASD coasts for about one second; menus stop the ship, but Escape does not add protection. Collisions remove inward velocity and preserve sliding. Shift has no multiplayer action.

Kinetic rounds travel at 1,300 units/s, fire six times per second before skills, and have a 1.5-unit collision/core radius with a six-unit visual trail. Other emitters and enemy cadence are unchanged. Conversions retain modifier multipliers while using the destination projectile's base speed.

Thruster Tuning I–II replaces Phase Dash/Long Phase (+25/50% acceleration), Vector Control I–II replaces Blink Cycle (+25/50% steering), Cruise Engines replaces Comet Drive (+10% speed, additive with Slipstream), Impact Dampeners replaces Storm Nova (half enemy ramming damage), and Field Repairs replaces Salvage Cycle (+0.25 hull per XP pickup). Existing learned IDs map one-to-one on load without healing or changing points/resources. Old dash timers are ignored.

Snapshots carry velocity, acceleration, steering, and menu state instead of dash state. Input carries `menu` separately from `editing`, so immobilization does not imply equipment cloak. Checkpoints save velocity; existing bodies retain it on reconnect, while arrival in a new sector starts at rest. Protocol v5 clients must refresh.

The header uses compact groups. The local minimap is centered; the command grid and canvas footer share one CSS-pixel layout exported by the renderer. Desktop viewports of at least 1280×720 and high-DPI scaling are supported; mobile layouts were removed. The world remains zoomed out 25%.


## Inventory expansion verification (v9)

The slot-inventory update adds persistent weapon objects, six typed ship systems, personal equipment drops, dormant mod assembly, and station sales. See [equipment rules](equipment.md). Existing run/replay and world rules are unchanged.

Rechecked on CARPPC (.NET 10.0.12, Edge, 1920 × 1080) with the four-weapon split workload at 192 NPCs / 512 projectiles:

- Server simulation p95: 10.18 ms; end-to-end p95 including 24-peer serialization and ordered persistence: 19.72 ms (gate: below 25 ms).
- Browser render p95: 10.20 ms; receive/expansion p95: 7.40 ms; frame-interval p95: 16.60 ms (render and interval gates: below 20 ms).
- Measurements use actual viewer packets with JSON expansion and private-state merge. Raw results and recordings are in `artifacts/combat-performance/combat-512.json`, `frames-512.json`, and `browser-512.json`.
- Browser checks cover 1280 × 720 and 1920 × 1080, pixel densities 1/2, a 3,000-weapon virtual inventory, ship sockets, stored builds, incoming-loot draft merges, purchases, dormant mods, recovery, and station sales.

Final verification: 408 .NET regressions passed in Release; Debug and Release builds completed with zero warnings/errors. Desktop browser regressions and formatting checks passed. The isolated Debug host used the same Development/UseLocalBrowserBundle settings as the Visual Studio launch profile and reported protocol 9.
