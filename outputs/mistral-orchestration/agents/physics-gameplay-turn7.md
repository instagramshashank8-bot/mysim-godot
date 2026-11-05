# physics-gameplay agent — turn 7 — suspension-geometry fix proposal — 2026-11-04

Role: physics/gameplay specialist. Branch `mistral/physics-gameplay` (turn-6 state `1def4dc` + this turn's data patch). Commit scope respected: this turn's COMMIT touches `data/native_suspension_profile.json` + this report only; `NativeSuspensionVehicle.cs` and `NativeCarColliders.cs` changes are **proposed as exact diffs for the coordinator** and NOT edited by me. No dispatches; no invented physics constants; everything below is anchored to run artifacts and the local hull manifests.

## 1. The measured descent (task 1)

**Run 41848766195** (DV + `native_ride_height=1` at `b5ca3e0`), artifact read locally, read-only:
- Both DV cases terminate at init with my gate firing exactly as designed: `Source clearance requires unsupported suspension extension: car=16 clearance=-0.20009995 requiredPreload=0.23509994 maximum=0.12` (case log 1; case log 2 contains the same car=16 line — **the case-2 label/route ran car 16 twice; car 0's ride gate has never been exercised at runtime** — a pipeline label/selection bug for the coordinator's list, cross-checked below against the bake data).
- `native-hull-summary.json` (baked live in that run) — the ground truth for the stance:
  - **car16**: wheelCentresY −0.310 (all four, level axles), recovered radii ~0.2996/0.2999, tyreBottom −0.6099; lowest hull parts: `LOD_A_WHEELGUARD_FRONT_LEFT/RIGHT` −0.81, then `LOD_A_STEERING_WHEEL` −0.71, then the real body: `LOD_A_CHASSIS` −0.60, bumpers/body −0.55..−0.57, strut uppers −0.48, strut lowers −0.39. `mountedHubs` = **{} — nothing is hub-mounted for car16**; every part baked as a body-fixed hull.
  - **car0** (same manifest family, computed statically from the bake since its runtime gate never ran): wheelCentresY −0.349, radii 0.3007, tyreBottom −0.6497; lowest parts: `LOD_A_BRAKE_CALIPER_FRONT_LEFT/RIGHT` −0.79 (rears −0.75) — the calipers bake as body-fixed hulls too (the bake's wheel-assembly template regex covers `CALIPER` but these are named `BRAKE_CALIPER_…`, which the regex does not match).
- **The DV trace of 41840755243** (the behavioural face of the same geometry): `started=true, gridHold=false, thr=1.00 from ~2 s, speed 0.000 the whole session, bodyContacts=2 tyreContacts=1` — the exact case-3 signature from my turn-5 table: commands flow, thrust dead, the car resting on two guard corners with one wheel grazing support. The turn-5 mechanism prediction (free-revving unsupported rear wheels + power-cap thrust collapse through `motorSpeed`) remains the running mechanism; the trace's stage column matched `launching` throughout.
- Underfloor/contact numbers carried over (turn-2 clean lap): the constant contact set was exclusively CHASSIS (×7816)/FRONTBUMPER (×828)/BODY (×87) vs DriveSurface across 1785/1792 samples — i.e. that lap rode at the CHASSIS datum (~1 cm above the tyre plane, grazing under compression), **never** on the guards — so the clean lap's bake/behaviour and the DV freeze's are consistent with the same geometry analysed at two rest states (graze at the chassis line vs wedge on the guard corners; the exact bake-set difference between those runs is recorded as an open item for the car-assembly side, worth one diff check of `native-hull-summary.json` between `5d21c56` and `b5ca3e0` lineages).

**The decisive number:** the car16 stance datum in the hull set is the **wheelguard at 0.20 m below the tyre support envelope** — a nonsense datum for a car whose real chassis floor (−0.60) sits ~1 cm above the tyre plane (static stance ≈ 10 mm) and whose target stance is 30–50 mm. The ride-by-preload path requires 0.235 m of preload against a 0.12 ceiling: **measured impossible, gate worked as designed**.

## 2. Why no spring parameter can fix it (the identity, so nobody re-tries)

Static equilibrium pins each suspension mount at `mountHeight = recoveredRadius + RestLength + preload − StaticSag` above the road (spring rate is calibrated `mg/(4·StaticSag)`, so at rest each spring length is `RestLength + preload − StaticSag` and carries `mg/4`). The floor's height above the road is the mount height minus the body-fixed offset `(mountLocalY − floorLocalY)`:

`floorHeight = radius + RestLength + preload − StaticSag − (wheelCentreY − floorY) − (RestLength − StaticSag)
            = radius + preload − (wheelCentreY − floorY)`

RestLength, StaticSag and mount offsets **cancel exactly**. With the wheelguard in the hull set, `(wheelCentreY − floorY) = 0.50`, giving `floorHeight = 0.2996 + preload − 0.50 = −0.20 + preload` — the guard always rests 0.20 m into the road, springs over-extended to zero support, wheels free. No rest length, no sag, no anchor height, no droop limit inside the spring state changes `(wheelCentreY − floorY)` — that is body/source geometry. **The fix must act on the hull datum, not the spring state.**

## 3. Candidate ladder with blast radius (task 2/3)

**G1a — FIRST PATCH (recommended, minimal): exclude wheel-assembly parts that reach below the tyre support envelope from the rigid hull bake.** Car16's offenders are the `LOD_A_WHEELGUARD_*` fairings; car0's are its `BRAKE_CALIPER_*` (−0.79). These are wheel-carrier members; as body-fixed hulls 0.14–0.20 m below the tyre plane they cannot coexist with any sane stance.
- Proposed coordinator diff (`NativeCarColliders.cs`, one line — NOT committed by me):
  `static bool WheelTemplate(string name)=>Regex.IsMatch(name,@"^LOD_A_(TYRE|TIRE|WHEEL|WHEELGUARD|(BRAKE_)?CALIPER|BRAKE_DISC|DISC|ROTOR|HUB)(_|$)",RegexOptions.IgnoreCase);`
  (current list lacks `WHEELGUARD` and matches `CALIPER` only un-prefixed).
- Blast radius: source meshes/UVs/visual assembly untouched (exclusion ≠ geometry edit — preservation holds); collision set loses two guard fairings + four caliper blobs — the wheel arcs/wells stop wall-colliding at those spots (open-wheel sides widen by the fairing thickness at wheel band; the tyres are rays and never collided there anyway); no stance/spring impact by definition.
- Expected effect on the 9 targets: none mechanical (the collision set change removes a foreign rest plane the targets never relied on); the re-verification battery is the proof, not a promise.
- Re-baselining: DV core re-run (both cases, correctly labelled — the car-0/label bug must be fixed first, coordinator list); RIDE-B re-dispatch (below); the clean-lap family re-runs under whatever stance the post-fix bake yields; source-backend impact history (`41794252920`, 17 impacts) re-checked — if the guard/caliper hulls were participants in those spurious contacts, this fix also de-risks the backend comparison.

**G1b — hub-mount the guards/calipers instead of excluding (more source-faithful semantics, deferred):** tag them into `mountedHubs` via the car-assembly mount metadata; they then ride with the wheel carrier (steer+suspension transform) instead of the body. Bigger blast: hub mount metadata + visual pose review (car-assembly cycle) + the wheel-well collision moves with the wheels. Only worth it if source evidence shows the guards/colliders must physically collide in the wheel band. Deferred behind G1a.

**G1c — keep guards, only change what the stance gate measures (rejected as the first patch but Keep as a named option):** excluding only from the clearance computation (measure the stance at the chassis/bumper/body datum) fixes the GATE but not the car: the guards still rest 0.20 m into the road at any pose — the freeze stays. Correct only as a COMBINED second step (gate datum + exclusion), and since the exclusion is needed anyway for the freeze, G1c alone is pointless. Rejected.

**G2 — ride-by-preload re-passed after G1a (NO change):** with the guards out of the datum, car16 stance = chassis at +0.0099 ⇒ required preload = **0.0251**; car0 stance = **+0.0497 already inside the 30–50 mm class** ⇒ required preload = **0** (the clearance value is a floor/minimum gate, not an exact stance). Both far below the 0.12 ceiling — the previously impossible path becomes trivial after the datum fix. The 35 mm figure stays EXPERIMENTAL (labelled in the data file + the turn-6 doc; nothing adopted).

**G3 — rest length / anchor heights / droop-rebound limits: explicitly NOT PART of the first patch** — proven unnecessary by the identity (section 2) and unmeasurable while the hull datum is wrong. The model has no explicit droop limiter; the support envelope is the ray reach (`radius+RestLength+preload+0.15` ≈ 0.68–0.72 m at the proposed preloads — no limit invention needed). Anchor heights per axle: recovered as level (−0.31 both axles for car16) from source; changing them is a source-geometry claim requiring car-assembly axle-fit evidence — not open to tuning here.

## 4. The minimal FIRST patch — decision (task 4)

**One patch, one concept: G1a, the hull-bake exclusion one-liner (coordinator commits) — carried by my data-file ledger (committed by me) + this report.** No suspension parameter changes at all: the rest length question is answered by the identity ("no"); the droop/anchor constants are untouched; the ONLY thing that changes is which parts may define the car's stance datum.

Verification chain for the oldest verification after the coordinator applies G1a (expected run shape, no dispatch by me):
1. **DV core on source/120 at the fixed bake — the frozen car must free**: `input_direction` + `acceleration_0_60` complete on the previously-frozen backend/rate; DV_TRACE shows `spd>0` from the launch phase onward, `bodyContacts` back to the floor-graze baseline (2 guard corners gone), 4-wheel support during motion; the 41834771421/41840755243 signature (`thr=1, spd=0, tyreContacts=1` for 30 s) must NOT recur.
2. **DV core + `native_ride_height=1`**: car16 gate prints `NATIVE_STATIC_CLEARANCE preload≈0.025 original≈0.0099 target=0.035`; car0 prints `preload=0 original≈0.0497` (stance already in class) — BOTH init passes → the full battery runs.
3. **The nine targets hold their bands** (turn-5/6 Group-2 rubric verbatim — travel judged run-static-relative; straight stability is the sharpest watch item). Any band failure → classify per the turn-5 failure protocol (no band edits without a written cause).
4. Only after 1–3: the RIDE-A/RIDE-B pair per the turn-6 readout + adoption gate (my numeric sign-off, car-visual sign-off, coordinator default flip, re-baselining list).

## 5. Open items

- Car16 twice / car-0 label bug in DV case 2 (41848766195 logs) — coordinator's DV runner loop.
- Guard-contact absence in the clean-lap histogram vs guard presence in the dvride bake: worth ONE diff of `native-hull-summary.json` between the `5d21c56` and current lineage by car-assembly (was the guard pair absent/holder earlier? whichever way it lands, G1a covers both).
- The `LOD_A_STEERING_WHEEL` hull (−0.71, below the chassis floor line!) is a case to review under G1b optics (interior parts as hulls) — noted for the assembly owners; no action by me.
- `.the 35 mm EXPERIMENTAL label` now lives in `data/native_suspension_profile.json` (scope + `minimumStaticClearance_status`) and all three turn docs; the G1a→G2 chain will re-print it wherever it next appears.
