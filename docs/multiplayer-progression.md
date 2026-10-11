# Multiplayer progression foundation

## Rules implemented
- Projectile-killed rammers drop 10 energy; gunners drop 15. No direct kill XP: physically collect the drop.
- Collection is server-owned, using swept movement contact (26-unit combined radius). Earliest contact wins; equal contact times resolve by player ID. A drop is removed once, so competing collectors cannot duplicate its value.
- Dead/disconnected players cannot collect. XP remains visible on the dead snapshot, and a fresh respawn resets XP to zero and level to one.
- Level thresholds use total run XP: 0, 20, 60, 120, 200... (`10 * level * (level - 1)`). Level is capped at 100, XP at 1,000,000. Levels currently have no combat effect; upgrade choices are a future increment.
- Each room holds at most 128 drops, expiring after 30 seconds. Overflow rewards are discarded rather than growing memory unboundedly. No death loot yet.
- Snapshot fields `energy`, player `experience` and `level` are additive to the current protocol. Missing energy arrays are tolerated by presentation. Clients still send only existing input/respawn messages, never reward values.
- Browser draws cyan energy diamonds, total XP, level and a progress bar. Single-player code is unchanged by this increment.

## Observed verification
- Release solution build including WASM bundle: exit 0, zero warnings/errors.
- First multiplayer run: exit 1, 76 passed / 1 failed. Existing `MissingInviteRoomReportsError` failed with a WebSocket close-handshake/connection-reset error. Cause is not diagnosed.
- Full Release solution test rerun without code changes: exit 0, 114/114 passed (77 multiplayer, 37 single-player), no skips. The successful rerun does not establish that the socket failure is fixed.
- Eleven new test cases cover thresholds, actual projectile kill/drop/collection, competing collection conservation, dead/disconnected exclusion, respawn reset, drop cap/expiry and snapshot JSON roundtrip. Private controlled fixtures are test-only; no authoritative mutation API was exposed to clients.

## Pending acceptance
- In browser: kill each alien type, collect its diamond and observe XP/level/bar; verify a remote observer sees the same drop disappear; die and retry to confirm level one/zero XP.
- Visual placement, live browser collection and dash-through pickup acceptance have not been exercised in this increment.
- Prediction/reconciliation, upgrades/modifiers, central risk/reward, partial PvP death loot, recap, scoring/bounty/salvage and endurance capacity remain incomplete.
