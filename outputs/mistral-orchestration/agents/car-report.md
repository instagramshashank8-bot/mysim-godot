# Car assembly specialist report — 2026-11-05

Branch `mistral/car-assembly`, worktree `C:/Users/shash/MySimGodot-wt-car`, base c87ae00.
Scope: VISUAL_REVIEW items 3 (mirrored logos/numbers), 6 (rear alignment), 4 (wheel closeups + swept clearance). Read-only inputs: instructions.md §5-10, VISUAL_REVIEW.md, feedback.md, CONTINUE.md tail, TASK_BOARD.json, FORMULA16_REAR_SOURCE_FOLLOWUP.json, FORMULA16_SOURCE_JOINT_VIEW_ACTUAL.json, FORMULA16_SOURCE_AXLE_ACTUAL.json, car16 assembly/UV evidence JSONs, car_map.json, game-review.yml, cloud_game_audit.py.

## 1. Mirrored logos/numbers on car16 — mechanism and per-surface policy

**Mechanism (derived from existing reviewed evidence, no new claims):** CarShader's authored default is `uv_scale=(-1,-1), uv_offset=(1,1)` — a 180° UV remap. The reviewed corrections in `data/car_surface_uv_repairs.json` are all V-only (`scale=[1,-1], offset=[0,1]`): car16 BODY/FRONTBUMPER/REARBUMPER `_mm_ext` (run 41572315488, paired rear/opposite-side views) and all 11 visible exterior roles of car20 (run 41453316712, eight baseline images). So the correct image-origin convention for these cars is V-only; the default's extra U flip is what mirrors decal text while leaving panel paint/carbon coherent — exactly the reviewed symptom. Any surface not yet in the repair file still renders with the extra U flip.

**Residual mirrored surfaces after the three reviewed repairs** (from car16 assembly evidence, `cloud/formula16-density/outputs/cloud-game/car16-channel-0-both-assembly.json`): 10 renderers bind the livery atlas (guid 449e4db78ecc6e21c9bd11a7fbf05975); 7 remain uncorrected — `LOD_A_DOOR_LEFT/RIGHT_mm_ext` (23 mm stubs at centreline), `LOD_A_EXHAUST_mm_ext`, `LOD_A_MIRROR_LEFT/RIGHT_mm_ext`, `LOD_A_STRUT_UPPER_FRONT_LEFT/RIGHT_mm_ext`. Additionally the **badges layer** (`LOD_A_BODY/FRONTBUMPER/REARBUMPER_mm_badges`, guid a903631edbed0e5cf8f2a64dc5a09eb2, separate alpha overlay carrying sponsor markings) has no correction entry at all and is the primary remaining suspect for the logos the 2026-11-04 review still saw with the three ext repairs active (run 41788844369, head e41e29b).

**Change (this commit):**
- `data/car_surface_uv_repairs.json`: 10 new entries for exactly those renderers, V-only, each with `"pending": true`, the evidence basis above, and per-surface confirmation language. No existing entry changed; no blanket flip.
- `src/CarLoader.cs` `SurfaceUV`: `pending` entries are inert in production and apply only when `MYSIM_SURFACE_UV_CANDIDATES=1`; the strict guard (source GUID + texture path match, else throw) applies whenever a pending entry is applied, and the log line is `SURFACE_UV_REPAIR_CANDIDATE` (distinct from `SURFACE_UV_REPAIR`, which the existing cloud assertions greps for — unchanged).
- Promotion path: after the paired-view run confirms a surface, flip its entry to reviewed (drop `pending`) in a follow-up commit with the run number in `evidence`. Rejection = delete the entry. Nothing else changes.

**Per-surface evidence status:** ext candidates inherit same-car, same-atlas family evidence (41572315488 / 41571290602 / 41510907196 — negative UV0 V measured, islands match atlas silhouettes under top-left origin). Badges entries rest on family evidence only and say so; they must be judged per rendered badge.

## 2. Disjointed rear — causes from SOURCE evidence, ranked

1. **Axle line offset, uncorrected in the reviewed images (strongest).** FORMULA16_SOURCE_AXLE_ACTUAL.json (run 41580145045, 6 views): shared lower-strut-derived axle translation of **-0.47653607 m** removes front-wing/chassis/rear-strut vs rubber intersections; spread 0.05488777 m; wheels/struts otherwise misaligned along Z. Rear struts sit at z=-1.874/-1.933 while the rear hubs sit at z=-1.589 (assembly evidence) — wheels visually detached from the rear suspension. **Verified fact:** the workflow log of run 41788844369 (the run behind VISUAL_REVIEW items 3/6) prints `SOURCE_AXLE_FIT: 0` (local copy: `outputs/ref-41788844369-job1.log`, line at 2026-11-04T07:10:49Z) — the reviewed rear images ran WITHOUT the axle correction, and `reference_evidence` never requests the axle report. The fix already exists behind `MYSIM_SOURCE_AXLE_FIT=1` / workflow input `source_axle_fit` (wired at game-review.yml L166/L190; opt-in by design).
2. **Uncorrected misc-atlas surfaces on the rear.** FORMULA16_REAR_SOURCE_FOLLOWUP.json: 14 renderers (BODY/CHASSIS/FRONTBUMPER/REARBUMPER `_mm_misc`, MIRROR_GLASS_L/R, STRUT_LOWER_FRONT/REAR L/R, STRUT_UPPER_REAR L/R, WHEELGUARD_FRONT L/R) all share misc guid aed61c8bdfb990d77961becb3daf5a23. The SF90 misc atlas is predominantly dark carbon with small hardware/colour swatches (manual review recorded in CONTINUE); with the default 180° UV these islands can sample wrong swatches -> high-contrast foreign patches on rear mechanicals. The followup explicitly defers until per-surface correspondence exists; no data entries added for misc (correctly).
3. **Duplicate body shells.** BODY/FRONTBUMPER/REARBUMPER exist as BOTH `_mm_ext` and `_mm_misc` renderers, both visible, with overlapping envelopes (e.g. REARBUMPER ext z=-2.392 size 0.761 vs misc z=-2.268 size 0.451; misc slightly larger everywhere). Double-rendered near-coincident shells produce z-fighting/interpenetration, most visible at the rear where the envelopes diverge.
4. **Ruled out:** temporal ghosting (persists at 39/40/41 s; no motion blur/TAA in the loader; blur meshes filtered), wheel-pivot family misassignment for tyres/wheels (LOD_A_TYRE*_mm_tyre and LOD_A_WHEEL*_mm split correctly by the `_REAR` rule at CarLoader AttachWheels; rear blur is excluded from duplication). Wheelguard templates are origin-centred and duplicated onto all four pivots, but the copies land at their original car-space positions (buried at the vehicle centre), so they are not the visible rear defect; they are excluded from WheelFitAudit as Spin descendants and their originals are hidden.

**Minimal reversible repair candidates (in order):**
- R1 (no code): re-run the reference views with `source_axle_fit=true` and compare rear/side/wheel views against 41788844369. If the rear closes up, adopt axle fit for the reference configuration and record it.
- R2 (shipped here, opt-in): `MYSIM_MISC_TWIN_HIDE_DIAGNOSTIC=1` in `CarLoader.Load` hides a `_mm_misc` renderer ONLY where the identical renderer name exists as `_mm_ext` (exactly BODY/FRONTBUMPER/REARBUMPER `_mm_misc` on car16), logs every hide, sets `misc_twin_hidden` (reported by the assembly audit), changes no geometry/UV. A/B against R1's images decides whether the misc shells are alternates (hide) or required detail (keep).
- R3 (next batch, not started): per-surface misc correspondence — capture `MYSIM_CAR_UV=v, MYSIM_CAR_UV_SCOPE=misc` renders of the rear misc set, then add reviewed entries to `car_surface_uv_repairs.json` per the §1 promotion path. The existing `car_uv_roles` depth already runs car16 misc/nonlivery V-flips at one camera as a zero-change probe.

## 3. Wheel/axle/wing clearance sweep fixture — implemented (opt-in)

`src/WheelFitAudit.cs` gains an in-process sweep behind `MYSIM_WHEEL_CLEARANCE_SWEEP=1`, invoked from the existing `MYSIM_WHEEL_FIT_AUDIT` call path (no Main.cs change; existing single-pose output and `PASS wheel fit evidence` print unchanged; new `sweep` section in the same JSON):

- Poses: steers {-28,-14,0,14,28} deg (override `MYSIM_WHEEL_CLEARANCE_STEERS`, hard cap +-45) crossed with travel steps {-0.08,-0.04,0.04,0.08} m (override `MYSIM_WHEEL_CLEARANCE_TRAVELS`, hard cap +-0.08 — the bound Main.cs already enforces for evidence poses) in four modes: front_both, rear_both, front_roll, rear_roll, plus one rest pose per steer = 85 poses default.
- Every pose steers through the real `WheelAnimation.Update` path (identical to gameplay and to `MYSIM_WHEEL_FIT_STEER`, including the kingpin visual candidate when enabled) and translates hubs within the evidence bound; front AND rear wheels; all four wheels probed per pose.
- Tested against: all visible `LOD_A_*` meshes + rebuilt wishbones (body/wings/brakes incl. fixed calipers/guards; rotating wheel meshes excluded) — the same filter and 15-sample-per-triangle barycentric sampling as the existing audit, captured once as a car-space cloud and transformed per pose (identical math, ~85x cheaper than re-walking meshes).
- Output per pose per wheel: `clearance` (positive = clear; negative = deepest sampled penetration), penetrating part names with depths, sample counts. Scope string states plainly: findings, not a swept-clearance certificate; hub translation is an evidence pose, not a suspension solution.
- Safety: pose restore in `finally` with `IsEqualApprox` verification (throws if any pivot/Spin/BrakeAssembly transform drifts), so subsequent audits/screenshots see the pristine state; empty body cloud fails loudly instead of serializing Infinity; requires `MYSIM_REBUILT_TYRES=1` (same as the existing audit).

This satisfies VISUAL_REVIEW item 4's "steering/suspension swept clearance evidence" requirement at the static-analysis level; continuous in-motion clearance during a recorded lap remains a separate, later requirement (existing `car_paint_candidate` depth already covers steer {-28,0,28} x travel {+-0.04} x front wheels with per-combo fit audits and can serve as the interim front-only evidence).

## Proposed dispatch configs (coordinator dispatches; existing depths only)

D1 — Mission 1 candidate evidence (needs the two one-line enablements below, or run as-is for baseline-only):
```
gh workflow run game-review.yml -R instagramshashank8-bot/mysim-godot --ref mistral/car-assembly \
  -f depth=reference_evidence -f source_axle_fit=true
```
with the handler extension below adding `MYSIM_SURFACE_UV_CANDIDATES=1` to the seven static views (keep the two drive frames clean so livery fixes are judged on static frames first). Reviewer compares per-renderer: badges/mirrors/exhaust/strut-uppers must read correctly with candidates ON vs OFF; each confirmed surface is then promoted per §1.

Coordinator one-liners (your files; the 422 precedent was for a 26th top-level input — these add NO input, only a depth option + env/handler lines):
- game-review.yml: add `surface_uv_candidates` to the depth choices; add env `SURFACE_UV_CANDIDATES: ${{ inputs.depth == 'surface_uv_candidates' && '1' || '0' }}` and route it in the shard map as single-shard `[0]` like `reference_evidence`.
- cloud_game_audit.py, REFERENCE_EVIDENCE block: `values['MYSIM_SURFACE_UV_CANDIDATES']='1' if os.environ.get('SURFACE_UV_CANDIDATES')=='1' else None` style insert (or a dedicated depth branch reusing the same view loop with the flag added and `MYSIM_MISC_TWIN_HIDE_DIAGNOSTIC='1'` for the R2 A/B leg).

D2 — Mission 2 A/B (R1+R2 in one dispatch once wired, else two `reference_evidence` runs, one with the env flags):
- Run A: `depth=reference_evidence, source_axle_fit=true` (R1).
- Run B: same + `MYSIM_MISC_TWIN_HIDE_DIAGNOSTIC=1` (R2) — compare rear/side views; the assembly audit JSONs (new `basisDeterminant` + `miscTwinHidden` fields) ship in both runs' artifacts for the handedness/hidden-set record.

D3 — Mission 3 sweep (needs one depth option + handler lines, same pattern):
```
gh workflow run game-review.yml -R instagramshashank8-bot/mysim-godot --ref mistral/car-assembly \
  -f depth=wheel_clearance_sweep
```
proposed handler (your file): single shard, one car16 process per the car_paint_candidate env set (`MYSIM_REBUILT_RIMS/TYRES/BRAKES=1, MYSIM_COMPONENT_MOUNTS=1, MYSIM_SOURCE_AXLE_FIT=1, MYSIM_SOURCE_KINGPIN_VISUAL=1, MYSIM_REBUILT_F1_SUSPENSION=1`) plus `MYSIM_WHEEL_FIT_AUDIT`, `MYSIM_WHEEL_CLEARANCE_SWEEP=1`, `MYSIM_FITVIEW=1`, `MYSIM_CAMPOS=3.5,4.5,5`, `MYSIM_SHOT`; assert file exists + `WHEEL_CLEARANCE_SWEEP` + `PASS wheel fit evidence` in output. Runtime estimate: one car-view process + ~85- pose CPU sweep (no per-pose rendering); if the step budget is tight, ship `MYSIM_WHEEL_CLEARANCE_STEERS=-28,0,28` first.

D4 — immediate no-code-change probe (optional): `depth=car_uv_roles` (car16 baseline/misc/nonlivery V-flips, one camera) as a coarse sanity signal for the misc/badges question while D1 wiring is reviewed.

## Verification status

- JSON parses (25 entries, 10 pending). Diff reviewed line-by-line; only the four owned files touched (`git diff --stat`: CarLoader +12/-2, CarAssemblyAudit +2/-2, WheelFitAudit +64/-1 line-ish, data +90).
- Compile proof is PENDING: local builds are not permitted; the next cloud core/compile run on this branch is the check. Risk points I self-reviewed: Godot `Basis.Determinant()`, `Transform3D.IsEqualApprox`, `Dictionary.ContainsKey/AsBool` on parsed JSON, `GD.Print` ternary first argument, object-typed `sweep` serialization (null -> `"sweep": null`).
- No dispatches performed (per guardrails). No local engine/render/asset processing: only file reads, metadata queries, one read-only `gh run view`/log fetch.

## Remaining risks / open

- The 10 pending entries are family-inferred until D1 confirms each; badges entries in particular have no badge-specific prior.
- If run 41788844369's dispatcher HAD set source_axle_fit (contradicted by the log line `SOURCE_AXLE_FIT: 0` found in its job log), R1's premise would weaken; the log line is the controlling evidence.
- The sweep's clearance metric is sampled (15 points/triangle, static cloud) — it can miss thin-grazing contacts; it complements, not replaces, in-motion evidence.
- `MYSIM_MISC_TWIN_HIDE_DIAGNOSTIC` changes `VehicleDimensions` measurement (hidden twins leave the visible-set bounds) while enabled — expected for the A/B, default-off otherwise.
- Wheel closeup framing (VISUAL_REVIEW item 4's `wheel-fl` framing complaint) lives in Main.cs/cameras — not my files; the D3 handler can add `MYSIM_WHEEL_CLOSEUP` legs per wheel if desired.
