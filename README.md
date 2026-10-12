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

- WASD or arrows: thrust; release to coast to a stop.
- Mouse: aim; left click or Space: fire.
- Tab: skills. I: modular weapon workshop. P: friends.
- M: sector map and pings. E: dock, undock, or interact.
- Escape: menu. R: reduced visual effects.

Explore planets, stations, wrecks, and hostile installations. Enemies launch from bases and marked arrival corridors. Buy modules at stations and edit up to four weapon chains anywhere. Death starts a new run with starter equipment; reconnecting resumes the current run. Friendships remain saved by callsign.

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
```

Browser regressions use Playwright with Microsoft Edge. Use an isolated test server and profile store for gameplay tests. `BrowserRoot`, `ProfilesFile`, and `FriendsFile` configure deployment paths; protect the persistent profile and friendship files when deploying or restarting.

See [sector rules](docs/inhabited-sector.md), [skills and workshop](docs/multiplayer-skills.md), and [current combat and performance verification](docs/cooperative-combat.md). Earlier implementation journals retain historical test results.
