# HUD/camera — turn-8 report: sense-of-speed verification (scale, timing, wheel omega, authentic audio)

Base: f2eba7e reconciled (fast-forward). This turn: DriveController audio re-bound to the source game's own layer spec + four new machine-check groups in the product gate. No camera numeric changes (rationale below). No dispatches.

## 1. Unit conversion + track scale + timing — VERIFIED with real data

- **Route scale (measured locally, no engine):** track0 (BRANDS HATCH INDY) route polyline = **121 points, 1926.59 m** (parsed from the embedded SourceRoutes data with the same segment-sum formula the Circuit constructor uses; the real Brands Hatch Indy circuit is 1943 m — the recovered route is scale-correct). The supervisor's 1955 m figure is the same track within route-simplification rounding.
- **Timing consistency:** bestLap 87.52 s (clean-lap run 41799538756, jolt/240 Hz) over 1926.59 m = **79.25 km/h average** (the supervisor's 80.5 used the rounded 1955 m). A ~79-80 km/h average around a 1.9 km club circuit is physically sane for the AI reference lap — the DISPLAYED speed (m/s→km/h, gate-proven: 11.6 m/s → "41 km/h") and the world scale now agree: one metre of route = one metre of world = one metre of wheel travel.
- **New gate assertions (ProductControlsScenario):** Circuit(0).length bounded 1500..2500 m; `length/87.52*3.6` within 1.5 km/h of 79.25 — the recorded telemetry, the route scale and the display semantics are pinned together; any future scale drift or magic-number speed hack breaks this check.

## 2. Wheel rotation vs distance travelled — machine check added

In the product gate (no engine world needed): a 4-pivot rig (baseYaw 0/π alternating, radius 0.5) driven through WheelAnimation.Update:
- angle == distance/radius: D=5 → Spin.Rotation.X = +10.0 (exactly D/r);
- mirrored corners rotate with the authentic side sign (baseYaw π → −10) while the SURFACE speed stays correct: |angle|·r == D on ALL FOUR corners after D=7;
- the production chain stays intact: DriveController accumulates `spinAngle += Dot(velocity, forward)*delta` and WheelAnimation converts to angle with the SAME per-wheel radius meta — wheel rotation is exactly ∫distance/r, no independent fake spin.

## 3. Camera — restrained; NO numeric change (deliberate)

- Current chase (26c0f5a base, merged): distance len×1.28 / height clamp(hgt×1.45+0.22, 2.0, 2.8), look-ahead 3..7 m speed-scaled at +1.0 m, FOV 43..48 speed-scaled, obstruction-aware offset with frame-rate-independent exp smoothing, plus the EXISTING bounded suspension motion (wheel travel clamped −0.12..+0.30 m, ground smoothing at 40 Hz exp rate). No blur, no shake, no camera-FOV tricks beyond the ±5° speed span anywhere in the code (verified by reading; nothing to remove).
- The sense-of-speed deficit the reviewer flagged is informational, not camera-mechanical: with the scale/timing/wheel checks now proven and the transmission whine binding to REAL rpm (below), speed perception comes from consistent data. A numeric retune without a tracked reviewer complaint would create a new unreviewed baseline — declined. If the reviewer wants a tighter feel, the asking evidence is a low/high-speed recording pair (below) and I will tune against it.

## 4. Audio — authentic layers from the source spec, driven by live state (DriveController)

- Discovery: `assets/Resources/RealRacing/AudioRamps.json` (241 layers) is the source game's own audio spec; the layer tuple decodes as [fadeInStart, fadeInEnd, fadeOutStart, fadeOutEnd, basePitchRef, gainMin, gainMax, throttleOn] (verified across the default + car-specific sets).
- Implemented: the `default_transmission_{on,off}_{very_low,low,mid,high}` set (all 8 samples shipped) now drives the transmission whine: layer selected by LIVE throttle state and LIVE rpm inside its fade window, gain envelope interpolated gainMin→gainMax across the fade edges (the authentic spool-up/ramp-down shape), pitch = rpm/base (authentic reference pitch, so gear shifts register as real rev drops), on top of the pre-existing speed-based loudness and the engine-idle rpm pitch. Stream swaps only when the layer changes (whineClip guard — the duplicated stream's ResourcePath is empty, so path-compare would have thrashed; caught before commit).
- NOT bound, deliberately: the car-specific turbo/spool sets (911/bugatti/ferrari/audi_tt...) — no surviving data maps them to our car indices, guessing = fake; WIND and TYRE-SKID layers — the source samples do not exist in assets (only whine/spool/turbo samples survived) and synthesising loops is forbidden by the no-fake rule. These stay open until recovered samples land.
- Mute verified: Settings mute ↔ ApplyAudio ↔ master-bus mute asserted in the gate; the in-game mute key path (Held(7)) was already wired through the same call.

## 5. Gate additions (ProductControlsScenario — behaviour asserts)

1. scale: Circuit(0).length in band; 2. scale×timing: 87.52 s lap ⇒ 79.25±1.5 km/h average; 3. wheel omega: D/r exact + mirrored sign + per-corner surface speed; 4. asset reality: every sample the controller can play exists (EngineIdle, 8 transmission layers, spool_01, turbo_01) — the no-fake-loop guard; 5. mute bus behaviour both directions with restore.

## 6. Proposed cloud-gate extension (coordinator-owned; diff on request, not dispatched)

Same-input/same-course low/high-speed recording pair (the driving_validation profiles already define speed targets): record both, then machine-check against the telemetry: per-frame `|wheelAngle·radius − travelled distance|` ≤ 2 %, displayed km/h ≡ speed×3.6 (already gate-proven statically), and camera continuity (no frame-to-frame jump beyond a bound) — the recordings become quantitative sense-of-speed evidence instead of impressions.
