# Track rendering — turn 6: night lighting audit, too-dark defect map, flag-gated candidate set, evidence protocol

Author: track-render specialist. Source diagnosis at the current integration HEAD (`ccca290`, fetched read-only; my night files are byte-identical between my branch and the integration — `git diff 158a349 ccca290` shows no `TrackNightLighting.cs`/`Conditions.cs`/fence-file entries). Patches this turn touch **only** `src/TrackNightLighting.cs` and `src/Conditions.cs` (my files); fence files (`TrackCutoutDiagnostic.cs` and company) are untouched while run 41843204651 is in flight. No dispatches.

## 1. Night light-audit — every source the track spawns

| # | Source | Type / count / energy / position pattern | Code |
|---|---|---|---|
| 1 | Directional "sun/moon" | 1 DirectionalLight3D; night energy **0.08** (profile), colour (0.6,0.7,1), rotation (-38,-35,0); `ShadowEnabled=true` always (Parallel4Splits, max 150 m) | `Main.cs:26-27`; values from `data/lighting_profiles.json` Night via `Conditions.Apply` |
| 2 | WorldEnvironment ambient | `AmbientSource=Color`; night **ambientEnergy 0.18**, colour (0.3,0.35,0.55) (bluish, dark); fog (0.05,0.07,0.12) | `Main.cs:24-25` + `Conditions.cs:Apply` (`env.AmbientLightEnergy=ambE`) |
| 3 | Tonemap/exposure | `env.TonemapExposure = 1.0` for ALL profiles; tonemap MODE never set → Godot default LINEAR — no night grading anywhere | `Conditions.cs:Apply` (`env.TonemapExposure=expo`), `data/lighting_profiles.json` Night `exposure: 1` |
| 4 | Sky dome | unshaded dome meshes (no emission/light); night sky near-black (skyTop (0.01,0.02,0.06), horizon (0.05,0.07,0.13)); `CastShadow=Off` on all dome meshes | `Main.cs:200` + `SkyDome.cs:13`; colours from the Night profile |
| 5 | Procedural floodlight candidate (MY file) | opt-in (`MYSIM_TRACK_FLOODLIGHTS=1` + `IsNight`): masts every **38 m** both sides, lateral offsets 9-14 m, height 14 m, ray-supported + clearance-vetted (rejections counted/audited); **bounded pool of 8 SpotLight3D** (energy 18, range 55 m, cone 65°, colour (0.82,0.89,1), attenuation 1, distance fade 110/45), of which only the **2 nearest** cast shadows; per-physics-tick reassignment to the camera-nearest supported stations with hysteresis; emitter strips `EmissionEnergyMultiplier=2` | `TrackNightLighting.cs` (`_PhysicsProcess`, `Build`), `Main.cs:204`, `data/track_floodlights.json` (self-labelled "provisional Godot light energy, not real circuit photometry") |
| 6 | Source-fixture candidate (owner's file, read-only for me) | opt-in (`MYSIM_SOURCE_FIXTURE_LIGHTS=1` + `IsNight`): lamp positions derived from ACTUAL source fixture meshes (face clustering + shaft/lens heads + ray clearance); pool of 8 SpotLight3D, energy **18 hardcoded**, colour (0.9,0.94,1), 2 shadows, range from `MeasuredFixtureLens` | `Main.cs:203`, `SourceFixtureLighting.cs:53` |
| 7 | Source night fixture audit / scenario lights | evidence-only audits (`MYSIM_NIGHT_FIXTURE_AUDIT`, `MYSIM_GRIDLIGHTCHECKS` scenarios) — not game lighting | `SourceNightFixtureAudit.cs`, `GridLightsScenario` |
| 8 | Car-mounted lights (headlights/brake/rain) | owned by the car/drive stack (car-assembly domain); unaffected by the night profile; listed for completeness | `CarLoader` / drive code |
| 9 | This turn's candidates | NO new lights — energy/ambient/exposure multipliers only (see §3) | `TrackNightLighting.cs`, `Conditions.cs` |

Car material response path: cars are `StandardMaterial3D` PBR (diffuse, roughness ≈ 1, metallic 0 / specular 0.12, no emission on body panels — the binder defaults seen in `CarLoader`/`UnityMaterialBinder`), so at night the car responds only to #1 (0.08), #2 (0.18 bluish) and whichever of the ≤8 pool spots currently cover it. Nothing on the car self-emits; dark sides are the arithmetic consequence of the fill values above.

## 2. Defect map — "road lit but car/environment too dark" (+ fixture grid)

Ranked by contribution, with citations:

1. **Ambient 0.18 bluish** (`data/lighting_profiles.json` Night `ambientEnergy`, applied in `Conditions.cs:Apply`) — the only light on everything outside fixture cones: car sides/undersides, terrain, grandstands, walls, marshal posts, foliage. Primary "environment too dark" contributor.
2. **Pool economics** (`TrackNightLighting.cs` `_PhysicsProcess` assignment policy + `track_floodlights.json` `activeLights=8`, `range=55`, fade 110/45): lights aim at the ROUTE centre (road lit); anything off-road beyond the 55 m cone gets nothing; at 14 m mast height with attenuation 1 the ground falloff is steep at cone edges → bright road stripe, dark everything else — exactly the rejected frame's pattern.
3. **Exposure 1.0 + linear tonemap** (`Conditions.cs:Apply`; mode never set): no night grading; a linear pipeline with 0.18 ambient renders midtones crushingly dark.
4. **Night sun 0.08 still shadow-casting** (`Main.cs:26` `ShadowEnabled=true` + Night profile `sunEnergy 0.08`): near-zero light contribution but full 4-split shadow cost, deepening shadowed faces.
5. **Near-black sky dome** (`SkyDome.cs:13` unshaded + Night profile sky colours): background reads as void — the "environment too dark" horizon.
6. **Car response** (§1 no-emission PBR): sides facing away from the 2 shadow spots get only 0.18 bluish ambient → the car reads darker than the floodlit road — the rejected frame's "road lit but car too dark".
7. **Fixture grid defects** (the archived night frame's "fixture grid"): the procedural candidate's uniform 38 m spacing + repeated offsets/heights produce a visible regular rhythm per mast; the REAL fixtures live in the source-fixture candidate (`SourceFixtureLighting`, owner file) with actual mesh-derived placements — the credible fix path for grid pattern claims; my candidates this turn deliberately do not touch placement.

Night+wet compounding: the ground shader multiplies albedo by `1-water*wet_darkening` (`Conditions.cs:ConfigureWetGround` + `TrackMaterialBinder.cs` GroundShader) — a night-wet road is darker still; the wet-night combination must be part of any evidence set (§4).

## 3. Candidate set (implemented; each independently flag-gated; defaults = exact current behaviour)

All in my two night files. Data files (`lighting_profiles.json`, `track_floodlights.json`) untouched — flags are runtime multipliers; promotion later = data edits by the coordinator after acceptance.

| Flag | Default | Effect | Code |
|---|---|---|---|
| `MYSIM_NIGHT_POOL_GAIN` | unset = 1.0 | Multiplies the floodlight pool's spot energy (18 → 36 at 2.0) AND the visible emitter strips (emission 2 → 4) so the heads stay consistent with the light they represent; logged as `poolGain=` in the `TRACK_FLOODLIGHTS` line and recorded in the light-audit JSON | `TrackNightLighting.cs` (`Build`) |
| `MYSIM_NIGHT_AMBIENT_LIFT` | unset = 1.0 | Night-only ambient-energy multiplier (0.18 → 0.45 at 2.5); keeps the profile's bluish night colour (0.3,0.35,0.55) — NOT white wash; logged `NIGHT_LIFT ambientEnergy=... ambientLift=...` | `Conditions.cs` (`Apply`, gated `IsNight`) |
| `MYSIM_NIGHT_EXPOSURE` | unset = 1.0 | Night-only tonemap-exposure multiplier (1.0 → 1.35 at 1.35 — a measured 35% lift, not a wash); logged in the same `NIGHT_LIFT` line | `Conditions.cs` (`Apply`, gated `IsNight`) |

Recommended first probe values (defensible from the defect map): pool 2.0 / ambient 2.5 / exposure 1.35 — each also run alone for attribution (§4). Notes for the owner: (a) `SourceFixtureLighting.cs:53` hardcodes energy 18 — the same `MYSIM_NIGHT_POOL_GAIN` read belongs there too so both fixture paths respond identically (owner file; one line); (b) a tonemap MODE change (filmic/ACES) is a global pipeline change — deliberately NOT in this candidate set.

## 4. Evidence protocol

- **Same-position night pairs, one flag dimension at a time:** for the reference night track (25, Singapore — the archived rejected frame's track) and optionally track 0: render pairs {all-off (rejected-state reproduction), +pool, +ambient, +exposure, all-three} at the SAME camera (fixed `MYSIM_CAMPOS`/startup camera), each at the same commit → 5 stills per condition; conditions: night-dry (TOD 3, WET 0), night-wet (TOD 3, WET 2), and night without floodlights (`MYSIM_TRACK_FLOODLIGHTS` unset → ambient/exposure validated alone). Acceptance: human visual verdict — car body readable (sides included), environment shapes readable beyond the road, road not washed out, no strobe.
- **Continuous night lap:** the autodrive continuous-lap runner pattern (per 41799538756) with all three flags at night, dry + wet → review in motion: pool reassignment popping (hysteresis adequacy at the 180 m radius / 0.85 score factor), road shimmer, emitter strips in the mirror, chase-cam car readability, wet-night darkness compounding.
- **Fixtures are PROVISIONAL — labelled everywhere and here:** `data/track_floodlights.json` self-labels "provisional Godot light energy, not real circuit photometry"; the light-audit JSON's scope line says "Station gaps measure placement, not lux/illumination; requires full track night/shadow/clearance review before promotion". No real-circuit illumination claims are made anywhere; the source-fixture candidate (real mesh-derived placements) remains the credible path for eventual authenticity.
- Attach per run: `MYSIM_TRACK_LIGHT_AUDIT` JSON (stations/rejection reasons/longest gap/selection samples + `poolGain`), the `NIGHT_LIFT` log lines, and the exact env matrix used.

## Verification status

Full re-read of both patched files (insertion points verified in place); balance check (TrackNightLighting 55/55 braces, 195/195 parens; Conditions +8 lines; CJK-garbage scan clean); API patterns (`EnvFloat` parse mirrors the compiled `float.TryParse` pattern in `Main.cs:61/121`; `EmissionEnergyMultiplier`, `LightEnergy` properties already used in the same files pre-patch). No compile, no render, no dispatch — coordinator owns execution. Fence files untouched this pass.
