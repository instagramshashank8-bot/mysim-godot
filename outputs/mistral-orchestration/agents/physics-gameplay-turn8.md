# physics-gameplay agent — turn 8 — steering-feel P0 — 2026-11-04

Role: physics/gameplay specialist. Branch `mistral/physics-gameplay` (this turn's commits on top of `6f174bc`). Measured with a headless chain harness (pure logic + the legacy Dynamics model compiled from this worktree, dt 1/240 s; throwaway, method + numbers recorded here), fixes committed in my files; the coordinator-owned actuators get PROPOSED diffs only. No dispatches; no physics constants invented.

## 1. The full chain, as traced (normalized input → front wheel → yaw)

| Stage | File (owner) | Exact behaviour / constants |
|---|---|---|
| 1 Keyboard intent | `ControlBindings.Held(2/3)` | boolean hold; baked arrow-key aliases (Up/Down/Left/Right) in `Held()`; unfocused-window zeroing |
| 2 Analog intent | `NativeRaceController` (coordinator) | left stick X: deadzone 0.08, then `sign·((|axis|−0.08)/0.92)^1.35`; triggers add via `max` (RT/LT); touch overrides; **assists=0/1 from this chain only affects TC/ABS, not steering** |
| 3 Smoothing | `src/Logic/SteeringInput.cs` (MINE) — **patched this turn** | keyboard: linear rate toward the request — attack was 2.6/s, release/reversal 5/s; analog path bypasses smoothing (direct); then `ToSimulation` flips screen→model sign once |
| 4 Pedal layer | `src/Logic/PedalController.cs` (MINE) | brake-to-reverse latch (|u|<1.2 m/s for 0.25 s); re-latch on throttle>0.05 with u>−0.5; steer passes through untouched |
| 5 Speed-dependent lock | `src/Logic/Dynamics.cs` (MINE) | `SteeringLock(u)=clamp((45−|u|)/37,0,1)·0.465+0.085` rad: 31.5° full at ≤8 m/s shrinking linearly to 4.87° at ≥162 km/h; `SteeringTarget = cmd·1.289·lock` clamped to ±lock; the lock-family is PINNED by an existing CoreChecks guard (must equal the previous formula exactly) |
| 6 Actuator | native `NativeSuspensionVehicle._IntegrateForces` (coordinator-owned): `WheelAngle=MoveToward(WheelAngle, target, 3.2·dt)`; legacy `Dynamics.Integrate`: same shape, 3.2 rad/s + crossing-snap | measured in the harness as the shared mathematical ramp |
| 7 Yaw/lateral generation | native: front-axle lateral demand `−capacity·tanh(α/0.12)` from the wheel-induced slip, friction-circle budget with TC/ABS reserves, then the **per-tick lateral-momentum clamp** `fy ≤ |side|·m_eff/(4·dt·…)`; legacy: same MF shape + a low-speed `Lerp(kin, rn, (u−3)/9)` kinematic-yaw blend | the native blend/clamp pair is the only unmeasured part of the chain (engine-only); see section 3 |

## 2. Measured before/after (the numbers) + the committed fix

The complaint trace (reviewer: improved but too constrained/static) is **measured** in the command chain, not the vehicle model:

| Metric (24 Hz-referenced chain, car-16 lock curve) | BEFORE (attack 2.6/s) | AFTER (attack 5.0/s — **committed**) | Verdict |
|---|---|---|---|
| Command t95 on a keyboard step (any speed) | 0.385 s | **0.188 s** | fixed |
| Full-lock front-wheel angle at 30 m/s, step | ≈0.55 s | **≈0.19 s** | fixed |
| 1 Hz ±1 correction sine: command peak | 0.746 | **1.000** | fixed |
| 2 Hz correction sine: command peak | 0.407 (−7.8 dB: a correction asked at 2 Hz arrives at 41%) | **0.629 (−2 dB); with the 3.2 rad/s actuator the achieved wheel angle holds 81% of the lock amplitude (0.222/0.274 rad)** | fixed |
| 0.5 Hz sine | 1.000 | 1.000 | unchanged (was never the problem) |
| Release decay (angle t95 from held lock) | 0.192 s | 0.192 s | unchanged (return rate was already 5/s) |
| Full-right→full-left zero crossing at 15 m/s | 0.20 s | 0.20 s | unchanged |
| Left/right symmetry | exact | exact | OK |
| Deadzone/analog transfer (measured, unchanged) | axis 0.15→command 0.031; axis 0.5→0.39 | same | progressive; left for the product gate to judge feel, not a measured defect |

**The vehicle-side battery (legacy model, car 16, fixed at attack-rate-independently — same before/after):** step-steer steady yaw 0.91/1.31/0.97/0.80 rad/s at 5/15/30/45 m/s (front-axle-limited balance; understeer-limited hairpin radius 13.8 m at 15 m/s ≈ 1.66 g against a 2.87 g tyre ceiling — the front axle carries ~52% of the load, so this IS full-lock saturation, not a clamp cheat); slalom lat peaks 2.05/1.20 m/s² at 20/30 m/s with no divergence; trail-brake (brake 0.4 + steer 0.5 at 25 m/s) no spin, lat peak 2.45; countersteer recovers (yaw settles 1.04 s after the counter); reverse latch engages (−11 m/s end state, steer-yaw sign correctly flips while reversing, forward re-latch works).

**Committed:** `src/Logic/SteeringInput.cs` (attack 2.6→5.0/s with the before/after in a comment) and permanent regression guards in `src/Logic/CoreChecks.cs` asserting the measured bands: 1 Hz peak ≥0.9, 2 Hz peak ≥0.55, full-lock angle ≤0.30 s at 30 m/s, release ≤0.30 s (plus the pre-existing lock-curve and symmetry guards). CoreRunner: PASS 88082 assertions; build 0 warnings/0 errors.

## 3. Coordinator-owned proposals (exact diffs PREPARED, NOT applied; pending native measurements)

1. **`NativeSuspensionVehicle` actuator rate (3.2 rad/s): NO change proposed now.** With the attack fix the total keyboard-to-full-lock response is ~0.19 s; the actuator is not the dominant term. Re-measure from the DV evidence once the driving validation lands on a moving car.
2. **The per-tick lateral-momentum clamp (`fy ≤ |side|·m_eff/(4·dt·…)`): measured analytically to eat low-speed steering authority — 64% of the front tyre capacity at 1 m/s full lock, 32% at 0.5 m/s** — while the kinematic turn at those speeds only needs ~2-7% of capacity. It also acts as the native model's only low-speed yaw blend (the legacy model has the proper `Lerp(kin, rn, (u−3)/9)`). PROPOSAL (only after the DV run measures the real native step-steer at low speed): replace the absolute authority cap with a per-tick force-increment bound (limit how much fy may CHANGE in one tick toward the demanded force, never limiting the demanded force below the momentum-conservative bound for more than the few ticks a transition needs). I will not commit any constant here without the native measurement; the DV input_direction/steering_rate_step samples in the pending run carry exactly the data needed (yaw vs command phase at 15 m/s).
3. **Analog curve (0.08/1.35): NO change** — measured transfer is progressive and smooth; the product gate (below), not the physics gate, judges the feel.

## 4. Defaults / saved settings (recorded for the gate)

`Settings.cs`: schema_version=2; defaults: `tc=true`, `abs=true` (assists ON in the product; the DV/physics runs force `MYSIM_ASSISTS=0`), audio mute default ON (muted, "default muted as requested"), last track/car/laps/camera persisted; atomic save + version gate (the persistence agent's stage). Controls: 9 actions (`Throttle, Brake/reverse, Steer left, Steer right, Handbrake, Pit request, Camera, Mute, ERS auto/manual`) with arrow-key aliases baked in `Held()`; remap persistence is exercised by the controls probe (incl. the `legacy_long_roundtrip` regression class).

**The product input gate (exported player + real input — the ONLY place the human chain validates end-to-end; spec for the next exported Windows cycle):**
1. Fresh-profile `settings.cfg` dumped before/after: remap a key (e.g. Throttle) → save → restart → remap active (the long-fix regression class, product path).
2. Keyboard feel measured, not adjectives: keypress→visible-full-steer ≤ ~0.4 s at the HUD/frame level (chain budget 0.19 s + video cadence), progressive (partial hold = partial lock), release decay ≤ ~0.3 s.
3. Analog: proportional sweep visible (no jumps at the deadzone edge), full authority by ~75% travel.
4. Steering cannot invert: LEFT key/stick-left turns left on screen (the ToSimulation mapping + the existing CoreChecks direction guard cover the logic; the product session proves the plumbing).
5. The five dynamic patterns (slalom, hairpin, trail-brake, countersteer, reverse-via-brake-latch) driven by hand on the exported player with the session recording + the assist states recorded per session.
6. Ride-height interplay: the exported session runs whichever configuration is adopted; assist states and the ride label (A/B) recorded in the same evidence.

## 5. DV tie-in + carry-forward

- The in-flight `41854714029` (defaults, guard-exclusion at `0de3123`) is the first DV expected to run on a MOVED car; its `input_direction`/`steering_rate_step` samples are the first NATIVE steering measurements. When it lands I will: (a) finish the DV judging per the turn-5 rubric; (b) measure the native step response against the harness references (yaw rise ~1.3 rad/s at 15 m/s, settle <2 s); (c) decide whether proposal 2 (the momentum clamp) graduates to a real diff or is dropped as moot.
- The turn-7 bake-fix verification chain (free + hold bands) rides the same run.
- No dispatches made; my commits: `src/Logic/SteeringInput.cs`, `src/Logic/CoreChecks.cs` (+ this report).
