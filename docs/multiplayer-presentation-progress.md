# Browser multiplayer presentation checkpoint

## Implemented; presentation acceptance remains open

The snapshot renderer now includes offscreen threat rings/directional strokes and telegraph `!` markers, including gunners. Markers are inset around the multiplayer telemetry and loadout HUD. Enemy silhouette flashes follow observed authoritative hull decreases, expire after 0.12 seconds, are capacity-bounded, and are suppressed/cleared under reduced effects. Warning cues remain visible with reduced effects.

The radar XP dots/key now use cyan, matching world energy pickups. The accessible DOM live-room menu owns its panel/actions; the canvas dims the live scene rather than drawing a competing menu. Transport controls are positioned above the canvas loadout strip, accounting for letterboxing.

Relevant sources: `src/ArcadeDodger/Web/BrowserArenaWorld.cs`, `BrowserArenaHud.cs`, and `Web/network.js`.

## Observed checks (Release, local Edge)

- `dotnet build ArcadeDodger.slnx -c Release`: exit 0, zero warnings/errors; native, server, tests and WASM AppBundle built.
- `dotnet test ArcadeDodger.slnx -c Release`: exit 0; 114 passed (37 single-player/model and 77 multiplayer), none failed/skipped.
- `node tests/browser-multiplayer.cjs`: exit 0; independent sessions in one room, remote movement/shots and server-confirmed shield damage. This is not latency/endurance or death/respawn acceptance.
- `BROWSER_URL=http://localhost:5088 node tests/browser-layout.cjs` (set environment using the shell's syntax): exit 0; canvas buffer/viewport and overflow checks at four sizes, DPR 1 and 1.5.
- Expanded `node tests/browser-presentation.cjs`: exit 0; join/menu/transport containment at 1760x974, 1280x720, 800x900 and 640x480, DPR 1/1.5; transport/loadout clearance; primary mouse fire; neutral movement/fire/dash during menu while server ticks advance; effects UI state round-trip; Resume, real transport disconnect/rejoin and intentional Leave.
- Inspected `artifacts/browser-check/presentation/menu-640x480-dpr1.png`: menu and transport controls contained, loadout unobscured by transport controls. Screenshots are generated artifacts, not committed golden references.

Read-only agent review found no definite defect in the warning/flash/color changes. Static review and the above checks do not prove pixel parity or renderer effect timing.

## Still needed before full presentation acceptance

- Focused full/reduced-effects rendered evidence for offscreen threats, gunner telegraphs and hull-decrease flashes; repeated-tick expiration and no flash revival after toggling effects.
- Pickup/loadout variant and death/respawn visual acceptance; combat readability and comparison against native reference scenes.
- Native interactive regression, broader browser coverage, aim/smoothness under latency/jitter and capacity/endurance checks.
- Enemy disappearance is still inferred as death for cosmetic feedback; review before claiming exact combat-event parity.

Later movement/progression/scoring/recap/bounty/salvage milestones remain separate work. This checkpoint is not full MVP completion or user visual acceptance. No deployment was performed; local verification servers were explicitly stopped (exit 1 from intentional termination, not a test failure).
