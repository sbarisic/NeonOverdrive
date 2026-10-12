Current multiplayer protocol: **v5**. See [cooperative combat](cooperative-combat.md) for run resets and reconnect persistence. Skill effects and unlock rules below remain in force.


# Multiplayer skill tree and modular arsenal

## Pilot Ascension (Tab)

Press **Tab** or click **Skills** for the protected in-game skill tree. It contains **69 illustrated square tiles** in eight branches, with names, Roman-numeral tiers, level badges and prerequisite links. Branch buttons jump to a section; touch/scroll explores the complete tree. Arrow keys and Tab navigate, Enter/Space/click learns, and Escape returns to the arena. Locked nodes remain focusable. Snapshots preserve keyboard focus and scroll position.

Earn **two points per level after level 1**. Every node costs one point and is purchased once. Skills and points reset with the run; there is no respec. Players can select **two emitter masteries** per run. The server validates level, prerequisites, ownership, points and the specialization limit before returning a private sequenced receipt. The menu preserves equipment cloak, hiding the ship from aliens and blocking damage, movement, fire and pickups.

| Branch | Skills and maximum effects |
| --- | --- |
| Defense | Shield Capacity I–VI each add/restore one shield (8 maximum). Hull Plating I–III each add/restore one hull (6 maximum). Quick Recharge I–II reduce shield delay to 4/3 seconds. Flux Recovery I–II regenerate 1.5/2 shields per second. Energy Siphon restores one shield per XP pickup. Living Metal I–II heal one hull every 10/5 seconds after 10 damage-free seconds. |
| Strength | Strength I–V grant +15/30/45/60/75% weapon damage, including damaging statuses. Dynamics I–II add 15/30% projectile speed, or laser range. Range Engineering I–II add 15/30% initial range. |
| Fire Rate | I–V add 10/20/30/40/50% firing frequency, or continuous laser DPS. Projectile cooldowns retain fractional timing. |
| Weapon Bays | II/III/IV require both a purchased skill and level 5/10/15. Each requires the preceding bay. Weapon 1 is free. |
| Masteries | Four tiers per emitter, guarded at levels 2/5/10/15. Learn roots in at most two emitter families. See below. |
| Mobility | Thruster Tuning I–II adds 25/50% acceleration. Vector Control I–II adds 25/50% counter-steering acceleration. Slipstream I–II adds 10/20% speed; Cruise Engines adds another 10 percentage points. Impact Dampeners halves enemy ramming damage. |
| Fortune | Fortune I–III grant +25/50/100% credits. Luck I–III have a 10/20/30% chance to double payouts. Lucky Find requires Luck III and level 8, raising the chance to 35%. |
| Collection | Collector Field I–II add 30/60 units of pickup radius. Astral Harvest I–II add 25/50% XP. Field Repairs restores 0.25 hull per XP pickup. |

Mastery tiers, in order:

- **Kinetic:** +15% direct damage; +25% speed; +1 hostile penetration; +25% additional direct damage.
- **Laser:** +20% range; +15% DPS; +25% shield damage; +25% additional DPS.
- **Rocket:** +20% explosion radius; +25% speed; +15% direct/splash damage; +25% additional splash damage.
- **Plasma:** +20% speed; +20% explosion radius; +15% direct/splash damage; +25% additional direct/splash damage.

Bonuses add within each mastery and multiply with global Strength and attachments. Converted descendants keep their originating weapon's bonuses. Strength scales statuses once; Fire Rate does not accelerate status pulses. Laser converters/mines retain their emission rates and carry the increased incoming DPS in each payload. Skill-adjusted range establishes checkpoint distances.

Ordinary independent tier series require levels 2/4/6/8/10/12. A dependent first tier requires at least two levels beyond its prerequisite; further tiers increase the guard by two. Shield II leads to Quick Recharge, then Flux Recovery. Shield III leads to Energy Siphon. Hull II leads to Living Metal. Strength I leads to Dynamics, and Strength II to Range Engineering. Phase Dash leads to Long Phase and Blink Cycle; Long Phase leads to Storm Nova. Slipstream II leads to Comet Drive. Collector I leads to Harvest; Collector II leads to Salvage Cycle.

Fortune/Luck apply to the collecting pilot after Salvage Beacon has determined the drop value: `floor(drop × Fortune multiplier × jackpot multiplier)`. The server uses a private deterministic roll per pickup/pilot, including failed-save retries. Only a successful wallet save consumes the pickup and produces a gold payout notification. XP is calculated and rounded independently.

`SkillCatalog.cs` is the authoritative catalog; `Room.CalculateWeaponStats` supplies combat and workshop statistics. Clients receive skill IDs as arrays rather than an integer mask. The HUD and prediction consume authoritative movement statistics. Only the WebAssembly multiplayer client is supported.

## Credit workshop (I)

Press **I** or click **Weapons** for the in-game workshop. **Shop** sells four emitter types and 24 attachments. **Loadout** shows one weapon chain at a time. Select Weapon 1–4 above the chain; each selector shows whether it is enabled, disabled or locked. Each weapon has an emitter and eight fixed distance checkpoints: **5%, 10%, 15%, 20%, 25%, 30%, 35%, 40%**. The server supplies the catalog, prices and descriptions. Its source of truth is `ModuleCatalog.cs`.

Each purchased copy can occupy one socket across all four rows. Disabled and level-locked rows also reserve copies. Drag from owned inventory, swap between weapon rows, or drag back to inventory to unequip. Click/Enter/Space selects and places; right-click or Delete clears. Left/right arrows navigate sockets; up/down changes weapon rows. All rows remain editable while locked. Scroll vertically through rows and horizontally on narrow screens. The preview shows the selected chain and cumulative damage/branch counts.

**Apply loadout** saves all four rows together. Reset restores the applied configuration. Closing discards unapplied edits; completed purchases remain owned. Dragging and keyboard focus survive snapshots. Shop and Loadout both keep the existing equipment cloak and suppress movement, firing and collection.

A new callsign receives 100 credits and four kinetic emitters. Weapon 1 starts enabled; the other rows start disabled. Laser emitters cost 150, rockets and plasma 180 each. Emitter 1 is starter equipment, not an unlimited free purchase. There are no power budgets, heat penalties, selling, trading or random item rolls.

Weapon 1 is free. Additional slots require the **Weapon Bay II/III/IV skills** and levels **5/10/15**. Death resets level and skills, relocking higher slots while retaining their saved chains. Credits, purchased copies and loadouts persist by normalized player name across rooms, reconnects and server restarts. Only one session per normalized name can be connected at once. Holding fire operates every enabled, unlocked weapon with its own cooldown.

Gold credit pickups are separate from cyan XP: rammers drop 5 credits and gunners 12. Salvage Beacon increases the drop by 25%, rounded up. Pickups last 30 seconds, with 128 credit drops per room; contested collection uses swept contact order, then player ID. Player deaths give no credits. Collection is blocked while editing. Failed persistence leaves the pickup available and rejects purchases/equipment changes without exposing an uncommitted success.

### Attachment catalog

| Attachment | Credits | Main effect |
| --- | ---: | --- |
| Amplifier | 120 | Double downstream damage; visibly thicker beam/projectile |
| Accelerator | 60 | +25% projectile speed or laser range |
| Range Extender | 80 | +30% range; checkpoint positions stay fixed |
| Piercing Core | 110 | Two additional hostile penetrations |
| Stabilizer | 50 | Halve later split angles |
| Overcharger | 140 | Double damage against shields |
| Twin Splitter | 100 | Two branches sharing damage |
| Tri-Prism | 160 | Three branches sharing damage |
| Scatter Bloom | 180 | Six branches, half remaining range |
| Seeker Head | 150 | Seek nearby aliens; laser bends at checkpoint |
| Ricochet Plate | 100 | Two wall reflections |
| Chain Relay | 190 | Jump to another hostile at half damage |
| Incendiary Coat | 80 | Burn for 3 seconds |
| Cryogenic Lens | 80 | Slow aliens for 2.5 seconds |
| Radiation Jacket | 140 | Irradiate nearby aliens for 4 seconds |
| Corrosive Payload | 120 | +25% alien vulnerability for 4 seconds |
| Tesla Coil | 150 | Damage charge triggers brief stuns |
| Gravity Core | 170 | Pull nearby aliens toward projectile/contact |
| Blast Chamber | 110 | Add splash; continuous beams pulse blasts |
| Proximity Fuse | 90 | Trigger contact payload before direct collision |
| Plasma Converter | 180 | Convert projectile; laser emits plasma volleys |
| Mine Layer | 160 | Terminate chain in an arming mine |
| Leech Circuit | 200 | Restore shield from actual hostile damage, including attached damage-over-time effects |
| Salvage Beacon | 100 | Mark aliens for bonus credits; does not stack |

Checkpoints use cumulative path distance and the initial skill-adjusted range. Range upgrades never move them. Empty boxes pass through; every crossed checkpoint executes in order, including multiple crossings during one tick. Branches inherit distance, effects and the remaining program, sharing damage equally. Collision on an approaching segment takes precedence over its next checkpoint.

Continuous lasers retain 4 base DPS. Amplifier at 5%, then Twin Splitter at 15%, gives 4 DPS upstream, 8 DPS after amplification, and 4 DPS per child. Damage accumulates fractionally for all weapons. Heavy Caliber, Rapid Blaster, Overclock, scatter and range skills continue to apply. Eight amplifiers are allowed; only visual thickness is clamped for readability. Mines terminate the remaining chain; plasma conversions carry it forward. Secondary payloads do not recursively trigger relays, blasts or conversions. Friends remain immune to every weapon and secondary effect.

Per-room limits remain 256 projectiles and 1,536 beam segments. Each pilot shares 64 beam segments across active laser weapons. Processing order rotates, and every active laser gets a segment quota. Mines are limited to 32 per pilot and 128 per room, arm after 0.5 seconds and expire after 10 seconds. Spawn saturation skips/delays new output without consuming purchased modules or credits. Split descendants remain silent and start at their actual fork; each firing laser weapon has a sustained hum with bounded overall volume.

### Persistence and deployment

Protocol **v3** sends named skill purchases, learned-skill arrays, private skill receipts and payout notifications. Welcome includes both authoritative catalogs. Player snapshots include unlocked weapon slots and effective statistics for all emitters. Four-weapon equipment and private workshop snapshots retain their wallet/inventory/loadout semantics. Old clients receive `refresh_required` and must reload. Browser and server must be updated together.

Profiles default to `data/profiles.json` beside the server executable. Set `--ProfilesFile` to an absolute path outside build/deployment output for a durable production store. Existing `FriendsFile` data is independent and unchanged. Profile writes replace the complete JSON file atomically before success is reported. Old in-memory equipment has no saved inventory to migrate; each existing name receives the starter profile on first use. This retains the existing name-based identity rather than adding password accounts.

The following verification sections describe earlier iterations; the current workshop verification appears at the end.

## Friends (P)

Press **P** or click **Friends** to open Pilot Alliances. Send a request to an online pilot or type a player name. Only the recipient can **Accept** an incoming request; sending a reciprocal request does not accept it. Requests can be declined or cancelled, and either friend can remove the relationship. Accepted friends receive an ally marker, and their bullets, rockets, plasma, splash and beams pass harmlessly through each other. The P menu grants the same equipment cloak as Tab and I.

Names are matched case-insensitively with surrounding spaces removed. Friendships and pending requests persist across reconnects, rooms and server restarts in `data/friends.json` beside the server executable. Set the server's `FriendsFile` configuration to an absolute path to keep the store outside a deployment folder. This uses the existing name-based login identity; it does not add password accounts. The last successfully joined name is remembered in this browser's local storage and prefilled next time.

Each snapshot contains only that pilot's social list and the receipt for their latest action. A failed disk save rejects the change. Each name supports up to 32 accepted or pending relationships.

## Earlier verification

- Release solution tests: 204 passed, zero failures/skips, exit 0. Includes expanded effects, purchase validation/lifecycle, protocol and prediction. The initial run exposed the old six-choice boundary assertion; rejection then started at ID 24.
- Release browser/WASM build: exit 0, zero warnings/errors.
- Real-server browser skill smoke: exit 0, including the original 24 icons, 21 links, horizontal wheel scrolling, descriptions, zero-point purchase prevention, live ticks, neutral movement, focus and Escape close. Screenshot inspected at `artifacts/browser-check/skills.png`. Initial scroll assertion was corrected because the focused node was already horizontally visible.
- Test servers explicitly stopped after those checks.

Limits: browser smoke does not earn XP and complete a skill purchase end-to-end; purchases and gameplay effects are covered by deterministic/server tests, not full manual play or balance acceptance. This is per-life progression, not permanent progression or the entire multiplayer MVP.

## Modular expansion verification (2026-10-11)

- Release solution and WASM build: zero warnings/errors. All 227 solution tests passed (190 multiplayer, 37 legacy model).
- Automated model coverage includes exact split distance and remaining range, emitter velocity, burn/radiation/cryo, splash immunity, piercing contacts within one tick, homing, amplifier speed tradeoff, all six new skill effects, level/slot/replay validation, equipment reset, mailbox ordering, cloak lifetime, and projectile bounds.
- `tests/browser-skills.cjs`: real-server coverage of 30 skills and 27 prerequisite links.
- `tests/browser-weapons.cjs`: real server and WASM client checks for eight slots, locked modules, an accepted loadout, cloak beyond spawn protection, input suppression, desktop/mobile containment, keyboard focus, Tab protection, WebAudio oscillator creation and mute. Desktop/mobile screenshots are in `artifacts/browser-check/weapons.png` and `weapons-mobile.png`.
- Final browser prediction regression passed with 300 ms inbound delay. Presentation checks passed at four viewport sizes and two pixel densities, including menu containment, input suppression, disconnect/rejoin and Leave. The temporary test server was stopped afterward.
- Sound quality and late-game balance still need human playtesting.


## Inventory and input follow-up (2026-10-11)

Replaced the dropdown form with eight empty/occupied icon sockets and a reusable module vault. Kept the existing authoritative equip protocol, unlock rules and protection. Desktop and narrow-screen screenshots were inspected after the change.

A new regression reproduced lost movement history under frequent input/aim updates: half a second of continuous movement yielded about 38 units instead of 150. Prediction now coalesces unchanged movement intervals while retaining refresh expiry, direction changes and stationary-dash aim boundaries. Menu pointer movement also updates the stored cursor position without sending gameplay movement or changing aim while blocked, so closing a menu no longer restores a stale aim coordinate.

Verification: clean Release solution/WASM build; 229 tests passed (192 multiplayer, 37 legacy). Real-server inventory tests cover pointer dragging across snapshots, moving/removing parts, invalid drops, keyboard/click placement, unlock locks, accepted equipment, cloak, mobile scrolling, closing mid-drag, fresh aim after close, sound/mute and no page errors. Existing 300 ms delayed-input prediction and Tab skill-menu browser tests also passed. This reproduces and fixes a specific jump source; it is not an endurance or variable-network-latency guarantee.

## Friends, split playback and continuous beams (2026-10-11)

Split descendants now carry their parent, ancestry and fork position. Playback retains a departing parent until the children's birth, then presents the children at the fork. Descendants do not trigger a ship muzzle flash or another firing sound. Lasers use one sustained hum even when branched; release, mute, a hidden page or stalled snapshots stop it.

Verification: Release solution/WASM build with zero warnings/errors; 240 solution tests passed (203 multiplayer, 37 legacy). Coverage includes recipient-only acceptance, persistent name matching, cancellation/removal, pending versus accepted friendly fire, rocket splash, laser DPS and fork damage, menu/stale-input shutdown, and split playback continuity. The real two-client browser test covers request/accept/rejoin, remembered nickname, mutual removal, P-menu protection, keyboard focus and mobile containment. Existing inventory, skills and 300 ms delayed-input browser checks pass. Audio event tests verify silent splits and a single continuous laser hum. Desktop/mobile friends screenshots and a WASM beam presentation fixture were inspected. Beam combat is covered by model tests; the presentation fixture is not an end-to-end level-up test.

## Collectible workshop verification (2026-10-12)

- Release solution tests: 267 passed (230 multiplayer, 37 legacy), no failures or skips. Covers atomic persistence and failed saves, duplicate/replayed purchases, ownership across locked rows, deterministic credit collection, all attachment behaviors, fractional damage, friendship protection, run resets and protocol v2.
- Deterministic combat checks include eight simultaneous amplifiers, 4/8/4 DPS laser amplification and branching, multi-checkpoint traversal, conversion across scatter rays, full-duration burn with leech, mine saturation retries and saved firing-time programs.
- A bounded 24-player simulation exercises four weapons per pilot with splitters, plasma conversion, mines and rockets. Assertions enforce room and per-pilot entity limits and snapshot size. This is a short automated load regression, not an endurance or production capacity certification.
- Real-server Edge/WASM workshop checks cover purchases and receipts, persistent wallets/loadouts after reload, drag/drop across locked and unlocked rows, copy reservation, keyboard placement, focus across snapshots, discarded drafts, cloak and input suppression, desktop/mobile layout, sound mute and page errors. Shop, loadout and mobile screenshots were inspected.
- Friends, skills, prediction with 300 ms inbound delay, full-window resize-safe aiming, and presentation regressions pass. A beam presentation fixture verifies thickness changes and continuous branches visually; combat damage is verified separately by model tests.
- Audio event checks cover silent split descendants, one sustained hum per firing weapon, bounded combined gain, selective release, reset and mute.

Restart the server and refresh clients together for protocol v2. Keep `ProfilesFile` outside build output when persistence must survive deployment. Sound quality and economic/combat balance still require human playtesting.


## Pilot Ascension v3 verification (2026-10-12)

- Release solution/WASM build: zero warnings or errors. **302 tests passed**: 265 multiplayer and 37 legacy, with no failures or skips.
- Each of the 69 nodes has purchase coverage for level guards, prerequisites, point cost, duplicate requests and sequence replay. Additional tests cover the two-mastery limit, bay activation/reset, every shield/hull tier, recharge/repair timing, movement/prediction, Nova, fractional cadence, mastery isolation, conversions, direct versus splash bonuses, Strength-scaled statuses and Fortune/Luck payouts.
- Failed-save tests confirm credit pickups remain available, Luck rolls stay stable, no early payout receipt is sent, and the eventual wallet survives reopening the profile store. Old v1/v2 clients receive refresh-required responses.
- Real-server Edge/WASM checks earn XP through combat, purchase Shield Capacity I and Fire Rate I through the actual menu, and verify receipts, point balances, shield capacity and stable focus. Desktop/mobile checks cover all square SVG tiles, prerequisite connections, branch navigation, keyboard controls, cloak and page errors. A separate high-level presentation fixture verifies the third mastery is locked while selected mastery upgrades remain available; it is not a live level-15 playthrough.
- Workshop purchase/drag/drop/persistence, friends, 300 ms delayed prediction, five viewport shapes/two pixel densities, presentation, continuous beams and audio regressions pass. The bounded 24-player/four-weapon test also runs with maximum Fire Rate and checks entity/network limits.
- Inspected screenshots: `artifacts/browser-check/skills.png`, `skills-masteries.png`, `skills-specializations.png`, and `skills-mobile.png`.

Restart the server and refresh browsers together for protocol **v3**. Existing persistent profiles need no migration. Sound quality, late-game balance and long-duration capacity remain human-playtest/endurance work.


## Single-weapon workshop view (2026-10-12)

Only the selected weapon chain and its preview are visible. All four drafts stay intact when switching. Move a part across weapons by dropping it on a weapon selector (the same checkpoint is moved or swapped), or select a part, switch weapons, and choose a socket. Arrow keys navigate the selectors; Up/Down from a socket switches weapons at that checkpoint. Apply still saves all four configurations atomically.

Verification: clean Release/WASM build and real-browser workshop regression, including one visible nine-socket chain, four selectors, matching preview, selection across snapshots, cross-weapon moves, inventory reservations, persistence, cloak and desktop/mobile screenshots.
