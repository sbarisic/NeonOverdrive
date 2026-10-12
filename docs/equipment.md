# Equipment and inventory — protocol v9

Weapons are individual objects with a permanent ID, an emitter, and eight editable mod positions. A weapon keeps its complete build when moved into inventory or another bay. Loose modules stack separately. Equipped components cannot be duplicated or sold individually without removing them first.

Weapon bays open at levels 1/8/16/24. Each bay activates `clamp(level − bay unlock level, 0, 8)` mods, in checkpoint order. All eight positions can be prepared beforehand. Locked bays retain their starter weapon and accept mod edits, but cannot change weapons or fire. The first bay starts enabled; other bays do not enable themselves when unlocked.

The Arsenal has a compact weapon socket and mod chain, a ship panel, an active/full-build preview, and a filtered inventory grid. Double-click a stored weapon to inspect and edit it. Click an item and a compatible socket, or drag between them. Right-click/Delete unequips; Apply saves the complete draft. Inventory has no carrying cap and mounts only visible rows. A draft supports edits to 32 weapon objects per transaction; save before editing further objects.

## Ship systems

One item fits each typed slot. All tiers are usable at pilot level 1. Tier I/II/III/IV corresponds to Haven/Cinder/Zephyr/The Sundering. There are no random affixes or power/fuel resources.

| Slot | First variant, per tier | Second variant, per tier |
| --- | --- | --- |
| Shield | Capacitor: +1 maximum shield | Quick-charge: −0.5 seconds recharge delay, minimum 1 second |
| Engine | Cruise: +5 percentage points maximum speed | Vector: +10 percentage points acceleration and steering |
| Radar | Survey: +250 exploration radius | Combat: +200 detection radius, without terrain discovery |
| Utility | Collector: +20 pickup radius | Salvage: +10 percentage points collected-credit bonus |
| Hull | Reinforced: +1 maximum hull | Reactive: 5% less incoming hull damage |
| Reactor | Weapon: +5 percentage points weapon/status strength | Shield: +10 percentage points regeneration rate |

Bonuses add to corresponding skills. Reactive armor multiplies existing reductions and does not protect shields. Equipping or swapping items never heals, refills shields, resets cooldowns, or clears statuses. Reduced maximum capacity clamps current health, including fractions. Engine gear does not change coasting deceleration.

## Drops and sales

Each living contributor/run with at least one actual damage point independently rolls for equipment: 15% per ordinary hostile, 35% per hostile installation. Status damage counts. Drops are personal, expire under existing personal-loot rules, and spawn in reachable space. Half are complete weapons; half are ship systems. Weapon families, ship categories, and their variants are equally weighted within those pools. Existing credit, XP, and module rewards remain. Friendly kills and rescue completion itself add no item roll.

Regional weapon tiers populate the first 2/4/6/8 entries of these chains:

- Kinetic: Accelerator, Piercing Core, Seeker Head, Amplifier, Ricochet Plate, Range Extender, Leech Circuit, Blast Chamber.
- Laser: Twin Splitter, Stabilizer, Range Extender, Amplifier, Tri-Prism, Incendiary Coat, Chain Relay, Leech Circuit.
- Rocket: Seeker Head, Accelerator, Blast Chamber, Amplifier, Range Extender, Ricochet Plate, Salvage Beacon, Proximity Fuse.
- Plasma: Seeker Head, Cryogenic Lens, Stabilizer, Twin Splitter, Corrosive Payload, Chain Relay, Range Extender, Amplifier.

Shops retain module purchases; buying an emitter creates a blank weapon. New systems and assembled builds are loot-only. Dock to sell unequipped items with a clean draft. Modules return 25% of catalog price, rounded down per component; complete weapons return the sum of their components and require confirmation. Ship systems sell for 25 × tier credits. Sales have no reward multipliers.

## Persistence and transport

Profiles store stable item IDs, loose stock, four bay references, six system references, and a revision. Legacy rows become weapon objects; spare emitters become blank weapons; installed components are subtracted from total ownership to form loose stock. Dormant mods stay installed. Migration is atomic and repeatable and preserves economic state and progression.

Protocol v9 replaces wire-level row loadouts with sequenced equipment intents. Commands identify changed weapon objects and slot assignments; they never accept item grants or client-generated stats. Private inventory pages contain at most 48 objects. Combat snapshots retain bounded four-bay projections and authoritative movement/radar/weapon values. Old clients receive the existing refresh-required response.

Equipment writes use the ordered persistence queue and current-run guards. Failed saves publish no partial inventory or wallet changes. Drop IDs freeze generated items across retries. Stale revisions are rejected; the client retains conflicting drafts for review and merges non-conflicting loot arrivals. Recovery resumes the same objects; fresh runs clear acquired equipment and recreate starter weapons. A prepared death reset retains its new weapon IDs when admitted.
