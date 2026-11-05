# Car-assembly turn 3 - wheel/axle/contact sweep and clearance - 2026-11-06

Branch `mistral/car-assembly`. New commits this turn:

```
0cb4c41 -> amended to 60379ad  Clearance sweep v2: full lock, full travel, tyre contact link, labelled worsts
e41d4f4  Fix candidate-gate env name mismatch that inerted run 41798036351's UV candidate leg
```

Actually: after the amend the sweep commit is `60379ad`. Order: `e41d4f4` (CarLoader.cs), `80ceb22` (none this turn - that is the turn-2 detector, already in history), `0cb4c41` amended to `60379ad` (WheelFitAudit.cs sweep v2). Current tip: `eb5e690` includes the turn-2 report; the turn-3 report commit follows this file.

## 1. The sweep evidence format (machine-checkable)

The sweep (`MYSIM_WHEEL_CLEARANCE_SWEEP=1`) now emits, per pose, per wheel, per obstacle class:

- Pose axes, all from the real systems:
  - Steering: full standstill lock from the real model, `Dynamics.SteeringLock(0)` = 0.55 rad = 31.5 deg. Default grid: -31.5, -15.75, 0, 15.75, 31.5 degrees. Override: `MYSIM_WHEEL_CLEARANCE_STEERS`.
  - Suspension: spring length from 0 (full bump) to RestLength+0.15 = 0.33 (full droop), the real `NativeSuspensionVehicle` range. Default grid: 0, 0.055, 0.11 (static), 0.165, 0.22, 0.275, 0.33. Override: `MYSIM_WHEEL_CLEARANCE_SPRINGS`. The applied wheel offset is `(RestLength-StaticSag) - spring` = 0.11 - spring, the same transform the physics applies to its wheel shapes in `_PhysicsProcess`. Every pose is therefore a reachable suspension state.
  - Mode: which wheels carry the offset. Default nine modes: front_both, rear_both, front_roll, rear_roll, diag (kerb), FL, FR, RL, RR (single corner). Override: `MYSIM_WHEEL_CLEARANCE_MODES`. The mode grid size is 275 poses at defaults.
- Per pose, per wheel (machine-checkable):
  - `wheelCentre`, `hub` (car space),
  - `contactPoint` = lowest point of the visible tyre geometry in car space (from the real tyre vertices, transformed by the pose),
  - `contactShift` = contact point minus the assembled static contact point (the scrub/lever-arm movement under steer and travel),
  - `groundGap` = contact height above the floor plane,
  - `springLength`, `wheelOffset` (the physical state this pose represents),
  - `categories`: per obstacle class {wing, floor, brakes, suspension, bodywork, wheelguard, other} the worst clearance and the worst part,
  - `offenders`: penetrating parts with depth,
  - `clearance` = deepest penetration (negative) or clearance.
- Aggregated per wheel (the worst-case table): for each obstacle class, the worst clearance over the whole sweep with the part name and the state (steer / wheel offset / spring length) that produced it.
- Console lines, grep-able: `WHEEL_CLEARANCE_WORST car=N wheel=W vs=C clearance=X part=P atSteer=S atWheelOffset=O` (one per wheel and class), plus `WHEEL_CLEARANCE_SWEEP_SUMMARY car=N poses=N worstClearance=X worstWheel=W worstVs=C worstPart=P atSteer=S atWheelOffset=O`.
- Deduplication: wheel states are cached by (wheel, steer, applied offset); equal states reached through different modes are evaluated once.
- Safety unchanged: poses applied through the real `WheelAnimation.Update` path; the pre-sweep pose is restored and the restore is verified; `DrySection` still requires the dry rebuilt tyre and throws otherwise.

## 2. Worst-case table

No run has been executed (no local runs; no dispatches by this role). The table below is the format with the values proven so far. It must be filled from the next car_evidence run's `WHEEL_CLEARANCE_WORST` lines or the JSON `perWheel` block.

| Wheel | vs | Worst clearance (m) | At steer | At wheel offset | At spring | Part | Source |
|---|---|---|---|---|---|---|---|
| front L/R | wheelguard (suspension) | -0.0257 | +/-28 | 0 (static) | 0.11 | LOD_A_STRUT_UPPER_FRONT_*_mm_ext | run 41580145045, FORMULA16_SOURCE_AXLE_ACTUAL.json (25.7 mm, 40 samples) |
| all | all | pending | pending | pending | pending | pending | next car_evidence run with the v2 sweep |

## 3. Fixes

Committed this turn:
- `e41d4f4`: the candidate-gate env name fix. Without it the pending UV candidates could never activate under the integration harness wiring; with it, the existing car_evidence wiring works.
- `60379ad` (0cb4c41 amended): the sweep v2 described above. The earlier `0cb4c41` commit message was corrupted by my own tooling; it was amended in place to the correct message before any push. Nothing was pushed at any point.

Fixes deliberately NOT committed (no blind geometry changes):
- The known proven penetration (front upper wishbone vs tyre, 25.7 mm at 28 deg steer, run 41580145045) has a candidate mechanism already wired: steering about the source kingpin axis (`MYSIM_SOURCE_KINGPIN_VISUAL=1` through `WheelAnimation.Update`, which the sweep exercises per pose). Whether it clears 25.7 mm at full lock is exactly what the sweep measures next run; the fix knobs (axle shift via `MYSIM_SOURCE_AXLE_FIT`, mount offsets via `component_mount_profiles.json`, rim geometry via `f1_wheel_profiles.json`) are all in my files and wait for the numbers. No blind geometry change is committed: every candidate would need the sweep numbers to prove the sign and size of the correction.
- No tyre was shrunk. No part is hidden. No collision is disabled.

## 4. Verification status

- Full file review of the new sweep and the detector; brace and paren counts balance; the sweep math was verified symbolically while writing (the offset-to-spring identity, the orientation sign convention, the transform chain against `NativeSuspensionVehicle._PhysicsProcess` and `_IntegrateForces`).
- Compile proof pending the next cloud run (no local builds allowed).
- No runs and no dispatches were performed this turn; only read-only `gh api`/`gh run view` queries and local file work.

## 5. Untracked evidence kept (not committed)

- `outputs/e8b80bb-audit.py`, `outputs/e8b80bb-CarLoader.cs`, `outputs/e8b80bb-repairs.json` (the e8b80bb blobs proving section 0), `outputs/ref-41788844369-job1.log` (the `SOURCE_AXLE_FIT: 0` line).
