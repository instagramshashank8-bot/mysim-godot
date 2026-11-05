# verification agent — turn 9 report (implementation of the user-path spec)

New files (all mine, committed this turn): `src/UserPathScenario.cs`, `tools/cloud_windows_userpath.py`, `data/userpath_profiles.json`. NO dispatches; the workflow wiring + Main.cs stay with the coordinator (requests below).

## 1. src/UserPathScenario.cs — the JSON-scripted device-level input harness

Implements the turn-8 spec exactly: reads `MYSIM_USERPATH_SCRIPT` (JSON: profile/durationSeconds/startOnRaceStart/events/asserts), injects through `Input.ParseInputEvent`, samples at 10 Hz (the 10-action `ControlBindings.Held()` echo + speed/heading/yawRate/steering/rpm/gear/input.throttle/brake/steer/position/damage), writes `userpath-events.jsonl` + `userpath-telemetry.json` + the `MYSIM_USERPATH_REPORT` verdict, exits non-zero on any band failure. The clock starts at player-ready or at `Race.started` (`startOnRaceStart`, default true — profile time is independent of load time and of the grid countdown); a 90 s wait limit fails loudly (names the grid-hold class) instead of hanging CI.

Verification done BEFORE writing (no local build exists — this is the evidence trail):
- Keyboard injection: godot 4.7.2-stable `core/input/input.cpp` `_parse_input_event_impl` updates `physical_keys_pressed`, which `Input.IsPhysicalKeyPressed` (`ControlBindings.Held`) reads — plus the empirical proof from the existing green `ControlsScenario`/`DrivingScenario` runs.
- Gamepad injection: `Input.ParseInputEvent(InputEventJoypadMotion)` updates axis state (`set_joy_axis` in the same parse path; `Input::get_joy_axis` reads it) — but `DriveController` gates the pad branch on `Input.GetConnectedJoypads()`, and device registration happens ONLY in `Input::joy_connection_changed` (OS-driven; **no script-level joypad-add API exists in 4.7.2** — verified by searching the engine source). Therefore the shipped profiles are KEYBOARD-ONLY (the game's default scheme; the honest, working path); `device:'pad'` stays in the schema for a future virtual/HIL setup.
- State surface: `Vehicle` fields (position/velocity/heading/yawRate/steering/speed/rpm/gear/input/damage — `src/Logic/Simulation.cs:115+`), `Race.started/paused` (`Simulation.cs:326`), `ControlBindings.Actions/Defaults/Held` — all read from the actual sources.
- The human path is live by default: with `MYSIM_AI`/`MYSIM_AUTODRIVE` unset the player vehicle is human-controlled (`DriveController` applies `ControlBindings` input; the driver strips all `MYSIM_*` vars then sets only what it needs: `MYSIM_USERPATH`, `MYSIM_INPUTCHECKS=1`, `MYSIM_RIVALS=0`, `MYSIM_NOPIT=1`).

Self-review caught and fixed BEFORE commit (no compiler exists locally — this list is the review record): missing `ScriptDoc` class; missing `using System.IO;`; a garbage first statement in the `yawRateMagnitudeAfter` case; inverted `heldEchoAtMost` semantics (now: held fraction <= maxFraction); dead/contradictory clock-start logic (rewritten clean); the `DrivingScenario` sibling removal moved to EVERY tick (it must act during the wait phase too — the rival script starts injecting at 0.8 s and kills the process at ~13 s); `Sample`/`ScriptDoc` fields as properties (System.Text.Json ignores fields).

## 2. HOOK REQUEST (coordinator-owned Main.cs — one line + one guard)

At `src/Main.cs:211` (the world-build path, where `drive` exists):

```csharp
// BEFORE:
if (System.Environment.GetEnvironmentVariable("MYSIM_INPUTCHECKS") != null) world.AddChild(new DrivingScenario { Drive=drive });
// AFTER:
if (System.Environment.GetEnvironmentVariable("MYSIM_INPUTCHECKS") != null && System.Environment.GetEnvironmentVariable("MYSIM_USERPATH") == null) world.AddChild(new DrivingScenario { Drive=drive });
if (System.Environment.GetEnvironmentVariable("MYSIM_USERPATH") != null) world.AddChild(new UserPathScenario { Drive=drive });
```

Existing `MYSIM_INPUTCHECKS` behaviour is unchanged when `MYSIM_USERPATH` is unset. My node additionally defends itself (removes a `DrivingScenario` sibling on every tick) so the harness works even before the guard lands — but the guard is still wanted (it stops the wasted spawn).

## 3. tools/cloud_windows_userpath.py — the packaged-player driver

Mirrors the proven persist-driver conventions: per profile TWO passes (script = the profile's events; replay = the RECORDED event stream of pass 1 converted back to a script) with a fresh `%APPDATA%\Godot\app_userdata\MySim` before each; windowed packaged exe (`--rendering-method gl_compatibility --resolution 960x540 --fixed-fps 60 --audio-driver Dummy` — the exact proven render-smoke shape); env stripped of `MYSIM_*` then set to `MYSIM_USERPATH`/`MYSIM_INPUTCHECKS=1`/`MYSIM_RIVALS=0`/`MYSIM_NOPIT=1`; verifies per pass: report exists + `failed==0` + `passed>=1` + exit 0 + events-injected == events-expected + no `ERROR:` lines (these runs inject no faults, so zero expected); then the DETERMINISM re-injection check (spec requirement): the two passes' telemetry must match within tolerance (speed 0.15 m/s, heading 0.03 rad, steering 0.02, yawRate 0.05, position 0.6 m; sample-count delta <= 2) — a mismatch fails the profile with the exact deviations listed. Summary JSON + non-zero exit on any failure; a missing report (unwired build) fails loudly with a clear message. Syntax-validated via `python -m py_compile` (allowed: metadata-level, no engine);profiles validated against my own schema by a throwaway checker (9 profiles, 0 errors; every assert kind/action name/key verified against the C# + `ControlBindings`).

## 4. data/userpath_profiles.json — the 9 profiles

`probe_key_echo` (the W press -> Held(0) echo within a frame — the probe stays), `throttle_step`, `brake_step`, `throttle_ramp` (keyboard duty ramp 0.2→1.0 through the game's own pedal filter), `steer_sine` (alternating A/D, 1.5 s half-period, under throttle), `slalom` (0.7 s cadence at speed), `hairpin` (approach, held full lock with throttle modulation; direction-AGNOSTIC yaw check — the sign convention belongs to the DV `input_direction` profile at the sim layer, not assumed here), `trail_brake` (brake held THROUGH turn-in: simultaneous brake+steer window asserted, rotation continues, throttle returns while still steering), `reverse_hold_forward` (extends the proven DrivingScenario leg: reverse ≤ -1 m/s, then forward). All thresholds deliberately conservative (pass on a working game, fail on a broken one); validated against the schema by the throwaway checker (9 profiles, 0 errors).

## 5. Workflow hookup request (coordinator-owned)

In `windows-build.yml`, after the persist step: `- name: User-path input assertions on the packaged Windows player` / `run: python tools/cloud_windows_userpath.py`, and add `build-cloud/userpath-*` to the diagnostics artifact paths. NOTE on runtime: 9 profiles x 2 passes of windowed software-GL rendering is the long pole (each pass = boot + the profile duration rendered every frame at fixed-fps); if the job budget gets tight the profiles can be split across shards via the driver (a `--only` filter would be a 3-line addition — ask me), but the DEFAULT runs everything (no silent reduction).

## 6. Honest limits

- No compile exists for the C# (no local builds allowed); the review record is in §1 and the first cloud run is the compile gate. The Python + JSON are machine-validated (py_compile + json.tool + the schema checker).
- The determinism check assumes fixed-fps determinism of the sim layer (the same assumption the existing replay/campaign harnesses make); if the platform layer introduces nondeterminism (threading), the check will surface it — that is its job, not a flake to be silenced.
- The gamepad user path (real sticks/triggers) remains UNTESTABLE on cloud runners (verified engine limitation above) — it needs a human/HIL session; the schema and code are ready for it.
