# Multiplayer lifecycle increment

- Shields recover one point after five simulation seconds without damage, then one point per second, capped at two. Hull does not regenerate. Dead ships do not regenerate. Both projectile and rammer damage restart the delay.
- Dead connected players can request an immediate fresh ship in the same room using the respawn button. The server validates sequence and life state. Identity remains; hull/shield, protection and combat cooldowns reset. Old held/pending input is discarded.
- Spawn selection maximizes clearance across 16 outer-belt candidates from live players, aliens and existing shots. This is a bounded heuristic, not a guarantee against future enemy fire. Protection lasts two seconds and ends on attacking.
- Respawn requests use a bounded mailbox separate from continuous input; the simulation thread applies the two in sequence order. They share existing WebSocket admission, sequencing and rate limits. Alive-player requests cannot heal or teleport.
- No single-player model or rendering changes in this increment.

## Observed verification

Release solution/WASM build: exit 0, zero warnings/errors (927ec0c1ee264523850b1a7b02ba680c).
Release multiplayer tests: exit 0, 66/66 (3b01ed2fb7dc4123a480d393d6725155).
Release single-player tests: exit 0, 37/37 (a1ffd5831a514c419ef0f2d531d43918).
New coverage checks shield-delay boundary, dead-ship non-regeneration, respawn reset/rejection, stale input rejection and real-socket handling of an alive-player respawn request without disconnect/reset. Server integration tests use OS-assigned loopback ports and dispose their hosts; no persistent test server was started.

## Still unverified / pending

The browser death/button/retry interaction has not been exercised in this increment. Manually join, die to aliens, click respawn, confirm full health and continued same-room play, then verify firing ends protection. Check repeated deaths, blur/focus and remote observers. Successful dead-player respawn over a real socket remains an integration-coverage gap (model success and socket routing have separate checks).
Prediction/reconciliation, latency measurements, progression/loot/upgrades, full recap, central risk/reward, salvage, bounties, leaderboards and capacity acceptance remain incomplete.
