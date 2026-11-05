# physics-gameplay agent — turn 3 — ride-height decision pack, backend impact protocol, DV judging — 2026-11-04

Role: physics/gameplay specialist. Branch `mistral/physics-gameplay` (worktree `C:/Users/shash/MySimGodot-wt-physics`; turn-2 state `a5215ef` merged by coordinator as `02e9c95`). Inputs read this turn: REVIEW_PACKET.md session-2 status, VISUAL_REVIEW.md (3 sections), JOB_REGISTRY.json (11:20Z), the failed-run artifacts under `outputs/repair-20261029/dv-failed-41820348686/`, and the coordinator's DV commits `3061afa`/`9c422a4` (read via local git — no dispatches, no runs, no workflow edits). One patch committed (section 2.3); no defaults changed anywhere.

## 1. DV pipeline status reconstruction (facts, from artifacts + local git)

- Run `41820348686` (first DV core, source backend) died at **4.07 s** in `PlaceBody → NativeSuspensionVehicle._Ready → NativeCarColliders.Install`: `Required native source-body hull cache missing for car 16`. Both per-case logs identical (7123 B). My validation code never ran (the body never initialised); the failure is infrastructure, upstream of the profiles, and the fast-fail behaviour (clean exit 1 with the true reason in `error_lines`) worked exactly as designed.
- Fix chain read from git: `3061afa` added DV to the hull-bake step; intermediate run `41823772505` then baked only car 0 (legacy `[[0,16][shard]]` selector) while the case needed car 16; `9c422a4` (current re-dispatch head) makes the bake read the DV manifest's cars per shard. Chain is coherent; failure records preserved by the coordinator.
- Merged DV runner contract (`tools/cloud_game_audit.py` lines 95-126, read at `9c422a4`): env `DRIVING_VALIDATION` + `DRIVING_VALIDATION_MANIFEST` (+ `DRIVING_VALIDATION_IMPACT` asserted to match the manifest's contents), always `MYSIM_ASSISTS='0'`, `MYSIM_REQUIRE_NATIVE_CAR_HULLS='1'`, `MYSIM_COMPONENT_MOUNTS='1'`, `MYSIM_SECONDS=900`; damage only for the impact manifest; report asserts `ok/completed/allPassed`, `rotationalTyres is True`, `assistsOff is True`, exact profile coverage, per-run no-abort, >0 assertions, sampleCount>20; writes `driving-validation-summary.json`.

## 2. Patch (this turn, my files only)

**`src/NativeRaceController.cs` — manifest-path alignment.** My controller read `GEOMETRY_CASE_MANIFEST` (my turn-1 proposal's name); the merged pipeline passes `DRIVING_VALIDATION_MANIFEST`. Core dispatches worked by coincidence (same default path); **wet/impact dispatches would have loaded the wrong manifest** (core profiles under wet weather, or a spurious "no case for car/track"). The selector order is now `DRIVING_VALIDATION_MANIFEST` → `GEOMETRY_CASE_MANIFEST` → `data/native_driving_validation_core.json`. Verified: CoreRunner PASS 88078 assertions, build 0 warnings/0 errors. No other logic touched.

## 3. TASK 1 — Ride-height decision pack (no default changes made)

### 3.1 The two configurations, exactly

| | A: current default (fielded) | B: raised candidate (already implemented, opt-in) |
|---|---|---|
| Switch | `MYSIM_NATIVE_RIDE_HEIGHT` unset / `0` (default) | `MYSIM_NATIVE_RIDE_HEIGHT=1` |
| Code path | none (preload 0) — `NativeSuspensionVehicle.Configure` skips the block | existing block in `NativeSuspensionVehicle.Configure` (my file, unchanged since before turn 1): `SuspensionPreload = max(0, targetClearance − actualSourceClearance)` per car, from hull manifest `partBounds` vs wheel contact plane |
| Parameters (existing file `data/native_suspension_profile.json`, unchanged) | not applied | `minimumStaticClearance=0.035`, `maximumPreload=0.12`, `bumpStopReserve=0.02`, `bumpStopRateMultiplier=12`; run FAILS if a car needs preload >0.12 (honest infeasibility, no silent clamp) |
| Static state (car 16 evidence) | chassis hull floor at/below road: 1785/1792 samples carry 4–10 chassis-vs-DriveSurface contacts; `underfloor` channel inactive (null) | floor raised to ≥35 mm静态 clearance; `underfloorPoints` population also activates the `underfloor` evidence channel (floor clearance measurable per tick) |
| Run-configuration delta (dispatch side, coordinator) | current DV/lap dispatches as-is | add workflow input `ride_height` → env `NATIVE_RIDE_HEIGHT` (the `MYSIM_NATIVE_RIDE_HEIGHT` setdefault mapping already exists in `cloud_game_audit.py` line 4); one yml line + one input, same pattern as `native_damage` |

Expected `NATIVE_STATIC_CLEARANCE` log line on B (preload, original clearance, target) — **the A/B dispatch pair must capture this line per car**; it is the parameter quantification (expected preload for car 16 is unknown until measured; the bound is 0..0.12).

### 3.2 Expected effect on the 9 handling targets (`data/native_handling_targets.json`)

| Target | Expected effect of +preload (floor +≥35 mm) | Band impact |
|---|---|---|
| braking_distance_from_speed | CoM rises by the preload amount → ~12-15% more static-to-dynamic load transfer at the same decel; rear lighter under braking; with brakes force-biased (29/21) and assists off, rear lock arrives earlier → stop distance drifts up marginally, stability margin down | band [0.7,1.45]×ref expected to hold; watch, do not pre-move |
| acceleration_response | more rearward transfer on launch = better RWD traction → marginally faster 0-60 | band holds (validated 2.40 s has 1.6× headroom to the ceiling) |
| steering_rate_low_speed / steering_lock_at_speed | actuator and lock curve untouched; body roll angle increases slightly for the same lateral g (higher CoM) → tracking ratios shift a few % | bands [0.9,1.1]/[0.5,1.4] hold in expectation |
| straight_line_stability | **biggest risk**: the current floor-scrape adds parasitic contact friction/damping that disappears at height B; hands-off drift may increase | 0.25 rad band stays — a failure here is REAL physics information about tyre-only stability, not a calibration error |
| kerb_and_edge_stability | floor no longer rides kerb tops; suspension/tyre response becomes the true signal (currently masked by the hull skating over kerbs) | measurement becomes MORE honest; bands unchanged |
| slip_visibility | small load-distribution shift only | unchanged |
| suspension_travel_compliance | static spring length moves with preload (idle length shifts from ~0.11 m upward); travel ENVELOPE re-baselines; compression floor (0.059 m observed) will move | metric is comparative/static-referenced — record static length per run in the report (already in `springLengths` samples); no band change |
| impact_recovery | bumpers/hull contact walls as designed instead of the floor ploughing; cleaner impact events; also removes the floor-scrape contribution to source-backend spurious contacts (ties into section 4) | bands unchanged |
| (evidence honesty) | ALL numeric evidence re-baselines | see rollout rules |

### 3.3 Rollout plan (no silent re-baselining)

1. **A/B pair, one commit, one dispatch each**: identical configs (clean-lap case, jolt/240/CCD/rotational, damage on) with `ride_height=false` (baseline reproduction) vs `ride_height=true`. Requirements on B: `NATIVE_STATIC_CLEARANCE` line captured; `underfloor` non-null with `minimumClearance ≥ ~0.03` and chassis-vs-DriveSurface contact count ≈ 0; same lap gates (finish, legal, zero impacts/damage, zero resets).
2. **Decision inputs**: B passes the gates; Δlap-time and the target shifts from 3.2 recorded (not judged); reproducibility (run each twice if time allows).
3. **Adoption = coordinator flips the pipeline default** (workflow input default or env default) in one commit labelled as the new physics baseline; my files need NO change (the code path already exists and is flag-gated).
4. **Re-runs required after adoption** (old passes stay valid *for configuration A* and are labelled superseded, never deleted):
   - Clean continuous lap on BOTH backends (the 41799538756 result describes A-only; every cross-backend comparison must be same-configuration).
   - All DV runs (core/wet/impact): accepted-configuration evidence must come from the adopted configuration. If adoption happens after 41826593001 lands, its results remain valid as A-configuration evidence and the suite re-runs under B.
   - Wet matrix Tier A: comparisons are only valid within one configuration; the already-completed wet-slick cells (41625514977, config A) must NOT be mixed with config-B cells — after adoption the matrix re-runs wholesale (the completed-cells exclusion list is configuration-tagged).
   - Grid-start reference (41630018363): preload changes settled-creep and launch dynamics; re-run under B before any grid acceptance claim.
   - Loaded-wheel/contact-plane evidence (41627667783): static geometry shifts; re-run under B before any clearance acceptance.
   - NOT re-run: settings persistence, LAN, pure-logic checks (unaffected by ride height); art/UV/fence passes are unaffected as IMAGE evidence but the car's visual stance changes — a car-assembly visual re-check of ride stance rides along with the next car evidence cycle (noted, not blocking).

## 4. TASK 2 — Per-backend impact comparison protocol (source 17 impacts vs jolt 0)

**The confound first:** the two existing datapoints differ in TWO variables at once — source/120Hz (17 impacts, damage 0.27, run 41794252920@71dfc6c) vs jolt/240Hz (0 impacts, run 41799538756@5d21c56) — and on different commits with different intermediate changes. They cannot attribute cause. The protocol below is ordered so each step isolates one variable; all runs on ONE commit (current head), same case (clean lap), damage enabled.

1. **Step 1 — 2×2 backend × rate matrix** on the clean-lap case: (source,120), (source,240), (jolt,120), (jolt,240). Decides: is the source backend's spurious-impact instability intrinsic (appears at both rates) or rate-driven (only at 120)? Is jolt's cleanliness rate-robust?
2. **Step 2 — conditional floor-scrape test**: if source fails Step 1 at both rates, repeat source+`ride_height=1` (same hz as the failure). The turn-2 finding (hull floor riding the road) is a plausible mechanical cause of solver-dependent spurious contacts; this single run confirms or kills the hypothesis. Outcome feeds section 3's adoption decision — if ride height fixes source, the backend choice and the ride-height choice are coupled and should be decided together.
3. **Step 3 — deliberate-impact fidelity on BOTH backends** (DV impact manifest, `DRIVING_VALIDATION_IMPACT=1`, damage on): the recorded deltaV distribution, damage partition and drive-away recovery... acceptance per my hardened profile (impact recorded, 0.5 ≤ Δv-rise ≤ 30, no teleport, upright, drives after). A backend that is "clean" by IGNORED contacts (missed collisions) fails here — silence must not be conflated with correctness; the deliberate-hit evidence is what proves the contact pipeline actually works.
4. **Step 4 — repeatability**: repeat the deciding runs (×2). Force-level results should be stable (the project's own calibration precedent: braking distance variation ≤0.0361% at bounded 480 Hz coupling); contact/damage COUNTS must be stable integers; a backend whose impact count varies run-to-run is not shippable regardless of the count.

**Decision rule (proposal; coordinator decides):** ship backend B only if it passes (i) zero unintended impacts/damage on clean laps at BOTH rates, (ii) deliberate-impact in-band with real recovery, (iii) no penetration artefacts beyond the standing CCD gates, (iv) repeatable counts. If both backends pass, the tie breaker is NOT mine (perf/tooling/NATIVE_LEGACY... ownership); if both fail, the failure characterisations from steps 1-3 order the FIX work — the choice is not forced.

**Regression guards (standing, after the choice):**
- Already live: `FinishAi` fails any lap when `DamageEnabled && (impactEvents>0 || damage>0)` — the zero-tolerance canary; keep the damage-enabled clean lap in the periodic rotation on the shipped backend.
- Already live: DV impact profile gates deliberate-hit fidelity per backend; keep it pinned to the shipped backend's regression set.
- Proposal (coordinator-owned, one line): `driving-validation-summary.json` gains `backgroundImpacts` (sum of `impactEventsDelta` over all runs of non-impact manifests; with damage off it is structurally 0 — the meaningful guard is the damage-ON clean lap above, so this is bookkeeping, not a new gate).
- Never valid as a guard: lap time, visual similarity, "CI green".

## 5. TASK 3 — Judgment rubric for run 41826593001 (DV core re-dispatch, 9c422a4) — ready to apply when the result lands

**A. Infrastructure preconditions (before any physics judgement):**
- Both case logs (`driving-validation-track-00-car-16.log`, `...-car-00.log` — note the manifest drives car 16 AND car 0 now) contain `NATIVE_SOURCE_BODY_COLLIDERS car=… parts=N` with N>0; no `NATIVE_COLLISION_FIXTURE_PROXY`; no `NATIVE_INITIALIZATION_FAILED`.
- Hull-bake step artefact shows BOTH cars baked from the DV manifest (the `9c422a4` fix); a car-0-only bake = the previous failure mode, fail the run.
- Run config recorded: backend (expect `source` per the failed run's `physics-backend-experiment.json` pattern — confirm), physics_hz as dispatched, `MYSIM_ASSISTS=0`, manifest = `data/native_driving_validation_core.json`.

**B. Report contract (mechanical, from `driving-validation-summary.json` + per-case reports):**
- Per case: `ok/completed/allPassed=true`; `rotationalTyres=true`; `assistsOff=true`; `runs` set == {input_direction, acceleration_0_60, braking_60_0, steering_rate_step, straight_stability, edge_excursion} (car 16) and minus edge_excursion (car 0); every run `ok=true`, `completed=true`, no `abortReason`, ≥1 assertion, `sampleCount>20`, `samplesTruncated=false`; `validationResets` = 5 (car 16) / 4 (car 0); `ruleResets=0`; `firstFailure=null`.

**C. Physics-quality reads (the numbers I will check, not just pass/fail):**
- `input_direction`: meanYawRate signs opposite and |mean| ≥ 0.05 both sides; `endSpeed:launch` inside 14.5–16.5.
- `acceleration_0_60`: duration 1.13–3.91 s (validated band); compare against the clean-lap 2.40 s reference (same backend? no — that was jolt; on source expect a delta — RECORD it as the first source-vs-jolt force-level comparison point).
- `braking_60_0` (FIRST full-brake data ever): distance vs [11.7, 24.3] m; decel min vs [−28,−12]; **if outside: do not touch the bands** — classify first (numerical transient at 120/240 Hz? brake-force caps? jolt-vs-source?) with the samples; a band change needs a written cause like the wet-decel one in turn 2.
- `steering_rate_step`: settle ratios ≈1.0 (rate-limited), tracking in [0.9,1.1], yaw signs correct, `yawRatePeakAbs < 4`, recovery < 0.35; slip channels populated (profile requires rotational).
- `straight_stability`: heading drift, lateral drift, yaw settling — first measurement of tyre-only straight stability on the ROAD frame... (with floor-scrape present, config A).
- `edge_excursion`: the off-surface grip question — compare `longitudinalAccelMin`/speed decay on/off surface; `nonSurfaceContactSamples` recorded (naming of grass nodes finally observable); suspension response over the edge.
- Global: `nonSurfaceContactSamples:all == 0` for every profile (dry, on-track); `chassisContactSamples` recorded as the A/B baseline evidence; no `positionJumpMax` ≥ 1.0 anywhere.

**D. Failure protocol (if anything fails):** preserve the report + logs; classify infrastructure (hull/bake/env) vs physics (real vehicle behaviour) vs profile-geometry (aim/timeout class); NO band edits without a written cause; no re-run until the failure class is named. A red run with clean evidence is progress; a green run with missing assertions is not.

## 6. Open items carried

- Ride-height A/B pair awaits a coordinator dispatch slot (needs the `ride_height` workflow input; one line, pattern exists).
- Backend 2×2 matrix (section 4 step 1) is the cheapest decisive next physics experiment; can run before or with the A/B.
- Wet + impact DV manifests still unrun (after core proves the pipeline).
- I have not changed any default, constant, or workflow; the only code change this turn is the manifest-path alignment in my own controller (section 2).
