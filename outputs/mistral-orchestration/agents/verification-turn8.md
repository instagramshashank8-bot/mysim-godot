# verification agent — turn 8 report (2026-11-05)

Deliverables: (1) exported-player user-path verification (facts recorded in EVIDENCE_INDEX §Exported-player user-path facts); (2) the user-path recording SPEC (below); (3) EVIDENCE_INDEX extended with the runs since 41849732286 + the fleet/night/P1/axle legs + the P1 leg-A dispatch record. Branch rebased onto `0de3123`. No dispatches; gh reads + the one authorized read-only artifact work only.

## 1. Exported player: default controller/backend + assets path (VERIFIED from committed source + the packaged run's evidence)

- **Default render backend: `mobile`** (`project.godot:11`) — every packaged-player evidence path (smoke/render/persist) forces `--rendering-method gl_compatibility`. FINDING: the packaged DEFAULT backend has NO render evidence; the verified Windows evidence is on a different backend than the shipped default.
- **Default controls: keyboard + gamepad; touch OFF.** `Settings.ScreenControlsEnabled` = `MYSIM_TOUCH` override, else `DisplayServer.IsTouchscreenAvailable()` (false on desktop). Bindings: WASD + arrows (both live), Space/P/C/M/H/E; gamepad stick/RT/LT/A/Y/Start; per-action overrides persist as `key_<n>` (long) — exactly the path `legacy_long_roundtrip` covers.
- **Fresh-install default is MUTED** (`mute=true` default in `Settings.cs`) — a first-run user-path finding worth a product decision.
- **Assets path:** the export's `include_filter` ships the JSON/txt data (hulls, lighting/weather profiles, uv repairs, RealRacing JSONs, Resources, StreamingAssets); textures/meshes pack via the imported-resource pipeline; `exclude_filter` keeps outputs/tools out. Run 41828819144's render step (windowed, 960x540, non-uniform image, car-framing + onSurface markers, OpenGL seen) proves the packaged asset path resolves and renders on Windows.
- **Input path:** key → `Input` → `ControlBindings.Held()` (polls `Input.IsPhysicalKeyPressed` + arrow fallbacks) → `DriveController`; `Held()` gates on window focus UNLESS `MYSIM_INPUTCHECKS` is set (the escape hatch already ships).

## 2. USER-PATH RECORDING SPEC (design; nothing implemented — owner to assign)

**Goal:** prove the EXPORTED player responds to real device-level input (the user path: key/joystick events → Input → ControlBindings → DriveController), with the input events and the telemetry recorded, on cloud Windows.

**The gap, precisely (verified in source):** `DrivingValidation` sets `ph.Throttle/Brake/Steer` DIRECTLY on the simulation — it never touches the Input/device layer. The only device-level exercisers are `DrivingScenario` (a hardcoded 13 s reverse/pause/resume/forward/camera sequence, CI Linux) and `ControlsScenario` (rebinding). No scripted device-level run exists on the packaged player; nothing records the injected event stream; no ramp/sine/slalom/hairpin/trail-brake vocabulary exists.

**Harness (proposed `src/UserPathScenario.cs` + `tools/cloud_windows_userpath.py`; owners to assign — not my files):**
1. Entry: one line in `Main._Ready` (coordinator-owned): `MYSIM_USERPATH=<profile>` → boot the REAL game world (track/car env as usual) + the driver node. Early-exit host pattern like the other scenario hooks.
2. Injection: the PROVEN in-repo mechanism — `Input.ParseInputEvent(new InputEventKey{Keycode=k,PhysicalKeycode=k,Pressed=p})` (keyboard; both keycodes set — the existing helpers' pattern that already passes CI) and `InputEventJoypadMotion` (真实 analog: RT/LT triggers for ramps, left stick X for true sine steering). Requires `MYSIM_INPUTCHECKS=1` (the shipped unfocused-window gate) and a WINDOWED packaged run (proven feasible: the render smoke runs windowed on the Windows runner).
3. Script format: JSON `[{ "t": seconds, "key": "W" | "axis": "left_stick_x"/"rt", "value": 0..1, "action": "down"|"up" }]` — or a named profile from a built-in library (below); the library generates the JSON so runs are reproducible and diffable.
4. **Recording (the requirement):** (a) `userpath-events.jsonl` — EVERY injected event (t, device, code, value, direction); (b) telemetry at 10 Hz — speed, position, yaw, gear, RPM + the `ControlBindings.Held()` ECHO of every action (what the game actually saw — closes the loop between injected and received); (c) the final JSON report (per-profile assertion results); (d) N frames spread through the run (not only the last).
5. Profiles (the DV-gap vocabulary): `throttle_step`, `brake_step`, `throttle_ramp` (keyboard pulse-train with duty ramp; gamepad RT for a true ramp), `steer_sine` (pulse-train + stick-axis sine), `slalom` (alternating steer pulses at speed), `hairpin` (approach, full lock, throttle modulation), `trail_brake` (brake held through steer-in, released under steer), `reverse_hold_then_forward` (extends the proven DrivingScenario leg).
6. Acceptance per profile: response CLASS matches input (sign, latency band, magnitude class — e.g. step-throttle → forward acceleration within the latency band; steer step → correct yaw-rate sign; slalom → lateral oscillation at the driven frequency; trail-brake → rotation continues under brake-off-throttle-off), zero non-finite states, per-stage expected-error counts (the pattern proven in the persist driver), AND a determinism check: re-injecting the recorded `userpath-events.jsonl` reproduces the telemetry within tolerance.
7. Where it runs: `windows-build.yml` after the persist step, on the PACKAGED player (the driver mirrors `cloud_windows_persist.py`'s conventions: per-profile runs, fresh state, report JSON, non-zero exit on failure, diagnostics artifact). Optional Linux repo runs for fast CI feedback.
8. First probe (cheap, inside the first implementation run): inject a synthetic W press, assert `ControlBindings.Held(0)` flips true within one frame — proves `IsPhysicalKeyPressed` reflects synthetic events with PhysicalKeycode set (the existing scenarios' PASS lines already imply it; the probe makes it explicit on Windows).

## 3. EVIDENCE_INDEX updates (runs since 41849732286 + fleet/night/P1/axle)

New section "Fleet / night / P1 / axle legs" (all verified via `gh run list`/`gh run view` this session): P1-LEG-A **41847896437 @ adf89ca FAILED** (queue idx=0; cause TBD; head predates the `a613a9a` resolution fix — plausible cause, unverified; leg B not yet dispatched, state `next=1` → no A/B conclusion); fleet_evidence 41848086075 (in progress); fleet wheel sweeps 41849732286 (in progress) + 41854428291 (pending, carries the axle-fit fix `6263c51` — the fit leg ran inert twice before); night 41849395054 (in progress) + 41849100640 (failed); axle-fit single-car 41843317303 (success); rewrap 41847531679 (success); the DV/product further-attempt series (41840755243, 41848766195, 41854714029; 41841959304, 41845517756, 41852901703). The P1/RIDE queue section now also records leg A's dispatch + failure in its live table.

## 4. Open

- P1 leg A's failure cause (logs); leg B dispatch (watcher-owned).
- The fleet/night/DV/product in-flight results (I record as they land).
- The spec's implementation ownership (src/UserPathScenario.cs + the Main.cs hook + the driver are not my files).
