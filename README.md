# Arcade Dodger

A small Windows x64 arcade game using C#/.NET 10 and **Raylib-cs 8.1.0**, pinned in the [application project](src/ArcadeDodger/ArcadeDodger.csproj).

Explore a bounded neon arena, blast attacking aliens, collect weapons and survive for a high score in **NEON OVERDRIVE**. A following camera, north-up minimap, attack telegraphs and offscreen threat indicators support one-hit bullet-hell survival. World-space stars/grid, alien engines, player trails, gold projectile streaks, destruction sparks/rings and bounded screen shake create a procedural presentation. The best score lasts only for the current process. No external art, audio, or saved data.

## Build and run

Requires Windows x64, the .NET 10 SDK, NuGet access on first restore, and a working graphics driver supporting OpenGL 3.3.
Run these commands from the repository root:

```powershell
dotnet restore ArcadeDodger.slnx
dotnet build ArcadeDodger.slnx -c Release
dotnet run --project src/ArcadeDodger/ArcadeDodger.csproj -c Release
```

The window is **1920 × 1080** and targets 60 FPS. The **2880 × 1620** arena is three times the **960 × 540** logical viewport in each dimension; rendering is scaled 2×. The camera follows the player and clamps at arena edges; the HUD and minimap remain anchored to the screen. Pause freezes simulation, trails, particles, rings and combat timers. Decorative pulses and background animation continue; the barrel still follows the cursor.

| Key | Action |
| --- | --- |
| Enter | Start from the title screen / restart after game over |
| WASD or arrow keys | Move |
| Mouse movement | Aim at cursor |
| Hold Space or left mouse button | Fire toward cursor |
| P | Pause / resume |
| F | Toggle reduced particles, flashes and shake (bullets/warnings remain visible) |
| Escape or window close | Exit |

## Weapons and modular modifiers

Collect labeled gold weapon pickups to switch between **Blaster** (single shot), **Spread** (three-shot fan), and **Rapid** (faster single shots). Cyan modifier pickups stack independently up to three levels each and persist across weapon changes:

- **Multishot** adds two projectiles per level to each volley.
- **Pierce** lets each projectile hit one additional enemy per level; the same shot cannot repeatedly damage the same enemy.
- **Overdrive** increases projectile speed and firing rate.

Walk over pickups to collect them. A random pickup appears nearby every six simulated seconds; destroyed enemies have a 28% chance to drop one. At most 24 pickups can be present. Uncollected pickups expire after twelve simulated seconds. Restart resets the loadout to Blaster with no modifiers. Survival time remains the score; kills are tracked separately in the HUD. Player-shot damage resolves before enemy/player collision that frame. Projectiles and pickups use world bounds, not viewport bounds.

Base weapon cooldowns are 0.22 seconds for Blaster, 0.30 for Spread, and 0.09 for Rapid. Each Overdrive level adds 70 world units/second to the base projectile speed of 650 and divides the cooldown by `1 + 0.25 × level`. Firing produces at most one volley per update. Aiming at the player center falls back to firing upward.

## Gameplay and scoring

- The 28 × 28 player moves at 300 world units/second, stays within world bounds, and moves at the same speed diagonally as along an axis.
- Contact with a surviving alien or hostile bullet ends the run immediately. There is no player health system. Hostile collisions are swept, including relative player motion.
- Score counts simulated survival seconds, not kills. The session best updates on game over, survives restarts, and is not saved when the process exits.
- **Rammers** pursue the player (2 health). **Gunners**, introduced at 12 seconds, maintain distance and fire three locked-aim shots after a 0.9-second warning (3 health). **Spinners**, introduced at 28 seconds, orbit and fire two slow rotating fans with deliberate gaps (4 health).
- The director uses `max(0.7, 2.3 - 0.012 × score)` seconds between spawn attempts. Candidates must be 420–1100 units away; spawning stops when six enemies are within 700 units. Caps: 24 enemies, 240 hostile bullets and three simultaneous ranged telegraphs/bursts. Attacks cancel beyond 700 units. Hostile bullets expire after six seconds or crossing arena bounds.
- The north-up minimap shows arena boundaries, the viewport, player, pickups and enemies within 1000 units. Nearby offscreen enemies have edge indicators; an exclamation mark warns of a telegraphed attack. Pink/white hostile bullets differ from gold player shots and cyan modifier pickups.
- Restart clears enemies, bullets, pickups, effects, kills, score and modifiers and returns the player to arena center.

These rules are implemented in [GameModel.cs](src/ArcadeDodger/GameModel.cs), [ArenaModel.cs](src/ArcadeDodger/ArenaModel.cs) and [CombatModel.cs](src/ArcadeDodger/CombatModel.cs).

## Tests

```powershell
dotnet test tests/ArcadeDodger.Tests/ArcadeDodger.Tests.csproj -c Release
```

Tests exercise plain C# rules without raylib/display initialization: movement/world bounds, delta cap, deterministic spawning, director progression/safety/caps, telegraph timing, attack budget/expiry, camera transforms with shake, north-up radar mapping, swept/relative collisions, damage ordering, pause/reset, scoring and existing weapon/modifier/pickup behavior. These are not manual input or visual acceptance tests.

## Publish for Windows x64

```powershell
dotnet publish src/ArcadeDodger/ArcadeDodger.csproj -c Release -p:PublishProfile=Windows
.\artifacts\publish\win-x64\ArcadeDodger.exe
```

Copy the **entire** `artifacts/publish/win-x64` folder, not just the executable. The [Windows publish profile](src/ArcadeDodger/Properties/PublishProfiles/Windows.pubxml) creates a self-contained application, including .NET and the raylib native library. Trimming and single-file bundling are disabled. End users do not need the .NET SDK.

For a short automated native/window check (opens a window, draws 120 gameplay frames, then exits):

```powershell
.\artifacts\publish\win-x64\ArcadeDodger.exe --smoke-test
```

A successful smoke check prints `SMOKE_OK`. It auto-fires with fixture pickups and all three alien types, requiring firing, at least two kills and hostile firing. This checks native loading and rendering startup, **not** manual input, visual quality or gameplay fairness.

Manual acceptance checklist: title/start; WASD and arrows; cursor aim with Space/LMB while moving and at camera/arena edges; minimap player/viewport tracking; alien silhouettes/telegraphs; offscreen warnings; bullet contrast under destruction effects; F toggle without hiding warnings/bullets; pickup collection/modifier stacking; pause/resume; one-hit game over; restart/loadout/effect resets and best score; Escape/window-close. Review late-run density and spinner gaps for one-hit fairness.

## Verification recorded during implementation

Fresh expansion checks on Windows x64 (2026-10-10), separately from manual acceptance:

- Release solution build: exit code 0, no warnings or errors.
- Release model tests: 30 passed, 0 failed, 0 skipped; exit code 0.
- Self-contained win-x64 expansion published successfully; exit code 0.
- Published arena smoke: initialized a 1920 × 1080 Windows/OpenGL window, rendered 120 frames, fired 7 volleys, destroyed 2 enemies and fired 21 hostile shots; printed `SMOKE_OK` and closed cleanly; exit code 0.
- Manual keyboard/mouse interactions and visual/playability review remain unverified. Use the checklist above before distributing.

## Design

- [Program.cs](src/ArcadeDodger/Program.cs): Windows guard and startup; recognizes `--smoke-test`.
- [GameApp.cs](src/ArcadeDodger/GameApp.cs): raylib input, drawing, and loop; closes the window in `finally`.
- [GameModel.cs](src/ArcadeDodger/GameModel.cs): display-independent state, movement, score and integration; injectable director randomness. Legacy obstacles remain only for regressions/fixtures, not automatic encounters.
- [GameModelTests.cs](tests/ArcadeDodger.Tests/GameModelTests.cs) and [CombatTests.cs](tests/ArcadeDodger.Tests/CombatTests.cs): xUnit model regression tests, not renderer or input tests.
- [CombatModel.cs](src/ArcadeDodger/CombatModel.cs): display-independent weapons, modifiers, pickups and nearest-first swept projectile collisions; pickup randomness uses its own seeded generator.
- [CombatRendering.cs](src/ArcadeDodger/CombatRendering.cs): projectiles, pickups, aiming, combat effects and loadout HUD.
- [ArenaModel.cs](src/ArcadeDodger/ArenaModel.cs): alien AI/director, hostile bullets and swept player collisions.
- [ArenaView.cs](src/ArcadeDodger/ArenaView.cs) and [ArenaRendering.cs](src/ArcadeDodger/ArenaRendering.cs): camera/radar transforms, alien shapes, telegraphs, bullets and minimap.
- [ArenaTests.cs](tests/ArcadeDodger.Tests/ArenaTests.cs) and [DirectorTests.cs](tests/ArcadeDodger.Tests/DirectorTests.cs): arena and director regressions.

Movement uses delta time, capped at 0.05 seconds per frame to prevent jumps after stalls. Scores measure simulated survival time, so a long stall does not inflate them. Pause and menu states freeze simulation. Diagonal movement is normalized; difficulty is bounded.

## Browser multiplayer sector

Multiplayer features a seeded star sector with planets, stations, asteroid cover, patrols and contracts. Press **E** to dock or interact, **M** for the discovered sector map, **I** for modular weapons, and **Tab** for skills. Buy modules at stations; rearrange equipment anywhere. See [the inhabited sector guide](docs/inhabited-sector.md) for sector rules. The [v5 cooperative combat guide](docs/cooperative-combat.md) covers run resets, regional enemies, personal loot, map pings, and performance verification.
