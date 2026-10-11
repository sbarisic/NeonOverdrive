# Local movement prediction checkpoint

Baseline multiplayer/presentation work committed as `848247e`. Prediction was implemented afterward as a separate working-tree change, following the user's request to prioritize responsiveness over remaining visual acceptance.

## Design

`LocalPrediction` is presentation-only. It integrates elapsed local intent intervals, not one movement step per packet: the server's Ack identifies the latest held intent and may repeat across ticks. Actual send timestamps approximately anchor reconciliation; no synchronized server clock exists. Small visual corrections decay over 60 ms, errors above 120 world units snap. Histories are capped at 128 entries, retention is one second, prediction horizon is 0.5 seconds, and stale intent expires after 8/30 seconds.

Browser input reaches C# on input changes and display cadence, independently of the 30 Hz transport. Only the local pilot is overlaid onto the interpolated snapshot; camera/aim follow its predicted position. Damage, shots, XP, hull and shield remain authoritative. Optional dash duration/direction and life-generation snapshot fields support dash and respawn resets. Menu/focus/death/disconnect neutralize local intent.

## Observed verification

- Release solution/WASM build: exit 0; zero warnings/errors.
- Release solution tests: exit 0; 127 passed (37 original model tests and 90 multiplayer tests), zero failures/skips.
- `node tests/browser-prediction.cjs`: exit 0. Real server with 300 ms artificial inbound message delay: rendered local X moved from 2780 to 2740.2498 while received authoritative X remained 2780; after release it settled to 2590.0007 versus authoritative 2590. Menu suppressed predicted movement.
- `node tests/browser-presentation.cjs`: exit 0, including layout, menu neutral-input, mouse-fire, reduced-effects UI, resume and disconnect/rejoin checks.
- `node tests/browser-multiplayer.cjs`: exit 0; independent sessions, remote movement/shots and server-confirmed shield damage.
- Local verification server explicitly stopped (exit 1 from intentional termination).

The latency script instruments main.js only in its test context to access C#'s actual rendered-position diagnostic; it delays real incoming events without manufacturing server snapshots.

## Limitations and follow-up

This verifies bounded local responsiveness, not FPS improvements or complete networking acceptance. Reconciliation clock mapping remains approximate. Explicit neutral intent immediately stops local dash presentation; the unchanged server can finish an active dash and reconcile it afterward. Prediction freezes beyond the bounded horizon. Longer jitter/packet-stall, repeated browser death/respawn, remote interpolation, multiple-browser coverage and endurance checks remain outstanding. No client-side shots or damage are predicted. Native/browser solo code paths were not intentionally changed; no fresh interactive solo acceptance is claimed.


### Input history refresh correction (2026-10-11)

Repeated RAF, pointer and transport calls now refresh the end of a held movement interval instead of inserting identical movement records. Aim-only changes during ordinary movement do not evict replay history. Actual direction/dash changes and stationary-dash aim changes retain boundaries; a real input timeout starts a new interval. Regression tests cover high event rates and timeout/direction preservation. The existing delayed-browser prediction test still passes.
