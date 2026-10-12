# Multiplayer MVP implementation contract

This document records the initial MVP contract and subsequent implementation slices. Earlier progression and weapon rules are historical; see [the current skills and workshop guide](multiplayer-skills.md) for the collectible-module system. The game is now WebAssembly multiplayer only. Single-player references below describe historical verification. Values below are initial tuning values, not measured balance or capacity claims.

## Architecture and implementation gate

- Reuse the existing browser C#/WASM and raylib rendering; keep single-player's one-hit death and pause behavior unchanged.
- New `ArcadeDodger.Multiplayer` library contains renderer-independent protocol and simulation rules. It must not reference raylib or the single-player executable.
- A headless ASP.NET Core server will own rooms, ticks, entities, damage, collection, levels and scoring. Clients send intent only.
- Prototype browser WebSocket interoperability and two independent browser players seeing movement, shots and server-confirmed damage before implementing aliens/progression/events. A renderer/runtime replacement requires a separate scope review.
- Start with 30 Hz fixed simulation, 15 Hz snapshots. Snapshot includes server tick, stable entity IDs and each player's last applied input sequence. Predict local movement, reconcile acknowledged input, interpolate remote entities. No client-authoritative hits or positions.
- Simulation is single-writer per room. Networking submits bounded commands; it never mutates entities directly. Slow clients receive the latest snapshot rather than an unbounded history. No graphics initialization on server.

## Initial rules (to implement and tune)

| Rule | Initial value / behavior |
| --- | --- |
| Arena | 2880 x 1620 bounded world; positions are ship centers |
| Ship | Radius 14, speed 300 units/sec, normalized diagonal movement |
| Hull / shield | 3 hull + 2 shield; basic hit costs 1; shield regenerates 1/sec after 5 damage-free seconds; hull does not regenerate |
| Dash | 0.15 sec at 900 units/sec, 3 sec cooldown, no invulnerability; zero movement dashes along aim |
| Basic weapon | Blaster, 0.22 sec cooldown, speed 650, radius 3, lifetime 2 sec; nearest-first swept collisions, no self-hits |
| Spawn | Sample outer-belt candidates and prefer greatest threat clearance; 2 sec protection ends on firing; retry if no acceptable spawn instead of forcing overlap |
| Death | Resolve once, stop input, recap; explicit respawn after at least 1 sec resets only that player |
| Disconnect | Clear held input after 250 ms without input; retain vulnerable ship for 5 sec after disconnect; no refresh recovery promise; silence timeout 10 sec |
| Rooms | Target 12, then test 24 players; provisional hard cap 24; retire empty rooms after grace cleanup; no restart persistence |

Use integer ticks for cooldowns/lifetimes, rounding durations up. Fixed-step movement is independent of packet rate. Input flooding must never buy extra simulation time. Processing order: commands, movement, weapon spawning, swept collision/damage, deaths, collection, rewards, snapshot. Resolve simultaneous contested collections by distance then stable player ID. A dead ship cannot collect on its death tick.

## Progression and reward contract

- Outer aliens reward 5 XP (rammer) / 8 XP (gunner); central encounters reward twice as much. Enemy/projectile/loot caps must be enforced before spawning.
- Start at level 1. Threshold to reach level L: `10 * (L - 1) * L` cumulative XP, cap level 10. Earn bankable upgrade choices at levels 2, 4, 6 and 8. Choosing never pauses the room.
- First choice establishes Blaster, Spread or Rapid. Subsequent choices apply Multishot, Pierce or Overdrive, maximum two ranks each. Proposed effects: Multishot adds two shots/rank; Pierce adds one target/rank; Overdrive divides cooldown by `1 + 0.15 * rank`. Bound final volley/projectile count. Branch balance remains provisional.
- XP pickups are server-created values with unique IDs, consumed atomically once. Expire after 30 sec; cap 128. At cap, discard rewards rather than duplicating or overwriting existing loot.
- A death drops `floor(earned run XP * 0.25)` once, divided into at most 8 pickups whose values sum exactly to that amount. Rest is destroyed. Build power itself does not transfer. No automatic XP for a player kill: death drops are the transferable reward.
- Score = survival seconds + alien reward XP + 25 per player kill + 50 per eligible bounty defeat. Collecting recycled player loot grants XP but not additional score. Stable ID breaks leaderboard ties.
- Mark the highest-scoring living player once level >= 4 and score >= 100. One bounty at a time. Award its defeat bonus only once, to an enemy killing that marked player. Clear/re-evaluate on death/disconnect cleanup.
- Every 90 sec, announce a central salvage location 10 sec before spawning 12 energy pickups worth 10 XP each; expire after 20 sec. No pause, shop, cargo banking, boss or convoy.

## Version 3 input protocol

UTF-8 JSON text WebSocket messages, maximum 4096 bytes per client message including all fragments, maximum JSON depth 8. Reject binary, unknown fields, duplicate fields, invalid types, non-finite numbers and malformed messages. Old clients receive `refresh_required`. Never trust a query parameter as player identity. One connected session is allowed per normalized player name; disconnected sessions do not prevent rejoining.

Messages:

- `{"v":3,"type":"join","name":"Pilot","room":""}`: exactly once before input. Name max 16 ASCII letters/digits/spaces/underscore/hyphen; trim, empty defaults to Pilot. Empty room requests automatic placement; explicit room is a server-generated 8-character lowercase hex ID. Full/missing explicit room reports an error rather than silently splitting friends.
- `{"v":3,"type":"input","seq":1,"x":0,"y":1,"aim":0,"fire":false,"dash":false,"editing":false}`: sequence 1..2147483647 strictly increasing per connection; axes -1..1; aim radians -pi..pi. No client delta, position, player ID, damage or score. Sequence acknowledged only when simulation applies the command. Dash is edge-triggered in simulation, not one dash per packet. Editing requests the equipment cloak and neutral gameplay input.
- `{"v":3,"type":"choice","seq":2,"choice":"shield.1"}`: validate the skill ID, prerequisites and available points on the server. Shares the connection sequence with other actions.
- `{"v":3,"type":"respawn","seq":3}`: valid only after death delay. Duplicate/stale sequence cannot re-trigger rewards.
- `{"v":3,"type":"buy","seq":4,"module":6}`: purchase one catalog item while editing. Ownership and credits change only after persistence succeeds.
- `equip` includes `v`, `type`, `seq` and exactly four `weapons`, each with `emitter`, eight `attachments`, and `enabled`. Validate reserved copies across every row and save the complete loadout atomically.

Welcome supplies the authoritative skill and module catalogs and weapon level guards. Additional weapons also require their bay skills. Learned skills are string ID arrays; private skill receipts and credit payout notifications accompany snapshots. Private workshop snapshots report wallet, owned copies, saved weapons and the latest action receipt. Public snapshots carry weapon configurations, credit pickups, mines and weapon/branch presentation data. See the workshop guide for persistence and execution rules.

Transport must additionally enforce join timeout (5 sec), token bucket limits (60 messages/sec, burst 90), one queued latest movement input and bounded action queue, send timeout, per-IP connection limits and a global room cap. These are not provided by JSON parsing alone. Validate Origin against configured same-origin HTTPS deployment; origin checks are not authentication. Client rendering must treat nicknames as text, never HTML.

## Regression and acceptance inventory

- Existing model regressions: movement, single-player one-hit death, pause, restart, weapon/modifier stacking, swept collisions, director caps, camera/radar.
- Existing browser layout script: canvas backing buffer, GL viewport, containment, aspect ratio and overflow at four viewport sizes and two DPRs. Build Debug browser bundle for the existing development server script.
- New protocol/model tests: malformed/oversized/duplicate/unknown fields, sequence replay, bounded movement/cooldowns, spawn protection, collisions, death ordering, collection conservation, choice legality.
- Live integration: joins/disconnects, room isolation, forged data, fragmented payloads, flood/slow/stalled clients, cleanup. Browser slice must prove actual WASM/WebSocket/raylib interoperability.
- Browser/manual acceptance: Chrome/Edge and Firefox; two independent sessions, same-room links/full-room feedback, mouse aim, focus loss, 0/100/200 ms RTT, HUD, recap, repeated respawns, original single-player.
- Capacity gate: 12 then 24 clients, at least 30 min each. Record tick p50/p95/p99, backlog, memory trend, bandwidth, entity counts and disconnects. Target p95 tick work below 33 ms with no sustained backlog/unbounded growth. Headless load is not browser performance.
- Hosting handoff later: same-origin HTTPS/WSS reverse proxy, health endpoint, metrics, bounded resource configuration, ephemeral restart semantics. No cloud provisioning or public deployment authorized.

## Observed baseline

Initial Release solution build (including browser WASM): exit 0, 0 warnings/errors. Existing model tests: exit 0, 37 passed, 0 failed/skipped. These checks predate multiplayer implementation. Browser/manual and load checks are not yet recorded here; none should be inferred from successful compilation.

## Network slice verification and local startup

S2 automated headless Edge check: two independent contexts joined one room, observed remote movement/shots, and received server-owned shield damage (2 to 1). Screenshots showed both ships, hostile shots and the damaged HUD. `tests/browser-multiplayer.cjs` exited 0. The first run failed because static-file MIME defaults rejected WASM debug symbols; explicit `.pdb` mapping fixed startup.

Release solution/WASM build exited 0 with no warnings/errors. After the MIME fix, the multiplayer protocol/model/live-server suite rebuilt and passed 54/54, exit 0, none skipped. Single-player browser layout passed eight viewport/DPR combinations, exit 0. These are slice checks, not full gameplay, latency, capacity or manual acceptance. Dash, respawn, prediction/interpolation and PvPvE progression remain pending.

From repository root (PowerShell, .NET 10 with wasm-tools, Node and Edge installed):

```powershell
dotnet build ArcadeDodger.slnx -c Release
$bundle = (Resolve-Path src/ArcadeDodger/Web/bin/Release/net10.0/browser-wasm/AppBundle).Path
dotnet src/ArcadeDodger.Server/bin/Release/net10.0/ArcadeDodger.Server.dll --urls http://localhost:5088 --BrowserRoot $bundle
# Another terminal: open http://localhost:5088/?multiplayer=1; root / opens multiplayer.
npm install --prefix artifacts/browser-check playwright
node tests/browser-multiplayer.cjs
$env:BROWSER_URL = 'http://localhost:5088'; node tests/browser-layout.cjs
```

## Bullet playback and initial NPC slice

- Shots carry server birth ticks and per-tick velocity. Delayed playback hides shots until birth and reconstructs past positions continuously, rather than holding new shots ahead of the playback clock. Missing snapshots still freeze playback; this is not local shot prediction.
- Server-owned rammers chase/contact-damage players; gunners manage range and fire hostile shots with a visual warning. Both have 3 hull and can be killed. Spawn attempts every 3 seconds, clear of living players, cap 24 NPCs. These are player-relative encounters, not central risk/reward zones.
- Release solution/WASM build: exit 0, zero warnings/errors. Multiplayer: 63/63 passed, exit 0. Single-player: 37/37 passed, exit 0. PvP test now isolates ambient AI without weakening damage assertions; NPC damage has separate coverage.
- Headless Edge `tests/browser-enemies.cjs`: exit 0; both enemy types and hostile shot birth/velocity received, no page errors. Screenshot `artifacts/browser-check/multiplayer-enemies.png` shows a rammer damaging the player; gunner is outside the viewport.
- Headless Edge `tests/browser-multiplayer.cjs`: exit 0; independent sessions, movement, shots, damage, no page errors. With NPCs enabled, the browser damage assertion alone cannot identify the attacker; isolated PvP model coverage does.
- Fractional-tick shot playback regression passes. Subjective smoothness, gunner visual acceptance, Firefox, latency and endurance remain unverified. Respawn, XP/loot/upgrades and events remain pending.
