# Car-assembly turn 4 - verdict protocol for sweep v2 - run 41832997990

Applies when the run result is pasted. No dispatches made.

## 0. Scope notes for this run

- The run uses the wheel_fit_review wiring: one car per shard (cars 15-18), `MYSIM_REBUILT_TYRES=1` set, `MYSIM_WHEEL_CLEARANCE_SWEEP=1` from the dispatch.
- The earlier env-name mismatch (the UV-candidate gate, fixed in commit e41d4f4) does not touch this run: the sweep trigger name matches, and this run sets no UV-candidate variables.
- Compile risk: this is the first run of the sweep v2 code. A compile failure consumes the run; the fix is mine and the rerun is one pass with a distinct-fix note.
- Runtime: about 275 poses per car with the state cache. If a shard times out, re-run it with `MYSIM_WHEEL_CLEARANCE_STEERS=-31.5,0,31.5` (three-point grid).

## 1. Expected JSON fields per wheel

Location: the wheel-fit JSON written by `MYSIM_WHEEL_FIT_AUDIT`, under the `sweep` key.

Top level: `car`, `steers[]`, `springs[]`, `modes[]`, `poses`, `bodyParts`, `perWheel[]`, `rows[]`, `scope`.

`perWheel[]` (aggregated over the whole sweep, one per wheel pivot):

```
{ wheel,
  categories: {
    wing | floor | brakes | suspension | bodywork | wheelguard | other: {
      worstClearance,      # metres, negative = penetration, worst over all states
      part,                # source mesh name of the worst part
      atSteerDegrees, atWheelOffset, atSpringLength   # state that produced it
    } } }
```

`rows[]` (one per pose; defaults 5 steer x (1 rest + 6 springs x 9 modes) = 275):

```
{ steerDegrees, mode, springLength,
  wheels: [ {
    wheel, hub[3], wheelCentre[3],
    contactPoint[3],       # lowest point of the visible tyre mesh, car space
    contactShift[2],       # contact xz minus the static contact (scrub movement)
    groundGap,             # contact height above the floor plane (y = 0)
    springLength, wheelOffset,
    clearance,             # deepest penetration over all groups (negative) or clearance
    penetratingParts, offenders[{part,category,deepest}],
    categories{ group -> {clearance, part} },
    samplesInspected } ] }
```

Sign convention: clearance below zero means the sampled body point sits inside the tyre cross-section by that depth. A detected depth is a lower bound (sampling is 15 barycentric points per triangle).

Console lines for the harness:

```
WHEEL_CLEARANCE_WORST car=N wheel=W vs=C clearance=X part=P atSteer=S atWheelOffset=O
WHEEL_CLEARANCE_SWEEP_SUMMARY car=N poses=N worstClearance=X worstWheel=W worstVs=C worstPart=P atSteer=S atWheelOffset=O
```

## 2. Thresholds

Model error, from source-checked numbers only:

| Source | Value |
|---|---|
| Tyre cross-section polygon error (the swept 21-point profile, radius about 0.30 m) | up to about 3.5 mm |
| Contact tolerance, measured on run 41627667783 | -1.571 to +1.168 mm |
| Combined model + contact error | about 4.6 mm |

Decision bands:

| Band | Rule | Action |
|---|---|---|
| PASS | worst penetration below 2 mm in every group and every state | inside the measured contact band; no action |
| WATCH | 2 mm to 5 mm | inside the combined model error; re-run pinned to the worst state (three-point grid + closeup) before any conclusion |
| FAIL | penetration above 5 mm vs wing, bodywork, floor, or brakes, at full lock or full travel, or any state | actionable; apply the repair ladder in section 3 |

Special cases:

- Rest state (spring 0.11, steer 0): the assembly is fully determined, no dynamic cover. Penetration above 2 mm at rest = FAIL directly.
- `groundGap` (tyre vs the floor plane) is not a failure axis: the body height compensates on the car. It is recorded per state. A rest-state groundGap far from the run's own static reference (above 0.10 m) is reported as an assembly anomaly, not a FAIL.
- `wheelguard` and `suspension` groups sit close by design. The same numeric bands apply; a failure there routes to the mount or kingpin repairs, not to body moves.

Rationale: a real interference above 5 mm exceeds every known model error. The baseline car carries usable clearances by design, so a real interference inside the travel envelope is an assembly fault of ours, not a source-design fault.

## 3. Repair ladder (per FAIL entry)

Input per entry: wheel W, group C, worst state S (steer / spring / mode), part P.

Apply the first matching rung. Then re-run the sweep pinned to S: `MYSIM_WHEEL_CLEARANCE_STEERS` + `_SPRINGS` + `_MODES` pin the exact state; add `MYSIM_WHEEL_CLOSEUP` for the visual.

| Rung | Signature | Cause class | Repair | Forbidden |
|---|---|---|---|---|
| 1 | vs body / floor / wing; both sides; or at rest | wheel position vs source (axle line) | `MYSIM_SOURCE_AXLE_FIT=1`; SourceAxleFitCandidate strut-derived axle move (known candidate -0.4765 m, spread guard 0.055 m) | moving body parts to chase the wheel |
| 2 | vs brakes / wheelguard only | hub-mounted part offset | `data/component_mount_profiles.json` keys (inboardTyreWidth, rearwardRadius, aboveHubRadius), only with source check for that part | hiding the part |
| 3 | only near full lock, both sides | steering line: hub centre vs kingpin | A/B `MYSIM_SOURCE_KINGPIN_VISUAL` 0/1 at the pinned state; adopt the kingpin line for the reference car if it clears | widening the wheel cut |
| 4 | vs the rebuilt arms only | authored suspension arms | `data/f1_suspension_visual_profiles.json`: arm width/thickness/joint radius, castor inside checked bounds; rebuilt arms only | touching source arms |
| 5 | any entry; part provably off its source spot | part transform | put that part back to its source spot; record the check | narrowing the tyre, cutting contact, hiding parts |

Rules: one rung at a time. Two rungs tie or conflict - stop and report; the coordinator chooses. Every change pins the state, re-runs, and shows fresh pairs. The tyre stays sized. The contact stays on.

## 4. Applying the run result

When the result is pasted:

1. Fill the worst-case table per car from `sweep.perWheel` or the `WHEEL_CLEARANCE_WORST` lines.
2. Mark each entry PASS / WATCH / FAIL per section 2.
3. For each FAIL, name the repair: exact file, exact knob, exact value, plus the pinned re-run config.
4. List what the run does not prove: number gaps, missing shards, unread sections.
