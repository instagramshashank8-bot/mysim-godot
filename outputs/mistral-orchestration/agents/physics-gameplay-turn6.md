# physics-gameplay agent — turn 6 — frozen-car family, contact evidence spec, RIDE readout — 2026-11-04

Role: physics/gameplay specialist. Branch `mistral/physics-gameplay` (worktree at `4129dc4` + this turn's commits). Inputs this turn: the four stall/contrast logs under `outputs/repair-20261029/mip2-41830995246/game-runtime-{0,4}-attempt-1/outputs/cloud-game/`, `data/TrackList.json` (route design geometry incl. elevation), the drive-surface meshes `data/surfaces/{Brands_Hatch_indy,YasMarina_gp,Circuit_de_Catalunya_national}.txt` (parsed offline — metadata reading, no engine), the clean-lap + DV-failure evidence from turns 2/5, and this turn's two small patches (below). No dispatches; no defaults changed; no physics constants invented.

## 1. Frozen-car family — beach-slope hypothesis: REJECTED; the data supports a vertical-datum mechanism instead

Spawn-region geometry, measured offline from the actual surface meshes + route design data (progress 0.988, launch = the route segment from the spawn through the lap start):

| Track | State | Route design pitch (first 160 m of lap) | Launch Δy (first 100 m) | Surface mesh near spawn (30 m radius) | SPAWN_GROUND y (log) |
|---|---|---|---|---|---|
| 0 Brands Hatch INDY | HEALTHY (launched, 27.9 m/s at t=2) | **0.0°** (design flat) | 0.0 m | y −7.83..−6.17, 4 layers | −7.096 |
| 28 Yas Marina GP | STALLED | 1.74° max (3.19 m/100 m) | +3.19 m | y −0.259..+0.874, 5 layers | +0.103 |
| 30 Catalunya NATIONAL | STALLED | **0.0°** (design flat) | 0.0 m | y 5.847..6.039 (flat, ±10 cm), 2 layers | +5.935 |

**Beach-slope is rejected on two counts:** track 30's spawn region is flat to within ±0.1 m with zero design slope and still stalls; and the HEALTHY track 0 has the busiest local surface structure of the three (four levels within 30 m). Slope does not separate stalled from healthy.

**What the data DOES separate — vertical datum agreement between the route design and the surface mesh:**

- Track 0: route design y=0, surface at −7.8 ⇒ the spawn sits **above** the road and falls onto it; the legacy spawner is a down-ray from around the design height, which finds the road.
- Track 30: route design y=0, surface at **+5.85** ⇒ the spawn sits **below** the road. The legacy ground seed (`DriveController`: `Contact.Ground(space, ToWorld(Player.position,0), Car.Position.Y, …); if(!seed.found) return;` — a downward ray) can never find a surface **below** the spawn height, so the tick returns every frame: silent infinite stall — exactly the observed log shape (RACE_READY → one PITSTATE line → nothing, no DRIVETEST, killed at timeout).
- Track 28: design 0.343 vs a 5-layer surface spread of 1.13 m; the seed ray locked onto a **lower layer** (hit `+0.103` while the drive surface in the region reaches +0.87) ⇒ driven under/beside the real road layer — a buried/wrong-layer variant of the same datum confusion.

**Owner of the fix:** the legacy spawner/ground-datum logic in `DriveController` (not my file) and/or the surface/route build (track side); the minimal form is to seed the ground search from far ABOVE the surface (the native spawner already does this — `PlaceBody` casts from y=200 down 400 m, datum-safe by construction, which is why no native run ever failed to find its road). Recommended guard for the track-sweep driver (coordinator's driver code): a per-case `travelled>x m by t=y s` stall assert so this class fails fast with a reason instead of timing out silently.

**Relation to the native frozen-car family:** separate mechanism, same family name. The native failures (41834771421; DV_TRACE `started=true gridHold=false thr=1 spd=0 bodyContacts=2 tyreContacts=1`) are NOT datum (native spawn is ray-safe) — they are the floor-rest + free-rev/thrust-collapse loop from turn 5, confirmed by that trace signature (thr=1, spd=0, 1 supported wheel). The ride-height configuration B removes the floor-rest pose entirely, so the ride A/B and this family share one root cause; the (c) 99.6%-chassis-contact lap is the mildest expression (the same floor rides the road without stopping anything on jolt/240).

## 2. Fleet contact-evidence spec (what every fleet render/ride evidence run must carry per car)

All fields exist in the telemetry today; two small enablers landed this turn (section 4). A fleet ride/render/physics evidence run is COMPLETE only if it emits, per car:

| Evidence | Field path (telemetry rows) | Meaning / units | Gate |
|---|---|---|---|
| Tyre-to-road gap, dynamic | `telemetry[].loadedWheels.wheels[i].minimumContactPlaneClearance` | lowest actual rubber vertex of the car's active tyre mesh vs the solver's current contact tangent plane, metres; **negative = rubber through the plane** (penetration-depth proxy, NOT a collision defect by itself); valid only when `wheels[i].supported` | `MYSIM_LOADED_WHEEL_EVIDENCE=1` (already worked in reference evidence: ±1.6 mm band measured on run 41627667783) |
| Tyre support + state per wheel | same rows: `supported`, `load` (N), `springLength` (m), `slip` (longitudinal kappa), `slipAngle` (rad), `temperature`, `grip`, `water`, `compound` | the tyre's live state vector at the same instant as the gap | same flag |
| Chassis clearance, dynamic + rest | `telemetry[].underfloor.{samples,groundHits,minimumClearance,maximumClearance,pitch,roll}` | lowest actual hull vertices (chassis/bumpers/body) ray-cast to the real drive surface: metres; plus body pitch/roll (rad) so clearance reads against attitude | **was gated to ride-height mode; patched this turn — now emitted on every native run** (read-only; no behavioural change) |
| Aggregate support | `telemetry[].contacts` (0-4), `tyreLoadRatio`, `springLengths[4]` | supported-wheel count, load share vs weight, per-wheel spring lengths | always |
| At-rest vs dynamic readout convention | rest = every sample with `speed < 0.5` after the settle (or the first 2 s post-spawn for spawn-state evidence); dynamic = the whole-lap min/max/distribution | — | fleet reports must state which window each number came from |

DV runs additionally carry the per-tick versions (`groundedMask`, `loads[4]`, `springLengths[4]`, `maxDeltaV`, `nonSurfaceContact`) in the validation report — finer-grained than the lap telemetry but not vertex-level (vertex casting per tick would be prohibitively expensive; the 12-tick lap cadence is the right sampling for that).

Fleet acceptance uses these as: static clearance ≥ the configured `minimumStaticClearance` **only under a ride-height-mode (or adopted-default) run**; floor-contact fraction ≈ 0 under the same; tyre gap within the measured ±1.6 mm band (else the contact-plane story is investigated, not assumed). Under the current DEFAULT (A) configuration none of those are failures — the floor rides the road by construction; recording them per car builds the before/after evidence for the ride decision.

## 3. RIDE-A / RIDE-B exact readout procedure (the pair: jolt/240/rotational; B adds `native_ride_height=1`)

**Step 0 — preconditions (both legs):** log contains `NATIVE_PHYSICS_HZ=240`, `ASSISTS_OFF …`, `NATIVE_SOURCE_BODY_COLLIDERS car=16 parts=N` (N>0), no `NATIVE_COLLISION_FIXTURE_PROXY`; backend confirmed `jolt` in `physics-backend-experiment.json`. RIDE-B additionally must print `NATIVE_STATIC_CLEARANCE preload=P original=C target=0.035` — record **P and C per car** (P is the mechanism-magnitude number; C documents the raw source clearance).

**Step 1 — ride fix proof (Group 1), from the LAP reports:**

| # | Check | Field / source | A expected | B pass criterion |
|---|---|---|---|---|
| 1.1 | Static clearance achieved | `NATIVE_STATIC_CLEARANCE` log line; `underfloor.minimumClearance` at rest (first settled samples) | line absent; underfloor null (A's underfloor now emits post-patch: expect ≈0/negative ground contact — the documented floor-rest) | clearance ≥ ~0.03 m at rest; `preload∈(0,0.12]` |
| 1.2 | Floor contact gone | `underfloor.minimumClearance` ≥ 0.03 sustained; chassis-vs-DriveSurface contact count derived from `contactParts`/`contactSurfaces` rows (count rows whose part name starts `SourceBodyHull_` **and** surface path ends `DriveSurface`) | ≈99.6% of samples carry such contacts (41799538756: 1785/1792) | ≈0 overall; transient corner/bump excursions <1% of samples, never sustained |
| 1.3 | Lap parity gates | `ok`, `finished`, `laps`, `ruleResets`, `damage`, `impactEvents`, lap-vs-`routeWidth` legality | clean | identical pass (no gate moved) |
| 1.4 | Lap time + pace deltas | `bestLap`, `time`, speed trace | A reference | Δ recorded (predict: ≤ ±2%; a floor-drag REMOVAL could even speed the lap — do not assume direction); no acceptance bound on Δ itself, it is baseline metadata |
| 1.5 | Backend side-read | same checks on the source-backend leg when it runs; source impact count vs the 17-impact history (`41794252920`) | 17 | a large drop = floor-scrape contribution confirmed for the backend protocol (feed turn-3 section 4) |

**Step 2 — the 9 targets hold (Group 2): one DV core run under the B configuration** (same envelope as the core manifest, `MYSIM_ASSISTS=0`, rotational on). Judge per target exactly as the turn-5 rubric states, with the ride-specific notes:

| Target | Rule on B |
|---|---|
| braking_distance_from_speed | inside [0.7,1.45]×ref (car16: 11.73–24.30 m); report Δ(A→B); Δ>+10%: investigate load transfer before any band talk |
| acceleration_response | inside [1.13,3.91] s; report Δ |
| steering_rate_low_speed | steerSettleRatio ∈ [0.5,1.4]; response < 0.2 s |
| steering_lock_at_speed | steerTrackingRatio ∈ [0.9,1.1] at 30 m/s; yaw peak < 4 rad/s |
| straight_line_stability | heading drift < 0.25 rad / 4 s coast — the sharpest watch item (parasitic floor damping disappears); a genuine B-only failure is reported as a tyre-only stability finding and fixed legitimately (damping/geometry), never by re-lowering the car onto its floor |
| kerb_and_edge_stability | yaw decay < 0.5 rad/s after the excursion; upright ≥ 0.9; + now floor-free so the numbers are the first honest ones |
| slip_visibility | lateral slip peaks > 0.06 rad present and finite (rotational on) |
| suspension_travel_compliance | travel judged **relative to the run's own static spring length** (B's static differs from A's by the preload — absolute cross-leg comparisons are void); travel strictly inside the envelope, no negative |
| impact_recovery | (impact manifest, B) Δv ∈ [0.5,30], no teleport, drives away |

**Step 3 — verdict grammar:** PROVEN = all Group 1 + no unexplained Group-2 regression + visual stance accepted. Any Group-2 failure on B = a real defect now measurable (fix list, not a revert). Partial Group-1 failures (e.g. preload hit the 0.12 ceiling for some car) = the affected car keeps configuration A explicitly and the limitation is recorded per car — never a silent per-car mixed default.

**Adoption gate for the DEFAULT change (who signs what):**
1. Numeric sign-off: me — the Group-1 readout table filled from the pair's actual reports (values, not adjectives) + the Group-2a DV run clean; committed here as an evidence appendix.
2. Visual sign-off: car-assembly/visual owners — the raised stance on the reference car (stills at the standard views; ride height changes the car's appearance).
3. Coordinator decision + execution: the default flip is a pipeline change (`ride_height` input default or the env default in `cloud_game_audit.py`) committed with the baseline label; my files need no change for adoption.
4. Re-baselining list (from turn 3, unchanged): clean laps on BOTH backends; ALL DV runs (core/wet/impact); the wet matrix conducted wholesale under the new baseline (the completed-cells exclusion list is configuration-tagged: 41625514977's wet-slick cells remain A-configuration evidence and do not mix with B cells); grid-start reference (settle/creep changes with preload); loaded-wheel clearance evidence (41627667763-analogue) re-run. NOT re-run: settings persistence, LAN, pure-logic checks; art/UV/fence evidence stands as images, with the stance re-checked in the next car-evidence cycle.
5. Until all gates pass: **35 mm stays EXPERIMENTAL** (labelled in `data/native_suspension_profile.json` — scope and a dedicated status field now say exactly that — and in this doc); nothing in production reads it as an accepted constant, and the A-mode default remains fielded.

## 4. Patches this turn (mine, committed; no defaults changed)

1. `src/NativeSuspensionVehicle.cs`: the underfloor evidence collection (lowest hull vertices of chassis/bumpers/body) no longer requires the ride-height mode — it runs on every native build so the fleet evidence spec above is emit-able in BOTH ride configurations; strictly additive read-only data (the ride-height mode itself untouched).
2. `data/native_suspension_profile.json`: the 35 mm value now carries an explicit EXPERIMENTAL/UNMEASURED label (scope rewrite + a `minimumStaticClearance_status` field) so no consumer can mistake it for calibrated data.
3. Verified: build 0 warnings/0 errors; CoreRunner untouched by these (no logic change) — full suite still 88078 assertions PASS from the pre-patch state and the build prove compilation.

## 5. Carry-forward

- RIDE-A/RIDE-B + the backend 2×2 remain queued behind the P1 pair; the readout above executes the moment both legs land.
- DV core (41840755243) readout still pending; the turn-5 decision table applies to its DV_TRACE lines when they land.
- Track-28/30 stalls: root cause narrowed to the legacy datum/ground-seed mechanism (track 30 proven: flat spawn, road 5.85 m above the seed origin, down-ray can never find it); fix belongs to `DriveController`/track-build owners; a fast-stall guard for the track sweep is a recommended driver addition.
