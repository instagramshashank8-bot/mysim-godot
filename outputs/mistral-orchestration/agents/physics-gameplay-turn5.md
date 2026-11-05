# physics-gameplay agent — turn 5 — stuck-at-spawn diagnosis + ride A/B rubric — 2026-11-04

Role: physics/gameplay specialist. Branch `mistral/physics-gameplay` (worktree at `7116def` + this turn's patch commit). Inputs: dv3-41834771421 artifacts (local), coordinator lineage `3061afa` → `9c422a4` → `39cdf33` → `95f69c6` (read from the local object store), turn-2 clean-lap analysis. 41840755243 was still in flight when this was written — the diagnosis below is the evidence-based decision tree the trace resolves; the concrete readout gets appended the moment the run lands (artifacts are`dv3-*`-style directory or the coordinator's paste).

## 1. What is already PROVEN from the failed runs

| Run | Boot | Outcome | Established fact |
|---|---|---|---|
| 41828824418 | HUMAN session (no MYSIM_AI) — DV python turned the race start on with no AI activation | car motionless at the grid, throttle commanded full for the whole session, grid hold never released | **Stage-1/2 failure class CONFIRMED once**: with the human boot, the race start sequence and the validation runner's time-based command schedule conflict (the validation timeline commanded full throttle ~1.2 s in, during the grid sequence). Coordination bug between two state machines — resolved by `39cdf33` (boot DV as the proven lap runs do: `MYSIM_AI=1`, `MYSIM_AUTODRIVE=1`, `MYSIM_PRECISE_AI=3`; the validation input then overrides the auto-driver, and the AI boot skips the countdown) |
| 41834771421 | AI session (`NATIVE_AI_LAUNCH_FAILURE` printed ⇒ AiTest=true ⇒ grid released at t=0), hulls baked (23 parts), assists off (my `ASSISTS_OFF` line), 120 Hz, source backend | car NEVER moved: at t=30 (sim), `bodyContacts=2 tyreContacts=1`; **NO validation report was written** (the launch-failure path called `FinishAi`, which writes `MYSIM_NATIVE_GAMEPLAY_REPORT` — unset in DV — so the per-tick telemetry died with the process) | **A second, physical failure class**: this is NOT the grid any more — the start sequence fired (grid released) and the car still could not move. The evidence gap (no report) is patched this turn (section 3) |
| 41840755243 (in flight) | AI boot + `DV_TRACE` every ~1 s (`t/started/gridHold/spd/thr/brk/stage/clock`) + `gridHoldTraceSeen` in the validation report | pending | designed to discriminate the frozen stage |

Headless timing note (so nobody mis-reads the logs): the failed session covered 30 simulation seconds inside ~9.7 wall seconds — headless Godot runs unthrottled, so sim-time ≫ wall-time is normal; the 1800 s wall budget is ample for `MYSIM_SECONDS=900`.

## 2. The freeze classifier for 41840755243's DV_TRACE (decision table)

Read the lines in order; the FIRST matching row assigns the stage and the owner:

| # | DV_TRACE signature (every 1 s line) | Stage frozen | Owner of the minimal fix |
|---|---|---|---|
| 1 | `started=False` on every line (and `gridHold=True`); `gridHoldTraceSeen=true` in the report | **race-start never fired / grid hold stuck** — start sequencing again (despite the flag fix: check the run actually dispatched with `MYSIM_AI=1`; a pipeline revert would reproduce 41828824418) | coordinator — start sequencing + dispatch env (not my file: the start release lives in the pipeline's boot flags + Race countdown) |
| 2 | `started=True, gridHold=False, spd=0.00` but `thr=0.00` while `stage=launching` (clock ≥ 1.2 s) | **command path dead** — the validation runner's command is not reaching the applied controls | mine — `NativeRaceController` override/`BindRaceControls` chain; the report's per-tick samples (now written on failure, section 3) will show applied vs commanded |
| 3 | `started=True, gridHold=False, thr=1.00, brk=0.00, spd≈0` from `stage=launching` until the guard fires | **thrust path physically dead** — the car has no effective drive force | mine — physics side; see the mechanism analysis below; the discriminating data is in the new on-failure report (wheel Omega + grounded mask per tick) |

**My primary mechanism hypothesis for case 3 (fits both observations: 1 grounded wheel, zero motion, source/120):** the spawn drop (0.8 m, chassis-floor lower than the tyre-contact plane — the turn-2 floor-scrape finding) lands the car ON ITS FLOOR HULL; in this resting pose the suspension ray support length lands outside the support envelope for 3 of 4 wheels (`tyreContacts=1`); the unsupported rear wheels then free-spin under drive torque (`RotationalTyreContact.Airborne` revs them), and because the engine force is power-capped by wheel speed (`power/max(motorSpeed,6)` with `motorSpeed` = rear wheel Omega when rotational tyres are on), the thrust collapses toward zero exactly when the wheels spin — a self-reinforcing stuck state: spinning wheels → weak force → no motion → wheels stay unloaded → keep spinning. The clean lap (jolt/240) escaped because its drop settled all four wheels into support within the first second (its sample 0 → sample 1 transition).

If the trace shows case 3 AND the on-failure report shows rear `wheelOmega` ramping while `groundedMask` stays ≤ 2 for 30 s, the hypothesis is CONFIRMED and the minimal fix is **not** in the drive-force code at all — it is the spawn/ride geometry:

- **Preferred (mine, profile-level, physics untouched):** make the validation `hold` phase condition-based instead of time-based: `until: {supportedWheels: 4}` with `maxSeconds: 8` — the rig refuses to start measuring (and refuses to command throttle) until all four wheels carry support; if support never arrives the phase times out and the report names the geometry state. Zero new physics constants.
- **Alternative (mine, controller):** lower the spawn drop (`PlaceBody` 0.8 m → ~0.15 m above the support envelope) so the drop cannot overshoot into a floor-rest state; weaker, because it tunes a magic height per backend.
- **Structural (ride-height decision, section 4):** configuration B (raised floor) removes the floor-rest pose entirely — the A/B pair and this failure share one root cause; if B is adopted the case-3 signature should disappear.

What I deliberately do NOT touch: the power-cap or wheel-spin physics (changing thrust behaviour to rescue a spawn artifact would corrupt every legitimate measurement the rig exists to make).

## 3. Patch this turn (mine, committed): evidence closure on the failure path

`src/NativeRaceController.cs`: when the AI-launch-failure guard fires while a validation session is active, the session now **writes its validation report first** (`validation.Abort("AI launch failure at t=30: totalDistance=… bodyContacts=… tyreContacts=…")` + `FinishValidation()` → exit 1) instead of bypassing to `FinishAi` (which wrote nothing under DV env). Result: every future stuck-or-slow session leaves behind the full per-tick evidence — `groundedMask`, rear `wheelOmega`, spring lengths, applied commands, phase timeline, `gridHoldTraceSeen` — instead of a one-line error. Non-DV runs keep the exact previous behaviour (`validation==null` → `FinishAi(false)` untouched).

Suggested (optional, coordinator file) trace extension for the NEXT instrumented run: append `applied=body.Controls.throttle` + `grd=`(supported wheel count) + `wo=`(rear wheel Omega) to the `DV_TRACE` line — moves stage-3 diagnosis from "after the run" to "from the log". My side already records all three in the report once the failure path fires.

## 4. RIDE-A/RIDE-B interpretation rubric (the pair proves the ride fix without breaking the 9 targets)

Scope note: RIDE-A = current default (floor at/below road; underfloor channel inactive), RIDE-B = `MYSIM_NATIVE_RIDE_HEIGHT=1` (35 mm static clearance; `underfloor` live). Both legs on ONE commit, same depth/case, same backend/rate as each other. Reference numbers for A come from the clean lap (`41799538756`, jolt/240) and the failed DV boot (source/120).

**Group 1 — the ride fix itself (B must show these; A anchors them):**

| Metric | A (expected/observed) | B pass criterion |
|---|---|---|
| `NATIVE_STATIC_CLEARANCE` log line | absent / preload 0 | present with 0 < preload ≤ 0.12 (record the exact value per car — it feeds every later comparison) |
| `underfloor.minimumClearance` (per-tick evidence) | null (channel inactive) | non-null for the whole session and ≥ ~0.03 m at static, dipping under load but never ≤ 0 for consecutive samples (transient < 0 during a hard kerb/impact hit = record + explain, not auto-fail) |
| chassis-vs-DriveSurface contact samples (the clean-lap derived metric) | 1785/1792 (99.6%) | ≈ 0 overall; tolerated only as rare transient corner/bump excursions (<1% of samples, never sustained) |
| Damage/impacts on the clean lap | jolt: 0; source: 17 (`41794252920`) | jolt stays 0; **source count dropping vs 17 would confirm the floor-scrape contribution** — feeds the backend protocol, not itself an acceptance |

**Group 2 — the 9 targets hold (DV core battery re-run under B; judge against the same hardened bands):**

| Target | B expectation vs A | Verdict rule |
|---|---|---|
| braking_distance_from_speed | small increase (higher CoM → more transfer, earlier rear lock with assists off) | stays inside [0.7,1.45]×ref; report Δ(A→B); Δ > +10% = investigate transfer, do not widen the band |
| acceleration_response | equal or slightly better (rearward transfer aids RWD) | inside [1.13,3.91] s; report Δ |
| steering_rate_low_speed / steering_lock_at_speed | mechanism untouched | same bands ([0.5,1.4] settle ratio, [0.9,1.1] tracking); any shift beyond ±0.05 = solver interaction worth a look |
| straight_line_stability | **highest risk**: floor-scrape parasitic damping disappears → drift may rise | the 0.25 rad band RETAINS: a B-only failure is a REAL tyre-only stability finding — fix by legitimate means (damping/geometry), never by re-lowering the car back onto its floor |
| kerb_and_edge_stability | becomes more honest (no floor bridging kerb tops) | recorded; compare wheel-lift/against-A qualitatively |
| slip_visibility | unchanged channels | same 0.06 rad presence criterion |
| suspension_travel_compression | static length shifts by the preload; envelope re-baselines | judge TRAVEL RELATIVE TO THE RUN'S OWN STATIC length (samples carry it); absolute length comparisons across A/B are meaningless |
| impact_recovery | bumper-first contacts, cleaner Δv | same band [0.5,30] |
| (report/contract) | `gridHoldTraceSeen=false`, full phase coverage | infrastructure preconditions from the turn-3 rubric all apply |

**Verdict grammar for the pair:** the ride fix is PROVEN iff every Group-1 criterion passes AND Group-2 has no unexplained target regression AND the visual stance change is accepted by the car/visual owners (ride height changes the car's appearance — their cycle, noted in turn 3). Any Group-2 failure on B defaults to "real defect now measurable", not "revert the ride height". If B fails the straight-stability band, the decision input is the drift VALUE vs A — a small degradation is the honest cost of removing the parasitic contact; a large one is a stability defect to fix separately.

## 5. Carry-forward

- Awaiting 41840755243: apply the section-2 table to the DV_TRACE lines + the on-failure report (the new patch guarantees the report exists even on this failure class), append the concrete readout to this file, and name the fix owner per the table.
- RIDE-A/RIDE-B and the backend 2×2 stay queued behind the P1 pair per the coordinator's ordering; the rubric above is ready to apply the moment they land.
- The turn-3 open items stand (ride-height A/B input wiring; backend matrix; wet + impact DV manifests after core is green).
