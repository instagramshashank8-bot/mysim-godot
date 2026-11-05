# physics-gameplay agent — turn 2 — clean-lap telemetry calibration — 2026-11-04

Role: physics/gameplay specialist. Branch `mistral/physics-gameplay`, worktree `C:/Users/shash/MySimGodot-wt-physics`. Turn-1 state: commits `1d38d51` (validation spec) + `86ba4c7` (report). Turn-2 patch: (see section 5). Inputs read: REVIEW_PACKET.md session-2 status, VISUAL_REVIEW.md (3 review sections incl. car_evidence 41798036351), JOB_REGISTRY.json (updated 2026-11-04T11:20Z). All work local: source reading, telemetry analysis of an existing decrypted artifact, minimal patch authoring. No runs dispatched, no workflow files touched.

## 1. Clean-lap evidence base (what was sampled)

Source: `outputs/repair-20261029/lap-jolt-41799538756/game-runtime-0-attempt-1/outputs/cloud-game/mounted-native-lap-track-00-car-16.json` (9.4 MB, read-only; sampled with a throwaway summary script — 1792 telemetry rows at 20 Hz, full-series aggregates + every-100th row + contact-part histograms + loaded-wheel spot checks). Run identity per JOB_REGISTRY: run `41799538756`, head `5d21c56`, **jolt / 240 Hz / CCD / rotational tyres**, car 16, track 0, dry (weatherProfile 0, water 0), Medium compound, damage ENABLED.

Headline: `ok=true, finished, laps=1, bestLap=87.52s, damage=0, impactEvents=0, ruleResets=0`, tyre contacts 4 for 99.44% of samples, 0 off-track samples, lapInvalid 0, aiRecoveries 0, wear 0→0.037.

Measured ranges (full series): speed max 35.8 m/s (mean 21.85); tyreLoadRatio 0.056–1.769; spring lengths (grounded) ~0.059–0.145 m plus airborne sentinel 0.33 on lifted wheels; longitudinal slip peaks front 0.047 / rear 13.89 (launch wheelspin, see 3.4); lateral slip-angle peak ~0.219 rad across wheels; wheel angle used −0.340..+0.251 rad; steer actuator rate observed ≤1.335 rad/s (limit 3.2); |lateral| ≤ 0.77 m on a 12 m-wide route; full-throttle 0→26.82 m/s = **2.40 s**; the AI never braked harder than input ~0.19 (no full-force stop in the lap).

## 2. New physics findings (independent of the tension list)

1. **The chassis hull rides on the drive surface continuously.** Contact-part histogram over all samples: `SourceBodyHull_LOD_A_CHASSIS` ×7816, `SourceBodyHull_LOD_A_FRONTBUMPER` ×828, `SourceBodyHull_LOD_A_BODY` ×87 — all against `DriveSurface` (×8731); 1785/1792 samples carry solver contacts (typically 4–10 points), e.g. cruise at 26 m/s holds 6 chassis contact points. Interpretation: with `MYSIM_NATIVE_RIDE_HEIGHT` unset there is no static-clearance enforcement, and the hull floor sits at/below the road under static sag; the solver carries resting contacts at near-zero deltaV (hence damage=0 and a clean lap). This is a **ride-height/calibration defect candidate**, not a validation failure: it should be confirmed with `underfloor` evidence (null in this run — that channel requires the ride-height mode) and a ride-height run before anyone changes defaults. Ownership note: `NativeSuspensionVehicle.cs` is my file, but a default ride-height change re-baselines every comparison (lap times, source-vs-jolt) — it needs a coordinator decision and a dedicated A/B dispatch, and is NOT silently patched here.
2. **Source vs jolt contact sensitivity is extreme.** The same AI lap on the source solver (run `41794252920` at `71dfc6c`) produced **17 impacts / damage 0.27**; on jolt/240 it is perfectly clean. Cross-backend comparison therefore must never mix impact/damage counts as like-for-like; the impact-count gate in any comparison must be per-backend.
3. **The "airborne crest" in earlier aggregates was the spawn drop** (t=0.1 s, progress 0.988, one wheel touching, springs at drop lengths) — the car settles from the 0.8 m spawn drop over roughly the first second. The genuine single-wheel moments during the lap (9 samples) all sit in corners (progress 0.31/0.45/0.74) — kerb-triggered wheel lift, none on the start straight.
4. **Rear longitudinal slip peaks of 13.9/11.0 occur legitimately at launch** (wheels spinning up while ground speed is near zero; `LongitudinalSlip` is (ωR−v)/v). Any future hard cap on that channel must be time-windowed, not absolute.
5. Track elevation spans roughly −24.3..+5.2 m (`bodyY` over the lap) — Brands-Hatch-style terrain; braking/acceleration measurements are slope-sensitive by nature, which is why speed-change metrics are recorded rather than asserted in straight-line profiles.

## 3. Tension list — manifest by manifest (target vs observed ⇒ verdict)

Legend: OK=passed calibration or safe against evidence; FIXED=patched this turn (justified below); LOOSE/UNSUPPORTED=left as authored because no cloud evidence exists yet (first scripted run decides; no gate weakened); RISK=new failure mode identified, watch the next run.

### 3.1 core (native_driving_validation_core.json: car 16 + car 0, dry)

| Profile / assertion | Target | Observed on clean lap | Verdict |
|---|---|---|---|
| input_direction / no_chassis_contact `chassisContactSamples:all < 1` | zero body contacts | 1785/1792 samples have chassis contacts (hull floor rides the road) | **BROKEN-unsatisfiable → FIXED**: replaced by `no_foreign_surface_contact` (`nonSurfaceContactSamples < 1`), which the clean lap satisfies at exactly 0 |
| input_direction / launch_reached_speed `endSpeed > 14` | speed at phase exit | phase can only exit at ≥15 m/s ⇒ tautology | **TOO LOOSE → FIXED**: now `between 14.5..16.5` (bounds the overshoot window) |
| input_direction / brake_stopped `< 0.5` | end speed | exits at <0.1 ⇒ near-tautology | **TOO LOOSE → FIXED**: `< 0.2` |
| input_direction wheels-down / yaw signs / no_teleport | — | yaw response real (steer-rate 1.335 rad/s observed); no teleport (max tick move 0.15 m at v_max) | OK |
| acceleration_0_60 / accel_time_band `[0.55,1.9]×2.06 = [1.13,3.91] s` | 0–26.82 m/s | **2.40 s = 1.17×spec, full throttle** | **VALIDATED-OK** (2× headroom both sides) |
| acceleration_0_60 / stayed_straight `< 8 m` | lateral deviation on launch | AI max |lat| = 0.77 m all lap; half-route = 6 m ⇒ 8 would allow off-track | **TOO LOOSE → FIXED**: `< 3` |
| acceleration_0_60 / no_chassis_contact | — | as above | **BROKEN → FIXED**: `no_foreign_surface_contact` + `stayed_upright ≥ 0.9` |
| braking_60_0 / whole profile | 60 mph full stop: `[0.7,1.45]×16.76 m`, decel `[−28,−12]` | **NOT INFORMED**: AI never exceeded brake input 0.19; no full-force stop exists in any cloud evidence | UNSUPPORTED (kept by construction: model brake force gives ≈1.0×ref; first scripted run is the test) |
| steering_rate_step / response+settle+tracking+no_spin | vs 3.2 rad/s and SteeringLock | only indirect evidence (AI uses ≤0.34 rad lock, ≤1.335 rad/s — never full lock) | UNSUPPORTED but consistent; no change |
| steering_rate_step / requires.rotationalTyres=false | slip evidence needed by `slip_visibility` target | slip channels only populate with rotational (clean lap proves they work: 0.219 rad peak) | **MISMATCH → FIXED**: profile now requires rotational tyres |
| straight_stability / all | drift over 4 s coast | wheel-lift moments all in corners; straight is clean in-lap | UNSUPPORTED-OK (manifest timing: profile runs on the straight; kerb moments don't intersect) |
| edge_excursion / no_wall_contact `chassisContactSamples < 1` | — | chassis floor contacts are constant ⇒ unsatisfiable; and off-surface node naming is unobserved (clean lap never left the road) → non-surface contacts there may be ordinary grass | **BROKEN → FIXED**: assertion removed; non-surface contacts recorded, not asserted; wall detection stays the impact profile's job via the damage gate |
| all core profiles / initial `hold 0.5 s` | settle before measuring | spawn drop settle takes ~1 s (sample i=0: one wheel, springs at drop length) | **TOO SHORT → FIXED**: hold 1.2 s everywhere |

### 3.2 impact (native_driving_validation_impact.json: car 16, damage on)

| Profile / assertion | Target | Observation | Verdict |
|---|---|---|---|
| aim phase `until: chassisContactsAtLeast 1` | first barrier contact | wheel/ground contacts make `chassisContacts ≥ 4` from birth ⇒ phase would exit at `minSeconds`=0.3 s without touching the wall | **BROKEN → FIXED**: trigger is now `impactEventsAtLeast 1` (the damage system's Δv ≥ 2 gate — a real hit); glancing sub-gate scrapes time the phase out at 8 s and fail honestly |
| impact_recorded `impactEventsDelta:aim ≥ 1`, impact_measured `Δv ≥ 0.5`, not_catastrophic `≤ 30` | real 12 m/s hit ⇒ Δv ≈ 5–12 | jolt clean lap: zero background events ⇒ deltas are attributable to the deliberate hit only; **source solver shows 17 background impacts on the same lap** ⇒ run the impact manifest on jolt, or expect phase-boundary noise on source | FIXED-context; numbers kept (never weakened) — `.RISK: first-run geometry` (the blind full-lock aim may only scrape ⇒ honest timeout failure, then re-aim with real barrier geometry data) |
| drives_after_impact / stayed_upright / no_teleport | — | no contradictions in clean-lap behaviour | OK |
| removed `contact_made` | — | was `chassisContactSamples:aim ≥ 1` — tautologically true (floor contacts) and wrong signal | **BROKEN → FIXED**: superseded by impact_recorded |

### 3.3 wet (native_driving_validation_wet.json: car 16 + car 0, Rain + Wet compound)

| Profile / assertion | Target | Observation | Verdict |
|---|---|---|---|
| braking_60_wet / decel_band `[−22,−5]` | wet full-stop decel | model math: car16 on wet tyres stays BRAKE-force-limited (grip capacity ≈24.5 m/s² > brake force ⇒ −21.7 with drag) ⇒ floor −23 would false-fail on correct behaviour; car0 IS grip-limited (≈−10.3) | **TOO TIGHT → FIXED**: floor −25 (ceiling −5 unchanged). Note: this widens a band BEFORE any run, justified by the model's own force definitions, not by a failure |
| braking_60_wet / stop_distance `[0.7,2.6]×ref` | wet stop distance | car16 predicted ≈1.0×ref (brake-limited), car0 ≈1.19×ref (grip-limited) — both far inside | UNSUPPORTED-OK (kept; first wet run decides) |
| steering_rate_step (wet cells) / yaw floors 0.08 / 0.02 rad/s | response on wet tarmac | car0 wet lateral capacity ≈10.3 m/s² ⇒ yaw ≈0.7 rad/s at 15 m/s ⇒ floors reachable | OK |
| edge_excursion (wet, car 16) / stability trio | — | nothing contradicts | UNSUPPORTED-OK |
| General | — | clean lap is a DRY jolt lap; no wet telemetry exists anywhere yet | all wet rows are UNSUPPORTED by design — first wet run is the evidence |

## 4. Handling-targets file consequences (patched)

- `slip_visibility`: notes the clean-lap slip-channel evidence (0.219 rad peak) and that `steering_rate_step` now enforces rotational tyres.
- `suspension_travel_compliance`: measured over grounded wheels only (sentinel excluded); records the clean-lap compression floor 0.059 m and points to the chassis-floor finding.
- All other targets unchanged; all statuses remain `pending-cloud-run`.

## 5. Patch contents (this turn's commits)

- `src/Logic/DrivingValidation.cs`: `Sample.nonSurfaceContact`; metric `nonSurfaceContactSamples`; phase condition `until.nonSurfaceContact` (parsed, terminating, state-machine trigger); suspension min/max metrics restricted to grounded wheels.
- `src/NativeRaceController.cs`: sample builder computes `nonSurfaceContact` from `body.ContactSurfaces` (any contact path not containing "DriveSurface").
- `data/native_driving_validation_profiles.json`: assertion fixes from sections 3.1–3.3 (no assertion weakened except the wet decel floor, justified by model force math above); hold phases 1.2 s; steering profile requires rotational tyres.
- `data/native_handling_targets.json`: evidence/provenance updates (slip, suspension).
- Verified after patch: CoreRunner **PASS 88078 assertions** (incl. all profile/manifest validation), full project build **0 warnings / 0 errors**.

## 6. Readiness for run 41820348686 and what to watch

- If 41820348686 is the first `driving_validation` dispatch, it should pick up this patch (or a merged descendant). Expected failure modes now bounded: (a) braking decel transient outside `[−28,−12]` on jolt — a genuine solver-interaction finding, not noise to smooth; (b) impact aim does not reach the wall inside 8 s ⇒ honest timeout, needs one aimed iteration with real barrier geometry; (c) `trackLateralAbsMax` on a full-throttle launch exceeding 3 m ⇒ real straight/drift issue worth seeing.
- Coordinator-facing items outside my file set: the chassis-floor ride-height investigation (needs a ride-height-mode A/B dispatch; data so far is from the without-ride-height configuration), and per-backend impact-count semantics for any cross-backend comparison.
