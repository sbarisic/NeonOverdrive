# Neon Overdrive

A WebAssembly multiplayer space combat game built with C#/.NET 10 and Raylib-cs. The browser renders the game; the ASP.NET Core server owns simulation, progression, combat, and persistence. There is no native or single-player game. Supported clients are desktop browsers with keyboard and mouse, at a viewport of 1280 × 720 or larger. Phone and portrait layouts are not supported; desktop resizing and high-DPI displays remain supported.

## Run locally

Install the .NET 10 SDK and the WebAssembly tools workload, then build and launch:

```powershell
dotnet workload install wasm-tools
dotnet build ArcadeDodger.slnx -c Release
$bundle = (Resolve-Path src/ArcadeDodger/Web/bin/Release/net10.0/browser-wasm/AppBundle).Path
dotnet run --project src/ArcadeDodger.Server -c Release --no-build -- --BrowserRoot $bundle
```

Open http://localhost:5088/ and choose a callsign. Share the room invitation link with other pilots. Old links containing `?multiplayer=1` still work. A server on the same origin is required; static hosting alone cannot run the game.

For Visual Studio, select **ArcadeDodger.Server** as the startup project. F5 builds the browser dependency and starts the server and browser together. There is no separate browser-only launch profile.

## Play

- A/D or Left/Right: rotate the hull. Q/E: raise/lower the saved throttle setpoint. Hold W or Up for immediate 100% thrust; release to return to the saved setpoint. Q/E do not change the setpoint while W is held. Hold S, Down, or the Brake button to cut the setpoint to zero and brake (takes priority over W). Turning preserves drift; zero throttle coasts indefinitely.
- Mouse: aim; left click or Space: fire.
- Right click a friendly ship or cargo: radio, trade, work, or tow. Remote radio does not stop combat.
- Shift: engage/cancel an equipped Warp Cruise Drive. Cruise needs five damage-free seconds and disables weapons.
- Tab: skills. I: modular weapon workshop. P: friends.
- M: sector map and pings. Use the visible nearby-action buttons to hail, dock, search, or manage salvage sites; E is reserved for throttle adjustment.
- Escape: menu. R: reduced visual effects.

### Flight assists

Near rocky planets, a cyan curve forecasts releasing W at the saved throttle setpoint, with fixed heading. Red marks a predicted collision. A green coast curve appears only after a complete safe, approximately closed circuit has been simulated with zero thrust. If the setpoint is nonzero, the green coast alternative is dashed and labelled to set throttle to zero; it does not imply that releasing W alone stops thrust. Forecasts are bounded to 120 simulated seconds and static terrain, and do not steer the ship. Short-range game gravity is not an inverse-square orbital model: a circular-speed estimate alone is not proof of a sustainable orbit.

Aim near a visible hostile NPC for a brief moment to acquire its lead marker. It remains attached while you aim ahead; dwell near another enemy to switch. The tracked hull is highlighted, and death or sensor loss clears tracking independently of ship selection. The first enabled weapon bay is the labelled reference. The prediction uses current target velocity, actual muzzle position, weapon speed/range and active accelerator/converter checkpoints; it fades on abrupt velocity changes and hides when out of range or blocked by terrain. It does not invent exact trajectories after splitting, homing, ricochet or mine stages. Lasers show a direct aim marker rather than a lead offset.

Explore a 24,000 × 16,000 sector with solid rocky planets, surface outposts, and orbital stations. Fly around planet rims, fight hostile patrols, and follow radio beacons to cooperative relief-ship rescues. Hail three roaming contacts for module trading and assignments. Search asteroid caches for personal loot. Fund shared salvage defenses and repair drones, then repel raids from The Sundering's factory. Buy modules at stations or roaming traders and edit complete weapon builds anywhere. Bays open at levels 1/8/16/24; each subsequent level activates a mod socket. Installed mods stay with unequipped weapons. Enemy equipment drops include ready-made weapons and six types of ship attachments; inventory is scrollable with no carrying cap. Fresh runs start at level 1 with four kinetic emitters and no purchased modules. Death permanently adds 5 credits to the next starting wallet (100 + 5 per replay). Leave starts fresh without adding a replay; an unexpected disconnect can resume the current run for less than 60 seconds. Friendships remain saved by callsign.

Protocol v16 adds temporary full-throttle input to the gravity-assisted flight and reliable-command systems. Flight retains inertia, held braking, and independent aiming; active engines receive extra thrust near rocky planets. Race records are separated by flight model (currently 6). The fleet systems include seven station-sold chassis, level-I–IV components, finite convoy stock, civilian roles, police, towing, and derelict encounters. Ship purchases retain equipment and current damage. Control eight sites to expand friendly territory; larger holdings attract stronger factory raids. Invasions, ancient signals, and resource rushes operate alongside contracts and rescues. Radio assignments cover bounties, interception, smuggling, recovery, exploration, and protection. Sprint, obstacle, and rally races keep class-specific personal records and ghosts without economic rewards. See [the combined release tracker](PLAN.md) for tuning and verification evidence.

## Projects and verification

- `src/ArcadeDodger.Multiplayer`: authoritative simulation, protocol, catalogs, persistence, and shared prediction.
- `src/ArcadeDodger.Server`: WebSocket server and browser asset hosting.
- `src/ArcadeDodger/Web`: WebAssembly renderer and browser interface.
- `tests/ArcadeDodger.Multiplayer.Tests`: simulation, persistence, protocol, server, and performance tests.

```powershell
dotnet test tests/ArcadeDodger.Multiplayer.Tests -c Release
npm install --prefix artifacts/browser-check playwright
$env:ARENA_URL = 'http://localhost:5088'
node tests/browser-multiplayer.cjs
node tests/browser-viewport.cjs
node tests/browser-inertia.cjs
node tests/browser-living-sector.cjs
```

Browser regressions use Playwright with Microsoft Edge. Use an isolated test server and profile store for gameplay tests. `BrowserRoot`, `ProfilesFile`, and `FriendsFile` configure deployment paths; protect the persistent profile and friendship files when deploying or restarting.

See [the current release and verification tracker](PLAN.md), [ship handling and salvage services](docs/ship-services.md), [sector rules](docs/inhabited-sector.md), [skills and workshop](docs/multiplayer-skills.md), and [combat rules and earlier performance measurements](docs/cooperative-combat.md). Earlier implementation journals retain historical test results.

## Code organization

The simulation stays in one authoritative `Room`, with partial files grouped by responsibility:

| Area | Files |
| --- | --- |
| Simulation loop and run state | `Room.cs`, `Room.PlayerSimulation.cs`, `Room.Sessions.cs`, `Room.Entities.cs`, `Room.Runs.cs` |
| Combat | `Room.Modules.cs`, `Room.ModuleStages.cs`, `Room.ProjectileContacts.cs`, `Room.Payloads.cs`, `Room.Beams.cs` |
| World and station services | `Room.Sector.cs`, `Room.EncounterSources.cs`, `Room.Stations.cs`, `Room.Contracts.cs`, `Room.Pings.cs` |
| Fleets and cargo | `ChassisCatalog.cs`, `ModuleLevels.cs`, `Room.WorldTrade.cs`, `Room.CivilianRoles.cs`, `Room.Cargo.cs`, `Room.Police.cs` |
| Territory and activities | `Room.Territory.cs`, `Room.WorldEvents.cs`, `Room.WorldMissions.cs`, `Room.Races.cs` |
| Transport data | `Snapshots.cs`, `SectorMessages.cs`, `Room.Snapshots.cs`, `Room.SectorSnapshots.cs` |
| Server | `Program.cs` configures hosting; `WebSocketSession.cs` owns socket sessions; `ArenaService.cs` schedules rooms |
| Renderer | `BrowserArenaWorld.cs` orders draw passes; actors, weapons, history, effects, and radar have separate files |
| Browser interface | `network.js` coordinates transport/input; `network-shell.js` builds controls; weapon drag handling and previews have separate modules |

Panel styles live in `*.styles.js` modules beside their components. They retain the existing insertion order and participate in the application's cache fingerprint. All browser JavaScript modules are deployed automatically by the browser project.

### Formatting

Restore the pinned development tools once:

```powershell
dotnet tool restore
npm ci
```

Format source and tests with `dotnet csharpier format src tests` and `npm run format:web`.
To check formatting without modifying files, run `dotnet csharpier check src tests` and `npm run check:web`.
Generated build output and browser recordings stay under ignored directories.
