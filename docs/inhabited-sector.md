# Inhabited sector (protocol v4 foundation)

**Current release:** [protocol v5](cooperative-combat.md) changes death/reconnect persistence, enemy populations, loot, cooperation, transport, and limits. Those rules supersede the corresponding v4 descriptions below.

Browser multiplayer now runs in a seeded 12,000 × 8,000 sector. The browser multiplayer sector is the only game mode.

## Playing

- **E** docks within 180 units of a friendly station, undocks, or starts a nearby contract interaction. Scans take three seconds and stop on movement, damage, firing, or opening equipment/map screens.
- **M** opens the sector chart. Exploration reveals a 1,200-unit radius. Select a discovered destination to set a direction/distance waypoint; contract boards can also locate assigned objectives.
- **I** edits the existing four weapon chains anywhere. The Shop tab is a read-only catalog until docked. Stations offer module purchases, hull repairs at 10 credits per missing point (including partial damage), and contracts.
- **Tab** retains the 69-skill tree. Existing equipment cloak and neutral gameplay input remain active in equipment screens and the sector chart.
- Station sanctuaries extend 300 units, suppress firing and damage, absorb incoming shots, and turn hostile pursuit away. Docked pilots cannot move, fire, or collect pickups. The shared world never pauses.

The sector has four themed planets below the flight plane, three stations, four hostile arrays, two boundary arrival corridors, eight asteroid fields, six wrecks, three survey probes and a volcanic thermal zone. Planets are scenery beneath combat; orbital asteroids and arrays block ship movement. Asteroids stop or reflect weapons and block splash/secondary effects. Destroyed arrays leave solid ruins.

The thermal vent warns for two seconds, erupts for four seconds at one damage per second, then rests for four seconds. Gas giant storms are cosmetic. All interactable destinations have asteroid-free connecting lanes. Shattered-world defenders have five hull instead of three.

## Enemies and contracts

There is no player-relative spawn timer. Each enemy snapshot records its source and AI state. Arrays start with two rammers and two gunners, support eight ships, and launch replacements every 20 seconds with a three-second warning. Corridors announce arrivals for five seconds and deliver three ships every minute, up to nine living ships each. Population saturation delays launches. Ships patrol, investigate attacks, engage visible exposed pilots, and return to their routes. Gunner acquisition includes an attack telegraph.

Arrays have 120 hull. Damage and damaging statuses work; gravity, slow and stun do not. A destroyed array never launches again during that sector. Existing patrols survive their base. Corridors continue providing encounters after arrays are cleared.

Each station offers one survey, recovery and suppression contract. A pilot can have one active contract. Survey three probes for 40 credits/30 XP; retrieve and return wreck data for 60/45; contribute damage to and destroy an array for 100/75. Rewards are fixed and claimed at the issuer. Progress and completed contracts are personal. A target destroyed without the pilot contributing clears that unavailable suppression contract. Recovery data is lost on death. Paid claims persist credits before consuming progress; retries cannot duplicate rewards.

## Lifetime and implementation

World state lasts until server restart or 15 minutes without connected pilots. Discovery and contracts are kept by normalized callsign within that room, including after death and rejoin. Respawn uses the last visited station and resets run progression; saved equipment, credits and friendships keep their prior persistence. This release adds no disk world save.

Protocol **v4** welcome carries static sector geometry and contract definitions. Player snapshots add docking state. Private sector snapshots carry discoveries, chart cells, contract progress, repair cost and sequenced action receipts. Dynamic source snapshots include hull, destroyed/launching state and warning time. Sector commands are `dock`, `undock`, `repair`, `accept`, `claim`, `interact`, and `cancel`, with a shared sequence and integer target. Old clients receive `refresh_required`.

The server owns all interactions, damage and payments. Client prediction shares swept obstacle movement with the server. Static geometry and dynamic combat use spatial grids; snapshots filter distant non-player entities to 3,000 units, while preserving a pilot's own weapon presentation. Limits remain 256 projectiles, 1,536 beam segments (64 per pilot), 128 mines (32 per pilot), and 96 enemies including installations.

## Verification

Run `dotnet test ArcadeDodger.slnx -c Release`. Sector coverage includes deterministic generation across 30 seeds, clear routes, swept collision/prediction, sourced patrols, permanent destruction, sanctuaries, all four emitters against cover, control immunity, contracts, interrupted scans, persistence failures, replay rejection, discovery, and respawn.

With an isolated multiplayer server, set `ARENA_URL` and run `node tests/browser-sector.cjs`. It checks real WASM rendering, station-only shopping, docking, contract acceptance, map selection, snapshot-stable focus, mobile layout, exploration, cloak, and rejoin after the disconnected-ship grace period. Existing weapon, skill, friendship, prediction, viewport, presentation, enemy, PvP, audio and asset-version regressions also cover v4.

The bounded-load model test spreads 24 pilots across the sector, each firing four extreme chains after source populations fill. On this development machine the isolated 180-tick sample averaged about 6 ms per simulation tick, with p95 about 9 ms. This excludes WebSocket serialization and network latency and is not a production capacity guarantee. Long-session balance and multiplayer playtesting remain useful follow-up work.

Restart the server and refresh browsers together. Visual Studio's existing server launch profile builds the browser bundle; no profile migration is required.
