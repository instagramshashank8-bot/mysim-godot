# FLEET MANIFEST — programme item 1 (carbody-livery turn 3)

Author: carbody-livery specialist. Branch `mistral/carbody-livery`. Basis: integration HEAD `39cdf33` (origin tip, fetched and verified). **No dispatches made.** Tool output (`fleet.json`) and these tables (`fleet-tables.md`) were generated read-only against the main working copy's asset store and are committed on this branch for the coordinator to merge.

## 0. Provenance and method (no folder guesses)

Enumeration sources — the same sources the runtime reads:
- Cars: `src/Logic/CarCatalog.cs` (`Folders[24]`, `Names[24]`, `RaceClass` logic mirrored in the tool).
- Tracks: runtime `assets/RealRacing/TrackList.json` (the copy `src/Main.cs` actually loads: `"res://assets/RealRacing/TrackList.json"`) with `src/Logic/SourceRoutes.cs` `Names/Points` as the route-order authority; `data/TrackList.json` cross-checked for divergence.
- Bindings: `assets/car_map.json` (exact Unity prefab assignment), `assets/car_pivots.json` (wheel hubs), `data/car_surface_uv_repairs.json` (UV policy state).
- Policy/evidence state: `outputs/mistral-orchestration/EVIDENCE_INDEX.md` (acceptance register at `39cdf33`), `VISUAL_REVIEW.md` (2026-11-03/04 review verdicts), `CONTINUE.md` (campaign chronology: geometry/material audits 41010486283/41021060145/41028506616, native lap matrices 1440/964/900/420 with per-car pass counts, wheel/paint/UV campaigns).

The tool (`tools/carbody_texture_inventory.py --manifest`) ran with **all verification asserts passing** at `39cdf33` sources:
1. CarCatalog = 24 cars; `Folders`/`Names` equal length.
2. `car_map.json` cars keys == catalog ids == `car_pivots.json` keys (24/24/24; every pivot entry has exactly 4 hubs).
3. Every car folder exists under `assets/RealRacing/Source/` with FBX (≥1; exactly 11 part files per car) and textures (11–24 PNGs).
4. TrackList = **60 tracks**; `SourceRoutes.Names` = **60**; **name-aligned at every index 0–59** (my earlier hand-count of 59 was wrong — the fail-closed assert settled it).
5. Every track scene file exists (`Source/<map>/<scene>`), every `variationPaths` entry exists.
6. `data/TrackList.json` == runtime `assets/RealRacing/TrackList.json` on count and every `map/layout/scene/name` field (**no divergence**).

Any future catalog edit that breaks 1–6 makes this tool exit non-zero (fail closed) — the manifest cannot silently drift from the runtime lists.

## 1. Fleet summary — ratings per car

Ratings vocabulary: **source-ready** = FBX+textures present (tool-verified); **render-integrated** = bound+pivot-complete+loaded by the documented runtime path and exercised in document fleet runs; **evidence-current** = newest visual/numeric evidence relative to the current head (all evidence predates `39cdf33` — the per-car date and what a re-run must re-prove are listed in §3); **defects-open** = documented, unclosed items only (no speculation).

All 24 cars: source-ready YES, render-integrated YES (fleet geometry audited PASSED 707 assertions at bef4bae-era 41010486283 + full 24-car geometry/material isolation PASSED at 9f654d7 41028506616 + material audit PASSED 41021060145 + every car id 0–23 rendered and lapped in the native matrices). Per-car state:

| id | car (class) | source dir | FBX/tex | livery: bound (alternates) | UV repairs P/Pend | evidence (newest documented) | defects-open (documented) |
|---|---|---|---|---|---|---|---|
| 0 | FERRARI 488 GT3 (GT) | `2018_ferrari_488_gt3/` | 11/15 | `ext_51` (5 total) | 0/0 | car-view review at `55d7841`-era + wheel-material review 41000944532; fleet matrices | dark rims + discontinuous body shading (ASSEMBLED_CAR_REVIEW era); mirrored-U defaults fleet-wide; body-atlas stroke marks |
| 1 | AUDI R8 GT4 (GT) | `2018_audi_r8_gt4/` | 11/12 | `ext_fictional_23` (2) | 0/0 | fleet matrices only | fleet-wide defaults; no car-specific review |
| 2 | BMW M4 GT4 (GT) | `2018_bmw_m4_gt4/` | 11/11 | **fallback** (1) | 0/0 | fleet matrices only | ext materials have NO `_BaseMap` (runtime `FallbackTex` binds the single livery); fleet-wide defaults |
| 3 | CAMARO GT4.R (GT) | `2018_chevrolet_camaro_gt4r/` | 11/12 | `ext_fictional_12` (2) | 0/0 | fleet matrices only | fleet-wide defaults |
| 4 | BMW M8 GTE (GT) | `2018_bmw_m8_gte/` | 11/12 | `ext_1` (1) | 0/0 | fleet matrices only | fleet-wide defaults |
| 5 | CAMARO ZL1 1LE (GT) | `2018_chevrolet_camaro_zl1_1le/` | 11/16 | `ext_black` root-level (6) | 0/0 | fleet matrices only | root-level `ext_black` explicit binding — verify intended (flat black paint livery); fleet-wide defaults |
| 6 | HURACAN GT3 (GT) | `2018_lamborghini_huracan_gt3/` | 11/17 | `ext_fictional_08` (6) | 1/0 (hood misc V-only ACCEPTED, run 41023270551) | role-UV comparison 41015188520 + hood fix + wheel review 41000944532; 29/60 in the 900-case lap matrix (worst road-car: 31 lap fails, cause never per-car diagnosed) | misc-scope V flips white wing struts (per-surface repairs required); dark rims/discontinuous shading; fleet-wide mirrored-U on badges |
| 7 | MERCEDES-AMG GT3 (GT) | `2016_mercedes_amg_gt3/` | 11/16 | `ext_fictional_2` (6) | 0/0 | fleet matrices only | fleet-wide defaults |
| 8 | McLAREN 650S GT3 (GT) | `2015_mclaren_650s_gt3/` | 11/11 | `ext_59` (1) | 0/0 | fleet matrices only | fleet-wide defaults |
| 9 | ACURA NSX GT3 (GT) | `2018_acura_nsx_gt3/` | 11/17 | `ext_fictional_40` (7) | 0/0 | fleet matrices only | fleet-wide defaults |
| 10 | BMW M6 GT3 (GT) | `2018_bmw_m6_gt3/` | 11/15 | `ext_fictional_34` (5) | 0/0 | fleet matrices only | fleet-wide defaults |
| 11 | PORSCHE 911 RSR (GT) | `2018_porsche_911_rsr/` | 11/11 | **fallback** (1) | 0/0 | fleet matrices only | fallback livery; fleet-wide defaults |
| 12 | FORD GT LM (GT) | `2016_ford_gt_lm/` | 11/15 | `ext_66` (4) | 0/0 | fleet matrices only | fleet-wide defaults |
| 13 | ASTON MARTIN GTE (GT) | `2018_astonmartin_gte/` | 11/11 | **fallback** (1) | 0/0 | deepest physics history: track8 (HONG KONG) exact-impact and track4 (INTERLAGOS) seam/spin campaigns — 6/60 laps in the 900 matrix; undamaged-spin contact witness captured (41513037207) — **dynamics investigation OPEN**, not an asset defect | fallback livery; fleet-wide defaults |
| 14 | CORVETTE C7.R (GT) | `2016_chevrolet_c7r/` | 11/12 | `ext_63` (2) | 0/0 | fleet matrices only | fleet-wide defaults |
| 15 | MERCEDES W10 (F1) | `2019_f1_mercedes_w10/` | 11/16 | `ext_44` (2, drivers 44/77) | 0/0 | deepest visual history: all 32 wheel views reviewed REJECTED (41310403219), deep-wet renders (41329426459, partial reviewed), full-lock steering-rig candidate (41424982897 wheel-body 19.4mm → candidate 0), axle-fit straight pass, body paint material iteration STOPPED (41337127855), 53/60 laps | wheel art REJECTED (raised rings/plain bright barrels/occluded brakes/body atlas); mirrored-U defaults + badges; wing/tyre overlap measured under steer (opt-in visual rig, unaccepted) |
| 16 | FERRARI SF90 (F1) | `2019_f1_ferrari_sf90/` | 11/15 | `ext_16` (2, drivers 16/5) | **3/10** (BODY/FRONT/REARBUMPER ext promoted; 7 ext + 3 badges pending — H1) | car_evidence 41798036351 @`e8b80bb` — 18 PNGs human-reviewed 09:10Z (narrow improvements accepted; whole car rejected); SF90 source-UV stats 41306343412; promoted trio validated in paired runs 41572315488; the TEN PENDING candidate entries were never exercised — the car_evidence-era candidate legs ran inert (env mismatch, fixed `e41d4f4`) | mirrored sponsor marks/numbers (H1 primary = badges layer); blur = `lowSourceRes` (1024² livery/256² badges = source ceiling); wheel binding defect FIXED via role alias (41573514607); twin-hide A/B never accepted; wheel/wing clearance unproven |
| 17 | RED BULL RB15 (F1) | `2019_f1_red_bull_rb15/` | 11/16 | `ext_23` (2, drivers 23/33) | 0/0 | same F1 wheel/paint campaigns REJECTED; axle-fit residual 7.1mm; 22/60 laps | wheel art; fleet-wide mirrored-U; badges uncorrected |
| 18 | McLAREN MCL34 (F1) | `2019_f1_mclaren_mcl34/` | 11/15 | `ext_4` (2, drivers 4/55) | 0/0 | same F1 campaigns; tyre diameter reference note (Pirelli 670mm nominal vs 599mm measured — missing source assembly scale UNPROVEN, no blind resize); 60/60 laps | wheel art; fleet-wide mirrored-U; badges uncorrected |
| 19 | McLAREN P1 GTR (Hyper) | `2015_mclaren_p1_gtr/` | 11/11 | `ext_51` (1) | 0/0 | fleet matrices; 14/60 in the 420 matrix | fleet-wide defaults |
| 20 | BUGATTI CHIRON (Hyper) | `2017_bugatti_chiron/` | 11/24 | `ext_blue` (6) | **11/0** (all 11 visible ext roles V-only ACCEPTED, run 41453316712) | most complete: 60/60 native layout passes with reconstructed wing kit (41453473569/41451231661), integrated atlas UV VERIFIED (41458535677), road tyre-ring fix VERIFIED (41460271788), mount reconstruction measured (translation + 828.889 mm strut spacing, ~1 mm base gaps, 41449769834; socket-level identity NOT claimed — ranked-pair mismatch 50 mm, 41429382493) | wheels/rim-tyre detail + interior quality rejected; wing kit = new design (NOT recovered pose); fleet-wide badges mirrored-U |
| 21 | PORSCHE 919 EVO (LMP) | `2018_porsche_919_hybrid_evo/` | 11/11 | **fallback** (1) | 0/0 | fleet matrices; 59/60 | fallback livery; fleet-wide defaults |
| 22 | ORECA 07 LMP2 (LMP) | `2017_oreca_07_lmp2/` | 11/12 | **fallback** (1) | 0/0 | fleet matrices; 54/60 | fallback livery; fleet-wide defaults |
| 23 | PORSCHE 911 GT3 RS (GT) | `2018_porsche_911_gt3_rs/` | 11/22 | `ext_black` root-level (6) | 0/0 | fleet matrices; 31/60; sw/mask material seen with `_ReflectionMap` in source `.mat` — our binder drops reflections (material feature gap, fleet-wide) | root `ext_black` binding (verify intent); fleet-wide defaults |

Fleet-wide documented defects (every car): (a) badges/sponsor decal surfaces ride the shader default `(-1,-1)` → mirrored marks (only car16 has staged candidates; nobody else does yet); (b) `lowSourceRes` is physical (no higher-res originals exist in the recovery; car16 §1.5 of turn-1); (c) source `_ReflectionMap`/reflection features dropped by the runtime binders (over-glossy read); (d) generated wheel/tyre candidates (opt-in) remain the rejected art for the F1 class.

## 2. Runtime integration path (every car flows through the same code)

`CarLoader.Load(folder)` — instantiate each `<car>_*.fbx` part (scale ×100, cm→m), name-policy visibility (LOD_A only; DAMAGE/BLUR/DEBRIS/SCRATCH/INT hidden; `_mm_*` surface materials), `WindingFixer.Fix`, then `ApplyExact`: `RecoveredComponentMounts.Apply` → `assets/car_map.json` GUID bindings (source `_BaseMap`/`_MaskMap`/colours/floats/queue; role-alias fallback for wheel nodes; `SurfaceUV` per-surface repairs from `data/car_surface_uv_repairs.json` — fail-closed on guid/texture mismatch; `CarAtlasTransposeDiagnostic` opt-in hook) → `AttachWheels`: `assets/car_pivots.json` 4 hubs (source-mirror yaw 180°, det +1), source wheel meshes duplicated under Spin/BrakeAssembly, tyre/rotor fallback only where the source material is untextured, `TyreAppearance` (compound markings formula-only 15–18), rim/brake candidate branches (opt-in envs), `SourceAxleFitCandidate` (opt-in), `F1SuspensionVisualBuilder` (15–18), `ChironReconstructedWingKit` (20, opt-in), then native hull colliders (`NativeCarColliders`) for physics modes.
Livery policy: explicit binding where car_map carries one; otherwise `FallbackTex` binds the car's own livery (first alphabetical file). **No runtime livery/variant selection exists in the game** (no selection code found); the 73 livery files fleet-wide (1–7 per car) are a fixed runtime bind plus unused alternates. The variants are driver-number liveries on the F1 cars and fictional/team liveries on the GTs.

## 3. Evidence still needed per car (no dispatches made — coordinator queue)

1. **Fleet render batch at a current head** — the `car_evidence` depth hardcodes car16 (`cloud_game_audit.py` `MYSIM_CARVIEW:'16'`); a whole-fleet matched batch needs the depth parameterised (one coordinator change) or per-car dispatches. Required views per car: front/left/right/rear/top/cockpit + 4 wheel closeups (existing car_evidence shape = the template; 18 PNGs × 24 cars).
2. **`car_assembly_audit` fleet-wide** (first post-`5bf41d7` run anywhere): per-surface `basisDeterminant` + `metresPerUv` + `MIRRORED_TEXT_RISK_*` + wheel-binding checks — closes every `pending-run-P5` field in the turn-2 inventories and H2's measurement.
3. **Livery/UV per-surface inventories for the 23 cars other than car16** (six-field standard; car6 partial). The turn-1 tool already automates this (texture side locally; surface side via `--assembly` join).
4. **Clearance sweeps** (wheel/wing): only W10 is measured end-to-end; RB15/MCL34/SF90 residual strut clearances known; all 20 other cars unmeasured (the existing read-only sweeps on the car-assembly branch generalise).
5. **Mirrored-badges candidates staged for cars ≠ 16** — none exist yet; the source-convention mapping (V-only) is validated per-surface on 3 cars only (6/16/20); **no blanket flip** — each car needs its own per-surface evidence run before any policy entry.
6. Wheel/tyre art rework for the F1 class per the standing rejection (raised rings/plain barrels/occluded brakes); road-car rims/interior per the Chiron note.

## 4. Tracks (60; runtime order = TrackList order = SourceRoutes order, assert-verified)

All 60 rows: scene + variation files exist (tool-verified), route name aligned 1:1 with `SourceRoutes.Names`, source spline SHA recorded in TrackList entries (provenance, not a runtime file). Runtime status: loaded per the `Main.BakeCollision`-style resolution (`res://assets/RealRacing/Source/<map>/<scene>`, scale ×100), collision via `CollisionBake` hulls plus the track-specific measured policies (triangle-preserving bridge partition on 15/43; exact seam welds on 4 — opt-in), DriveSurface + `SourceRoutes.Points[i]` routing for AI/legality, `baseTrackIndex` groups the 31 layout variants onto their base tracks (0–28 base; 29–59 variants — CATALUNYA+2, DAYTONA+2, DUBAI+5, HOCKENHEIM+2, INDIANAPOLIS+1, LEIPZIG+2, MONZA+1, NUERBURGRING+2, SILVERSTONE+3, SUZUKA+2, YASMARINA+1, COTA+2, LEMANS+1, LIMEROCK+2, REDBULL+1, SEBRING+2).

Night/weather support (profile-driven, all 60): `data/lighting_profiles.json` + `weather_profiles.json` + `SurfaceWeather`/`RainVolume`; `weather_review` rendered 60 tracks × 5 conditions (300 captures, success). Quality state per the documented reviews: night coverage on audited tracks 0/25/28 starts improved but **whole-circuit night coverage rejected** (dark starts at 25/28; light candidates REJECTED — source-head fixture audit found 0 usable emitters), rain visual = faint/no convincing spray (motion review rejected; opt-in `RainVisibilityCandidate` unhooked-progress).

Per-track documented defects (documented-only rule; global items inherit to every row):
- **4 INTERLAGOS**: two measured construction seams (welded 1+1 triangles exactly, opt-in production fix fleet-regressed); car13 lap-validity failures are controller/dynamics investigation (6/60 in the 900 matrix), NOT the track mesh.
- **15 MONACO + 43 MONZA JNR**: historic bridge/deck contact failures — FIXED under the triangle-preserving bridge partition policy (all-24-car Monza matrix PASSED 5805eb3; Monaco 23/24 → car20 fixed with verified wing kit; fleet regressions green).
- **22 RICHMOND / 40 INDIANAPOLIS ROADCOURSE / 11 LEIPZIG KLEINERKURS**: historic engine-shutdown-on-teardown failures — FIXED (phased `CleanExit` + resource disposal; native_shutdown_recovery 8/8 PASS).
- **0 BRANDS HATCH / 25 SINGAPORE / 28 YAS MARINA**: night-lighting audits (25/28 original dark, zero source emitters; granular lens/offset candidates rejected) — **night coverage OPEN**; track UV densities measured (TRACK_UV_DENSITY_ACTUAL): road primary 24–27 m/tile at 2048² ≈ 75–87 px/m (fine), runoff/outer ground ~50 m/tile at 1024² ≈ 20 px/m, UV1 channel mostly unusable — "flat/repetitive" = material-variety gap (no normal/detail layer), resolution adequate.
- Global standing defects on ALL tracks (VISUAL_REVIEW 2026-11-03/04): white/speckled grandstand/fence surfaces (alpha + mip; miprepair candidate FAILED schema-era, rerun required at `49ece2c`+), black foliage/horizon card masses, road broad-grey repetition, bright grass streaking, tree-billboard wall edges. None accepted fixed; every track row inherits these.
- Track-side evidence currency: native physics matrices (1440 @6623aa3/07bd271-era, 964 fresh matrix, 900/420 aggregates with per-car counts quoted in §1) all predate `39cdf33`; a current-head spot matrix is **required** before any per-track acceptance language (coordinator queue).

## 5. fleet.json tool output

`outputs/mistral-orchestration/manifest/fleet.json` (committed on this branch) carries the full machine-readable rows: per-car `fbx/textures/liveriesOnDisk/boundLivery/boundRenderers/pivots/uvRepairsPromoted/uvRepairsPending/specialPaths`; per-track `map/layout/scene/sceneBytes/baseTrackIndex/variationPaths/routeName/sourceSplineSha/hasAiPoints/hasCentrePoints`; plus the assert results (`assertsPassed=true`, `problems=[]`). Re-run any time with:

```
py -3 tools/carbody_texture_inventory.py --manifest --root <repo-root> \
  --out-json outputs/mistral-orchestration/manifest/fleet.json \
  --out-md  outputs/mistral-orchestration/manifest/fleet-tables.md
```

Exit 0 only when every assert passes; exit 3 with the exact assert lines on catalog drift (cars/tracks/routes/file existence/TrackList divergence) — the coordinator can wire this tool into the intake/review workflow as a read-only gate.

## 6. What this manifest deliberately does NOT claim

No visual acceptance, no whole-game claim, no "persistence/perf stage closed" language. Ratings are evidential states at `39cdf33` with named sources; anything not documented above is listed as needed evidence, never inferred.
