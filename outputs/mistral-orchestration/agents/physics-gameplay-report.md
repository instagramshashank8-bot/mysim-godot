# physics-gameplay agent report — 2026-11-04

Role: physics/gameplay specialist. Branch `mistral/physics-gameplay` (worktree `C:/Users/shash/MySimGodot-wt-physics`), parent `c87ae00`. Commit `1d38d51` — 9 files, only owned paths (`src/Logic/*.cs`, `src/NativeRaceController.cs`, `data/native_*.json`, this report). No other files touched; no workflow dispatched; no local engine/render/asset processing (local work = editing + `dotnet build`/CoreRunner compile-and-logic checks only).

## 1. What changed

### Mission 1 — controlled continuous-driving validation spec (data + audit hooks)

- **`data/native_driving_validation_profiles.json` (new)** — 8 scripted scenario profiles, each with phases (timed or condition-terminated: speed thresholds, chassis-contact, impact-event, off-track), abort guards (max duration, lateral excursion, position jump) and fail-closed assertions:
  1. `input_direction` — signed yaw consistency for both steer signs at the native layer, throttle/brake effect, no inversion, no teleport.
  2. `acceleration_0_60` — full-throttle 0–26.82 m/s; time banded against `RecoveredVehicleProfiles.Acceleration[car]`.
  3. `braking_60_0` — full-service stop from 60 mph, dry; distance banded against `RecoveredVehicleProfiles.Brakes[car]*0.3048`; decel band; straightness.
  4. `braking_60_wet` — same under the case's weather/compound with a grip-honest wider band (documented).
  5. `steering_rate_step` — full-lock steps at 15 and 30 m/s, both directions: response onset, settle time vs the 3.2 rad/s actuator rate, achieved lock vs `Dynamics.SteeringLock(speed)`, no spin, recovery after centring.
  6. `straight_stability` — hands-off coast at 30 m/s: heading/lateral drift, yaw settling (mirrors the existing `maximumYaw<0.15` precedent).
  7. `edge_excursion` — controlled off-surface excursion and recovery; records the grip/suspension response (see finding F1).
  8. `impact_wall_recovery` — controlled barrier contact with damage enabled: impact event + DeltaV recorded, no teleport/rescue, drive-away recovery.
  Each profile resets the car to the same spawn between profiles (mechanical rig reset, never during measured phases), so every profile gets the full start straight.
- **`data/native_driving_validation_core.json` / `_wet.json` / `_impact.json` (new)** — case manifests in the existing `data/native_*.json` pattern (`car`/`track`/`weather`/`compound` + new `drivingValidation: [profile ids]`). One case per (car,track) per manifest (matches the harness's case-selection contract). Wet manifest changes ONLY weather+compound vs core. Impact manifest is separate so damage stays off elsewhere; impact profile must run last (enforced by a CoreChecks rule).
- **`src/Logic/DrivingValidation.cs` (new, ~450 lines, Godot-free)** — pure-logic engine so `tools/CoreRunner` (core.yml) compiles and tests it without the engine: profile/manifest parsing with fail-closed validation (unknown metric/op/phase/reference ⇒ throw, never silently pass), the per-profile state machine ( samples in → command out), ~30 metrics (brake distance, decel extremes, steer response/settle/tracking, yaw stats, slip peaks, grounded fraction, suspension travel range, impact DeltaV, position-jump/teleport detector, off-track fraction, upright cosine), assertion evaluator (`between`/`betweenReference` with factor bands against named anchors, `lt/lte/gt/gte`), and the multi-profile runner (reset requests, abort propagation, failure accounting). Metrics read dt from sample timestamps so decimated retention stays valid.
- **`src/NativeRaceController.cs` (modified, +93/−1)** — thin glue only:
  - `MYSIM_DRIVING_VALIDATION=1` selects validation mode: per-tick sample building from the live `NativeSuspensionVehicle` (speeds, heading/yaw, wheel angle + target via `Dynamics.SteeringTarget`, loads, spring lengths, slip channels, contacts, water, impact counters), scripted command applied to `Player.input` after `Race.Advance` (overriding the AI) and before `body.BindRaceControls`; mechanical reset between profiles via the existing `PlaceBody` (counted as `validationResets`, distinct from `ruleResets`); completion writes `MYSIM_DRIVING_VALIDATION_REPORT` and exits 0/1. Session end/failure paths abort honestly with the reason; existing lap/other paths are untouched (no gate weakened — validation mode is a separate mode with its own PASS/FAIL line `PASS/FAIL driving validation scenarios`).
  - `MYSIM_ASSISTS=0` override turns traction control and brake-stability assist off for physics acceptance (default behaviour unchanged; recorded as `assistsOff` in the report).
  - Report metadata records physicsHz, recordFps, rotationalTyres, assistsOff, damageEnabled, backend (env if present), weatherProfile/water/compound, simulatedSeconds, validationResets, ruleResets, per-run metrics/assertions and the full per-tick sample arrays (capped, with `samplesTruncated` flag).
- **`src/Logic/CoreChecks.cs` (modified, +81)** — headless guards that run on every push via core.yml: all data files parse; every profile referenced by the three manifests exists; (car,track) uniqueness per manifest; impact-last ordering; wet-matrix cell validity + the completed wet-slick cells excluded; handling targets reference real profiles; metric maths verified against synthetic telemetry (constant-deceleration brake distance = v²/2a within 0.2 m); assertion evaluator in-band/out-of-band/missing-data behaviour; four fail-closed parsing rejections; scripted state-machine completion. **These guards already caught and fixed two real defects before any cloud run** (an unimplemented metric name used by the profiles, and an empty-data metric returning 0 instead of null).

### Mission 2 — dry/wet comparison matrix

- **`data/native_wet_comparison_matrix.json` (new)** — 6 executable cells (cars 16 and 0 × dry/overcast/wet-wet-tyre on track0) with controlled variables recorded in the file (same car/track/laps/AI mode/rivals; only weather+compound vary), the already-completed Rain2/Medium1 cells deliberately EXCLUDED from the executable list (recorded under `alreadyCompletedCells` with run 41625514977 as evidence — re-running them would violate the no-duplicate rule), and direction-only `expectedOrderings` for the analyst (lapTime dry < wet-wet < wet-slick; overcast ≈ dry since initialWater is 0 in both — a visual-only control; water gates per cell). Runs under the EXISTING `native_geometry_recovery` named-case depth once wired (Tier A below); comparisons are computed from `mounted-native-layout-results.json`, machine-unenforced by design (sensitivity evidence, not calibration).

### Mission 3 — Monoposto-derived measurable targets

- **`data/native_handling_targets.json` (new)** — 9 measurable targets translating the purchased-reference review categories (predictable responsive handling; coherent silhouette; visible tyre-slip smoke; camera-roll-over-kerbs with recoverable car) into metrics, profiles and acceptance bands. Every target's numeric anchor is from THIS project (recovered profiles, the model's 0.12 rad slip knee, the existing native grid/straight gates) with an explicit `referenceBasis`; the file states plainly that the purchased title's physics data is unknown and none is claimed. All statuses `pending-cloud-run`; none accepted.

## 2. How it was verified (evidence)

- `dotnet run --project tools/CoreRunner` (pure logic, no engine): **PASS 88078 assertions**, including the new `PASS driving validation scaffolding: profiles, manifests, matrix, targets, metric maths and fail-closed parsing`. This is the same harness core.yml runs on push.
- `dotnet build MySimGodot.csproj`: **0 warnings, 0 errors** (compile proof only — Godot runtime behaviour is NOT locally verified; no engine ran locally).
- NOT yet verified (by design, cloud-only): actual in-engine scenario execution, governor behaviour on the real track0 straight, report contents from a live run. Nothing here claims driving evidence.

## 3. Exact proposed dispatch configs (coordinator dispatches; local run configs proposed)

### Tier A — wet comparison matrix (existing depth; 2-line wiring)

Wire in `game-review.yml` (coordinator-owned):
1. Add `wet_comparison_matrix` to the `case_set` choice options.
2. In the `GEOMETRY_CASE_MANIFEST:` mapping add: `inputs.case_set == 'wet_comparison_matrix' && 'data/native_wet_comparison_matrix.json' ||`
3. REQUIRED correction in `tools/cloud_game_audit.py` line ~115 — the current `result['water']>0` fails every dry/overcast cell (their water is 0 by construction). Replace with the exact-water check (strengthens, does not weaken): `ok=ok and result['weatherProfile']==case['weather'] and result['tyreCompound']==case['compound'] and (result['water']>0)==(case['weather'] in (2,3))` — note: also covers the Fog profile (initialWater 0.1) consistently.

Dispatch (first pass):
```
workflow: game-art-and-runtime-review (game-review.yml)
depth: native_geometry_recovery
case_set: wet_comparison_matrix
cars: default (cases drive cars 0 and 16)
physics_backend: source
physics_hz: 120
rotational_tyres: true
record_native_race: false
native_damage: false
```
Expected artifacts (completion contract): `mounted-native-layout-results.json` (+ per-case `mounted-native-lap-track-00-car-XX.json` reports and logs) with every row `ok:true`; `weatherProfile`/`tyreCompound`/`water` matching each cell. Repeat later with `physics_hz: 240` for rate robustness. All four wet-slick cells already covered by run 41625514977 — do not re-run.

### Tier B — driving validation (new depth; wiring spec provided)

Wire in `game-review.yml` (coordinator-owned):
1. Add `driving_validation` to the `depth` choice options.
2. Add to the `case_set` options: `driving_validation_core`, `driving_validation_wet`, `driving_validation_impact`; extend `GEOMETRY_CASE_MANIFEST:` mapping: `inputs.case_set == 'driving_validation_core' && 'data/native_driving_validation_core.json' || inputs.case_set == 'driving_validation_wet' && 'data/native_driving_validation_wet.json' || inputs.case_set == 'driving_validation_impact' && 'data/native_driving_validation_impact.json' ||`
3. Shard mapping: `inputs.depth == 'driving_validation' && '[0]'` (all cases are track0 → shard 0).
4. New env lines in the runner env block: `NATIVE_DRIVING_VALIDATION: ${{ inputs.depth == 'driving_validation' && '1' || '0' }}` and a new optional boolean input `assists_off` with `ASSISTS_OFF: ${{ inputs.assists_off && '1' || '0' }}`.

New branch in `tools/cloud_game_audit.py` (coordinator-owned; provided verbatim, placed with the other top-level depth branches):
```python
if os.environ.get('NATIVE_DRIVING_VALIDATION')=='1':
  cases=json.loads(pathlib.Path(os.environ.get('GEOMETRY_CASE_MANIFEST','data/native_driving_validation_core.json')).read_text())['cases']
  shard_cases=[c for c in cases if c['track']%12==shard]
  rows=[]
  for case in shard_cases:
    label='driving-validation-car-%02d-%s'%(case['car'],'-'.join(case['drivingValidation'])[:60])
    report=out/(label+'.json')
    values={'MYSIM_TRACK':str(case['track']),'MYSIM_CAR':str(case['car']),'MYSIM_NATIVE_GAMEPLAY':'1','MYSIM_AI':'1','MYSIM_AUTODRIVE':'1','MYSIM_SECONDS':'400','MYSIM_RIVALS':'0','MYSIM_DRIVING_VALIDATION':'1','MYSIM_DRIVING_VALIDATION_REPORT':str(report),'MYSIM_NATIVE_GAMEPLAY_CHECKS':'0'}
    if 'weather' in case:values.update(MYSIM_WET=str(case['weather']),MYSIM_STARTING_COMPOUND=str(case['compound']))
    if os.environ.get('ASSISTS_OFF')=='1':values['MYSIM_ASSISTS']='0'
    text=run(label,values,['--headless'],timeout=1800)
    data=json.loads(report.read_text())
    ok=data['ok'] and data['completed'] and data['allPassed'] and all(run['ok'] for run in data['runs']) and 'PASS driving validation scenarios' in text and 'NATIVE_COLLISION_FIXTURE_PROXY' not in text
    if 'weather' in case:ok=ok and data['weatherProfile']==case['weather'] and data['compound']==case['compound']
    rows.append({'car':case['car'],'track':case['track'],'ok':bool(ok),'profiles':case['drivingValidation'],'report':data})
    (out/'driving-validation-results.json').write_text(json.dumps(rows,indent=2))
  assert rows and all(r['ok'] for r in rows),'See preserved driving-validation failures; no gate weakened'
  print('PASS driving validation shard',flush=True);sys.exit(0)
```
Notes: `MYSIM_NATIVE_GAMEPLAY_CHECKS` must be '0' here (its pause-at-5s phase machine would freeze the scripted scenarios); `rotational_tyres`/`native_damage`/`physics_hz` flow through the existing `NATIVE_*` setdefault mapping; explicit expected-artifact contract (`driving-validation-results.json` + per-case reports) per the wrong-mode lesson in feedback.md.

Dispatches (in order; each is one run):
```
A) core dry:      depth=driving_validation case_set=driving_validation_core   physics_backend=source physics_hz=120 rotational_tyres=true  assists_off=true native_damage=false
B) wet:           depth=driving_validation case_set=driving_validation_wet    physics_backend=source physics_hz=120 rotational_tyres=true  assists_off=true native_damage=false
C) impact:        depth=driving_validation case_set=driving_validation_impact physics_backend=source physics_hz=120 rotational_tyres=true  assists_off=true native_damage=true
D) rate variant:  A again with physics_hz=240 (compare metric drift 120 vs 240)
E) jolt baseline: A again with physics_backend=jolt (first jolt comparison on identical scripted scenarios; compare driving-validation-results.json metric-by-metric; backend recorded in-report and in physics-backend-experiment.json)
```

## 4. Findings and remaining risks

- **F1 (expected finding, by design): the native model applies NO off-surface grip reduction** — the legacy model multiplies grip by 0.62 off-road (`Dynamics.Integrate`), `NativeSuspensionVehicle._IntegrateForces` does not (`mu=tyres[i].Grip(Water)*1.18*GripMul(CarIndex)` regardless of surface). `edge_excursion` will measure this directly; if grass grip equals tarmac grip that is a real physics defect to fix in a follow-up (data-driven off-surface factor), not something to hide.
- **F2: `MYSIM_NATIVE_RECORD_FPS` only accepts 15** (`NativePhysicsClock` throws otherwise). Recorded validation/visual runs are 15 fps; anything describing smoother captured motion needs that constant revisited (not changed here — out of my files' scope).
- **F3: scenario geometry assumption.** Profiles assume the track0 start straight fits ~120 m bursts from the spawn. Abort guards make a too-short straight an honest recorded failure (with full telemetry), not a crash; if that happens the fix is per-profile `maxSeconds`/speed tuning in the profiles JSON, not weaker assertions.
- **F4: the 3.2 rad/s steer actuator rate** now appears in two places (`NativeSuspensionVehicle` and the `steerActuatorRate` reference fed by `NativeRaceController`). Kept in sync by review; a shared constant would be the cleaner follow-up (not done to keep this diff minimal).
- **F5: backend attribution.** The game process does not receive the backend selection (it is applied to `project.godot` and recorded in `physics-backend-experiment.json` by `cloud_native_backend.py`); the validation report records `MYSIM_PHYSICS_BACKEND` only if present. Cross-backend comparisons must pair reports with that artifact from the same run.
- **F6: mid-profile rule resets** (e.g. stuck-vehicle recovery) would appear as a position jump and fail the profile honestly; `ruleResets` is now in the report to disambiguate.
- Assumption I did not validate: that model-coordinate steer command sign produces the yaw sign asserted in `input_direction` (+command → +yaw rate). It follows from `forward.Rotated(up,WheelAngle)` and `heading=atan2(forward.X,forward.Z)`, and screen-side mapping is separately covered by the existing CoreChecks convention test — but the first cloud run is the actual proof; a failure there is a recorded finding, not a framework error.
- Local environment note: the sandbox shell was missing `ProgramFiles`/`ProgramFiles(x86)`/`ProgramData`/`TMP`, which broke nuget restore until set explicitly — CI is unaffected.
