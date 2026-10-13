# Inhabited sector — protocol v10

Desktop browser multiplayer runs in a seeded **24,000 × 16,000** sector. New rooms use the expanded generator. Active rooms retain their geometry until restart or expiration; no live terrain migration occurs.

## Navigation and cover

Haven, Cinder, and The Sundering have solid **1,160-unit** radii. Zephyr remains a non-solid, 760-unit gas giant. Ships slide around rocky rims without gravity or landing. Solid planets and asteroids block projectiles, beams, targeting, and damaging effects. Destroyed outposts remain solid cover.

Three stations, four hostile arrays, three friendly surface outposts, two boundary arrival corridors, eight asteroid fields, six wrecks, three survey probes, and a volcanic thermal zone occupy the sector. Rocky arrays and outposts sit on the rim; Zephyr's array stays in orbit. Interactions sit outside the solid discs. Shared obstacle-aware routes connect station approaches and objectives; every route segment is checked with swept collision. NPCs cache routes and follow the same geometry as player prediction.

- **E** docks within 180 units of a station, undocks, or starts a nearby contract interaction. Scans take three seconds and stop on movement, damage, firing, or opening equipment/map screens. The objective has a visible scan bar.
- **M** opens the sector chart. Planets and stations remain visible. Exploration reveals a 1,200-unit radius; hostile markers retain fog-of-war rules. Select a destination or radio beacon for a routed waypoint. The local minimap has zoom controls and the same route.
- **I** opens the workshop. Modules can be bought only while docked. Bay and attachment availability depends on level; see [weapon progression](multiplayer-skills.md).
- **Tab** opens the 66-skill tree. Equipment screens and the chart retain cloak; Escape only immobilizes the ship.
- Station sanctuaries extend 300 units, suppress firing and damage, absorb incoming shots, and turn hostile pursuit away. Docked pilots cannot move, fire, or collect pickups. The room never pauses.

The thermal vent warns for two seconds, erupts for four seconds at one damage per second, then rests for four seconds. Gas storms are cosmetic. Regional levels and health/damage scaling remain unchanged.

## Surface combat

Every rocky friendly and hostile outpost begins with two stationary turrets and two rovers. Rovers use regional gunner values and move around the circular surface; turrets have twice the gunner hull. Friendly ground units also have shields equal to their maximum hull. They face outward and fire only with clear sight. Player weapons pass through friendly units; hostile ships can attack them.

Outposts have 120 hull and remain destroyed for the sector. Turrets never regenerate. Surviving outposts can replace rovers within their two-unit quota. Surface loot ejects into clear exterior space. All ships, installations, turrets, and rovers share the **192-NPC** budget. The projectile cap stays **512**; beam and mine limits are unchanged.

Hostile arrays support eight patrol ships and launch replacements every 20 seconds with a three-second warning. Corridors deliver three ships each minute, up to nine per corridor, after a five-second warning. Saturation delays launches. Existing patrols survive base destruction. NPC targets distinguish pilots from other combat NPCs.

## Emergency rescues

Relief cruisers travel from station approaches through patrol shipping lanes. They have 240 hull and 120 shields times regional health scaling, speed 100, and a regional gunner weapon. Shields begin recharging after eight damage-free seconds.

A cruiser engaged by six to eight existing hostile ships broadcasts a room-wide radio beacon. No acceptance action spawns enemies. Only one rescue can be active. Its objective panel shows hull, shields, and remaining designated attackers; station contracts remain independent. Clicking its map or minimap beacon tracks the moving cruiser without revealing terrain or hidden enemies.

Destroy every designated attacker while the cruiser survives. Each pilot/run dealing at least one actual damage, including statuses, receives personal pickups worth **80 × regional level credits** and **60 × level XP**. There is no rescue module roll. Pickup modifiers, expiry, collection restrictions, and ordered persistence follow normal personal loot. Outcomes are frozen once; death invalidates the old run's entitlement. If both the cruiser and final attacker die in one tick, the rescue fails. The beacon disappears immediately on failure. Resolved events impose a seeded four-to-six-minute cooldown; rescued cruisers continue toward their destination.

## Contracts and lifetime

Stations retain survey (40 credits/30 XP), recovery (60/45), suppression (100/75), and repeatable Patrol Interdiction (60/45). One station contract can be active per pilot. Rewards are claimed at the issuer. Interdiction credits actual damage participation against three designated-corridor ships and has a five-minute claim cooldown. Recovery data is lost on death. Destroyed suppression targets cannot regenerate.

World state lasts until restart or 15 minutes without connected pilots. Same-room discovery and contract progress follow existing death rules. Fresh-run resets, permanent replay bonuses, and the strict 60-second reconnect window are documented in [cooperative combat](cooperative-combat.md).

Protocol v8 welcome includes geometry, NPC definitions, and authoritative weapon requirements. JSON and packed presentation snapshots include NPC faction, health/shields, surface placement, and rescue state. Older clients receive `refresh_required`. Restart the server and refresh clients together; Visual Studio's server launch profile builds the browser bundle.

## Verification

Run `dotnet test tests/ArcadeDodger.Multiplayer.Tests -c Release`. Coverage includes many-seed generation, swept paths, planet cover, surface factions, weapon gates and profile migration, natural relief traffic, rescue outcomes, run invalidation, persistence, and protocol handling.

Use an isolated server/profile store for the desktop browser regressions. `browser-surface-expansion.cjs` checks WASM rendering, locked sockets, radio selection, terrain privacy, and failed-beacon removal. Existing menu, workshop, skills, sector, radar, prediction, combat, audio, lifecycle, and cache-version checks remain relevant. Performance commands and machine-specific measurements are in [cooperative combat](cooperative-combat.md).
