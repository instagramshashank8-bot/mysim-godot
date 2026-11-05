# Car-assembly turn 5 - protocol applied to run 41837315358 - 2026-11-06

Run: 41837315358, wheel_fit_review at e17fdf5, SUCCESS, four shards (cars 15-18), sweep v2 live: 275 poses per car, springs 0..0.33, steer set plus/minus 31.5 deg. Artifacts downloaded read-only into `outputs/wfr-41837315358/` (my worktree, uncommitted). Applied the turn-4 protocol.

## 1. Verdict: one systematic failure, all four cars

Every FAIL in this run points at ONE mechanism: the wheels sit at the raw pivot positions, roughly 0.45 m forward of the source suspension's hub anchors (the known axle offset, run 41580145045). This run ran WITHOUT the axle fit (no `-source-axle.json` artifacts; the recorded hub positions match the raw pivot table).

Evidence chain, car 15 as the example:

- Front wheel hub z = +2.078, tyre radius 0.301, so the tyre spans z in [1.78, 2.38].
- Front wing (the FRONTBUMPER meshes) spans z in [2.10, 2.86], y in [0.08, 0.32]: the ranges overlap the tyre on all axes.
- The probe finds the wing surfaces 45 mm inside the tyre at rest.
- With the axle fit the wheels move back 0.477 m: the tyre span becomes [1.30, 1.90], clearing the wing band by about 0.2 m.

## 2. Worst-case table (per car, metres, negative = penetration)

| Car | Wheel(s) | Group | Worst | Worst state | Part | Verdict |
|---|---|---|---|---|---|---|
| 15 W10 | FL,FR | bodywork | -0.0455 | rest, steer -15.75 | FRONTBUMPER_mm_misc | FAIL (axle) |
| 15 W10 | RL,RR | bodywork | -0.0453 | rest | CHASSIS_mm_misc | FAIL (axle) |
| 15 W10 | FL,FR | suspension | +0.0718 | - | STRUT_LOWER_FRONT | PASS (in tolerance) |
| 15 W10 | RL,RR | suspension | -0.0021 | -31.5 deg, offset -0.055 | STRUT_UPPER_REAR | WATCH |
| 16 SF90 | FL,FR | bodywork | -0.0455 | -31.5 deg, offset -0.165 | FRONTBUMPER (ext and misc) | FAIL (axle) |
| 16 SF90 | RL,RR | bodywork | -0.0453 | -31.5 deg, offset +0.055 | CHASSIS_mm_misc | FAIL (axle) |
| 16 SF90 | FL,FR | suspension | +0.0485 | - | STRUT_UPPER_FRONT | PASS (in tolerance) |
| 16 SF90 | RL,RR | suspension | -0.0203 | -31.5 deg, static | STRUT_UPPER_REAR | FAIL (axle) |
| 17 RB15 | FL,FR | bodywork | -0.0455 | +31.5 deg | FRONTBUMPER_mm_misc | FAIL (axle) |
| 17 RB15 | RL,RR | bodywork | -0.0455 | -31.5 deg, offset -0.055 | CHASSIS_mm_misc | FAIL (axle) |
| 17 RB15 | FL,FR | suspension | +0.1195 | - | STRUT_LOWER_FRONT | PASS (in tolerance) |
| 17 RB15 | RL,RR | suspension | -0.0052 | -31.5 deg, static | STRUT_UPPER_REAR | FAIL (marginal, axle) |
| 18 MCL34 | FL,FR | bodywork | -0.0455 | -15.75 deg, offset +0.11 | FRONTBUMPER (misc and ext) | FAIL (axle) |
| 18 MCL34 | RL,RR | bodywork | -0.0455 | -31.5 deg, static | CHASSIS_mm_misc | FAIL (axle) |
| 18 MCL34 | FL,FR | suspension | +0.1325 | - | STRUT_LOWER_FRONT | PASS (in tolerance) |
| 18 MCL34 | RL,RR | suspension | -0.0119 | -31.5 deg, offset -0.055 | STRUT_UPPER_REAR | FAIL (axle) |
| all | all | wheelguard | +1.17..+1.62 | far states | WHEELGUARD_*_misc | no contact; the shells sit far from their wheels (separate placement defect, recorded since turn 1) |
| all | all | wing, floor | no parts | - | - | the F1 wing and floor surfaces live inside the FRONT/REARBUMPER and CHASSIS meshes; they are tested there |
| all | all | other | +0.52..+0.80 | - | cab, exhaust | clear |

Diagnostic semantics per the reviewer: PASS means within tolerance, not a physical clearance pass; the -45 mm class is a real, visible-interference-level finding.

## 3. Named repair per FAIL

All FAILs route to rung 1 of the ladder: the axle line.

- Repair: enable the source axle fit in the run config: `source_axle_fit=true` on wheel_fit_review. The code exists (SourceAxleFitCandidate, committed; strut-derived, spread-guarded). This is a configuration fix, not a code change - no new geometry commit is made for it.
- Proof to demand from the rerun: the `-source-axle.json` artifacts appear; the bodywork worsts move from -0.045 to positive; the rear-strut entries clear. If any bodywork entry stays negative with the fit on, THAT entry gets its own pinned rerun and escalates to rung 2 (mount) or 5 (part transform) with the specific part named.

## 4. Staged code fix (separate commit, not production)

Commit 671842a on my branch: the sweep now clamps mode-applied offsets to the suspension envelope (the bump stop limits the rise to +0.11 m; the run's tables recorded +0.22 m from the negated roll/diag branches at full droop - states the suspension cannot reach) and counts clamped states in the summary. This removes physics-impossible states from future tables. It is instrumentation repair, not car geometry; staged for your review, not merged.

## 5. What this run does NOT prove

- The absolute positions assume the raw pivot table. With the axle fit the numbers change; the -45 mm verdict predicts clearance after the fix but is not yet measured.
- Static poses only: no dynamic roll/yaw transients, no speed-dependent steering, no kerb strikes. The envelope corners are covered; continuous motion is not.
- The probed cross-section is the rebuilt tyre's section (required by the fit check); the source tyre rubber differs slightly.
- Four cars of 24. The other twenty are untested by this instrument.
- Sampling is 15 points per triangle on viewed geometry: sub-5 mm misses are possible in both directions; PASS is within-tolerance, not a clearance certificate.
- The wheelguard shells' displacement (1.2-1.6 m from their wheels) is measured but its source-correct placement is unresolved; it is a separate defect with its own repair path.
