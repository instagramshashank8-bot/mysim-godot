# Owner-battery consolidated reading card — per artifact drop — 2026-11-04

One checklist, applied mechanically per run/artifact; consolidates the DV-core sheet (`d228674`), the wall sheet (`64b5593`) and the track-8 lapValidity rules. No dispatches; no acceptance claims without verified terminal states; forks/head line: corridor fix `b538efd` merged, axle candidate wired, owner-side dispatch only.

## 0. Global rules (apply before EVERY reading)

1. Read ONLY artifacts of a COMPLETED run (`status=completed`); queued/running = "verified wait", never progress.
2. The expected-artifact inventory per run class must be complete BEFORE reading (the wrong-mode lesson): any missing file = the drop fails its inventory gate (see the per-command tables).
3. Never an identical re-dispatch: every red verdict names a fingerprint class + a distinct fix owner before anything re-runs.
4. Charts refresh from VERIFIED TERMINAL STATES only.
5. Any bound breach = the values quoted verbatim into the ledger (actual vs expected); nothing tuned at judgement time.

## 1. Command: DV core (legs: jolt/240 first, then the source/240 pair)

Artifacts (inventory gate): `driving-validation-track-00-car-16.json`, `…car-00…` (or the case set the dispatch names), `driving-validation-summary.json`, both case logs, `physics-backend-experiment.json`.

| Step | Check | PASS | Fail path |
|---|---|---|---|
| 1.1 | `backend=jolt` / `NATIVE_PHYSICS_HZ=240` / `ASSISTS_OFF` line | per dispatch inputs | infrastructure |
| 1.2 | Hulls + `HUB_POSE_RESTORE`: car16 2 wheelguard lines, car0 4 caliper lines; no `NATIVE_COLLISION_FIXTURE_PROXY` | present | infrastructure (older bake lineage) |
| 1.3 | DV contract | `PASS driving validation scenarios`, exit 0, summary `ok:true`, per case `ok/completed/allPassed`, coverage exact (car16: 6, car0: 5), every run ok/no-abort/≥1 assert/>20 samples/not truncated, `validationResets` 5/4, `ruleResets=0`, `gridHoldTraceSeen=false` | infrastructure / contract |
| 1.4 | Freed car | trace: `started=True` line one, `gridHold=False` throughout, `thr=1 → spd rising` at launch, stage column marches, **no sustained spd=0/`tyreContacts<4` while thr=1**; `chassisContacts` at the floor-graze baseline; distance > 0 per profile | the freeze class returned → STOP, samples to physics |
| 1.5 | Nine bands | accel [1.13,3.91] s; brake [11.73,24.30] m + decel [−28,−12]; steer response <0.2 s, settle [0.5,1.4], tracking [0.9,1.1], yaw signs + peaks <4, recovery <0.35; stability 0.25 rad / 3 m / 0.15 rad/s; edge (car16) 0.5/2.5/0.9/>5; grounded ≥0.9 (0.95 braked/coasted); jump <1.0 | profile-geometry (mobile aimless) / physics (band breach, samples attached) |
| 1.6 | Verdict writing | jolt leg GREEN = chart-eligible; record every actual as the jolt baseline | — |
| 1.7 | Source pair | same bands VERBATIM; tendencies recorded (impulse [0.8,1.25]×, force [0.5,2.0]× = guidance); source-only breach = a backend finding (the choice is the coordinator's) | same fingerprints, source-labelled |

## 2. Command: wall battery (jolt leg, then the source pair)

Artifacts: the DV harness files for the `driving_wall` set (`wall_contact_low/high/graze` + `impact_wall_recovery`), the summary, case logs, backend JSON + `MYSIM_NATIVE_DAMAGE=1` evidence.

| Step | Check | PASS | Fail path |
|---|---|---|---|
| 2.1 | 1.1–1.4 preconditions + freed car (the wall profiles run the same session mechanics) | as above | infrastructure |
| 2.2 | Per wall profile: `wall_touched ≥1`; `impact_recorded ≥1` (low/high/impact profiles); `maxDeltaVRise` [1.5,20] (impact profile [0.5,30]); **impulse identity [1.5,16]×mass (graze [0,16])**; jump <2; drive-away >0.5; upright ≥0.7 | all rows | X-class breach (identity/jump) = CRITICAL, samples verbatim to the coordinator; G-class aim miss (mobile, no contact) = a NEW aim geometry attempt; S-class Δv<2 graze = a higher-approach profile version; R-class = recovery forensics |
| 2.3 | Record-only reads | `impactTickPeakDeltaV` / `impactPeakForceEstimate` (order 0.2–2.1 MN at these approaches) / `impactContactSeconds` / the restored-guard participation (wheel-band contact part names now include WHEELGUARD only if genuinely at that band — the audit lines decide) | — |
| 2.4 | Source pair | same bands verbatim; event-count nuance (>20 in one aim phase flags the backend decision); agreement guidance as in 1.7; inputs identical (manifest revision + dispatch recorded) | source-labelled findings |
| 2.5 | Verdict | all four profiles' rows green on jolt (+ source rows compared) → VERIFIED; charts refresh with the verified terminal state | — |

## 3. Command: track-8 rerun (corridor fix verification, car13/track8 lap case)

Artifact: `mounted-native-lap-track-08-car-13.json` (+ log) — the `lapValidity` block decides:

| Case | Reading | Verdict / next action |
|---|---|---|
| `invalidRows==0 && bestLap>0` (+ finished/laps/distance gates pass) | the corridor fix VERIFIED by the strict gate itself; `maxAbsLateral` recorded vs `routeHalfWidth=6` (the corridor margin) | VERIFIED; chart-eligible |
| `invalidRows>0` | the exit trace (`firstInvalidProgress/Lateral/Time` + `maxAbsLateral`) = the corridor finding with exact coordinates | NOT verified; a distinct corridor/data fix, then a new rerun |
| `invalidRows==0 && bestLap==0` on a finished lap | the timing fingerprint (by construction impossible from validity) | escalate to physics immediately |
| `bestLap>0` despite `invalidRows>0` | a mid-run restart: a clean counted segment exists | judge the VALID segment; the pre-restart rows recorded as context |
| `finished=false` | launch/retirement failure class (not this sheet's scope) | infrastructure / the launch-failure evidence path |

## 4. Command: axle arms (wheel_fit_review with `source_axle_fit=true`)

- Artifact inventory: `-source-axle.json` per pass present = the fit actually ran (the turn-5 lesson: without it the sweep measures the raw pivots and the verdicts are void).
- The verdicts are car-assembly's (their per-wheel worst tables + `STANCE`/`PART_HUB_OFFSET` lines); the physics intersection: the `STANCE` no-hubs value should hold near the measured ~0.025 class (the ride gate's datum integrity while the axle candidate moves anchors) — a large drift = a physics intersection finding before any acceptance.
- No re-baselining without a verified fit: if the fit moves the recovered anchors, ALL stance/geometry numbers re-baseline (the corridor/lap/standards sheets re-checked on the next runs).

## 5. Command: windows userpath run (the exported player, the 9-profile suite, `MYSIM_USERPATH=1`)

The userpath leg is the P0 steering-feel validation END-TO-END: the DV profiles command through the REAL human chain (keyboard smoothing → directional flip → pedal latch → lock → actuator), not the direct DriverInput injection. Same manifests, same bands — they now measure the whole chain.

| Step | Check | PASS |
|---|---|---|
| 5.1 | Preconditions | the DV-core/wall preconditions + **the report's `userPath:true`** (injected direct = the wrong mode: the leg is void, re-run in userpath mode) |
| 5.2 | Chain-inclusive steering authority (at the WHEEL-ANGLE level, from the report samples + assertions) | a 1 Hz correction sine reaches ≥ **0.90×lock** (measured 1.00); a 2 Hz correction sine ≥ **0.65×lock** (measured 0.81); keyboard step → full front-wheel lock ≤ **0.30 s** (measured ~0.19); release decay ≤ **0.30 s** (measured 0.19) |
| 5.3 | Progressive feel | partial keyboard hold = proportional partial lock (the ramp property; the windows evidence records at least three partial-hold points) |
| 5.4 | Reverse-pedal semantics | the brake pedal latches reverse within ≤0.3 s of a standstill hold; reverse motion follows the held pedal; the throttle re-latches forward (exercised by every impact/wall profile's reverse_out/recover_fwd tail) |
| 5.5 | The nine bands | applied verbatim — the userpath regime is the stricter measurement (distances shrink slightly under the 0.19 s ramp + ~0.25 s latch delay: recorded, expected, not failures) |
| 5.6 | Chain hygiene | no profile inherits another's command/latch state (the boundary resets): each profile's first samples read command≈0 / middle-pedal neutral after the hold phase |
| 5.7 | Verdict | userPath + all rows = the user's P0 steering feel VERIFIED end-to-end on the exported player; the failure classes split: chain authority (physics/my files), glue (coordinator), device/platform (the windows evidence path) |

## 6. Contact-position proof — the restored hub-mounted guards (DV/wall + lap traces)

The reviewer-directive section: the restored collision must prove its corrected POSITIONS, not just its presence. Three evidence channels, mechanically readable:

**6.1 The DV/wall sample compositions (my `wheelBandContactSamples` metric, committed):** every DV/wall report now carries the per-profile `wheelBandContactSamples` split of `nonSurfaceContactSamples`:
- **PASS composition:** wall/wheel-band contacts occur under the WHEELGUARD/CALIPER names ONLY at genuine non-drive-surface events (wall bands/kerb edges at the arch); the FLOOR-REST rows (spd≈0, sustained, high chassisContacts with tyreContacts<4) must read `wheelBandContact=false` — the arches never rest on the ground any more.
- **FAIL fingerprint (the blob regression):** rest rows with `wheelBandContact=true`, or wall rows where the only contact parts are the body-fixed chassis at the belly while spd stays 0 — the misplaced-blob state returning; escalate to physics with the samples.
- The lap telemetry's `contactParts` histogram is the same proof at lap scale: WHEELGUARD/CALIPER rows may appear ONLY in contact windows concurrent with non-DriveSurface surfaces or kerb-band events — NEVER as the steady-state floor-contact constituents (the clean-lap baseline: CHASSIS/FRONTBUMPER/BODY only, now joined by guard rows only at real wheel-band events).

**6.2 The position cross-check (the geometry, not just the part names):** for wall profiles, the contact-window samples' `(x,z)` must sit near the corridor edge the wall occupies (|trackLateral| ≥ ~routeHalfWidth), not mid-corridor; a guard-named contact at mid-corridor = a mis-placed member (the pose correction failed) — escalate to physics with the sample's coordinates.

**6.3 The audit agreement:** the wheel-fit sweeps' `PART_HUB_OFFSET` lines for the restored parts must read ≈0 (the pose correction landed); `HUB_POSE_RESTORE` lines in the bake log must match the part set (car16: 2 guards; car0: 4 calipers). Any disagreement between the two channels = the restore is inconsistent — stop the physics readout.

## 7. Mounted-hull audit expectations — the axle-fit FITTED state

Which `PART_BELOW_WHEEL_PLANE car=N part=P axle=A lowestY=X delta=D` lines must DISAPPEAR once the axle fit is applied on top of the restored mounts:

| Line class | Fitted-state expectation |
|---|---|
| `…part=LOD_A_WHEELGUARD_…` | **GONE** — hub-mounted + pose-corrected: the part's floor sits at hub height, above the wheel plane, by construction |
| `…part=LOD_A_BRAKE_CALIPER_…` | **GONE** — same mechanism (car0's class; the runtime-disable note: car0's gate never exercised pre-restore) |
| `…part=FRONTBUMPER…` / `…CHASSIS…` bodywork rows at **−0.0455** (the turn-5 worsts) | **GONE with the axle fit** (the 0.45–0.48 m anchor correction moves the tyre spans clear of the wing/floor bands) — their persistence at the FITTED state = the fit did not apply to that car (the `-source-axle.json` inventory check first!) |
| `…part=STRUT_UPPER_REAR…` at **−0.02** (axle-class) | **GONE** (the fit's own target); a residual −0.02-class line = the fit's rear-anchor estimate for THAT car needs the escalated rung (mount/parts-transform, their ladder) |
| STRUT rows inside the suspension group | STAY within their tolerance bands (PASS/WATCH per the turn-4 protocol) — the struts legitimately live near the wheel plane; their clearance is posture-dependent, not a defect |
| cab/exhaust/others (+0.52…+0.80) | unchanged (clear) |

Reading rule: the fitted-state audit is ACCEPTED only when every axle-FAIL line class above is absent AND the stance lines still hold (the STANCE no-hubs value ≈ the measured ~0.025-class + the fit's shift) — the fit fixing the wheels must not re-break the body datum; both sheets' numbers quoted into the ledger together.

## 8. Escalation map (who takes which fingerprint)

| Fingerprint | Owner |
|---|---|
| Missing artifacts / older bake / wrong backend / parse | coordinator (pipeline) |
| Guard-blob rest, band breach, timing pair (`invalidRows==0 && bestLap==0`) | physics (me) |
| Exit-trace corridor coordinates | corridor/track |
| Wheel-fit/stance/`PART_HUB_OFFSET` drifts | car-assembly |
| Workflow parse/`403` dispatch blockers | coordinator + owner child-dispatch path |

Verdicts recorded per run as: `PASS / FAIL(<class>, <assert id>, <actual vs expected>) / UNVERIFIED(<reason>)` — the ledger line the charts consume.

## 9. Command: DV start sequence (`race_start_sequence`, case `native_driving_validation_start`)

**Preconditions (mechanical):** the case is car16/track0/dry with `gridStart:true`; the dispatch MUST carry `MYSIM_GRID_START=1` (the boot otherwise skips the countdown for AI boots). The runner fails fast with `Driving validation profile race_start_sequence requires the grid start; dispatch with MYSIM_GRID_START=1...` — that thrown message = `UNVERIFIED(config)`, NOT a physics FAIL; re-dispatch with the env. No damage requirement; assists per physics-acceptance rule; userpath leg (`MYSIM_USERPATH=1`, §5) is the stronger proof and runs the same profile through the real input chain.

**What the report must show (per asserted metric, phase-s keys):**

| Check | Report field | Bound | Failure class if out |
|---|---|---|---|
| Jump-start attempt actually commanded | `throttleHoldSeconds:grid` | ≥ 1.0 s | FAIL-validation (profile didn't drive; check userpath/throttle latch) |
| Grid held under full throttle | `maxSpeed:grid` | < 0.15 m/s | race-start sequencing (hold force released too early / never applied) |
| Grid end state not launched | `endSpeed:grid` | < 0.1 m/s | race-start sequencing |
| Countdown duration | `duration:grid` | 4.5–6.5 s | timing: >6.5 = clock start late/stuck; <4.5 = countdown skipped/short (boot path) |
| Release bite at lights-out | `speedRiseTime:release` | < 0.5 s | throttle path dead at release (§5 throttle-latch fingerprint) |
| Race started in release | `raceStartedFraction:release` | ≥ 0.95 | clock started late relative to hold-off |
| Launch reached speed | `endSpeed:release` | 4.8–7.0 m/s | launch dynamics (compare against the freed-car DV-core trace first) |
| Burned-in guards | `positionJumpMax:all`, `uprightMinCosine:all` | < 1 m; ≥ 0.9 | teleport / loss of control |

**Trace reading rules:**
- Per-tick `raceStarted=false` through the grid with `countdown` stepping 5→0 at 1 Hz = the lights timing layer; the light GUI itself is already proven (fixture).
- `raceStarted` never true → phase overruns `maxSeconds 12` → FAIL with fingerprint = race clock start (countdown expiry), NOT the DV runner.
- `maxSpeed:grid` high WITH `raceStarted=false` and commanded throttle 1 = hold released early (GridHold brake force dropped) — wake fingerprint shares the frozen-car contact family only if `bodyContacts` composition is wrong; check §6 fields in the same trace.
- `duration:grid` < 4.5 s with `raceStartedFraction:release` still ≥ 0.95 = the boot skip fired partially (countdown shortened but hold respected) — escalates to coordinator (start sequencing constants).
- Compound/wetness: this case is dry; a wet variant is a NEW case, not a re-dispatch of this one.
- The `chassisContactSamples` metric is RECORDED but NOT asserted here by design: the ~1 cm stance floor legitimately grazes under full brake-stop compression; a chassis-contact assert would false-fail (nonSurfaceContactSamples remains the loss-of-control guard).

## 10. Recovery-phase telemetry (R-window forensics) — the wall battery's recovery readout

**What it is:** the wall judgement sheet's R-class ("the car could not leave the barrier under its own drive — check the sample window") previously had no per-window evidence; the reading was a manual sample dig. The R-window instrumentation closes that: every rise of the cumulative impact counter opens a recovery window recording what the car did under its own drive — motion recovery time, path covered, inputs applied, surface state — until motion (speed magnitude) holds at `MotionSpeed` for `HoldSeconds` or the window caps at `MaxSeconds` (unresolved: `Recovered=false`, `motionTime=null`).

**Flag-gating:** enabled per profile by `setup: {"recordRecovery": true}` — currently ON for exactly the four wall profiles (`wall_contact_low/high/graze`, `impact_wall_recovery`); every other profile reports null recovery metrics (an assertion on them without the flag FAILS with "metric not observed" — never a silent pass). A profile with the flag and no impacts reports `recoveryWindowCount: 0` (observed zero). Physics defaults untouched.

**Report fields (per run, only when flagged):** `recoveryMetrics` (the summary metrics below) + `recoveryWindows[]` (per window: `startT,endT,startSpeed,Recovered,motionTime,pathLength,maxReverseSpeed,offTrackFraction,throttleFraction,brakeFraction,steerPeakAbs`).

| Metric | Reads | Use in the wall readout |
|---|---|---|
| `recoveryWindowCount:all` | full stream | must equal the run's total `impactEventsDelta` (per-impact accounting) |
| `recoveryRecoveredFraction:all` | windows | the R-class gate: 1.0 = every impact followed by recovered motion; <1.0 = R failure with the window array naming WHICH one |
| `recoveryMotionTimeMax:all` / `Mean` | recovered windows | how long the car sat unable to move after impact; the drive-away bound stays on `endLongitudinalSpeed:recover_fwd`, this quantifies the gap |
| `recoveryPathLengthMax:all` | windows | the path back (reverse-out + re-approach distance) |
| `recoveryOffTrackFractionMax:all` | windows | how much of the recovery was off the driving surface (a car pinned off-line reads high) |
| `recoveryThrottleFractionMean:all` / `BrakeFractionMean` | windows | the inputs actually commanded during recovery (the reverse-out shows throttle in reverse + brake release timing) |
| `recoverySteerPeakAbsMax:all` | windows | countersteer/straightening evidence |

**Reading rules for the wall battery legs (jolt leg + source pair):**
- Window count parity: `recoveryWindowCount` must match the run's total `impactEventsDelta`, and the source/jolt pair must show the SAME window count (the §2 impact-count parity rule, now per-window checkable).
- Recovery parity (extends the wall sheet's rule 4): `recoveryRecoveredFraction = 1.0` on BOTH backends is the shipping gate; a backend that leaves a car stuck (fraction < 1) fails R even when its impact numbers look better.
- `motionTime` comparisons across backends are DESCRIPTIVE (recovery is scripted-input-dependent); only the recovered/unrecovered fact and the input record are parity evidence.
- A window with `startSpeed < 1` (car stopped by the hit) and a large `maxReverseSpeed` = the scripted reverse-out worked; a window with `startSpeed > 5` = a glancing scrape at speed (near-instant recovery is the honest reading, not a bug).
- Unresolved window (`Recovered=false`) with finite contact numbers = the R-class failure, now carrying its own evidence (inputs applied, off-track fraction, path) — attach the window object, not just the assert id, to the failure report.
- The window's inputs are the COMMANDED stream (Sample.throttle/brake record the DV command); in userpath mode the chain's applied output is visible in the session telemetry's appliedThrottle, not here. Note this when comparing userpath vs scripted legs.
- Serialized samples now also carry `nonSurfaceContact` and `wheelBandContact` per tick — use them to audit any window's contact composition against §6 without re-deriving from metrics.

## 11. Pit-stop sequence validation — SCOPE SPEC (spec only this turn; the pitCompleteness item)

**What the servicing state machine already covers (logic-level, unit-tested in CoreChecks — keep, never weaken):** assisted entry→service→rejoin with wear reset; no service before race start (service clock frozen); pit-speed penalty (limit 16.666667 m/s; overspeed > +0.5 m/s held ≥ 1 s latches once); occupied box refuses a second service; unsafe-release hold + release after clearance; explicit repair duration extends the service clock (`PitServiceTimeline` stages READY/JACK UP/WHEELS OFF/FIT & SECURE/JACK DOWN/RELEASE; service 7.5 s, bodywork repair +8 s configurable); tyre-only stop preserves bodywork damage; staged tyre+compound swap; safe release (`servicing→pitDone`). Entry gate: within 16 m of `PitRoute[0]` with `PitValid`; box gate: within 1.2 m of `PitBox` AND speed < 1.2 m/s; exit: past `PitRoute.Length-4` within 5 m of the route end.

**What is NOT yet validated (the game-level gap — needs DV-runner extensions, flag-gated, additive only):**
1. The DRIVEN entry: a car under script/userpath input physically reaching the pit entry, committing (`pitActive`), and decelerating inside the pit speed limit. Open design question to resolve BEFORE implementation: who steers a human-driven (`externalPhysics`) car while `pitActive` — the pit-route lane target exists on the AI path; the profile must either script the steering explicitly or the runner gains a documented, flag-gated pit-lane assist. No silent assist introduction.
2. Box stop accuracy: the stop within the 1.2 m / 1.2 m/s gate, with stop position/heading recorded (a stop 1.19 m off still services — whether that tolerance is the intended box behaviour needs game-level evidence).
3. Service timing against the WALL CLOCK: measured `servicing` duration in a live run vs `PitServiceTimeline.Duration` (+ `RepairDuration` when repair is requested) — the unit tests prove state consistency, not real-time consumption under physics load.
4. Work-performed verification on the LIVE car: tyres actually replaced (compound applied, wear reset), bodywork repaired only when requested (tyre-only preservation must hold in the driven case, not just the unit case).
5. Release under traffic: the clearance hold exercised with a REAL stopped car behind (fixture), release on clearance, no contact during the exit roll.
6. The driven exit + rejoin: pit speed limit through the exit, rejoin onto the driving surface with a bounded `offTrack` burst, and lap validity intact across the stop (a pit cycle must not poison lapValid/bestLap).

**Required runner extensions when implemented (all additive, flag-gated):** `Command.pitRequest` (the pit-request input); Sample fields `pitActive/servicing/pitDone` (mirroring the Network-synced state); phase triggers `until.pitActive`, `until.servicing`, `until.pitDone` alongside the existing speed/wall-clock machinery; case data on a circuit with `PitValid` true and the stop's service terms. Metrics then: `pitEntrySpeedMax`, `boxStopDistance`, `boxStopSpeed`, `serviceWallClockSeconds`, `pitOverspeedSeconds`, `rejoinOffTrackSeconds`, `tyresReplaced`, `compoundAfter`. Until that lands, the pitCompleteness claim stays exactly "logic-unit-covered, not game-validated" — no acceptance language.

## 12. Contact-archetype force characterization — guard band vs body panel (the wall battery's discrimination layer)

**Purpose:** the restored hub-mounted guards carry collision; the wall runs must distinguish a GUARD contact from a BODY-PANEL contact mechanically (per-tick, from recorded samples), so "the wall was hit" splits into "WHAT was hit". This rides on §2 (wall battery) and §6 (contact positions); it changes no assertion.

**Classification (per tick, in `NativeRaceController.BuildValidationSample`, from `ContactParts`):**
- `wheelBandContact` — any contacted part matches WHEELGUARD/CALIPER (existing, §6).
- `bodyPanelContact` — any contacted part that is neither the drive surface nor the wheel band (NEW).
- Mixed tick = both flags set (a tick touching a guard AND a panel): contributes to BOTH signatures; `mixedContactSamples` measures how often that happens.

**Flag-gating:** profile `setup: {"recordContactArchetypes": true}` — currently ON for the four wall profiles alongside `recordRecovery`. Unflagged profiles return NULL archetype metrics (asserting them without the flag fails closed); the per-run report gains `contactArchetypes` only when flagged; serialized samples carry `bodyPanelContact` per tick always (the raw classification is cheap and audit-relevant).

**The archetype metrics (per-run `contactArchetypes`, phase-scoped values available to assertions):**

| Metric | Measures |
|---|---|
| `bodyPanelContactSamples:all` | ticks whose contact set includes a body panel (incl. mixed ticks) |
| `mixedContactSamples:all` | ticks touching BOTH the wheel band and a panel (ambiguity made measurable) |
| `wheelBandContactSeconds:all` / `bodyPanelContactSeconds:all` | contact duration per archetype (same accumulation rule as `impactContactSeconds`) |
| `wheelBandTickPeakDeltaV:all` / `bodyPanelTickPeakDeltaV:all` | the largest per-tick velocity jump observed while that archetype was in contact (the force signature's raw material; the impulse identity of §2 applies to the whole-event numbers, not per-archetype) |

**The archetype table (structure now; numbers ONLY from measured runs):**

| Property | Guard-band contact (hub-mounted, outboard) | Body-panel contact |
|---|---|---|
| Where (§6 cross-check) | contact patches at the wheel arches, wheelBandContact true, floor-rest rows NOT in the band | mid-panel/belly positions; wheelBandContact false |
| Duration expectation (structural hypothesis, to verify) | SHORT: small rigid patch sweeping past the obstacle | LONGER: larger compliant panel area in contact |
| DeltaV shape (structural hypothesis, to verify) | sharp per-tick spikes concentrated at first contact | distributed across the contact window |
| Count expectation per wall hit | the arch clips FIRST on a square hit (guard-first), panels follow on deeper penetration | panels dominate glancing/offset hits |
| Numeric band separating the classes | **TO BE FILLED from the first measured battery artifacts — no value is known or assumed** | **TO BE FILLED likewise** |

**INTEGRITY RULE:** until the first measured wall runs land, these metrics LABEL contacts (which archetype, how often, how long) — they do NOT gate on magnitude DIFFERENCES between archetypes. Any numeric "guard vs panel" threshold quoted before a measured artifact exists is invented physics and must be rejected in review.

**Battery reading rules (jolt leg + source pair):**
- Archetype parity: the same scripted approach on both backends must assign the SAME archetype to each impact event (the geometry is fixed by the script); a different label = a contact-resolution divergence fingerprint → physics investigation, class S.
- A guard-labelled cluster must ALSO satisfy §6's position rules (arches, not belly): a guard label with belly-position contact is the OLD misplacement fingerprint — escalate to car-assembly, not physics.
- `mixedContactSamples` > 0 is information, not failure: it measures genuine multi-part contact; investigate only when it dominates (a "guard" hit that is mostly panel contact questions the guard's coverage).
- Per-sample audit: the serialized `bodyPanelContact`/`wheelBandContact` flags let any cluster be re-derived from the samples without new instrumentation.

### 12.1 Measurement protocol — how the first artifacts fill the table (prep; no numbers exist yet)

**Cluster extraction (mechanical, from a run's serialized `samples`):**
1. A contact CLUSTER is a maximal run of consecutive samples with `nonSurfaceContact=1` (allow no gaps; a 1-tick gap starts a new cluster).
2. Per cluster record: `tStart/tEnd` (first/last sample), `n` (length), `archCounts` = {wheelBand ticks, bodyPanel ticks, mixed ticks}, `peakDeltaV` per archetype present (max per-tick |Δv| over the cluster's samples for that archetype, computed from consecutive `longitudinalSpeed`/`lateralSpeed` exactly as the metric does), and the §6 position read (first-contact sample's contact parts).
3. Cluster label: `guard` if wheelBand ticks ≥ bodyPanel ticks, else `panel`; a cluster with both > 0 is labelled `guard+panel(mixed)` — the label is descriptive, never a pass/fail.
4. Cross-checks per cluster: a `guard`-labelled cluster's first-contact parts must be WHEELGUARD/CALIPER names (§6); a `panel` cluster triggering an impact event must also show the §2 impact fields consistent (impactEventsDelta increments once per event-level expectation).

**What fills the numeric column:** the per-archetype observed envelopes across the FIRST measured set = the four wall profiles × both backends (jolt leg + source pair), one entry per (profile, backend, cluster). The envelope is `min–max` of: cluster duration, per-archetype peak Δv, samples-count. Qualification for entry: the run must be a completed terminal state (global rule), its archetype labels must satisfy the §6 cross-check, and the source/jolt pair must agree on every cluster's label (§12 parity); a pair that disagrees on a label contributes NO numbers until the divergence is resolved.

**Adoption criteria (when a number becomes usable):**
- Descriptive envelope first: the filled table is DESCRIPTIVE (observed ranges on the measured configurations) — it never gates until the coordinator promotes a specific bound.
- Promotion requires: the envelope stable across BOTH backends and at least two independent runs per backend (no identical re-dispatches — distinct runs), the §6/§2 cross-checks green on every contributing run, and an explicit sign-off line in this card naming the runs that produced it.
- Any promoted bound then appears in the table WITH its source runs named; a later run contradicting a promoted bound is a class-S fingerprint (§8), not a silent re-baseline.

**Fill-in template (paste into §12's table when the first artifacts land):**

```
Measured (run <id>, <backend>, <date>):
- guard clusters: n=<count>; duration <min>–<max> s; peakΔv <min>–<max> m/s; labels verified vs §6: <yes/no + exceptions>
- panel clusters: n=<count>; duration <min>–<max> s; peakΔv <min>–<max> m/s
- mixed clusters: n=<count>; dominant archetype: <...>
- source/jolt label agreement: <k>/<n> clusters
- separation observation (descriptive only): <e.g. "guard durations 3-9x shorter than panel on every paired cluster">
Promoted bounds: <none yet | bound + source runs + signer>
```

## 13. Two-instance LAN test (owner-run) — reading rules

**Artifact:** both console transcripts of the scripted two-instance run (`agents/lan-two-instance-test.md` §1 commands; exit codes recorded). This is the LAN plan's phase-1/2 exit evidence — the FIRST live LAN verification; everything LAN stays CODE-PRESENT-UNVERIFIED until it passes.

**Required lines (mechanical):**

| Instance | Must contain | Must NOT contain |
|---|---|---|
| Host (A) | `RACE_READY`, `LAN_REMOTE_CAR model=<n> driver=<name>`, `LAN_HOST_ADOPTED cars=2`, `LAN_HOST_EXIT connected=True inputs=<n>`, n ≥ 1, exit 0 | `LAN session error`, `Connection lost`, `RACE_LOAD_FAILED` |
| Client (B) | `LAN_RACE_READY track=<n> role=Client`, ≥1 `LAN_SYNC ... hostSpeed=` > 3, `PASS LAN_CLIENT sync observed:`, `PASS LAN_CLIENT exit: sync observed`, exit 0 | `LAN session error`, `Connection lost`, `Room connection error`, any `FAIL LAN_CLIENT` |

**Verdict grammar:** `PASS` = every row above holds on the recorded consoles; `FAIL(<class>, <line/absence>, <expected>)` per the test doc's fingerprint table; `UNVERIFIED(<reason>)` for environment failures (firewall prompt, port blocked, wrong address — the run never tested the product).

**Reading notes:**
- `inputs=<n>` on the host counts the client's received INPUT sequence — the reverse-direction proof (client→host control channel); `inputs=0` with a connected session = the INPUT path broken, class S.
- The `sync observed` marker fires when the HOST's car exceeds 3 m/s AS SEEN ON THE CLIENT — states flowing AND applied to the persistent scene objects; without it the visual similarity of two static grids would be worthless as evidence.
- The `LAN_SYNC` cadence lines (2 s) are the timing evidence: `phase=GRID→RACE` transition must follow the host's START within ~a second of lines; a long gap = the START gating stuck (protocol, class S).
- The disconnect leg (kill the client mid-race) is a SEPARATE run with its own expectation: the host keeps racing, the remote car continues under AI control; the host console must NOT show a crash — the graceful-degradation proof.
- Single-player regression guard: the same build with no LAN envs must-show ZERO `LAN_*` lines in a normal run — the hard-off proof.
- Environment failures (firewall prompts, port conflicts) are `UNVERIFIED(config)`, never product failures; re-run after the environment fix.

## 14. Single-player regression net (the LAN/bridge work's blast-radius check)

**Purpose:** the LAN/bridge work touched `Main.cs` (StartLanRace + the menu lambdas + the `_Ready` env block + the adoption hook + the catch-clear), `NativeRaceController` (the LAN fields + the `_Ready` block + the `_PhysicsProcess` tick/visual + `_ExitTree`), and the DV/userpath plumbing. Every battery run on a non-LAN path DOUBLES as the single-player regression check by these assertions. ALL of them are log-reading rules on runs the battery already produces — no new runs needed.

**A1 — the master absence proof (every non-LAN run):** a run launched without `MYSIM_LAN_HOST`/`MYSIM_LAN_JOIN` must contain ZERO `LAN_*` lines (`LAN_REMOTE_CAR`, `LAN_HOST_ADOPTED`, `LAN_HOST_EXIT`, `LAN_RACE_READY`, `LAN_SYNC`, `LAN_CLIENT`, `LAN session error`). Any `LAN_*` line in a non-LAN run = **A1 FAIL — the stale-`PendingLanHost` injection or a gate leak: the run is INVALID as evidence, class S.** This is the one-line catch-clear guard's verification (the failed-load leak path: a LAN host load failure used to leave the static set for the NEXT race to inherit).

**A2 — the DV-core signature unchanged:** every DV run (core/wall/start) keeps its exact report shape: `RACE_READY` + the per-profile PASS/FAIL lines + the report JSON with `userPath`/`damageEnabled`/`backend`/`runs[].metrics`/`assertions` unchanged; the new fields (`bodyPanelContact` per sample, `recoveryMetrics`/`contactArchetypes` on flagged profiles) are ADDITIVE only. A MISSING pre-existing field or a changed assertion semantic = regression, class S.

**A3 — the traffic/rivals semantics unchanged:** `MYSIM_RIVALS=N` (N>0) without `MYSIM_NATIVE_TRAFFIC=1` must still throw the experimental-traffic error (the LAN block only zeroes rivals when a session is actually adopted — a non-LAN run's rivals path is untouched); with the flag, the traffic behaves as before (`opponents` evidence in the report).

**A4 — the userpath chain holds:** the userpath leg (`MYSIM_USERPATH=1`) reports `userPath:true` and its chain-inclusive bands hold (1 Hz ≥ 0.9×lock, 2 Hz ≥ 0.65×lock, lock ≤ 0.30 s, release ≤ 0.30 s — CoreRunner-enforced on every push; the run-level metrics are the live proof). A collapsed authority = the routing chain regressed, class S.

**A5 — the menu flows:** the FLOWCHECKS run (`MYSIM_FLOWCHECKS`) green — the menu→race→results→menu cycle is the single-player navigation proof; the LAN lambdas' edit in `ShowMenu` must not have moved the single-player buttons (the flowchecks exercise them).

**A6 — the start-sequence unchanged:** the `race_start_sequence` profile (§9) still passes with its countdown band — the LAN start work did NOT touch the single-player start path (the LAN countdown arming lives in the session, not the Race boot).

**A7 — the new-env inertness:** each new env is absent-inert by construction: `MYSIM_LAN_HOST`/`LAN_JOIN` open panels ONLY in the `_Ready` branch guarded on `MYSIM_TRACK` absence; `MYSIM_LAN_AUTOSTART` acts only inside a hosting panel; `MYSIM_LAN_QUIT_AFTER` acts only when a LAN session exists (and rejects out-of-range values loudly at `_Ready`). A CI/DV run setting none of them cannot reach any new code — A1 is its evidence.

**A8 — the LAN-hosts-without-native guard:** LAN envs + a missing `MYSIM_NATIVE_GAMEPLAY=1` must fail LOUD (`LAN_HOST_REQUIRES_NATIVE`, the session dropped and disposed — no silent skip, no leak into later races). Seen without the message = the pre-guard build; treat as A1-class: the run invalid, re-run with the guard.

**What a REGRESSED single-player run looks like (the summary the reader scans for):**
- `LAN_*` lines where none belong (A1/A6) — the session leak or a gate regression.
- A DV report missing pre-existing fields, or assertion semantics shifted (A2).
- The traffic gate silently accepting rivals without the flag (A3).
- `userPath:false` on a userpath dispatch, or the authority bands collapsed (A4).
- The flowchecks failing on the untouched menu paths (A5).
- A silent solo race where a LAN host was requested — `LAN_HOST_ADOPTED` absent with no error (A8, pre-guard builds).

**Verdict integration:** these checks ride EVERY existing battery reading (§1–§5, §9): one glance at the log for the A1/A6 markers while applying the section's own rules. A1 failures invalidate the run as acceptance evidence REGARDLESS of the section's own outcome.

## 15. The wet/night matrix (D0-first) — per-cell reading rules

**BLOCKERS FIRST (verified against c677943's artifacts — the matrix is NOT executable until these land):**
1. **B1 (fixed on my branch):** the wetnight case files' cases carried NO `drivingValidation` lists — `ParseCaseProfiles` throws on exactly that ("has no drivingValidation profiles") and the wetnight case_sets are wired into the `DRIVING_VALIDATION_MANIFEST` chain ONLY — every `depth=driving_validation case_set=wetnight_matrix_*` dispatch died at boot. My branch carries the corrected pair (the lists added, mirroring `native_driving_validation_core.json`/`_wet.json` exactly: car16 carries `edge_excursion`, car0 does not; the dry file keeps its no-weather-keys design; the wet file keeps weather 2 + compound 3 + gains the `requiresWetWeather` marker). MERGE THE PAIR before the first matrix dispatch.
2. **B2 (tool fix needed — not my file):** `cloud_game_audit.py:114` asserts `all(weather>0)` for any manifest whose NAME contains "wet" — `"wetnight_matrix_dry"` contains "wet", so the DRY file dies at the tool's assert no matter its content (dry cases cannot carry weather>0). Proposed one-line fix: honour an explicit JSON marker — `if spec.get('requiresWetWeather', 'wet' in manifest.name and 'wetnight' not in manifest.name):` — backwards compatible (the old wet manifests keep the name-based assert; my wet file carries the marker; the dry file passes).
3. **B3 (dispatch design — coordinator/verification):** the `driving_validation` depth runs shard `[0]` ONLY and the tool filters `case['track']%12==shard` — the matrix's TRACK-8 cases (8%12=8) SILENTLY NEVER RUN: the workflow goes green with half the matrix missing. Options: (a) extend the shard list for these dispatches (a yml change), (b) move the second track to one with `track%12<=5` (a data change — the verification agent's matrix-design call), (c) a shard-filter exemption for the wetnight manifests. Until one lands, only the track-0 half of every cell is real; a "green" matrix run proves NOTHING about track 8.

**How the cells run (the dispatch shape):** `depth=driving_validation`; `case_set=wetnight_matrix_dry` serves D0/N0/N1/N2 (the same 4-case dry manifest; the cell identity comes from the yml env block: unset = D0, `TOD:3` = N0, +the light package = N1, +vehicle lights/anchored = N2); `case_set=wetnight_matrix_wet` serves W1/W2 (weather 2 + compound 3 come from the CASE data via the tool's env translation — the manifest level wins); W0 (optional) = the wet manifest with all cosmetic knobs off. The dispatch commands remain in `OWNER_MERGE_INSTRUCTIONS.md` (its matrix section is the operational source of truth; THIS section is the reading layer).

**The D0-first gate:** D0 (dry day, every candidate knob off) must reach a verified terminal state BEFORE any N/W cell is READ. A failed or unfinished D0 → every other cell `UNVERIFIED(no-baseline)`: collect the artifacts (preserve failure records), issue no cell verdicts. D0's own reading = §1 (the DV-core bands, per case) + §14 (the regression net) + `frame-hashes.log` as the pixel baseline.

**Per-cell reading table:**

| Cell | Proves | DV read (physics) | Visual read | Fingerprints |
|---|---|---|---|---|
| D0 (dry day) | the baseline: the DV contract on both cars × the tracks in the manifest + the pixel baseline | §1's bands per case; `RACE_READY`; the §14 net | the reference frame set | any band breach = §1's class; `LAN_*` lines = §14 A1 |
| N0 (TOD 3, knobs off) | the darkness baseline + THE PHYSICS INVARIANCE | the DV metrics ≈ D0's (same manifest): headline deltas within run noise; every band holds | dark is CORRECT (N1 exists because N0 is dark) — unreadable at N0 is not a failure | a band breach or a systematic metric shift = the lighting env leaking into physics (class S, physics+render joint) |
| N1 (+light package) | readability restored WITHOUT touching physics | still ≈ D0 | body/near barriers/kerbs discernible; no hue inversion | physics drift = the leak (class S); still unreadable = the package insufficient (render owner) |
| N2 (+vehicle lights, anchored) | vehicle lights + anchored scheme + pool budget intact | still ≈ D0 | `poolBudgetUntouched=true` MUST appear; the anchored scheme cross-checked in the audit JSON | missing/false `poolBudgetUntouched` = blocking (render); the fixture-view frames gap = the known N2 gap (use `night_fixture_audit` for the station frames until the port lands) — N2's visual verdict is PARTIAL until then; the DV side stays full |
| W1 (wet day) | the wet physics contract under the wet grip model | the wet profiles' own bands (`braking_60_wet` et al.); the report's `water` > 0; the DIRECTION expectations (analyst checks, never gates): wet braking ≥ dry, wet accel duration ≥ dry | the wet-road shading responds; the DRY cells show ZERO road delta | `water`=0 in a wet run = the weather plumbing broken (class S); faster-wet-than-dry = investigate (AI-line variance caveat), not auto-fail |
| W2 (wet night FULL — the money cell) | everything at once | the DV ≈ W1's (the night adds nothing physical) | ≥ N1 pool visibility (no black-hole); rain streaks ≥ 2 px at 10–40 m on ≥ 5 frames; THE FENCE WIRE STAYS VISIBLE (the turn-1 reject must not re-enter — BLOCKING) | a black-hole = blocking (render); the fence crushed = the turn-1 regression (blocking); physics drift vs W1 = class S |
| W0 (optional clean wet) | the wet baseline without cosmetic candidates | the wet bands; the cleanest wet-physics reference | the no-candidate wet shading | as W1 |

**Cross-cell invariants (the section's core):**
1. **Physics invariance under lighting:** N0/N1/N2 ≈ D0 and W2 ≈ W1 (same-manifest pairs). Hard rule: every cell's assertions pass their own bands; analyst rule: the headline metric deltas stay within a few % of the pair's base — a systematic shift = the cosmetic env leaking into physics, class S, blocking the CELL not the matrix.
2. **Direction expectations** (W vs D): wet slower/longer on braking/acceleration — analyst checks, never auto-gates (the AI-line variance caveat).
3. **§14 rides every cell** (the regression net: zero `LAN_*` lines, the report shape, the env inertness).
4. **Completion:** the matrix is DONE when every cell has a verified terminal state AND the cross-invariants hold AND (post-B3) both tracks' cells exist. The chart consumes per-cell verdicts, verified terminal states only; a partially-run matrix (e.g., track-8 missing) is recorded as PARTIAL, never as green.
