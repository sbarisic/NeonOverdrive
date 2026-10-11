# S3 incremental combat verification

This supplements the historical network-slice results in `multiplayer.md`; it is not full MVP acceptance.

Multiplayer Shift (either key) requests a dash: six server ticks (0.2 seconds) at triple speed, with a 90-tick (three-second) cooldown from activation. Holding Shift repeats when ready. Direction locks at activation, using movement direction or aim when stationary. Dash does not grant invulnerability; arena clamps and swept shot collision still apply. Snapshot `dashCooldown` reports remaining server ticks. Existing single-player controls are unchanged.

Release multiplayer tests: exit 0, 57 passed, zero failed/skipped, including three new dash tests for duration/cooldown/packet-rate independence, stationary aim/disconnect, and boundary clamping. The first attempt exited 1 before producing test results because the running local server locked a DLL; stopping that owned server allowed the successful retry. Release solution build, including WASM bundle: exit 0, zero warnings/errors.

This increment has not been browser-playtested; the previous browser slice check predates dash. The local server was stopped for verification. S3 remains in progress: shield regeneration, respawn/safe-spawn retry, prediction/reconciliation, remote interpolation and latency trials remain pending. Aliens, upgrades, loot, salvage, scores/bounties and run recap are not implemented. Endurance/capacity and manual acceptance remain unverified.
