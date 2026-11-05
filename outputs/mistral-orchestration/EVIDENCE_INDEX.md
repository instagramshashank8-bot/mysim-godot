# EVIDENCE INDEX — MySim acceptance-claim register

Maintained by the verification agent (branch `mistral/verification`). One row per acceptance claim covering: settings persistence, continuous lap, HUD bounds, wheel closeups, cockpit, fences, UV candidates. This is the reviewer's index — every claim points to an exact run ID, commit and artifact path with an honest status.

Status vocabulary (nothing here implies visual acceptance or whole-game acceptance):
- **machine-verified** — an automated gate in the named run passed (green run + assertion); artifact content checked by machine, not necessarily seen by a human
- **human-pending** — machine evidence exists; human review not yet done
- **rejected** — explicitly rejected by review/metrics; evidence preserved
- **failed** — the run failed; preserved for history; proves nothing in either direction
- **superseded** — a newer run/claim replaces this one

Updated: 2026-11-04, session 5. Run statuses via `gh run view --json` (plus `--log-failed`/artifacts for 41830995246, explicitly tasked). Artifact paths marked `✓disk` verified on the local disk; `(CI artifact)` = exists as a run artifact, not downloaded. Content figures not independently re-read are marked `(content per ...)` with the source.

## Settings persistence

| Claim | Run | Commit | Artifact path | Status |
|---|---|---|---|---|
| Atomic save + typed fallback + legacy/foreign/corrupt/malformed + failure preservation (28 base cases, Linux, isolated per-scenario HOME) | 41710482854 | `dfe0ed3` | `outputs/repair-20261029/cloud/settings-persist-result/` ✓disk | machine-verified — SUPERSEDED: 2 of 28 (`temp_live_kept`,`rename_live_kept`) passed vacuously (un-globalized path, `""==""`); fixed and re-proven by the rows below |
| Same 28, non-vacuous re-proof + `contract_sync_case_names` live (fixture case names == audit `required_cases`, both directions) | 41798040383 | `e8b80bb` | CI artifact (not downloaded locally) | machine-verified |
| 29-case Linux basis incl. `legacy_long_roundtrip` (long-typed `Get` fix at `9ff443c`; regression found by 41795630650's controls probe) | 41818941902 | `9ff443c` | CI artifact (not downloaded locally) | machine-verified |
| **Windows packaged-player persistence VERIFIED** — build + smoke + persist, all 5 scenarios green, real `%APPDATA%\Godot\app_userdata\MySim\settings.cfg` | **41828819144** | `e82a362` (`e82a3620702d13155cdee6d3d5cefb80411216e7`) | `windows-build-diagnostics-41828819144-attempt-1` (downloaded + read this session); per-scenario `windows-settings-persist-{scenario}.json` + `windows-settings-persist-summary.json` | **machine-verified. EXACT per-scenario passed/failed (from the artifact): legacy 9/0, foreign 5/0, corrupt 6/0, malformed 5/0, failures 12/0 — total 37 (29 base + 3 win + 5 contract_sync), exit 0, zero missingCases, zero unexpectedErrors. `platform:"windows"` on ALL five reports. The three cases `win_sharing_violation_rejected` / `win_sharing_violation_tmp_kept` / `win_sharing_violation_live_kept` present and PASSED in `failures`. Expected-error fingerprint verified in the logs: exactly 1 `SETTINGS_SAVE tmp=FileCantOpen` + 2 `SETTINGS_SAVE rename=Failed` per capture in `failures` (dir-occupied tmp; env-seam rename; FileShare.None delete-block); paths confirm the real runner `%APPDATA%` location. Scope: Windows runner + packaged player; NOT crash-consistency (row below); not stage-10 closure |
| Prior Windows run 41819498045 (`d3629e8`) — failure fully explained | 41819498045 | `d3629e8` | `windows-build-diagnostics-41819498045-attempt-1` (not re-downloaded by this agent) | failed — cause per the coordinator's autopsy (commit `372f92f`; content per commit message, not independently re-read): the driver's blanket `'ERROR:' not in text` check tripped on the fixture's EXPECTED `Settings` error emissions, AND the fixture's contract expected the 3 win cases in EVERY scenario on Windows (`PlatformExtra()` was not scenario-scoped — a bug in this agent's turn-1 code, fixed at `372f92f` by scoping `PlatformExtra(scenario)` to `failures`); the `failures` scenario's own 12 assertions were 12/12 green incl. the win cases. No `Settings.Set` production defect was found by that run |
| Windows decisive-run requirement (driver requires win cases + `platform=windows`) | SATISFIED BY 41828819144 | `2862b04` (enforcement) → verified at `e82a362` | see the VERIFIED row above | closed — the 41828819144 run satisfies every requirement of this row |
| **Crash-consistency through the delete-then-rename window — OPEN** | none — no crash-injection test exists | `2862b04`+ (Godot Windows rename = delete-then-rename, verified in 4.7.2 source) | — | **OPEN, explicit:** the verified save path is safe between the tmp write and the rename START, but Godot's Windows rename deletes the live file before `MoveFileW`, so a crash/power-loss inside that window leaves NO live settings file (the tmp recovery copy survives on disk). No test injects a crash there; the current fixture cannot catch it. Closing this needs a crash-injection harness or an OS-atomic `File.Replace`-based path in `Settings.cs` — `temp_live_kept`/`win_sharing_violation_live_kept` do NOT cover this window |
| Three-way contract sync: fixture `BaseCases` = `cloud_game_audit.py required_cases` = `cloud_windows_persist.py required` (29 base / 32 with win cases; verified by set comparison at `2862b04`) | static verification (turns 2-3, this agent) | `2862b04` | `src/SettingsPersistChecks.cs`, `tools/cloud_game_audit.py`, `tools/cloud_windows_persist.py` | machine-verified (static) + runtime-enforced on every repo run by `contract_sync_case_names` |
| Prior Windows dispatch attempts (misconfigured; never evidence) | 41818937383 (ran at stale `a6416fb`), 41819339436 (cancelled; tag/checkout mismatch) | — | — | failed, preserved; never cite as Windows evidence |

## Driving validation / product controls

| Claim | Run | Commit | Artifact path | Status |
|---|---|---|---|---|
| Driving-validation full profile (scripted scenario, assertion/coverage gates) | 41828824418 | `e82a362` | CI artifact | failed — grid-hold: the session never started (validation input cannot release the start sequence); fix `39cdf33` boots the session like the proven lap runs (AI/autodrive activation flags) |
| Same, boot-fix head — boot FIXED, travel NOT | 41834771421 | `39cdf33` | CI artifact | failed — booted fine (race ready, hulls present) yet failed to travel 10 m; SHARES the no-travel symptom with 41828824418 (per `95f69c6`); `DV_TRACE` telemetry (started/gridHold/speed/pedals/stage @1s + gridHoldTraceSeen) added at `95f69c6` to expose the frozen stage; the next DV run must carry it |
| Same, pre-manifest-fix attempt | 41826593001 | `9c422a4` | CI artifact | cancelled (completed) — not in flight |
| Product controls review (control rebinding/settings in the real product flow) | 41830853569 | `d9940a2` | CI artifact | failed — diagnosed by hud-camera (per `5305458`/`99964a4`): failing BEST-lap assertion precision; fix `5305458` merged |
| Product controls re-run at the fixed head | 41835648940 | `f2eba7e6a6639ea4ffea239123185fa85db55132` (merge of `99964a4`) | CI artifact | failed — remaining bug: standings header-row (header vs data rows; per coordinator, hud-camera fixing; root cause + by-name fix landed at `6ffdf63` AFTER this run) |

## Continuous lap

| Claim | Run | Commit | Artifact path | Status |
|---|---|---|---|---|
| Clean continuous lap, jolt/240 Hz/CCD/rotational: `finished=true`, `laps=1`, `bestLap=87.52s`, `damage=0`, `impactEvents=0`, `ruleResets=0`, 4 tyre contacts, no chassis contacts (content per packet — metrics not re-read this session) | 41799538756 | `5d21c56` | `outputs/repair-20261029/lap-jolt-41799538756/encrypted-review-0-attempt-1/game-review-41799538756-1-0/review/` ✓disk (89.6s video + telemetry) | machine-verified metrics; video human-pending — NOT yet watched by the human reviewer |
| Earlier source-solver lap attempt: 17 impacts, damage 0.27 | 41794252920 | `71dfc6c` | (per JOB_REGISTRY) | failed, preserved — superseded by the jolt run above |

## HUD bounds (per-resolution)

| Claim | Run | Commit | Artifact path | Status |
|---|---|---|---|---|
| Objective HUD bounds: `Hud.EmitRects()` at capture time + `reference_evidence` asserts speed/gear/info/minimap/standings/hybrid rects inside viewport at 640x360…2560x1080 | first run 41820839687 | `076363b` | CI artifact | failed (shard 0, audit step) — superseded by the row below; no bounds result produced |
| Same claim at the fixed head (`5bf41d7` CS0103 fix in the merged mirrored-text detector) | 41825439700 | `5bf41d7` | CI artifact | in progress at this refresh — human-pending (no result yet); bounds claim OPEN |
| Reference-evidence run at the inventory-era head | 41830989890 | `d9940a2` | CI artifact | failed — NOT in flight (the tasking listed it as in-flight; `gh run view` shows completed failure at `d9940a2`); cause not in available metadata |
| HUD visual improvements at 5 resolutions — EXISTENCE-ONLY, no bounds asserts | 41798036351 | `e8b80bb` | `outputs/repair-20261029/car-ev-41798036351/` ✓disk | machine-verified (existence only) — superseded for the bounds claim by the rows above |

## Wheel closeups

| Claim | Run | Commit | Artifact path | Status |
|---|---|---|---|---|
| All 4 wheels + steered-FL real closeups, engine gates pass (`corners=32`, not whole-car) | 41798036351 | `e8b80bb` | `outputs/repair-20261029/car-ev-41798036351/` ✓disk | machine-verified; human-pending (wheel/wing clearance unproven from occluding perspectives per VISUAL_REVIEW 2026-11-04T09:10Z) |
| Clearance sweep v2 (suspension-state poses, full lock, labelled worsts) — first dispatch, previously opt-in and never run | 41832997990 | `71dbbe1` | CI artifact | failed — died mid-sweep at the 420 s default timeout, AFTER emitting real worsts including **-45 mm bodywork penetrations at full lock** (content per commit `e17fdf5`); the timeout fix (`e17fdf5`: sweep-case timeout 1500 s + per-wheel sweep evidence asserted) followed |
| **Clearance sweep v2, completed** | **41837315358** | `e17fdf5` (`e17fdf57ad5e4454cf88c35cd7039a6ea0fb53c8`) | per-car `41837315358` wfr dirs (CI artifact); evidence: `sweep.perWheel` worsts + `WHEEL_CLEARANCE_WORST` lines | **machine-verified** (run SUCCESS; per-wheel sweep evidence asserted by the audit per `e17fdf5`) — human-pending: the negative-penetration worsts (e.g. the -45 mm class) need human review before any clearance acceptance |
| car_evidence sweep v2 | 41831318759 | `4d0622f` | CI artifact | failed — NOT in flight (the tasking listed it as in-flight; `gh run view` shows completed failure at `4d0622f`); cause not in available metadata |
| Earlier `wheel-fl` capture was a whole-car three-quarter image, not a closeup | 41788844369 | `e41e29b` | `outputs/repair-20261029/cloud/reference-evidence-41788844369/` ✓disk | rejected (VISUAL_REVIEW 2026-11-04 item 4) |

## Cockpit

| Claim | Run | Commit | Artifact path | Status |
|---|---|---|---|---|
| Genuine cockpit view: `COCKPIT_VIEW mode=2 fov=75 seatLocal=(0,0.88,0.05) seatOffsetFromAuthored=0.000 wheelsHidden=True` (authored seat, not overhead) | 41798036351 | `e8b80bb` | `outputs/repair-20261029/car-ev-41798036351/` ✓disk | machine-verified; human-pending (halo dominance / instrument readability / driveability with motion open) |
| Earlier cockpit frame was an overhead whole-car image, not cockpit evidence | 41788844369 | `e41e29b` | `outputs/repair-20261029/cloud/reference-evidence-41788844369/` ✓disk | rejected (VISUAL_REVIEW 2026-11-04 item 5) |

## Fences / foliage / alpha

| Claim | Run | Commit | Artifact path | Status |
|---|---|---|---|---|
| true-Alpha blend candidate reduces fence speckle | 41632524155 | `5492d90` | `outputs/repair-20261029/cloud/fence-true-blend-result/` ✓disk | rejected (no speckle reduction; evidence kept) |
| A2C candidate (alpha-to-coverage + alpha-to-one over 4x MSAA) reduces fence speckle | 41647350933 | `6d2069a` | `outputs/repair-20261029/cloud/fence-a2c-result/` ✓disk | rejected (edge density unchanged in 8/9 cells; VISUAL_REVIEW rejection stands) |
| mip-repair candidate (`fence_miprepair_review`) | 41795630650 (`5288329`): failed on the controls probe — fence evidence never produced; 41818948189 (`9ff443c`): FAILED mixed (shards 0/3/4 audit-step failures; 1/2/5 ok; schema fix postdated the run); **41830995246 (`d9940a2`, invariant reads the flat schema): FAILED — verified this session: shards 0 and 4 failed, 1/2/3/5 succeeded; shard 4's exception is `RuntimeError: track-28 failed; see saved logs` raised in the audit's GENERIC track block (cloud_game_audit.py:710), NOT in the miprepair invariant code — cause of the track-0/28 engine failures TBD (saved logs); the evidence DID ship via `if: always()`: artifacts `game-runtime-0..5-attempt-1` + `encrypted-review-{0,1,2,4,5,...}-attempt-1` listed** | `d9940a2` | CI artifacts (listed via the API this session) | failed — the fence-miprepair INVARIANTS have still never failed; the failures are in the generic per-track runs; the shipped encrypted-review artifacts for the successful shards are the candidate's evidence and await extraction/viewing |
| Baseline fence/grandstand speckle + black foliage masses | visual reviews 2026-11-03 and 2026-11-04 | — | see `VISUAL_REVIEW.md` | rejected (standing defect; no fix accepted) |

## UV candidates / liveries

| Claim | Run | Commit | Artifact path | Status |
|---|---|---|---|---|
| car16 misc twin-hide A/B (implemented on the car-assembly branch, merged; runtime-3 in the evidence run is the twin-hide-off variant) | 41798036351 | `e8b80bb` (impl `09a3d16`/`ca7ce66`) | `outputs/repair-20261029/car-ev-41798036351/` ✓disk | machine-run only; human-pending — VISUAL_REVIEW 2026-11-04T09:10Z: do not promote on cleaner silhouette alone |
| car16 V-only UV-candidate A/B (the ten pending entries) | NONE — the leg was INERT in every run so far: `cloud_game_audit.py` wired `SOURCE_UV_CANDIDATES` but the `CarLoader` gate read `MYSIM_SURFACE_UV_CANDIDATES` (env mismatch; verified by car-assembly-turn2 against the e8b80bb blobs; fix `e41d4f4` accepts both spellings, merged) | fix `e41d4f4` + detector `80ceb22` (both merged) | — | **never run** — the "pending UV candidate NOT accepted" verdict is NOT evidence against the candidate; a run at a head with `e41d4f4`+`80ceb22` must show `SURFACE_UV_REPAIR_CANDIDATE` lines (leg live) + `MIRRORED_TEXT_RISK_SUMMARY` before any UV-candidate verdict |
| Mirrored logos/numbers root cause (UV orientation/handedness per surface) | none yet — detector `80ceb22` emits the per-surface taxonomy (`ok/mirrored_text_risk/rotated_text_risk/flipped_text_risk/mixed_risk/...`) for the next run | — | — | open — awaiting the candidate-enabled run + human review before any livery change |

## Exported-player user-path facts (verified from committed source + run 41828819144's evidence; session 8)

| Question | Verified answer | Source |
|---|---|---|
| Packaged player's DEFAULT render backend | `mobile` (`project.godot:11 rendering_method="mobile"`) — while ALL packaged-player evidence (smoke/render/persist) forces `--rendering-method gl_compatibility` | `project.godot`; `tools/cloud_windows_smoke.py`/`cloud_windows_render.py` (the flag is in every invocation) |
| Default control scheme (fresh install, desktop) | Keyboard physical keys + gamepad; touch OFF (申诉`Settings.ScreenControlsEnabled` = `MYSIM_TOUCH` env override, else `DisplayServer.IsTouchscreenAvailable()` = false on desktop) | `src/Settings.cs`; `src/ControlBindings.cs` |
| Default bindings | WASD + arrows (both live), Space handbrake, P pit, C camera, M mute, H ERS auto/manual, E overtake; gamepad stick/RT/LT/A/Y/Start; per-action overrides persisted as `key_<n>` (long) — the `legacy_long_roundtrip` case covers exactly this path | `src/ControlBindings.cs` (`Defaults` array) |
| Saved-settings defaults | `mute=true` by default — a fresh install starts MUTED (user-path finding: first-run experience is silent until M pressed); `screen_controls` false on desktop | `src/Settings.cs` defaults |
| Required assets path (what the export ships) | `include_filter` ships `assets/generated_native_car_hulls/*.json`, `data/lighting_profiles.json`, `data/weather_profiles.json`, `data/car_surface_uv_repairs.json`, `assets/*.json`, `assets/RealRacing/*.json`, `assets/Resources/*.txt`, `assets/StreamingAssets/*`; textures/meshes pack via Godot's imported-resource pipeline automatically; `exclude_filter` keeps outputs/tools/builds out | `export_presets.cfg` |
| Proof the packaged asset path resolves on Windows | run 41828819144's render step passed its asserts (960x540 non-uniform image + `PASS complete visible car framing` + `onSurface=True` + OpenGL renderer seen) — the packaged player loaded and rendered the real car/track assets (the script's own gates; run SUCCESS) | `tools/cloud_windows_render.py` contract + run 41828819144 |
| Windowed vs headless on the packaged player | the render smoke runs WINDOWED (no `--headless`; `--rendering-method gl_compatibility --resolution 960x540 --fixed-fps 60 --audio-driver Dummy`) — a windowed,input-capable run on the Windows runner is proven feasible | `tools/cloud_windows_render.py` |
| Input path the user's keys take | key → `Input` → `ControlBindings.Held()` polls `Input.IsPhysicalKeyPressed` (+ arrow fallbacks) per frame → `DriveController`; `Held()` returns false when the window is unfocused UNLESS `MYSIM_INPUTCHECKS` is set (the injection escape hatch, already in the shipped code) | `src/ControlBindings.cs:7`; `src/Main.cs:211` |
| Existing synthetic-input mechanisms | `Input.ParseInputEvent(new InputEventKey{Keycode,PhysicalKeycode,Pressed})` — PROVEN in-repo (`ControlsScenario.KeyEvent`, `DrivingScenario.KeyState`, `ProductControlsScenario`); `DrivingScenario` already asserts reverse/pause/resume/camera THROUGH this path on CI | `src/ControlsScenario.cs:7`, `src/DrivingScenario.cs:7` |
| What DV covers vs the gap | DV drives at the SIM LAYER: ` DrivingValidation` sets `ph.Throttle/Brake/Steer` directly — it never touches the Input/device layer (verified: no ParseInputEvent/InputEvent in DrivingValidation.cs). GAP: no scripted DEVICE-level input run exists on the packaged player; no input-event logging; no ramp/sine/slalom/hairpin/trail-brake vocabulary | `src/Logic/DrivingValidation.cs:103` vs `src/DrivingScenario.cs` |

## User-path recording spec (DESIGN — proposed harness; nothing implemented)

Full spec in `outputs/mistral-orchestration/agents/verification-turn8.md` §2 (this agent's file). Summary:

| Element | Design |
|---|---|
| Harness | NEW `src/UserPathScenario.cs` (proposed; owner to assign): early-exit host (one `MYSIM_USERPATH=<profile>` hook in `Main._Ready`, coordinator-owned line) that boots the REAL game world (track/car env as usual) + a driver node reading a JSON script `[{t, key|axis, down|up|value}]`; injects via the PROVEN `Input.ParseInputEvent` path (keyboard: key+physical keycode; gamepad: `InputEventJoypadMotion` for true analog ramps/sine); requires `MYSIM_INPUTCHECKS=1` (existing gate) and a WINDOWED packaged run (proven feasible by the render smoke) |
| Logging (the requirement) | every injected event appended to `userpath-events.jsonl` (t, device, code, value, direction); every 0.1 s a telemetry row (speed, position, yaw, gear, PLUS the `ControlBindings.Held()` echo — what the game actually SAW, closing the loop); final JSON report + N frames through the run (not just the last); the input log + the Held()-echo together prove injection reached the game |
| Profiles (the DV gap) | `throttle_step`, `brake_step`, `throttle_ramp` (keyboard pulse-train with duty ramp; gamepad RT for true ramp), `steer_sine` (pulse-train + gamepad axis sine), `slalom` (alternating steer pulses at speed), `hairpin` (approach, full lock, throttle modulate), `trail_brake` (brake held through steer-in, released under steer), `reverse_hold_then_forward` (extends the proven DrivingScenario leg) |
| Acceptance per profile | response class matches input (sign + latency band + magnitude class), zero non-finite, zero unexpected ERROR lines (per-stage expected-error counts as in the persist driver), AND a determinism check: re-injecting the recorded event log reproduces the telemetry within tolerance |
| Where it runs | `windows-build.yml` after the persist step on the PACKAGED player (proposed `tools/cloud_windows_userpath.py` driver, mirroring the persist driver's conventions); Linux repo runs optional for CI-fast feedback |

## Fleet / night / P1 / axle legs (runs since 41849732286; verified via gh this session)

| Leg | Run | Commit | Status (verified) | Note |
|---|---|---|---|---|
| P1-LEG-A (badges candidates removed) | 41847896437 | `adf89ca` (matches the queue's intended ref — verified) | FAILED | queue idx=0 (p1queue-log.txt); cause TBD from logs; the head predates the `a613a9a` `--resolution 1280x720` fix (the canvas=64x64 headless-default failure class) — plausible cause, UNVERIFIED; leg B not yet dispatched (state `next=1`) → NO A/B conclusion possible |
| fleet_evidence (fleet leg) | 41848086075 | `b5ca3e0` | in progress | |
| fleet wheel sweep batch 1 (cars 0,1,2,3) | 41849732286 | `6505f16` | in progress | the `6505f16` plan: 5 dispatches x 4 cars cover the 24-car fleet |
| fleet wheel sweep batch 2 (cars 0,1,2,3) | 41854428291 | `6263c51` | pending | carries the axle-fit fix: `6263c51` — the source_axle_fit input now actually reaches the depth gate (the car-assembly catch: the fit leg ran INERT twice) |
| night_evidence | 41849395054 | `e76a1e1` | in progress | after the `e76a1e1` shard-expression repair |
| night evidence (paired night frames) | 41849100640 | `110fd7f` | failed | same-camera paired night frames (title truncated in gh); cause TBD |
| axle-fit verification (cars 16) | 41843317303 | `a996a9d` | success | wheel_fit_review single-car |
| evidence rewrap | 41847531679 | `684285d` | success | evidence infrastructure |
| DV further attempts | 41840755243 (`95f69c6`, failure), 41848766195 (`b5ca3e0`, failure), 41854714029 (`0de3123`, in progress) | — | mixed | the DV_TRACE-era series |
| product further attempts | 41841959304 (`c057ce0`, failure), 41845517756 (`ccca290`, failure), 41852901703 (`a613a9a`, in progress) | — | mixed | the resolution-fix era |

## P1 / RIDE-HEIGHT A/B dispatch-queue tracking (serial watcher — live record)

The coordinator runs a serial dispatch queue (`outputs/mistral-orchestration/p1-queue-watcher.ps1`, committed at `5f2b9ee`; watcher PID live per the tasking). Queue definition (read from the script this session):

| # | Tag | Ref | Depth | Inputs | Intended variable |
|---|---|---|---|---|---|
| 0 | P1-LEG-A badges-candidates-removed | `mistral/p1-badge-baseline` (=adf89ca `adf89cad3c9c43836054088299693645c2f22b08`) | car_evidence | cars=16 | badges candidate entries REMOVED |
| 1 | P1-LEG-B badges-candidates-active | `39cdf33` | car_evidence | cars=16 | (same lineage, candidates active) |
| 2 | RIDE-A baseline | `codex/repair-20261029` | native_geometry_recovery | cars=16, jolt, 240Hz, rotational=true | clean-lap config, NO ride-height |
| 3 | RIDE-B ride-height candidate | `codex/repair-20261029` | native_geometry_recovery | cars=16, jolt, 240Hz, rotational=true, native_ride_height=true | +native_ride_height ONLY |

**Confound checks (my standing duty as each dispatch lands):**

- **Leg A vs Leg B — VERIFIED CLEAN (pre-flight, from the committed trees):** `git diff adf89ca 39cdf33` touches exactly ONE file (`data/car_surface_uv_repairs.json`; 300+/315- is re-serialization noise). Semantic comparison of the two JSONs: legA=22 entries, legB=25; the ONLY additions in B are the three badges entries (`16/LOD_A_BODY_mm_badges`, `16/LOD_A_FRONTBUMPER_mm_badges`, `16/LOD_A_REARBUMPER_mm_badges`, all `pending:true`); ZERO entries only-in-A; ZERO payload differences (scale/offset/texture/guid/pending) on the 22 shared entries; badges-mention counts 0 vs 9. **The pair differs ONLY in the intended variable. No drift.**
- **RIDE-A vs RIDE-B — pre-verified at queue level:** identical refs and inputs except `native_ride_height=true` on B only (from the watcher script). Confound risk to check when the runs land: BOTH legs run at `codex/repair-20261029` — a MOVING ref; if a commit lands between the two dispatches, the pair differs by CODE, not just the flag. Mitigation: compare the two runs' headSha at recording time; flag any difference.
- **Watcher caveat (recorded):** the watcher records the dispatched run id via `gh run list --limit 1` 15 s after dispatch — under concurrent load this can bind the WRONG run id. Every recorded id gets verified against its expected ref/depth (headSha + workflow + display title) before being entered here as evidence.

**Dispatch log (updated as each dispatches; from p1queue-log.txt / p1queue-state.json + gh verification):**

| # | Tag | Recorded run id | Verified HEAD | Verified status | Confound check |
|---|---|---|---|---|---|
| 0-3 | — | — | — | — | no dispatches had occurred at this recording (p1queue-log.txt / p1queue-state.json absent; checked 2026-11-04 session 5) |

## TEXTURE-INVENTORY QC (addendum, turn 3)

Applies to the texture-inventory deliverables of the carbody-livery and track-render agents (`agents/*turn*` files) when they land. Every inventory ROW claiming a texture-level finding must carry ALL SIX fields below; the QC flags every empty/hand-waved field. Until an inventory passes this QC, none of its rows may be cited as acceptance evidence.

Required per-row schema:

| # | Field | Valid form | Hand-waved (flag these) |
|---|---|---|---|
| 1 | atlas GUID + file | BOTH the source GUID (as resolvable in `guid2path.json`/`car_map.json`) AND the resolved file path under `assets/` | file name only (`ext_16.png`), "the livery atlas", "misc atlas", no GUID |
| 2 | dimensions | WxH pixels of the actual imported texture (e.g. `512x512`; the `512²` shorthand is accepted) | blank, `various`, `see atlas` |
| 3 | UV channel | which channel the finding applies to: `UV0` / `UV2` / `none-bound`, plus whether the material uses the UV-flip policy | unqualified `UV`, blank |
| 4 | orientation class | controlled set: `as-authored` / `mirrored-X` / `rotated-90` / `transposed` / `mixed-per-island` / `indeterminate (+why)` | prose ("looks flipped"), blank |
| 5 | defect taxonomy value | controlled set: `black-card` / `alpha-bleed-dark-RGB` / `mid-alpha-scissor-loss` / `sub-pixel-coverage-collapse` / `mirrored-UV` / `wrong-scale` / `compression-artifact` / `low-res-source` / `none` | adjectives ("ugly", "broken", "bad"), blank |
| 6 | correction family | controlled set: `none-needed` / `mip-rebuild` / `uv-remap-per-island` / `uv-remap-per-surface` / `threshold-change` / `material-rebinding` / `geometry-fix` / `source-art-fix` | `TBD`, "fix later", blank |

Cross-checks the QC performs when an inventory lands: (1) every GUID resolves to the stated file and vice versa; (2) stated dimensions match the audit/manifest metadata where available; (3) rows classed `as-authored` must state the UV-flip-policy exception (per track-render-report §1.1, `uv_scale=(-1,-1)`/`offset=(1,1)` applies to non-`org_`/non-crowd names); (4) every row with a defect value != `none` names exactly one correction family (or `none-needed` with a reason); (5) every row cites the run/commit whose evidence produced it — uncited rows are flagged; (6) no row may contradict a REJECTED entry above without a new run ID.

QC status — the formal inventories LANDED at `dc8e061` (`outputs/mistral-orchestration/inventory/{car16,car6,track0_brands_hatch}.{md,json}`; generated read-only from source metadata). QC performed 2026-11-04 session 4 by reading the committed files:

| Inventory | Rows (verified) | Field 1 GUID+file | Field 2 dimensions | Field 3 UV channel | Field 4 orientation class | Field 5 defect taxonomy | Field 6 correction family | Verdict |
|---|---|---|---|---|---|---|---|---|
| car16 (car, json+md) | 15 | PARTIAL — full file paths; GUIDs present per BINDING (`boundTo[].guid`, 125 bindings; sample `a903631edbed0e5cf8f2a64dc5a09eb2` for `LOD_A_BODY_mm_badges`) but rows with ZERO bindings (unbound textures: `arms`, `driver_16`, `driver_5`, `sha`, `wheel_blur`, `ext_5`) carry NO guid | YES (`px`+`width`/`height`, exact) | **MISSING** (0 `uv*` fields anywhere) | PARTIAL — repair entries carry `scale=[1,-1]`/`offset` + `pending` (the flip state) but no orientation-class value; rows without repairs say nothing | **MISSING** — no defect field; rows with `repairs: []` are ambiguous (no-defect vs not-assessed) | PARTIAL — `pending:true` implies `uv-remap-per-surface` pending, but no explicit family field | GAPS: UV channel (all rows), defect taxonomy (all rows), orientation class (explicit), GUID on unbound rows. The binding/repair data itself is precise and traceable |
| car6 (car, json+md) | 17 | PARTIAL — same shape (148 binding GUIDs; 9 rows have bindings; 8 unbound rows lack GUID) | YES | **MISSING** | PARTIAL (2 rows carry repair scale/offset+pending; 15 rows say nothing) | **MISSING** | PARTIAL (2 rows imply pending uv-remap) | GAPS: same as car16 |
| track0_brands_hatch (track, json+md) | 72 | **MISSING — 0 GUIDs in the file; ALL 72 rows have empty `boundTo` and empty `repairs`** (verified: 0/72 rows with bindings; 72/72 md rows end `- | -`) | YES (exact) | **MISSING** | **MISSING — none** | **MISSING — none** | GAPS: the track inventory is currently a FILE LISTING (file/px/bytes/import), not an inventory: no texture-to-surface/material linkage at all, no UV, no orientation, no defects, no corrections. It cannot support any acceptance claim in this form |

QC verdict: NONE of the three inventories satisfies the six-field contract. Blocking gaps, in priority order: (1) track0 has NO linkage to materials/surfaces — every acceptance-relevant field is empty; (2) all three lack the defect-taxonomy field entirely — without it, "no defect recorded" is indistinguishable from "not assessed"; (3) all three lack an explicit UV-channel field (the car inventories' repair entries imply UV0 but never state it; the UV-flip policy makes the effective channel question non-trivial); (4) orientation class exists only implicitly via repair `scale`/`pending` on the few repaired rows. The car16/car6 inventories' existing columns (file/px/bytes/import/boundTo/repairs) are accurate and traceable as far as they go — the gap is coverage of the SCHEMA, not wrongness of the data. Requested additions (owner of the inventory generator): per-row `guid` (resolvable for unbound textures via the source maps), `uvChannel`, `orientationClass`, `defectTaxonomy`, `correctionFamily` per the controlled vocabularies above; for track0 additionally the material/surface linkage that the car inventories already have.

## Scope and honesty notes

- This index covers ONLY the claim families listed in the coordinator's tasking (persist, lap, HUD bounds, wheel closeups, cockpit, fences, UV). The full 15-stage scope (all cars/tracks, weather/night, racing AI, pits, menus, championship, LAN, audio, performance, asset acceptance, final Windows e2e) has far more open claims — see `outputs/repair-20261029/TASK_BOARD.json` and `CONTINUE.md`.
- "machine-verified" reflects the named run's automated gates only. No entry in this table constitutes visual acceptance, motion acceptance, or stage-10/15 closure.
- Zero-assertion passes, self-comparisons and content-free greens are excluded by construction (see the fixture's contract rules and the packet's history).
- Update procedure: re-verify run states with `gh run view --json`, re-check artifact paths on disk, add the row, never delete rejected/failed rows.
