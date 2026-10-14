# Modular Fleets, Territory Warfare, and a Living Sector

Status: v15 reliability repairs in progress; prior acceptance below is historical, not acceptance of this revision. Baseline patch and untracked files are saved under ignored `artifacts/living-sector-baseline/`.

## Accepted specification and tuning

Separate chassis (geometry, four hardpoints and base stats), faction (friendly, neutral, hostile and per-pilot enforcement), and persistent sector-lifetime NPC role/goals/state. Roles include trader, miner, scavenger, patrol, mercenary, courier, pirate and existing combat/support roles. States include traveling, working, fleeing, fighting, repairing and docking.

### Chassis

| Design | Size | Hull/shields | Armor | Speed | Turn degrees/s | Price / station |
|---|---|---|---|---|---|---|
| Starter Skiff | 28x14 | 3/2 | 0% | 220 | 120 | starter |
| Needle Scout | 32x16 | 3/2 | 0% | 260 | 150 | 150 / Arrival |
| Kestrel Fighter | 40x22 | 5/3 | 5% | 230 | 130 | 300 / Arrival |
| Mule Freighter | 52x30 | 8/4 | 10% | 170 | 80 | 400 / Engineering |
| Warden Corvette | 56x32 | 9/5 | 10% | 195 | 95 | 550 / Engineering |
| Aegis Frigate | 72x40 | 12/7 | 15% | 165 | 70 | 800 / Frontier |
| Bastion Cruiser | 88x48 | 16/10 | 20% | 140 | 55 | 1200 / Frontier |

Acceleration/counter-steering scale by chassis speed / 220; passive coasting remains unchanged. All designs retain four level-gated weapon bays and six system sockets. Ship purchases replace the run's chassis without a garage, trade-in, healing, shield refill or cooldown/status reset. Preserve fractional absolute health and clamp only when capacity falls. Fresh runs restore the starter; recovery preserves the current design. NPC regional/role scaling and special rescue durability remain.

Reusable vector rectangles, triangles, polygons, bridges, engines and armor panels define identifiable silhouettes, faction markings and damage effects for ships, stations and ground units. Active bays alone display swiveling weapons at authored hardpoints; firing originates there with immediate cover checks. Larger designs have larger shared collision geometry; broad-phase bounds never substitute for precise projectile contacts. No player geometry editing or independently destructible blocks.

### Combat and component levels

Kinetic speed 650; rocket 240; plasma and converted plasma 300; ordinary NPC rounds 300; telegraphed sniper 650 units/s. Preserve damage/cadence/range, beams, swept contacts, friendly pass-through and continuous split playback; lifetime follows distance. Respect the shared 512-projectile budget without firing starvation.

Warp Cruise Drive is a tiered Engine item: multiplier 2 + 0.5*tier, spool 2s, cooldown 10s, requires 5 damage-free seconds. Active cruise disables weapons, halves turn rate and doubles acceleration. Damage, collision, towing, docking and menus cancel it. Physical flight cannot bypass cover. Shift and a visible button expose spool/active/cooldown states.

Components have levels I-IV, separate from regional assembled-weapon tiers. Migrate existing installed and loose components to I without loss. Shops sell I; loot and finite convoy stock supply higher levels; no crafting/upgrading. Price = catalog price * level; resale 25% per component. Emitters gain 15% damage per level above I. Ordinary bonuses above neutral scale by 1 + 0.25*(level-1). Seeker steering scales by 1 + 0.5*(level-1), range +100/level; pierce/ricochet gain one contact/level. Split counts remain fixed; additional levels give 10% downstream range each. Slow/angle reduction retain safety limits; status durations and stun retriggers remain bounded. Converter/mine tiers improve their specific blast radius without double-scaling inherited damage. Dormant stages have no effect; preview/tooltips show actual resolved values.

Drop levels II-IV use 60% matching / 30% one lower / 10% two lower, clamped to I. Enemies above IV guarantee IV on a successful equipment roll. Preserve frozen personal contribution rewards.

### Society, trade and cargo

Right-click visible friendly/neutral ships: applicable Radio/Trade/Work/Tow actions. Sensor-range radio is nonmodal, moving and unprotected; close trade retains existing stop/cloak. Cargo/tow require proximity and LOS. Players cannot attack ordinary allies/neutrals; police can target a wanted pilot. Trader flight/reporting, miner distress, scavenger recovery, mercenary bounty pursuit and pirate cargo targeting use actual world goals. Bounded/rate-limited radio chatter sits above the bottom-right grid; ambient mute never hides mission warnings.

Up to three physical convoys within the NPC cap. Finite server-owned manifests are loaded once per trip, priced at 60% tier-adjusted value above resale. Reserve stock for concurrent durable purchases. Destroyed cargo cannot duplicate sold goods; salvage freezes once. Delivery consumes goods into abstract station trade. Mining/scavenging produce fixed-value cargo only: no crafting, production chains or resource economy.

One tow per pilot and owner per target; attach within 180/LOS, break beyond 350, blocked beam, damage, death, disconnect or Stop tow. Eligible disabled NPCs, wreckage and containers only. Tow speed 60%, no cruise, swept geometry for both bodies. Delivery consumes one stable cargo ID and grants durable frozen reward once.

Seeded derelicts near planets/fields offer Reactor/Cargo hold/Navigation data/Abandon choices with clues; per-pilot sector resolution survives deaths/rejoins and prevents rerolls.

### Missions, police and racing

Ordinary assignments remain separate from station contracts, rescues and races. Elite bounty: actual contribution to existing stronger enemy; interception: existing hostile courier before station arrival; smuggling: tagged cargo without scans; recovery: wreckage before competitor delivery; exploration: three narrowing sensor readings; protection: defend actual working miner/convoy. Reserve eligible existing targets, never spawn enemies on acceptance. Any friendly station/contact accepts valid turn-ins. Regional rewards: exploration/recovery 40 credits/30 XP; combat/interception/smuggling 60/45.

Police patrol friendly territory. Warn then scan tagged contraband for 3s within 350/LOS. Surrender confiscates and fails cargo mission without debt; completed scan offers confiscation plus fine min(25,wallet). Refusal/flight means pursuit; wanted clears after 60s outside detection. No permanent reputation or police kill rewards.

Sprint, obstacle and rally courses support solo and synchronized multiplayer countdowns. Own builds; records/ghosts keyed by seed, chassis, drive tier and resolved performance class. Score is elapsed time plus debounced 2s collision penalties. Damage-free entry required. Participants cannot deal/receive combat damage or attract NPCs, collect loot, trade, tow, capture or progress other missions. Corridor/progress/duration bounds terminate protection. Cruise usable on rally. No economic farming rewards. One bounded authoritative personal-best ghost per course/class; records survive fresh runs.

### Territory and events

Eight equal control sites: six salvage sites plus two outer stations; Arrival is permanent friendly sanctuary excluded from percentage. Initially outer stations and two nearest unbuilt salvage sites friendly, four salvage sites hostile (50/50). Ownership provides no free player structures. Defeat defenses and occupy within 350 uncontested for 30s; both factions physically capture. Contested/hostile stations deny new friendly docking. Occupied stations cannot change ownership until docked players leave or disconnect lifecycle expires; no forced ejection. Borders are nearest-site cells, clipped to sector; do not reveal enemies/terrain.

At friendly fraction p: raid size 4+round(8p), regional level+floor(4p) capped 8, interval 5-2p minutes after resolution. Friendly reinforcement strengthens below half. No automatic captures or mid-battle stat rewriting. Both factions/pilots in owned uncontested territory heal 1% max hull/s capped 0.5 after 10 damage-free seconds. No shield refill/status clearing/resurrection. Repair structures retain combat healing.

192 shared combat NPC / 512 projectile limits include police, convoys, structures, patrols and event competitors. Wait for capacity rather than removing active units. One major event, seeded 6-10 minute gap: invasion (2 minute corridor warning and physical pressure-sized advance); ancient signal (incomplete bearings, scavengers, frozen rare technology); resource rush (finite physical salvage cargo and competitors). Existing missions/rescues coexist. Factory destruction permanently stops local raids; independent corridor invasions remain possible.

## Implementation checklist

- [x] Chassis catalog, shared vector geometry, derived stats, factions
- [x] Component-level ownership, migration, simulation and preview
- [x] Hardpoints, slower combat, cruise, precise collisions/prediction
- [x] Run-bound station chassis purchasing and comparison UI
- [x] NPC roles, radio, chatter, finite-stock convoys
- [x] Cargo, towing, police and enforcement
- [x] Ownership, captures, pressure, patrols and repair
- [x] Invasions, ancient signals and resource rushes
- [x] Six mission families and derelict encounters
- [x] Three race categories, multiplayer starts, records and ghosts
- [x] Protocol v11, paged/revisioned state, cached-client refresh
- [x] Migration, regression, soak and performance gates

## Verification and release

Economic operations must be sequenced, server-owned, run-bound and durable before acknowledgement. Failed writes publish no partial money/items/world mutation; stale drafts stay reviewable. Deploy only after combined acceptance, coordinated server restart/browser refresh, new sectors only.

Verify chassis/hardpoints/collision/equipment preservation; all tier/dormancy boundaries and conversions; cruise/latency/input frequency; stock races/persistence/tow delivery; police/radio lifecycle; occupied capture/repair/pressure/fog; mission success/failure/disconnect/death/issuer loss; race scoring/checkpoints/immunity/ghost limits; desktop 1280x720 and DPR; full .NET/browser regressions and Debug/F5. Mixed 192-NPC/512-projectile benchmark requires server p95 <25ms/browser p95 <20ms plus sustained projectile occupancy test.

### Verification results — 2026-10-13

- Release and Debug browser/server builds: zero warnings/errors. Debug verified with `-p:BuildBrowserClient=true` and the actual `ArcadeDodger.Server` launch profile against isolated persistence. Setting `BuildingInsideVisualStudio=true` alone in a CLI build skips dependency builds and is not a substitute for this check.
- .NET non-performance suite: **463 passed**, including the ten-minute simulated-world soak. Report: `artifacts/living-sector-regressions-final.log`; TRX: `tests/ArcadeDodger.Multiplayer.Tests/TestResults/living-sector-regressions.trx`.
- All three combat benchmark configurations (256/96, production 512/192, experimental 1024/256) passed. Only the production configuration sets the shipping budgets. Reports: `artifacts/living-sector-performance-final.log` and `artifacts/combat-performance-v11/`.
- Desktop browser regressions cover multiplayer, inertia, menu navigation, asset versions, workshop layout, services, outposts, surface expansion, equipment inventory, replay counters, death/reset, friendships, cooperation, sector chart, radar, skills, weapons, viewport/DPI, presentation, prediction, objectives, combat effects, beams, audio, enemy rendering, and the living-sector interface. Results and rechecks are under `artifacts/living-sector-browser/`.
- Three simultaneous desktop clients completed a two-minute flight/fire/recovery soak without browser exceptions or snapshot budget violations: `artifacts/living-sector-browser/soak.log`.
- New radio UI checked at **1280×720, DPR 1 and 2**, including keyboard contact selection, no cloak during remote radio, shared race countdown, cancellation, and sticky Close controls. Screenshots: `artifacts/browser-check/living-sector-1x.png` and `living-sector-2x.png`.
- Fresh-sector service checks pass for authored dialogue, close-range trading/cloak, purchases, exits, and all six equipment colors. The service test requires a fresh sector because contacts leave Arrival after 60 seconds; it now identifies its own pilot rather than assuming the first snapshot player is local.
- Cached-client refresh checks pass through the Debug launch profile: `artifacts/living-sector-browser/debug-assets.log`.
- Final production benchmark, CARPPC / .NET 10.0.12, **192 NPCs / 512 projectiles / 24 peers**: server end-to-end p95 **18.743 ms**, simulation p95 **10.330 ms**, browser render p95 **6.800 ms**, browser receive p95 **2.000 ms**, frame interval p95 **8.400 ms**, no long browser frames. Each pilot emitted at least **136 primary shots** during the measured 600 ticks; longest quiet interval **17 ticks / 0.57 seconds**. Gates: server <25 ms, browser <20 ms. Latest logs: `artifacts/living-sector-performance-production.log`, `artifacts/living-sector-browser/performance-production.log`, `artifacts/combat-performance-v11/combat-512.json`, and `browser-512.json`.
- CSharpier, Prettier, and `git diff --check` pass.

### Local deployment

- No older server was listening on port 5088 at deployment. The rebuilt Release server now serves the rebuilt Release browser bundle at `http://localhost:5088/`; HTTP 200, `Cache-Control: no-store`, versioned module fingerprint `1D2B5AA81F81543E` verified. New rooms generate the v11 world.
- The server uses the existing `src/ArcadeDodger.Server/bin/Debug/net10.0/data/` profile/friend paths explicitly, preserving the local F5 data store. A pre-migration profile copy and SHA-256 record are in `artifacts/v11-deployment-backup/`. Server output: `artifacts/living-sector-deployed.log`.
- Debug/F5 behavior was separately exercised with isolated profile/friend files, including a real browser join, dialogue/trading, v11 private world data, DPR checks, and stale-client handling. Stop the currently running local server before starting another F5 instance on the same port.
- Reload an already-open browser tab to receive the coordinated client update. No running sector was edited in place. Source changes remain uncommitted.

## Post-implementation audit — 2026-10-13

Reviewed mission acceptance, target eligibility, actual simulation ordering, completion and failure, economic callbacks, radio/trading transitions, NPC destinations, event resolution, equipment-level projections, territory, racing and the accepted feature checklist. The initial implementation's tests exercised some mission helpers in an order different from the actual room tick; the audit adds real-tick coverage.

### Corrections

- Resolve defeated NPC contributions before evaluating protection outcomes. A final attacker kill now completes the assignment; simultaneous destruction of the protected ship still fails it.
- A combat target dying during a race, or otherwise disappearing, fails its assignment instead of leaving an unfinishable active job. Races still grant no mission progress.
- Recovery assignments select available, unreserved containers or disabled ships, excluding cargo already being delivered by an NPC or pilot.
- Completing the third anomaly reading consumes the world signal and retains its frozen technology on the ready assignment. It cannot be offered again with the same unique weapon identity. Final readings require clear line of sight.
- Contact assignment wallet and XP rewards commit atomically. Both assignment interfaces share the cooldown and offer abandonment. Inspected contraband cannot be discarded by claiming or abandoning the smuggling job.
- Police payment callbacks publish durable success or a retryable failure instead of leaving the interface at `pending`.
- Civilian flight overrides station pauses and records the actual refuge. Friendly contacts, police and convoys choose friendly destinations after ownership changes.
- Radio trade clears older pending flight input, and the client holds trade intent until acknowledgement. Returning to remote radio releases the trade cloak.
- Radio exposes the original trader's module shop, keyboard selection of nearby salvage, appropriate anomaly actions, mission instructions and turn-in guidance. Refreshes preserve button focus and panel scroll.
- Resource-rush beacons follow remaining event cargo. Resource rushes and ancient signals resolve when their goods/signal are consumed, starting the next-event cooldown immediately.
- Replace allocating LINQ skill-stat lookups with equivalent loops and compare snapshot deltas directly as UTF-8 bytes. No population, projectile, gameplay or benchmark thresholds were reduced.

### Audit verification

- **473 .NET regressions passed**, including ten additional audit cases, natural rescue emergence and the simulated-world soak. `artifacts/living-audit-tests.log`.
- Release and Debug/F5 browser/server builds succeed with zero warnings/errors. `artifacts/living-audit-build.log` and `living-audit-debug-build.log`.
- The initial production performance rerun failed at 27.29 ms and an isolated rerun at 26.90 ms. After the allocation fixes, the unchanged production benchmark passes at **20.477 ms end-to-end p95**, 11.372 ms simulation, 8.810 ms serialization for 24 peers; each pilot fires at least 136 primary shots, maximum quiet interval 17 ticks. `artifacts/living-audit-performance-optimized.log`, `artifacts/combat-performance-audit/combat-512.json`.
- **All 28 desktop browser suites passed**, including the actual Debug/F5 launch profile, cached-client refresh, radio trade/remote transitions at DPR 1/2, and three simultaneous clients flying/firing/reconnecting over two minutes. `artifacts/living-audit-browser-results.txt` and corresponding `living-audit-browser-*.log` files.
- Production browser playback: **8.800 ms render p95**, 2.500 ms receive p95, 12.400 ms frame-interval p95; one long frame in the recording. The render gate remains below 20 ms. `artifacts/living-audit-browser-performance.log`, `artifacts/combat-performance-audit/browser-512.json`.
- CSharpier, Prettier, whitespace checks, audio-event tests and weapon-chain-model tests pass.
- Restored the final Release server on `http://localhost:5088/` using the existing Debug profile/friend files; HTTP 200 verified. A second pre-deployment profile backup and hash are under `artifacts/v11-deployment-backup/`. Reload existing tabs for the rebuilt client. No active sector was modified in place.

### Regression fixes found during integration

- Stations block ship movement; generator and route tests now start at the exterior arrival approach. Every chassis can reach docking range without crossing the station body.
- New contacts spawn outside the station body, with all three within hailing range of the arrival approach.
- Relief traffic keeps moving while defending itself until a rescue forms. This prevents small skirmishes from pinning a relief ship indefinitely and starving the six-attacker emergency threshold.
- Protection assignments reserve actual attacking NPCs; interception ignores couriers already at their destination. Couriers depart again after their docking pause.
- Event signals publish an objective or approximate bearing without exploration cells or hidden NPCs. Radio can set navigable event waypoints.
- The radio panel uses the shared icon-button styling, keeps its Close action visible while scrolling, and hides its launcher when open.

### Bounded state and implementation choices

Private inventory retains revisioned page synchronization. Convoy manifests contain five entries per trip, sent only on revision changes; cargo is bounded to 64 sector objects and filtered by the viewer's sensors. These bounded stock/cargo sets fit one response; no unbounded inventory is embedded in snapshots. Radio retains at most 32 messages. Each profile retains at most 32 race-class records with at most 1,800 authoritative ghost samples per record.

The new systems live in focused `Room.*` partial modules; geometry, chassis, component levels, and territory cells are shared catalogs/helpers. No phone layout, ship-building editor, crafting economy, permanent reputation, or garage was added.


## Inertial ship flight — protocol v12

- [x] Replace desired-velocity steering with hull-directed thrust. Zero throttle preserves momentum. A/D rotation stops on release; mouse aim remains independent.
- [x] Starter thrust is 110 units/s², opposing thrust 165 units/s², scaled by chassis and existing skills/engine bonuses. Opposing thrust requires a direction against current velocity; perpendicular thrust preserves sideways motion.
- [x] Hold X or the footer Brake button to cut throttle and brake at 300 units/s² without reversing. Braking cancels cruise. Button pointer capture, keyboard activation, blur/cancel handling, and menu/death clearing prevent stuck input.
- [x] Keep speed caps along the resulting velocity; lost cruise/tow speed allowance bleeds off at 300 units/s². Preserve swept collision sliding and lifecycle full stops. Input timeout/disconnection releases thrust and braking; recovery preserves the coasting body.
- [x] Shared authoritative/predicted integration, brake intent, braking and cruise-multiplier snapshots, JSON/packed parsing, protocol v12, and cache refresh. Existing checkpoint velocity/heading/throttle fields remain valid; held brake intent is deliberately not persisted.
- [x] HUD speed/braking indication, independent travel-direction marker, control help, and opposing-thrust skill description. Race classes include flight model version 2, keeping old records/ghosts separate.
- [x] NPC navigation, weapon balance, equipment ownership, and economic state remain unchanged.

Verification on CARPPC / .NET 10.0.12:

- 499 .NET regression cases pass, including new drift/rotation, seven-chassis acceleration, perpendicular/opposing thrust, speed-cap direction, overspeed decay, exact braking distance, timeout/recovery, packet-frequency/frame-cadence, strict brake parsing, and cruise/prediction cases. `artifacts/inertial-dotnet-final.log`.
- All 28 desktop browser regression suites pass. Combat fixtures now explicitly brake and allow time to leave sanctuary under slower acceleration. Contact fixtures were rerun in a fresh sector because roaming traders had departed during the combined suite. Initial logs and successful reruns are retained as `artifacts/inertial-browser-*.log`; consolidated status is `artifacts/inertial-browser-verified.txt`.
- Brake pointer/keyboard behavior and footer containment pass at 1280×720, DPR 1 and 2. Shared prediction settles to authority with 300 ms inbound delay. Three clients complete the two-minute flight/fire/recovery soak without browser exceptions.
- Debug/F5 launch and Release server/WASM builds pass with zero warnings/errors; verification uses isolated profiles under `artifacts/inertial-verification`.
- Production 192-NPC / 512-projectile / 24-peer benchmark passes unchanged gates: server end-to-end p95 **18.4434 ms** (<25), browser render p95 **6.7 ms** (<20), receive p95 **2.1 ms**, frame interval p95 **8.4 ms**, zero long frames. Minimum 136 primary shots per pilot; maximum quiet interval 17 ticks. Reports: `artifacts/combat-performance-inertial/`.
- Temporary local server stopped after verification. No production profiles were used.

## Waypoint responsiveness and canvas typography

- [x] Replace synchronous browser waypoint searches with one incremental job advanced once per rendered frame (256 work items / 2 ms soft budget). Chart/minimap readers return cached results immediately; long routes show a plotting indicator.
- [x] Cache grid clearance and swept edges per ship radius, use an eight-direction heuristic, and remove redundant route points only after swept visibility checks. Reuse valid routes during flight; cache unreachable results; cancel obsolete distant destinations and avoid starving searches for moving contacts.
- [x] Use the pilot's actual chassis clearance for waypoints. Validate route endpoints against sector boundaries and preserve obstacle-safe approaches.
- [x] Bundle SIL-OFL Rajdhani SemiBold for all Raylib HUD/world labels. Rasterize once at 48 px, generate mipmaps, and use trilinear filtering. Shared measurement matches rendering; compensate for TTF cap height. License and source are included under `src/ArcadeDodger/Web/Fonts`.

Verification on CARPPC / .NET 10.0.12:

- All 506 .NET regressions pass (`artifacts/waypoint-full-tests-final.log`). Seven focused navigation tests cover incremental budgets, collision-safe routes, radius isolation, boundaries, path reuse, unreachable caching, cancellation, and moving targets.
- Desktop browser navigation test visits all planets, stations, and hostile arrays at 1280x720, DPR 1/2. Route API calls max 2.1 ms; render p95 13.6/12.3 ms during planning. Long cold routes can take several seconds to finish while gameplay continues. `artifacts/navigation-browser-final.log` and `artifacts/navigation-font-dpr*.png`.
- Eight existing browser suites pass: chart, radar, viewport, presentation, inertia, surface expansion, objective effects, and cached asset versions. `artifacts/navigation-browser-*.log`.
- 192-NPC / 512-projectile / 24-peer benchmark retains the gates: server end-to-end p95 23.5802 ms (<25), browser render p95 11.9 ms (<20). `artifacts/combat-performance-navigation/`.
- Debug server/WASM build and browser formatting checks pass. Verification uses isolated profiles; temporary local server stopped afterward.

## Launch screen

- [x] Replace the bare startup message with a navy/gold/cyan flight-console panel, authored vector ship, orbital animation, launch stages, and an inertial-flight hint. Inline critical styles and SVG render before the runtime downloads; reduced-motion preferences disable animation.
- [x] Use the .NET runtime's resource-progress callback for counted resource loading, then indeterminate initialization stages. Do not invent an overall completion percentage. Runtime import, initialization, and frame failures show an error with a focused Retry launch button.
- [x] Browser checks pass for initial paint, actual progress callbacks, stage changes, successful join, DPR 1/2, reduced motion, runtime download failure, keyboard retry, and warm reload. Existing asset-version, viewport, and presentation suites also pass. Evidence: `artifacts/loading-browser.log`, `artifacts/loading-browser-*.log`, and `artifacts/loading-screen-*.png`.
- [x] Debug browser bundle builds without warnings/errors; web formatting and diff checks pass. Temporary test server uses isolated profiles on port 5098; the user's existing port-5088 server is left untouched.


## Ship control, selection, contact, and UI reliability (protocol v13)

- [x] Hold right mouse to steer by the shortest angle at the effective turn rate; manual A/D overrides it. Shared server/prediction substeps preserve momentum, throttle, independent aim, braking and cruise behavior. Cursor intent is included in prediction history and is cleared with held controls.
- [x] Separate Throttle, Speed and Inertia header groups. Vector Thrusters retain their additive acceleration bonuses, add 10% turning per tier, and display relative handling as `1 / (1 + 0.10 * tier)`. Race performance classes use flight model 3.
- [x] Uniform white/cyan/blue engine exhaust, anchored behind each engine and animated with thrust. NPC exhaust follows observed movement; reduced effects omit the outer glow/trail.
- [x] Typed client-local ship selection from the world, minimap and four-row scrolling contact list. Stable sector-local numbers, faction symbols, hover brackets, selected details and an overlapping-target chooser use only detectable contacts. Short clicks select; holding left mouse for 180 ms fires, while Space fires immediately.
- [x] Selected-target radio replaces right-click radio. Modal screens hide radio, chatter, objectives and target details. Escape dismisses the active menu, then radio, then selection. Snapshot updates do not reopen dismissed radio.
- [x] Spatially indexed swept convex ship contact across factions. Bounded separation distributes correction by chassis area and removes closing normal velocity while preserving sliding. Docked/menu ships, hailed contacts and structures remain fixed. Ramming damage uses actual hull contact and existing cooldowns. Spawns, undocking and larger replacement chassis check occupied space; prediction uses visible hull geometry.
- [x] Explicit dormant activation levels and separate component tiers. Preview stages show conversions, downstream projectile color/type/speed/range and effects. Range exhaustion prevents unreachable later split stages from inflating the preview. Mine/converter radius and laser-to-plasma units are resolved consistently.
- [x] Reconcile service and radio controls by stable keys instead of replacing pressed/focused elements. Local management/navigation stays available during pending transactions. Server eligibility explains range, line of sight, captured-site requirements, docking and unavailable targets. Transaction controls track acknowledgements and clear on rejection/disconnect.
- [x] JSON/packed snapshots, welcome/version checks, derived inertia, contact names/capabilities and eligibility updated together to v13. Existing loading-screen work and economic/profile state are preserved.

Verification on CARPPC / .NET 10.0.12:

- 529 .NET regression tests pass (`artifacts/control-tests.log`), including new steering/cadence/tier, precise swept contact, unequal mass, fixed/protected bodies, disconnected exposure, spawn clearance, ram cooldown and prediction checks.
- 96 server-generated module/tier fixtures match browser preview damage, range, speed, radius, branch count and projectile kind. Fixtures are emitted by `ExportAuthoritativePreviewFixtures` through `NEON_PREVIEW_FIXTURES`; the same path is consumed by `tests/weapon-chain-model.cjs`.
- Desktop checks exercise 1280x720 at DPR 1/2, click-versus-hold firing, world/minimap/list selection, radio Escape/dismissal, modal layering and a real pointer-down/snapshot/pointer-up sequence retaining the same button node.
- The 192-NPC / 512-projectile / 24-peer benchmark passes: server end-to-end p95 **15.896 ms**, browser rendering p95 **8.8 ms**, receive p95 **5.0 ms**. Each pilot admits at least 150 primary shots; the longest quiet interval is 17 ticks. The saturation fixture now suppresses territorial repair of its artificial 10,000 hull and counts admitted fire instead of only rounds surviving the birth tick. Reports: `artifacts/combat-performance-ship-controls/`.
- All 32 desktop browser suites pass, including three clients in a two-minute flight/fire/recovery soak. Consolidated evidence: `artifacts/control-browser-summary.json`; detailed logs: `artifacts/control-*-browser.log`.
- Debug and Release server/WASM builds pass with zero warnings/errors; C# formatting, Prettier and diff checks pass. All verification profiles and server binaries live under `artifacts/`; production profiles are untouched. The temporary port-5098 test server was stopped; no user server was stopped.


## Reliable controls, NPC flight, gravity and CRT (protocol v14)

Implementation tracker for the accepted combined update. Existing uncommitted v13 and loading-screen work is preserved.

- [x] Independent chorded mouse button transitions, missing-release reconciliation, fresh-press recovery, and keyboard coexistence.
- [x] Persistent NPC velocity/heading/throttle; shared integration, acceleration-based route following, stops, stuns and contact velocity feedback.
- [x] Rocky-planet gravity shared with prediction: 400-unit reach, `45 * (1 - altitude / 400)^2` acceleration, safe sliding. Gas giants/projectiles/loot unchanged.
- [x] Gravity-aware transit routes, exterior survey points, and tiny braking-drift tolerance for scans.
- [x] Context action strip, authoritative eligibility, stable keyed controls, selected-target priority and construction confirmation navigation.
- [x] Semantic multicolor controls and persisted Off/Subtle/Enhanced CRT settings, world shader with clean fallback and unfiltered HUD text.
- [x] Late objective overlay pass, gravity indicator, chassis-panel breakup, material/weapon impact visuals and event-driven spatial impact audio.
- [x] Protocol v14 JSON/packed fields, cached-client handling and race movement class version 4.
- [x] Full regression, browser visual/layout review, Debug launch build and mixed-population performance acceptance.

Presentation budgets: 192 debris fragments / 24 per destruction; reduced 64 / 8. Existing 24 transient audio voices, 192 combat NPCs and 512 projectiles remain unchanged. NPC engines initially use 2x chassis acceleration/opposing thrust and 1.5x turn rate, retaining role speed caps. Economic state requires no migration.

Implementation details:

- NPC routes use cached incremental searches with a room-wide 2,048-work-item budget per tick and 128-item slices. Rocky gravity zones are preferred avoidance areas; a physically valid fallback can cross a narrow gravity corridor. Ordinary combat ships retain one route destination instead of cancelling searches through a second navigation call.
- Context buttons include docking, hailing, trader services, scans, cargo towing, mission readings/rewards and outpost construction management. Prices lead to the existing confirmation flow. Held pointer targets retain identity and position across reordered snapshots; stale-action errors appear above the strip. Full labels remain in tooltips at compact widths.
- Service completion uses a bounded green pulse; contacts distinguish shield ripples, kinetic sparks, plasma splashes and rocket shockwaves. Muzzle flashes use authoritative hardpoint birth positions. Rock events occur only after reaching cover, avoiding duplicate/premature sounds when a ship intercepts the shot.

Verification on CARPPC / .NET 10.0.12:

- **547 .NET cases pass**, including gravity cadence at 30/60/144 Hz, safe sliding, braking and escape, NPC acceleration/stun coasting, completed station routes across seeds, fractional shield/hull impact events, piercing followed by cover, destruction metadata and existing persistence/recovery/economy regressions. `artifacts/v14-acceptance-dotnet.log`.
- **32 desktop browser regression suites plus the recorded combat benchmark pass**, including the multiplayer soak, fresh-sector contact/quest/equipment tests, real button chords, manual rotation/throttle/brake alongside mouse input, Space surviving mouse release, pointer cancellation/outside release, modal/radio behavior and cached-client refresh. `artifacts/v14-browser-verified.json` links each result. Earlier combined-suite failures included roaming contacts leaving spawn, a CRLF-sensitive fixture and old direct-velocity test autopilots; fresh-sector runs and inertial-aware controllers retain the original assertions.
- CRT defaults, all three settings and persistence, semantic colors, stable contextual clicks during reordered snapshots and footer containment pass at 1280x720, DPR 1/2. Contextual Hail opens the live authoritative contact/trade flow. Scan-bar screenshots deliberately overlap an asteroid and remain readable above it. Chassis breakup and material-contact fixtures render without browser errors. `artifacts/v14-final-ui-*.log`, `artifacts/v14-objective-final.log`, `artifacts/v14-combat-final.log`.
- Event-driven impact audio passes spatial/material/deduplication/mute checks; weapon-model fixtures remain green. `artifacts/v14-audio-events.log`, `artifacts/v14-weapon-model.log`.
- Production **192-NPC / 512-projectile / 24-peer** benchmark passes unchanged gates: server end-to-end p95 **22.2379 ms**, simulation p95 **11.5601 ms**; browser render p95 **10.4 ms**, receive p95 **8.2 ms**, frame interval p95 **16.7 ms**. Each pilot admits at least **137** primary shots, longest quiet interval **19 ticks**. One browser frame exceeded 50 ms; p95 gates passed. `artifacts/v14-performance/`. Initial runs overlapping other verification work exceeded a gate; isolated final runs and reports are retained separately.
- Debug/F5 browser/server build and Release WASM/server builds pass with zero warnings/errors. C# formatting, Prettier and diff checks pass. No economic migration is required. Protocol v14 and movement model 4 require coordinated server restart/client refresh; the temporary verification server was restarted with isolated artifact profiles and stopped after acceptance. No user server was stopped.


## Gameplay reliability and planet escape (protocol v15)

Accepted repair scope: preserve existing work and economic state; repair planet escape,
convoy lifecycle/navigation/placement, patrols, shield recharge, gravity weapons,
module damage/drop tiers, protection/smuggling/turn-in, race staging and preparation,
contact visibility/maps, command acknowledgements/eligibility, and shared UI presentation.

### Tuning and compatibility
- Gravity remains 45 units/s² with quadratic falloff over 400 units.
- Active thrust gains 2 × local gravity magnitude along the hull; movement model 5.
- Escape target: all stock chassis within 10 seconds; towing within 15 seconds.
- Civilian hull = ceil(60 × chassis hull / 8 × regional health / level-I health).
- Civilian shield = ceil(30 × chassis shields / 4 × regional health / level-I health).
- Race staging: 20 units, <5 units/s, 10° alignment, 60-second deadline, 10-second countdown.
- No equipment/profile migration; no free healing. New population tuning applies on sector restart.

### Implementation / acceptance checklist
- [x] Shared assisted thrust, surface separation, NPC acceleration agreement; all-chassis escape tests.
- [x] Stock transitions/reservations; full-room seed-44 stall and multi-seed completed journeys.
- [x] Safe distributed spawns, scaled durability, territory patrols, common shield maintenance.
- [x] Fixed-body immunity to gravity weapons, collision-safe pulls, shield-aware module effects and tiered drops.
- [x] Protection tracking and outcomes; smuggling reroutes; capability-based mission turn-in.
- [x] Physical race staging, incremental course preparation, stable versioned records.
- [x] Consistent detectability, names/factions/numbers, true map positions, station ownership labels.
- [x] Correlated bounded command results, acknowledgements, reusable action eligibility, stable controls.
- [x] Keyboard button activation, semantic colors, shared CRT panels, overlay ordering and debris fade.
- [x] Full .NET/browser regressions, Debug build, recovery/economy verification, multi-density desktop review.
- [x] Mixed 192-NPC / 512-projectile / 24-peer benchmark and sustained navigation/combat soak.

The repaired flows pass the acceptance checks recorded below. Existing local servers remain untouched;
only temporary verification servers are started/stopped. Deployment requires a coordinated
v15 server restart and browser refresh.

### Repair ledger

| Finding | Repair and regression coverage |
|---|---|
| Heavy ships could struggle against rocky-planet gravity. | Shared thrust adds twice the local gravity magnitude before throttle scaling. All seven chassis are checked with no upgrade, all Vector Thruster tiers, and towing. Zero-throttle gravity, braking and tangential contact retain their existing rules. |
| Convoy stock could refresh repeatedly at a refuge. | Loading records its station and waits for outstanding reservations. Arrival at the same station preserves IDs and sold stock. A different completed station journey prepares the next manifest. |
| Convoys stalled near station approaches, including seed 44. | Ships use exterior approaches, a five-second progress watchdog, rotating routing-budget priority, and collision-checked local escape approaches. Full-room seed 44 and 123 simulations run for ten simulated minutes. |
| Additional civilian ships crowded Arrival and ignored regional durability. | Traders start at different friendly stations; workers start near reachable work areas. Spawn searches reject occupied or obstructed berths. Hull and shields scale once from chassis and region; named contacts and relief ships keep their special tuning. |
| Patrol decisions differed from combat acquisition; returning ships missed shield recharge. | Patrol interruption shares the combat range, line-of-sight and sanctuary evaluator. Police/patrol routes visit friendly control sites. Shield maintenance runs before movement handlers can return. |
| Gravity weapons moved fixed structures; shield hits did not consistently feed module effects. | Shared stationary-body exclusion, swept pulls, bounded persistent pull velocity and ship-contact resolution. Absorbed shield damage now counts for Leech and Tesla; contribution remains limited to actual damage. Installation component drops use the existing level-weighted roll. |
| Protection attackers could abandon their assignment and progress was unclear. | Designated attackers retain their protected-ship target. The private objective publishes remaining attackers, health, participation and outcome reason. Markers use detectable positions or explicitly labelled last-known observations. Destruction resolves before success. |
| Smuggling destinations could become impossible; arbitrary allied units accepted turn-in. | Unavailable destinations reroute to the nearest eligible friendly station, preserving cargo and frozen reward. No destination leaves an explained waiting state. Turn-in requires an eligible station or a mission-capable contact. |
| Race starts depended on arbitrary joining positions and blocked on route generation. | Incremental cached course preparation, collision-checked physical staging, low-speed/heading readiness, timeouts and synchronized countdown. Normal-input integration tests physically complete Sprint, Obstacle and Rally. Record classes use movement model 5 and stable equipment values. |
| Contact identities and visibility differed between maps and the world. | Identified snapshots use sensor range instead of the transport interest margin. Shared stable contact numbers, faction colors, trader symbols and names are used across selection and maps. Ownership changes update station presentation. Overlap choosers include stations as well as ships. |
| A newer receipt could erase a pending transaction's result. | Bounded run/sequence-correlated result journal, explicit acknowledgement cursor, retained pending outcomes and queue backpressure. Delayed completions receive a new cursor. Clients clear only matching commands, and reset pending UI on run changes/disconnects. |
| Service buttons disagreed with server rules or became unresponsive. | Shared eligibility covers trade, construction, docking, repairs, search, towing, mission acceptance/claim and race entry. Keyed controls retain pointer/focus identity; local navigation stays usable while purchases save. |
| Menu styling, objective labels and debris fading were inconsistent. | Shared visual surface styling is separate from panel layout. Semantic action colors, native focused-button Space activation, late opaque/clamped objective labels, and consistent material alpha are retained. Browser regressions caught and corrected full-screen panel layout inheritance. |

### Current verification evidence

- Stock chassis escape and towing tests pass, including all four Vector Thruster tiers.
- Assisted motion agrees across 30/60/144 Hz within the regression tolerances.
- Seed 44 and 123 full-room convoy journeys pass, with no stationary traveling convoy exceeding the 45-second regression limit.
- All three race categories complete physical staging and an entire course through ordinary input commands; gate/record unit tests remain separate.
- 96 server-generated module/tier fixtures match the browser weapon preview (`artifacts/v15/preview-fixtures.json`, `weapon-preview.log`).
- **638 .NET regression cases pass in both Debug and Release** (`artifacts/v15/acceptance-dotnet.log`, `acceptance-release-dotnet.log`; final Release run: 58 seconds), including persistence failures, legacy loading, recovery, receipts/backpressure, all-chassis escape, mission outcomes and physical race completion.
- **33 desktop browser suites pass**. `artifacts/v15/browser-verified.json` links the passing logs; earlier failed runs remain available. Coverage includes 1280×720 and larger viewports, DPR 1/2, cached-client refresh, simultaneous controls, radio/menu layering, contact selection, trading, equipment, recovery, correlated command outcomes, and a three-client two-minute flight/fire/reconnect soak.
- Browser race checks physically fly from Arrival around nearby contacts into their staging slots at DPR 1/2, then verify countdown immobilization and cancellation. Separate .NET integration tests physically complete all three courses. Older straight-line browser controllers were corrected for omitted zero-valued snapshot fields and occupied departure paths; no player position is teleported by the browser fixture.
- Visual review corrected long weapon-effect text overflowing its checkpoint buttons. The preview details now scroll independently; a layout assertion checks every checkpoint's text height. Updated screenshots show readable previews, inventory, and footer controls at 1280×720.
- **Production Release performance passes** on CARPPC / .NET 10.0.12: 192 combat NPCs, 512 projectiles, 24 peers; server end-to-end p95 **20.4271 ms**, simulation p95 **11.9537 ms**, 24-peer serialization p95 **7.3403 ms**. Every pilot admits at least **136** primary shots; the longest quiet interval is **19 ticks**. The unchanged gates are 25 ms server and 20 ms browser.
- Recorded viewer-packet playback in Edge passes: render p95 **9.2 ms**, receive p95 **6.7 ms**, frame interval p95 **12.6 ms**; one frame exceeded 50 ms. Evidence: `artifacts/v15/performance-release/` and its server/browser logs. Measurements ran without concurrent regression workloads.
- The first benchmark accidentally used **Debug** and exceeded the production timing gate (**41.9737 ms** end-to-end p95). That evidence remains in `artifacts/v15/performance/`. Profiling confirmed unoptimized projectile simulation was the main cost. The documented Release benchmark was then run with enforcement enabled; no workload, entity cap, firing rate, or timing threshold was reduced. Debug/F5 is validated for operation, not production throughput.
- Server and browser Debug/F5 and Release builds pass with zero warnings/errors. C# formatting, Prettier, and diff checks pass. Profile and friendship files used by verification are isolated under `artifacts/v15/`; existing economic state and uncommitted work are preserved.
- Deployment requires a coordinated **v15 server restart and client refresh**. The isolated port-5105 verification server is stopped. No existing user server was stopped or restarted; no production profiles were changed. Acceptance is complete for the repaired flows and Release performance gates above.
