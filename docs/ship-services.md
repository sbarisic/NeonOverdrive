# Ship handling and shared salvage services — protocol v10

## Controls

A/D (or Left/Right) turns the hull at 120 degrees per second. W/S (or Up/Down) changes throttle by 50 percentage points per second. Releasing the keys holds throttle; zero throttle coasts to a stop. There is no reverse thrust. Mouse aim remains independent of the hull. The header shows throttle and playthrough number (replays plus one).

Simulation and prediction use the same steering integrator, with substeps capped at 1/120 second. Existing acceleration, counter-steering, gear bonuses, passive braking, and swept collision sliding still apply. Menus, docking, hailing, and death stop the ship. Missing input and disconnects clear throttle and allow coasting. Checkpoints and snapshots include hull heading and throttle.

Ship systems use blue for Shield, orange for Engine, violet for Radar, green for Utility, rose for Hull, and yellow for Reactor. Labels and distinct icons remain visible. Piercing rounds emit a bounded directional contact effect without ending the projectile or repeating damage.

## Roaming contacts and caches

Three named ships start near Arrival Port with shuffled trader, explorer, and bounty roles. They pause for 60 seconds, follow station routes, and pause at each destination. Destroyed contacts return at Arrival Port after five minutes, subject to the shared NPC cap.

Press E within 180 units and clear physical line of sight. A chooser appears when several services are nearby. Hailing stops both ships and grants the pilot equipment cloak; the contact remains vulnerable outside station sanctuary. Other pilots can hail the same ship independently. Leaving the conversation, disconnecting, or losing the contact ends the hail.

Traders sell the existing module catalog at station prices. Exploration assignments require three designated unsearched caches and pay 40 credits plus 30 XP. Bounties require at least one actual damage contribution to each of three defeated roaming hostiles and pay 60 credits plus 45 XP. Turn completed work in at any contact or docked station. Claims impose a five-minute assignment cooldown. Station contracts and emergency rescues remain separate.

Each asteroid field contains three caches in clear space. Hold still and search for three uninterrupted seconds. A cache grants personal pickups worth 20 credits and 10 XP per regional level. Claims belong to a callsign for the life of the sector and survive deaths and rejoins. Other pilots can search the same cache. Rocks remain indestructible cover.

## Salvage outposts

Each of the six salvage wrecks has fixed, reachable construction sockets. Approach within 180 units to open management; recovery-contract interactions remain available through the chooser. Activating the core costs 100 credits. Two turret sockets cost 100 credits each; a repair drone bay costs 150. The core has 120 hull and other structures 60, multiplied by regional health scaling.

The repair bay restores 0.5 hull per second to each allied target within 350 units and clear line of sight, including pilots, NPCs, and other structures. It preserves fractional health, cannot heal itself, and does not restore shields or resurrect destroyed units. Its orbiting maintenance drone is cosmetic. Outposts provide no sanctuary, income, equipment, or station docking.

Any pilot may fund a socket. Construction reserves the socket and NPC capacity while its charge passes through ordered persistence. Failed charges create nothing. If the site, pilot, or range becomes invalid after a committed charge, the refund retries until durable success (or until that run has been replaced). Destroyed structures can be rebuilt at full price. Core destruction disables the entire site. Sites survive individual departures and reset with the sector.

## Factory raids

The Sundering's Raid Factory supports 16 mobile ships. Four minutes after the first activation, it may dispatch six existing offensive ships to the nearest active salvage site, breaking ties by site ID. Repair tenders and rescue attackers are excluded. Raids physically travel through the sector; the warning marks the threatened site without exposing hidden enemies.

Only one raid runs at a time. All designated attackers destroyed ends the raid; a fallen site sends survivors home. The next interval is seeded at three to five minutes. Factory destruction permanently prevents new raids, while ships already dispatched remain active. Ordinary hostile rewards apply; friendly structures never drop loot.

The cap remains 192 combined combat NPCs and 512 projectiles. Contacts and structures count toward the NPC cap; cosmetic drones do not.

## Deployment and verification

Protocol v10 changes input semantics, heading/throttle snapshots, packed presentation, and service state. Old clients must refresh. Restart the server and refresh browsers together; world generation changes take effect only in new sectors. Preserve the profile and friendship stores. Restarted sectors reset shared structures, caches, contacts, and assignments under the existing fresh-run rules.

`ServiceWorldTests` covers contact roles and movement, concurrent hails, death/replacement, personal cache claims across lives, assignments, construction failures/races/refunds, repairs, population limits, raids, piercing contacts, and generation across 50 seeds. `browser-services.cjs` checks real hailing, dialogue, cloak lifecycle, gear colors, and map contacts. Existing browser regressions cover desktop sizes, pixel densities, workshop drafts, recovery, prediction, combat, and cached-client refresh.

Acceptance run on CARPPC (.NET 10.0.12, Edge): 429 .NET tests passed; Debug and Release builds completed without warnings. The 192-NPC / 512-projectile four-weapon workload included three contacts and a core, two turrets, and repair bay. Server simulation p95 was 6.22 ms and end-to-end p95 13.04 ms (25 ms gate). Browser render p95 was 5.9 ms, receive p95 1.9 ms, and frame-interval p95 8.4 ms (20 ms gate), with no frames over 50 ms. Raw measurements are in `artifacts/combat-performance/`. The browser benchmark was run without concurrent regression workloads.
