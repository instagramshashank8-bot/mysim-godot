# Carbody & livery — turn 1 (roles 3+4 combined)

Author: carbody-livery specialist (Mistral Vibe), branch `mistral/carbody-livery` (worktree `C:/Users/shash/MySimGodot-wt-carbody`, base `2862b04`).
Scope of this turn: **read-only evidence**. No dispatches were made (coordinator owns GitHub). No engine/render/test execution locally (bash on this machine resolves to a broken WSL stub; all shell work done via desktop-commander `cmd`/`powershell` for source reading and metadata only). No production file was changed except the additions listed in §5 (report + one read-only detector tool), committed on this branch.

Inputs read first, as instructed: `outputs/mistral-orchestration/REVIEW_PACKET.md` (session-2 status), `outputs/mistral-orchestration/VISUAL_REVIEW.md` (latest 2026-11-04T09:10Z section), `outputs/mistral-orchestration/feedback.md` (18:18Z / 23:25Z / 06:55Z / 08:25Z / 09:10Z entries), `outputs/repair-20261029/CONTINUE.md` (tail:UF/wheel/role/UV campaigns through the 4151xxxx/4157xxxx era and the merged-car-assembly state).

Sources of evidence used below (all read this session):
- `src/CarLoader.cs` (CarShader, ApplyExact, SurfaceUV, AuditUV, twin-hide, AttachWheels), `src/CarAssemblyAudit.cs` (incl. TextTag mirror detector), `src/CarMaterialAudit.cs`, `src/CarUvTopologyEvidence.cs`, `src/TrackSceneAudit.cs` (UvSummary), `src/Logic/CarCatalog.cs`.
- `assets` metadata (main working copy `C:/Users/shash/Documents/MySimGodot/assets`, not in git): PNG IHDR headers, all 15 car16 `.png.import` files, car16 `.fbx.import` files, car16 FBX binaries (size + embedded-texture scan), `assets/car_map.json` (car16 material defs), `assets/unity_map.json` (bump/normal grep).
- Prior cloud evidence on disk: `outputs/repair-20261029/SF90_SOURCE_UV_RESULTS.json` (run 41306343412), `FORMULA16_SURFACE_UV_ACTUAL.json` (run 41510907196), `FORMULA16_UV_CORRESPONDENCE_ACTUAL.json` (runs 41571290602/41572315488/41573514607), `FORMULA16_REAR_SOURCE_FOLLOWUP.json` (runs 41574450084/41627667783), `TRACK_UV_DENSITY_ACTUAL.json` (run 41502718760), `cloud/formula16-livery-wheels-*/review/*assembly.json`, `cloud/formula16-density/outputs/cloud-game/car16-channel-0-both-assembly.json`.
- `data/car_surface_uv_repairs.json` at this branch (authoritative policy state).

Correction-family taxonomy used (exactly as instructed): `mirroredU`, `flippedV`, `upsideDownIslands`, `wrongAtlas`, `wrongUVchannel`, `reversedNormals`, `tangentHandedness`, `normalGreenChannel`, `lowSourceRes`, `importLoss`. Where a real finding does not fit any of these (two cases: degenerate/sliver UVs and a dropped reflection feature), it is labelled explicitly as an extra row marked **(outside taxonomy)** rather than force-fitted.

---

## 1. ROLE 4 — texture inventory

### 1.1 Identification

- car16 = `2019_f1_ferrari_sf90` ("FERRARI SF90", `src/Logic/CarCatalog.cs` `Folders[16]`). The reference Formula car used by `reference_evidence` and the UV campaigns.
- Reference car chosen for this inventory: **car6 = `2018_lamborghini_huracan_gt3`** (only car with an accepted per-panel UV repair — `6/LOD_A_HOOD_mm_misc`, run 41023270551 — plus the clearest role/misevidence trail: runs 41015188520/41023270551).
- Track0 = **Brands Hatch Indy** (`data/TrackList.json` entry 0, `Brands_Hatch/indy`, scene `processed/high/hatch.fbx`).

### 1.2 car16 source atlas inventory (all files, measured this session)

All 15 textures are 8-bit RGBA PNGs (IHDR colour type 6), **no ICC/gAMA/iCCP chunks** (plain sRGB assumption, no embedded profile). None is premultiplied-tagged; PNG cannot encode premultiplication, so the alpha-halo check in §1.9 remains the verification for the two decal atlases.

| File (under `assets/RealRacing/Source/2019_f1_ferrari_sf90/`) | Pixels | Bytes | Bound to (from `car_map.json` + assembly audit) | UV channel in use | Import (from `.import`) |
|---|---|---|---|---|---|
| `livery/2019_f1_ferrari_sf90_ext_16.png` | 1024×1024 | 646,169 | guid `449e4db7…` → all 10 visible `*_mm_ext` surfaces (BODY, DOOR L/R, EXHAUST, FRONTBUMPER, REARBUMPER, MIRROR L/R, STRUT_UPPER_FRONT L/R) | UV0 only (uv2 `present:false` on every surface, runs 41306343412 + 41510907196) | lossless (`compress/mode=0`), `mipmaps/generate=true`, `size_limit=0`, no VRAM |
| `livery/2019_f1_ferrari_sf90_ext_5.png` | 1024×1024 | 642,946 | **not referenced by car16's materials** (alternate-livery spare, same pattern as driver-5) | n/a | lossless, mips, no limit |
| `2019_f1_ferrari_sf90_misc.png` | 512×512 | 19,374 | guid `aed61c8b…` → 14 visible `*_mm_misc` shells (BODY, CHASSIS, FRONT/REARBUMPER, MIRROR_GLASS L/R, STRUT_LOWER×4, STRUT_UPPER_REAR×2, WHEELGUARD×2) | UV0 only | lossless, mips, no limit |
| `2019_f1_ferrari_sf90_badges.png` | **256×256** | 21,653 | guid `a903631e…` → `BODY/FRONTBUMPER/REARBUMPER_mm_badges` (alpha decal overlay) | UV0 only | lossless, mips, no limit |
| `2019_f1_ferrari_sf90_lights.png` | 512×512 | 98,656 | `BODY_mm_lights`, `REARBUMPER_mm_lights` (alpha by name rule) | UV0 only | lossless, mips, no limit |
| `2019_f1_ferrari_sf90_mask.png` | 1024×1024 | 41,420 | `_MaskMap` of all four car16 source materials; bound to shader `mask` but **inert unless `_UseMask`=1** (car16 sw material has `_UseMask=0`; body/mask gating to be printed by the detector — see §5) | (data map, UV0) | lossless, mips, no limit |
| `2019_f1_ferrari_sf90_wheel.png` | 512×512 | 51,423 | `LOD_A_WHEEL_mm`, `LOD_A_WHEEL_REAR_mm` (binds after the source-role alias fix; pre-fix these fell back — see §3.4) | UV0 only | lossless, mips, no limit |
| `2019_f1_ferrari_sf90_wheel_blur.png` | 512×512 | 93,248 | `LOD_A_WHEEL_BLUR*` — **hidden by design** (motion-blur variants filtered in `CarLoader.Load`) | n/a | lossless, mips, no limit |
| `2019_f1_ferrari_sf90_cab.png` | 512×512 | 107,734 | `INTERIOR_mm_cab`, `STEERING_WHEEL_mm_cab` | UV0 only | lossless, mips, no limit |
| `2019_f1_ferrari_sf90_int.png` | 512×512 | 70,571 | `LOD_A_INT_*` — hidden by `CarLoader` (interior) | UV0 only | lossless, mips, no limit |
| `2019_f1_ferrari_sf90_sw.png` | 1024×1024 | 288,075 | `*_mm_sw` steering-wheel shift lights (interior) | UV0 only | lossless, mips, no limit |
| `2019_f1_ferrari_sf90_sha.png` | 256×256 | 20,683 | not bound to any visible surface in the audit rows | n/a | lossless, mips, no limit |
| `2019_f1_ferrari_sf90_arms.png` | 1024×1024 | 452,298 | not bound to any visible surface (driver-suit family; source-referenced only inside `int_a/int_b.fbx` as `arms.psd`) | n/a | lossless, mips, no limit |
| `2019_f1_ferrari_sf90_driver_16.png` / `_driver_5.png` | 1024×1024 | 763,240 / 725,763 | not bound to any visible surface (driver suit per car number) | n/a | lossless, mips, no limit |

Source-shader convention (from the recovered Unity side, quoted in `CONTINUE.md` and consistent with `car_map.json`): the source `RR3Car.shader` family samples `_BaseMap` with **scale 1 / offset 0** and a bottom-left UV origin; `_MaskMap` and `_ReflectionMap` are separate assets. Our `CarShader` (`src/CarLoader.cs`) instead defaults to `uv_scale=(-1,-1)`, `uv_offset=(1,1)` and does **not** implement `_ReflectionMap` at all (see §1.8 and §1.7).

### 1.3 car16 per-surface rows (visible exterior; taxonomy applied)

Sources: run 41306343412 (`SF90_SOURCE_UV_RESULTS.json`, exact UV0 bounds + world AABB per surface), run 41510907196 (`FORMULA16_SURFACE_UV_ACTUAL.json`, BODY/misc vertex+collapsed counts), livery-wheels assembly audit (binding state after the source-role fix), `data/car_surface_uv_repairs.json` (policy state at this branch).

Orientation background (needed to read the table): raw imported UVs match the authored Unity atlas (independent proof: correspondence overlay run 41571290602 — "raw UV islands correspond to authored atlas silhouettes"). Unity authors with a **bottom-left** origin; Godot uploads images top-left, so the faithful mapping in Godot is `uv_scale=(1,-1), uv_offset=(0,1)` ("V-only"). The shipped default `(-1,-1)/(1,1)` additionally mirrors U (that extra `u'=1-u` is exactly a horizontal mirror of every decal sampled through it).

| Surface (renderer) | Atlas | UV0 bounds (raw) | Current transform | Orientation class | Visible defect | Correction family |
|---|---|---|---|---|---|---|
| `LOD_A_BODY_mm_ext` | ext_16 1024² | U 0.001–0.998, V −0.997…−0.373 (6694 uv, 628 collapsed-uv world tris) | **(1,−1)/(0,1)** — repair promoted | authoredDirect (post-fix) | blur of livery detail; text orientation OK after fix | `lowSourceRes` (only) |
| `LOD_A_FRONTBUMPER_mm_ext` | ext_16 | U 0.000–0.713, V −0.819…−0.208 (1019 uv) | **(1,−1)** promoted | authoredDirect (post-fix) | same as body | `lowSourceRes` |
| `LOD_A_REARBUMPER_mm_ext` | ext_16 | U 0.477–0.998, V −0.356…−0.003 (3566 uv) | **(1,−1)** promoted | authoredDirect (post-fix) | same as body | `lowSourceRes` |
| `LOD_A_DOOR_LEFT_mm_ext` | ext_16 | 0.5859–0.5947 / −0.5024…−0.4902 (**14 verts**) | default (−1,−1) | mirroredU | none visible (2.3 cm cube, hidden in body) — see §3.2 | `mirroredU` (cosmetic; candidate pending) |
| `LOD_A_DOOR_RIGHT_mm_ext` | ext_16 | **identical to LEFT** (same box, same UVs) | default (−1,−1) | mirroredU | none visible | `mirroredU` (cosmetic; candidate pending) |
| `LOD_A_EXHAUST_mm_ext` | ext_16 | 0.871–0.999 / −0.007…0.000 (269 uv) | default (−1,−1) | mirroredU | rear-region detail mirrored if any text (candidate pending) | `mirroredU` |
| `LOD_A_MIRROR_LEFT_mm_ext` | ext_16 | 0.383–0.478 / −0.569…−0.494 (345 uv) | default (−1,−1) | mirroredU | mirror-cap markings mirrored | `mirroredU` |
| `LOD_A_MIRROR_RIGHT_mm_ext` | ext_16 | 0.383–0.478 / −0.876…−0.801 (345 uv) — **distinct island from LEFT (good)** | default (−1,−1) | mirroredU | same | `mirroredU` |
| `LOD_A_STRUT_UPPER_FRONT_LEFT_mm_ext` | ext_16 | 0.318–0.542 / −0.633…−0.494 (74 uv) | default (−1,−1) | mirroredU | foreign-patch look on visible suspension (wrong-island sampling through the U flip) | `mirroredU` |
| `LOD_A_STRUT_UPPER_FRONT_RIGHT_mm_ext` | ext_16 | 0.318–0.542 / −0.875…−0.737 (74 uv) | default (−1,−1) | mirroredU | same | `mirroredU` |
| `LOD_A_BODY_mm_badges` | **badges 256²** | 0.027–0.978 / −0.723…−0.021 | default (unset → (−1,−1)) | **mirroredU** | **sponsor marks/numbers mirrored + soft** (primary suspect for the review's "mirrored logos/numbers") | `mirroredU` + `lowSourceRes` |
| `LOD_A_FRONTBUMPER_mm_badges` | badges | 0.027–0.202 / −0.196…−0.021 (nose number region) | default (−1,−1) | mirroredU | nose number mirrored/soft | `mirroredU` + `lowSourceRes` |
| `LOD_A_REARBUMPER_mm_badges` | badges | 0.027–0.202 / −0.196…−0.021 | default (−1,−1) | mirroredU | rear marks mirrored/soft | `mirroredU` + `lowSourceRes` |
| `LOD_A_BODY_mm_misc` (+ CHASSIS/FRONT/REARBUMPER misc, STRUT_UPPER_REAR L/R) | misc 512² | ≈full atlas (e.g. BODY 0.023–0.967 / −0.986…−0.016; 5158 uv, 706 collapsed-uv tris on BODY) | default (−1,−1) | mirroredU | carbon/mechanical detail mirrored where directional; mostly non-directional textures so低 visual impact | `mirroredU` (low priority; candidates not yet staged) |
| `LOD_A_STRUT_LOWER_*_mm_misc` ×4 | misc 512² | full-atlas subranges | default (−1,−1) | mirroredU | suspension arms visible in wheel wells | `mirroredU` (low priority) |
| `LOD_A_WHEELGUARD_FRONT_L/R_mm_chassis` | **car_chassis.png (shared 512 atlas)** | **Δ≈0.0007 UV (sliver: 0.802,−0.170→0.802,−0.169)** | default | **degenerate sliver UV (outside taxonomy)** | renders as flat colour speck — likely intentional flat plastic; verify sampled colour vs source material before any change | (outside taxonomy: `degenerateUV`) — treat as intentional until source-proven otherwise |
| `LOD_A_MIRROR_GLASS_L/R_mm_misc` | misc 512² | 0.908–0.916 / −0.086…−0.083 (tiny island) | default | mirroredU (immaterial — flat glass) | none | none needed |
| `LOD_A_TYRE_mm_tyre` (+`_REAR`) | **none** (guid `411a9c60…` has no `_BaseMap`) | full 0–1 | n/a | n/a | flat dark rubber — **correct fallback**, source tyre material is untextured | none (not a texture loss) |
| `LOD_A_WHEEL_mm` (+`_REAR`) | wheel 512² | ~full atlas | default (−1,−1) | mirroredU (rim lettering if any) | rims "plain" in captures are the **generated** rim/tyre candidate parts, not lost source textures (see §3.4) | `mirroredU` (minor) |

Notes on the table:
- "Collapsed-UV world triangles" (628 on BODY ext, 706 on BODY misc, run 41510907196) are triangles whose three UVs coincide (zero UV area but non-zero world area). They are **not** necessarily defects: they render a single texel. The prior session's caution stands: "collapsedUV needs triangle detail before repair" — do not bulk-treat them. The detector in §4 reports them per surface.
- `uvScale (0,0)` rows in older audit files are the documented shader-getter ambiguity (unbound parameter read as 0), **not** real transforms (`FORMULA16_REAR_SOURCE_FOLLOWUP.json` auditCaution; fixed in the current `CarAssemblyAudit` which treats unbound as the authored default).

### 1.4 Import pipeline verdict (car16)

- All 15 textures: lossless import, mipmaps generated, no size limit, no VRAM compression at rest; runtime filter `linear_mipmap_anisotropic`, wrap `repeat_enable` (shader source). **No import-side quality loss found** → `importLoss` is **not** the explanation for any observed car16 defect.
- Two residual import-side checks remain open (both verifiable in one cloud run, no code change):
  1. CI imports assets from the encrypted payload on a fresh workspace; local `.import` files (with `mipmaps/generate=true`) are **not in git** (`assets/` ignored). If CI regenerates imports with Godot defaults, mipmap state could differ from local. Verify once by printing `Texture2D` size + mip availability (or reading the CI `import.log`, e.g. `cloud/formula16-blur-review/review/import.log`) — expected: same lossless+mip state.
  2. `detect_3d/compress_to=1` on every texture means an editor "3D detect" pass would re-import them VRAM-compressed. Not active in headless runs (`vram_texture:false` observed), but a Studio/editor session on this repo could silently flip it. A one-line CI guard (assert `compress/mode=0` post-import for the car atlas set) would close this permanently — proposed, not implemented (coordinator-owned import tooling).
- Mesh import: ufbx (`fbx/importer=0`), `meshes/ensure_tangents=true`, `generate_lods=true`, `deduplicate_surfaces=true`, `apply_root_scale=true`; `CarLoader` applies `Scale=100` (source FBX authored in cm units; verified by measured car dimensions ~1.57×0.94×4.66 m).

### 1.5 Highest-quality original — recovery result

- `car_map.json` material entries carry full provenance paths into the recovered original project (`D:/MySim-recovered/full-game-build/project/Assets/RealRacing/...`) and they point at **exactly the PNG files shipped in `assets/RealRacing/Source/...`** — i.e. the shipped textures ARE the recovered originals, same resolution.
- The ext-part FBX binaries (`a`–`h`, `shared`) contain **no** embedded images and no external texture references (ASCII + UTF-16 scans; the only texture filename reference anywhere is `arms.psd` inside `int_a/int_b.fbx` — the driver-suit texture, not livery).
- No alternate/higher-resolution copies of the SF90 atlases exist on this machine: `D:` is not mounted; `assets/RealRacing/RecoveredNativeTextures/` and `C:\MySimWork\TextureStage` contain only track/common assets; the encrypted source payloads (which produced `assets/`) are the same files.
- **Conclusion: for car16 there is no higher-resolution original left to recover on this machine.** The 1024² livery / 256² badges atlases are the source ceiling. Any further sharpness must come from the cloud-only upscale candidates in §1.9 or from Material/feature parity (§1.7), never from local upscaling.

### 1.6 Reference car6 (`2018_lamborghini_huracan_gt3`) inventory summary

Texture set (all measured): `ext_fictional_08/09/17/18`, `ext_grt_11/19` 1024² (six liveries; `FallbackTex` picks the first alphabetical livery for unlisted cars — car6's in-game livery is therefore `ext_fictional_08` unless `car_map.json` pins otherwise; thehood evidence used the car6 set), `badges`+`badges_02` 512², `cab` 512², `int` 1024², `lights` 512², `mask` 1024², `misc` 512², `sha` 256², `sw` 512², `wheel`+`wheel_blur` 512². Import settings: same lossless+mip+no-limit family (spot-checked; full 14-file sweep is one command of the §5 tool).

Known car6-specific evidence (from prior runs, quoted in `CONTINUE.md`/repairs file):
- `6/LOD_A_HOOD_mm_misc`: V-only promoted — bonnet sampled the **seat-colour atlas region** (wrongAtlas) through the default transform; fix restores the original carbon island (run 41023270551). Classic `wrongAtlas`+`mirroredU` combo, fixed by the V-only correction on that surface.
- Role comparison (run 41015188520): misc-scope V-only made the bonnet coherent but turned **wing struts white**; nonlivery scope worsened glass → per-surface policy confirmed; global/role-wide flips rejected. This is exactly why the remaining surfaces need per-surface, per-renderer evidence rather than role-wide flips.

### 1.7 Track0 (Brands Hatch Indy) ground inventory + density

Textures bind from `assets/RealRacing/RecoveredNativeTextures/common/...` (per-surface bindings and densities measured in run 41502718760, `TRACK_UV_DENSITY_ACTUAL.json`):

| Texture | Pixels | Used by (track0 examples) | UV0 m/tile (measured) | ≈ px/m at mip0 | UV1 m/tile | Verdict |
|---|---|---|---|---|---|---|
| `surface/road/gnd_road_01.png` | **2048×2048** | `gnd_road_1..7`, `gnd_road_008..017`, runoff_b | 23.6–27.4 | 75–87 (1.2–1.3 cm/px) | 52–105 | adequate; the "flat" look is repetition + no detail layer, not resolution |
| `surface/asphalt/gnd_asphalt_dusty_noskids_01.png` | 1024×1024 | runoffs, outer asphalt, concrete_01..05, misc | 50.0–51.0 | ~20 (5 cm/px) | 11–375 | borderline; visible tiling |
| `surface/grass/gnd_grass_mowed_01.png` | **2048×2048** | grass_outer_*_mm6 | 44–61 | 34–46 | 66–143 | adequate |
| `gnd/grass/gnd_grass_patchy_01.png` | 2048×2048 | terrain_near_*, grass_outer_02 | 55–56 | ~37 | 60–107 | adequate |
| `surface/grass/gnd_grass_terrain_patchy.png` | 1024×1024 | grass_outer_* (unnumbered) | 19.9–97.0 | 11–51 | 187–322 | the 97 m/tile row (`_14`) is the worst ground density on track0 |
| `surface/asphalt/gnd_painted_asphalt_green.png` | 1024×1024 | painted green run-off | 50.1–50.2 | ~20 | 14–43 | borderline; 2–4 collapsed UV tris on two rows |
| `gnd/dirt/gnd_dirt_path_trans_grass_02.png` | 1024×1024 | sand/dirt path | 13.9 | ~74 | 26.7 | adequate |

Cross-cutting track0 facts: no normal maps are bound anywhere (grep over `CarLoader`/`TrackMaterialBinder`/`UnityMaterialBinder` has zero bump/normal handling; `unity_map.json` contains no `_BumpMap`), the primary channel is UV0 with UV1 mostly unusable (2.7–25 px/m) — so a `wrongUVchannel` swap is **not** supported for ground either (matches `TRACK_UV_DENSITY_ACTUAL` conclusion "PrimaryUVs not collapsed; blanketUVchannel swap unsupported"). The "repetitive/flat" review verdict therefore maps to: modest per-tile density on runoff/outer ground (20 px/m), single-tile repetition, and **no normal/detail/variation layer** — a material-variety gap, not an import or resolution failure. Density numbers to anchor any future ground-material work are already instrumented per-surface by `TrackSceneAudit.UvSummary` (fields `worldArea/uvArea/metresPerUv`).

Source-folder census for the track (tool-verified, §4: 72 textures under `Source/Brands_Hatch`): 27× 1024², 27× 512², 9× 256², 5× 128², 2× 512×256, 2× 64². All are lossless imports (`mode=0`); **34 of 73 import with `mipmaps/generate=false` — every one of them a baked-lightmap page (`sprites_0..14`, `shadows_0..6`)**, which is expected for UV-exact lightmap atlases; the ground/asphalt/grass textures all have mips. No import-side defect found for the perceived ground quality.

### 1.8 Mirrored logos/numbers — root cause (ranked, with the evidence)

Established mechanism (not a guess — each step has a run behind it):

1. Source authored in Unity: bottom-left UV origin; `RR3Car.shader` samples `_BaseMap` with **no transform** (scale 1 / offset 0 — quoted from the source shader in the repair history for `Car6_misc`).
2. Godot's ufbx import preserves the authored UV values (correspondence proof, run 41571290602: raw islands overlay the authored atlas silhouettes exactly).
3. Godot uploads images with a top-left origin, so faithful sampling in Godot needs exactly one V flip: `uv_scale=(1,-1), uv_offset=(0,1)`. This is what every **accepted** repair uses (car6 hood; car20's 11 visible ext roles, run 41453316712; car16 BODY/FRONTBUMPER/REARBUMPER ext, run 41572315488).
4. The shipped `CarShader` default (`uv_scale=(-1,-1)`, `uv_offset=(1,1)`) is a 180° rotation: it fixes V **and additionally mirrors U**. Every surface still on the default therefore renders its decal content horizontally mirrored.

Ranked hypotheses for the remaining "mirrored logos/numbers" (state after the 09:10Z review, i.e. with the ext trio repaired and everything else pending):

- **H1 (primary, mechanism established, fix staged):** all three `_mm_badges` surfaces (BODY/FRONTBUMPER/REARBUMPER) are still at the default (−1,−1) → every sponsor mark/number drawn through the badges overlay is mirrored. Supporting: the whole-car probe at run 41788844369 still showed mirrored marks **with** the ext repairs active, and the repairs file itself names the uncorrected badges layer "the primary remaining suspect" (both pending entries carry this basis). The addendum's observation ("the preserved reference frames visibly mirror several logos") is consistent: the preserved frames pre-date any badges fix.
- **H2 (secondary, unmeasured):** `CarShader` is `cull_disabled`; the badges/lights overlays are separate shells coincident with the body. Wherever an overlay's winding faces inward, its **backface** shows the texture mirrored even with correct UVs. Not yet measured — needs the shipped `TextTag` detector (first integration run 41823396466 failed compile CS0103; fixed at `5bf41d7`, which is an ancestor of this branch; no completed run has emitted `MIRRORED_TEXT_RISK_SUMMARY` yet) plus per-side captures.
- **H3 (rejected, do not revisit):** global U/V flips (run 41305360090 — every global flip/channel visually rejected: UV0 scrambled, UV2 uniform), UV2 channel switch (no UV2 exists on any car16 body surface — runs 41306343412/41510907196), blanket texture flips or "fix the PNG" (source PNGs are correct; correspondence proven), instance-determinant mirroring (wheel pivots rotate 180° = determinant +1; no negative-determinant instance evidence on car16 — `basisDeterminant` is instrumented in `CarAssemblyAudit` and will confirm on first successful run).

Required to close H1/H2 (proposed, not dispatched — coordinator owns runs): the matched A/B in §4.1. No blanket UV flip, no invented liveries, no recolours, no upscaling as a "fix" for mirroring.

### 1.9 Highest-quality-original policy + cloud-only upscale candidates (proposal only)

- Precondition (done, §1.5): no higher-res original exists locally for car16; the source ceiling is 1024² (livery) / 256² (badges). Recovery of `arms.psd` from the int FBXes (if ever wanted) is a cloud task and concerns the driver suit, not liveries.
- If the reviewer accepts an upscale route, it must be **cloud-only, class-specific, and per-asset**:
  - **Albedo (sRGB art: `ext_*`, `cab`, `int`, `sw`, `wheel`)**: 2× candidates only; per-island evaluation (never whole-atlas blankets); preserve logos, alpha silhouettes, UV seams exactly; compare against the original at 100% and count invented high-frequency structure (reject if the upscaler invents detail).
  - **Data maps (linear: `mask`, any future normal/roughness/metallic maps)**: any resize in LINEAR space with renormalisation (normals re-normalised per texel; masks clamped to their original value range). Never treat them as sRGB art.
  - **Alpha decals (`badges`, `lights`)**: alpha-silhouette preservation check + mip-chain alpha-coverage check (the import already sets `fix_alpha_border=true`; `premult_alpha=false`); halos classified before/after.
  - **Tiling textures (track ground)**: evaluate tiling seam behaviour after any resize (the §5 tool can diff tile-ability); the priority for tracks is a detail/variation layer, not resolution.
- Optimization budget (to be measured, not assumed): distance-based texel-density targets per surface class (cockpit-adjacent ≥1 texel/px at 2–3 m; body livery ~0.25–0.35 px/mm is the current physical ceiling; ground 20–87 px/m by class), VRAM estimate before/after (current car16 set ≈ 6.9 MB decoded; a blanket 4K×4 body atlas would be ~67 MB decoded per livery — rejected on that basis alone), load-time from import logs, and a LOD/mip policy (mips already on; mip bias only if shimmer is measured).
- Measurement protocol for ANY candidate: same commit, same camera/light/resolution, macro + normal driving distance, dry/wet/night; inspect seams, text orientation, aliasing, temporal shimmer; jettison any regression. No sharpening-as-detail; no indiscriminate 4K/8K atlases.
- **Midjourney is not in use and no such integration/access exists; nothing in this report uses or assumes it. Any generated replacement via already-authorised tooling would have to be labelled `generated` with licence/provenance verification — default: do not.**

---

## 2. ROLE 3 — carbody assembly (car16 evidence, generalised where stated)

### 2.1 Assembly census (car16, from the assembly-audit rows + source FBX layout)

- Source parts: `a`–`h` (exterior, 41 KB–1.5 MB), `int_a`/`int_b` (interior), `shared`. `CarLoader.Load` instantiates every `.fbx` in the folder, scales ×100 (cm→m), keeps only `LOD_A` non-damage, non-blur, non-debris meshes; `LOD_B`/`*_DAMAGE_*`/`*_BLUR*`/`BONNETCAM`/`SCRATCH`/`LIT_` are hidden by name policy; **all `LOD_A_INT_*` are hidden** (`isInt → Visible=false`).
- Visible exterior shell stack (per region): BODY carries **four coincident shells** (`_mm_ext` livery + `_mm_badges` sponsor overlay + `_mm_lights` + `_mm_misc` carbon/mechanical); FRONTBUMPER carries ext+badges+misc; REARBUMPER carries ext+badges+lights+misc. This layering is **intentional RR3 decal composition** (each shell has its own atlas + alpha rules), not duplication damage — but it is also why the misc-twin question exists (§2.3).
- Expected roles all present in the import (BODY/CHASSIS/DOORS/EXHAUST/FRONT+REARBUMPER/MIRRORS+MIRROR-GLASS/INTERIOR/STEERING WHEEL/STRUT_UPPER×4 + STRUT_LOWER×4/WHEELGUARD×2/WHEEL+TYRE front+rear + BLUR variants). **No missing-part evidence found in the imported census**; the F1's open-wheel "gaps" (no sidepods/roof, exposed suspension) are the car's design, and the struts/wheelguards are present and bound.

### 2.2 Per-finding: intentional vs damage (evidence per surface)

| Observation | Verdict | Evidence |
|---|---|---|
| Open-wheel layout: no sidepod/roof meshes; exposed strut arms | Intentional (F1 design) | car16 is `2019_f1_ferrari_sf90`; strut/wheelguard meshes present and bound (assembly rows) |
| `DOOR_LEFT_mm_ext` and `DOOR_RIGHT_mm_ext` are both 2.26 cm cubes at the **same** position with **identical** UV rects | Import/source artefact (placeholder stub), not a visual defect (hidden inside body) | run 41306343412 rows: identical centre (0, 0.2817, −0.3533), size, UVs; 14 verts each. Needs a source-level comparison before any removal — do **not** delete by inference |
| `WHEELGUARD_*_mm_chassis` UV0 sliver (Δ≈0.0007) → flat colour speck | Likely intentional flat plastic; unproven | sliver measured in assembly rows on `car_chassis.png`; verdict needs the source material colour (`17b01929…` def) — one read in the next audit run |
| Source `TYRE` meshes have no base texture (guid has no `_BaseMap`) → flat dark rubber | Intentional (source is untextured slick) + fallback path correct | `car_map.json` materials; `CarLoader.AttachWheels` fallback only fires when no source texture/guid — audited behaviour |
| Wheels looked "plain/low-detail" in accepted captures | **Generated** rim/tyre candidate parts (opt-in), not lost source textures | visible `MountingBell/Inner+OuterSidewall/SidewallLetter/CaliperHousing/@MeshInstance3D@*` rows are `TyreMeshBuilder`/`WheelRimBuilder`/`F1SuspensionVisualBuilder` outputs (no texture by design); source `LOD_A_WHEEL_mm` binds `wheel.png` after the role fix (§2.4) |
| Body region "disjoint/high contrast" rear surfaces (09:10Z review) | Mixed: material boundary between 4 shells + pending mirroredU on badges; not yet attributable | needs the §4.1 paired captures + TextTag run; **no geometry fix by guessing** |
| Normals/tangents | No normal maps in use anywhere (see §1.7); tangents generated at import (`ensure_tangents=true`); winding policy: all imported car surfaces verified clockwise already ("flipped 0", WindingFixer audit) — the historical reversed-normal bug was the **road** WindingFixer sign bug, fixed and regression-tested (runs 40933457055/40933461700) | `tangentHandedness`/`normalGreenChannel`/`reversedNormals` are dormant families for cars today; they activate only if normal maps get ported (then: Unity(DX, green−) vs Godot(OpenGL, green+) — flip G on import, never on the texture file) |
| Holes | No holes found in the assembled visible census for car16; the UV-topology dumps (3 panels, private) allow per-triangle seam inspection when needed | `CarUvTopologyEvidence` (MYSIM_FORMULA_UV_TOPOLOGY=1) |

Residual risk, stated plainly: the source FBX files store node names compressed (ASCII/UTF-16 scans find none), so a **source-vs-import node-count parity check cannot run locally**. A cloud-side one-liner during import (count `Model` nodes per FBX vs imported `MeshInstance3D` per part) would close the "silent missing geometry" question definitively — proposed in §4.4, not implemented here.

### 2.3 The misc twin-hide — status and the required matched evidence

What it is (`src/CarLoader.cs:27-30`, opt-in `MYSIM_MISC_TWIN_HIDE_DIAGNOSTIC=1`): hides a `*_mm_misc` renderer **only** where a same-named `_mm_ext` renderer exists (same source part imported once per material role), logging every hide and setting `misc_twin_hidden` meta for the assembly audit. Nothing is deleted; geometry/UV untouched; default off.
For car16 the eligible pairs are BODY, FRONTBUMPER, REARBUMPER (ext+misc both visible). For cars with more shells the effect differs per car — which is exactly why a blanket "misc twin hide" is not a repair.

Status: **not accepted as production repair** (VISUAL_REVIEW 09:10Z: "do not promote misc-twin hide simply because cleaner silhouette"; feedback 08:25Z: opt-in diagnostics only, "do not promote without source-matched manual images and assembly/clearance checks"). Agreed — and the reason is structural: `_mm_misc` is not a duplicate of `_mm_ext` content; it is a different atlas (carbon/mechanical islands, `misc.png`) mapped over the same surface. Hiding it removes real source detail wherever the ext atlas does not cover that detail. "Cleaner silhouette" is exactly what you would see when a detail layer is deleted.

The matched evidence the reviewer requires (proposal — same-commit A/B at named regions):

- **One commit** (whichever integration head is current), **one workflow depth**, two legs in the same run: leg A = env unset, leg B = `MYSIM_MISC_TWIN_HIDE_DIAGNOSTIC=1`; identical camera rig, lighting, resolution, shard count (single shard — the review already flagged the six-duplicate-shard waste).
- Named regions (same names in both legs, cropped identically): (1) engine cover/spine 3⁄4 rear-low; (2) nose + front wing top-down; (3) left sidepod full profile; (4) rear wing + diffuser low-rear; (5) cockpit rim close-up; plus the standard five HUD-resolution full frames as regression guards.
- Each pair judged on: (a) shimmer/z-fighting between the coincident shells (should reduce when hidden), (b) **loss of carbon/mechanical detail anywhere the ext atlas doesn't carry it** (any loss = reject), (c) decal edge changes. Numeric companions in the same run: the assembly audit with `miscTwinHidden` flags (so the hidden set is machine-recorded, not inferred) and the twin inventory from the §4 tool.
- Acceptance rule stated in advance: promote only if every named region shows no detail loss AND at least one visible artefact (shimmer/ghosting) measurably reduces; otherwise the twin stays and the real fix is per-material boundary work.

### 2.4 Wheel assembly/textures — history and residual

- Defect (documented in `FORMULA16_UV_CORRESPONDENCE_ACTUAL.json`): imported `LOD_A_WHEEL_mm` / `LOD_A_WHEEL_REAR_mm` did not match prefab keys (`LOD_A_WHEEL_mm_wheel`), so all four visible wheels fell back to untextured standard materials without source GUID. Fix: `CarLoader.ApplyExact` source-role alias resolution (`CARMAP_SOURCE_ROLE_ALIAS`, probe run 41573514607); post-fix runs show `LOD_A_WHEEL_mm` bound to `2019_f1_ferrari_sf90_wheel.png`. **Resolved for car16**; the same alias path should be spot-checked on the other 23 cars in the next full-car audit (one line in the existing audit output).
- Residual: wheel pivot mirroring uses RotY(180°) (determinant +1, no mirror); `MYSIM_WHEEL_MIRROR=legacy` switch exists for A/B. Tyre compound rings are `TyreAppearance` (formula-only since the road-car ring fix). None of this is a texture-inventory loss.

---

## 3. Proposals requiring coordinator dispatches (I dispatch nothing)

1. **P1 — badges V-only A/B (closes H1):** single shard, current integration head, two legs in one run: A = defaults, B = `MYSIM_SURFACE_UV_CANDIDATES=1` (both spellings accepted since `e41d4f4`) with the three staged pending `16/*_mm_badges` entries active. Captures: nose number, engine-cover marking, sidepod sponsor, rear wing endplate — macro + race distance, straight-on and 3⁄4. Verdict rule: every named marking reads correctly (not mirrored, not upside-down) in B and unchanged-readable in A → promote those entries from `pending`; any marking that degrades → that surface's entry stays pending with the capture attached.
2. **P2 — remaining ext surfaces (doors, exhaust, mirrors, strut-uppers):** same harness, same run as P1 (their candidates are already staged pending); per-surface verdicts as above. Doors are cosmetic (hidden inside body) — verify no visual change, then promote or drop.
3. **P3 — misc twin-hide A/B:** §2.3 protocol verbatim.
4. **P4 — macro comparison set (source-matched, front/rear/side/cockpit):** the addendum's macro evidence: fixed rig at matched viewpoints of the real SF90 livery reference (`ext_16.png` itself, rendered flat in a neutral-material capture — the repo already has the "neutral material captures" pattern from `58c8b2e`) vs the in-game car at the same camera; plus cockpit macro. This is the evidence the reviewer asked for to judge "correct surface/side orientation per surface" beyond single-region crops.
5. **P5 — first successful assembly-audit run with the current `CarAssemblyAudit`** (the `5bf41d7` fix has never run): emits `basisDeterminant` per mesh, `MIRRORED_TEXT_RISK_*` tags (closes H2's measurement half), `metresPerUv` per surface (closes the density column with measured numbers), and the wheel-binding state for all 24 cars if run fleet-wide. One depth, one shard for car16 first.
6. **P6 — source-vs-import node-count parity (closes the §2.2 residual):** during the CI import step, log per-FBX `Model`/`Geometry` counts and compare with imported mesh counts; fail loudly on a deficit. Coordinator-owned import tooling; one-time.

---

## 4. Patches on this branch (read-only; committed here, not merged anywhere)

- `tools/carbody_texture_inventory.py` — **new, read-only detector** (stdlib only, optional PIL): builds the §1 inventory automatically for any car folder (`--car <folder-name>`, `--index <n>`) or a track textures set (`--track`): PNG dimensions/colour type/chunks, `.import` compression/mipmaps/size-limit, `car_map.json` binding reverse-map (which prefab keys reference each texture, per GUID), `car_surface_uv_repairs.json` policy state per bound surface, optional `--assembly <audit.json>` join to attach measured `basisDeterminant`/`metresPerUv`/visible flags per surface, and emits `--out <markdown>` + `--json <file>`. No network, no writes outside the requested outputs; exits non-zero if the car folder or map is missing (no silent empty reports).
- **Tool validation (this session, read-only against the main working copy's assets):** `--index 16` → 15 rows, every binding and every pending/promoted repair state matched the hand-built tables above (including the full `_MaskMap` reverse map: `mask.png` is the mask of **every** car16 material, and `wheel.png` binds via the `LOD_A_WHEEL_mm_wheel` prefab keys); `--index 6` → 17 rows; `--track Brands_Hatch` → 72 rows (source of the §1.7 census). Errors are fail-closed (exit 3 on missing inputs).
- No C# changes in this turn: the runtime side of what the inventory needs (per-surface `basisDeterminant`, `textOrientation` tags, `metresPerUv`, texture binding) already exists at this branch in `CarAssemblyAudit`/`CarMaterialAudit` — it has simply never had a successful run (P5). Adding more C# now would duplicate existing detectors before their first proven output.

Commits on `mistral/carbody-livery`:
1. `carbody-livery: turn-1 inventory report (car16 per-surface taxonomy, mirrored-U root cause, track0 densities; read-only)` — this file.
2. `carbody-livery: read-only texture-inventory detector (PNG/import/car_map/repairs/assembly merge; no mutation)` — the tool.

---

## 5. Open items / explicitly not done

- The `metresPerUv` per-surface numbers quoted in §1.2/§1.3 for car16 are **analytic estimates** from measured UV bounds + world AABBs (runs 41306343412/41510907196); measured per-triangle `metresPerUv` will come from P5 (instrumentation already merged, never yet run successfully).
- CI-vs-local `.import` parity and the `detect_3d` guard (§1.4 items 1–2) are unverified hypotheses with a cheap verification path; not yet run.
- Source-FBX node-name parity (§2.2 residual) unverifiable locally (compressed FBX name tables) — P6.
- car6 per-surface UV numbers were not re-measured here (no run carries them); its table uses the texture facts + the accepted repair evidence. The §4 tool will fill the table from the next assembly audit that includes car6.
- I did not touch: workflows, dispatch configs, `data/car_surface_uv_repairs.json`, any production source file, any asset. No rendering, no engine execution, no downloads; no external services of any kind were used for these assets.
