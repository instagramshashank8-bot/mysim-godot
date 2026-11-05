# Track rendering — turn 3: tree-wall diagnosis + treeline breakup, road inventory + road-variation candidate, fence checklist status

Author: track-render specialist. Branch `mistral/track-render` (base for this turn: `f74a98a` = turn-2 grass candidate, which the coordinator merges after the fence verdict). No dispatches, no workflow edits, no runs. Integration branch fetched read-only; my four owned files are unchanged on the integration side (verified: `git diff f8f39cf 2862b04` over `TrackMaterialBinder.cs`/`GroundFixer.cs`/`TrackCutoutDiagnostic.cs`/`TrackSceneAudit.cs` is empty), so all patches below sit directly on the merged state.

## 0. Fence checklist status (item 3)

- The coordinator's fix at `49ece2c` is verified on the fetched tip: `tools/cloud_game_audit.py` L296 now reads `entry.get('level0KeptAt05')` / `entry.get('level0KeptAt035')` (my flat schema), and L297 ADDS a missing-key assertion with a key-listing diagnostic — the invariant block is now stronger than the turn-2 state, not weaker. Per-level constancy (L299, ±0.02) unchanged.
- Caveat that still matters: run 41818948189 was dispatched at `9ff443c`, BEFORE `49ece2c`. Unless it was re-dispatched onto a ref containing the fix, it runs the OLD python and is expected to finish red with a `TypeError` at ~L298 **after** all five renders complete; uploads are `if: always()` so the artifact still carries the full evidence set. The turn-2 checklist (`agents/track-render-turn2.md`) already covers this case in its Step 0 rule 1 — it remains READY as written: on receiving the shard-0 `cutout-hatch-fence_miprepair_candidate.json` I will check `mipRepairedCount=390`, three `mipRepair` entries with `anomaly=false`, per-level constancy within 0.02 (computable from the flat fields in the JSON itself regardless of the run's exit code), and only then hand the PNG pair to the human review flow with the checklist's accept/reject rules. If a re-dispatch on a post-`49ece2c` ref happens instead, the same checklist applies unchanged and the run can go green end-to-end.

## 1. The continuous vertical forest-billboard wall — diagnosis

Evidence base: `outputs/repair-20261029/cloud/cutout-identity-private/review/day0-filtered-fence-baseline/cutout-hatch-baseline.json` (1125 rows; NOTE: this Nov-3 file predates the `worldOrigin`/`boundsSize` fields — those arrive with the NEW audits from 41818948189, whose code emits them for every row); defect statements in VISUAL_REVIEW ("Treebackdrop forms obvious dark nearcontinuous wall/horizon edge"; 09:10Z: "continuous vertical forest billboard wall still has hard edge").

Findings:

1. **The wall is the treeline backdrop strip, not the tree instances**: `org_brandshatch_treeline_00` … `org_brandshatch_treeline_24` — 25 separate renderer nodes, all mapped to the ONE texture `org_brandshatch_tree_line_1.png` (all 25 renderer names enumerated from the audit rows). A repeated identical strip = the continuous vertical wall; its hard edge is the strip run's uniform height/termination.
2. **Tree instances are individual nodes, not one merged mesh**: 320 tree surfaces across 320 DISTINCT renderer nodes (`org_tree_oak_a_type_a_NNN`, `org_tree_x_oak_a_type_a_NNN`, oak/pine/oak_c families), using 14 distinct textures (base + `billboarded/*_type_[a-f]` variants across oak_a/oak_c/oak_d/pine_fine_a). So per-instance variation is structurally supported: separate `MeshInstance3D` nodes can be re-transformed without touching any mesh, UV or source file.
3. **Variant-swap breakup is NOT supported**: disk search finds exactly ONE treeline texture (`org_brandshatch_tree_line_1.png`, plus its un-imported `_alpha` companion in `RealRacing/Source/...`) across `BH/textures`, `Brands_Hatch/textures` and `RealRacing/{RecoveredNativeTextures,Source}/...`. No `tree_line_2+` exists anywhere. Per the brief's condition, texture-variant reassignment is therefore OFF the table; the candidate uses per-instance transform variation only.
4. Spatial bounds/positions of the 25 segments are NOT in the old audit JSON (fields verified absent from row 0); they arrive with 41818948189's audits (new code emits `worldOrigin`/`boundsSize` per row). Final amplitude sanity (segment length vs yaw) should be confirmed from that data before any promotion; the implemented amplitudes are deliberately conservative.

**Candidate (implemented, flag-gated `MYSIM_TREE_BREAKUP=1`, default off = untouched):** `TreeBreakupPass` in `TrackMaterialBinder.cs` —

- Scope: nodes named `org_brandshatch_treeline_*` only (the 25-strip wall; the 320 tree instances are deliberately untouched this candidate — the reviewer's complaint is the wall's continuous edge).
- Deterministic per-segment variation from an FNV-1a hash of the node name (stable across runs, no randomness): yaw within ±1.5°, height scale 0.94..1.06.
- The world-space BOTTOM of each strip stays planted: the local AABB bottom (`GetAabb()` bottom minus origin) is compensated into `Position.Y` (`pos.Y - (scaleY-1)*bottomLocal`), so no floating/sinking gaps at the ground line regardless of where the node origin sits (exact for near-identity bases; noted as an assumption in the audit record).
- Transforms only: no mesh, texture, material or UV change; fully reversible (env off = untouched); per-segment record written to `MYSIM_BINDING_AUDIT` as `tree-breakup-<layer>.json`; log line `TREE_BREAKUP segments=25 ...`.
- Expected effect and honest limit: the uniform top edge and repeated strip face break up (skyline variation + slight angle change kills the "printed wall" read); it cannot add silhouette detail that the single texture does not contain, and yaw overlap at segment joints slightly doubles alpha density there (±1.5° chosen to keep that sub-visible; verify on the matched render before any promotion).

## 2. Road — inventory and improvement candidate

**Inventory (from `assets/BH/tex_by_material.json`, 198 entries, and the disk pool):**

| Entry (BH map) | Texture | Note |
|---|---|---|
| `asphalt-_gnd_asphalt_01` | gnd_asphalt_01.png | main track0 asphalt albedo |
| `asphalt_noskids-_gnd_asphalt_noskids_01` | gnd_asphalt_noskids_01.png | NoSkid substitution target of gnd_asphalt_01 (`UnityMaterialBinder.NoSkid`) |
| `road_common-track_gnd_asphalt_01` | gnd_asphalt_01.png | same texture, second material name |
| `asphalt_painted-green_gnd_painted_asphalt_green` | gnd_painted_asphalt_green.png | painted runoff, not the racing surface |
| `grass-gnd_grass_01_*` (2 entries) | gnd_grass_01.png | ground grass (covered by the turn-2 grass candidate, untouched here) |

- Tiling: asphalt uses `world_scale 0.16` (uv/metre → ~6.25 m tile period), world-anchored sampling (`tuv = wp.xz * world_scale`) — every road piece shows the same phase; this is the structural "repetitive" cause.
- Detail sources available to the shader: procedural fbm only — `detail_scale 90 / detail_amp 0.10`, `macro_scale 0.18 / macro_amp 0.07`, `macro_tint (0.96,0.96,0.98)` for asphalt — i.e. the anti-flat layers exist but are tuned nearly invisible. NO normal/detail textures are referenced by `tex_by_material.json`; the only non-albedo asphalt textures on disk are `gnd_asphalt_spec.png` / `gnd_asphalt_noskids_spec.png` (not wired into this shader).
- Texture pool on disk (inventory, not candidates): `gnd_asphalt_02/03`, `_dusty_01`, `_dusty_noskids_01`, `_noskids_03`, `_spec`, `_sand_01`, `_brightgreen_01` plus many `gnd_road_*` coloured tarmacs belonging to other circuits' styles. Swapping albedos per road segment was considered and REJECTED as the candidate: different albedos seam at road joints and the pool variants carry other tracks' art direction.

**Candidate (implemented, flag-gated `MYSIM_ROAD_VARIATION=1`, default off = untouched):** `RoadVariationPass` in `TrackMaterialBinder.cs` — same shape as the grass pass: dedup over `gnd_asphalt*` ShaderMaterials only; turns up the existing procedural layers (`detail_amp 0.10→0.16`, `macro_amp 0.07→0.11`, `macro_scale 0.18→0.26`, `macro_tint (0.96,0.96,0.98)→(0.95,0.97,1.00)` — barely-cooler, slightly stronger macro contrast); audit `road-variation-<layer>.json`; log `ROAD_VARIATION materials=N`. Honest limit, stated in code and here: this adds non-repeating procedural contrast so the surface stops reading flat, but it CANNOT break the 6.25 m tiling period itself (world-anchored sampling, no offset uniform — a true anti-tiling fix needs an owner-side shader offset uniform or cloud texture work). Appearance only; road physics/fairness untouched.

## 3. Commit contents and verification status

One commit on `mistral/track-render`: `src/TrackMaterialBinder.cs` (two new flag-gated passes + FNV-1a helper + two call lines) and this file. Fence checklist: READY as written in turn 2, with the L296 status above folded in — send the shard-0 `cutout-hatch-fence_miprepair_candidate.json` and the five PNGs when the run lands and I will run the checklist and return the accept/reject packet. Verification status of this turn's changes: full re-read of the modified file in place; brace/paren balance 53/53 / 243/243; API patterns matched to CI-proven usage; no compile, no run, no dispatch (coordinator owns execution).

## 4. ADDENDUM — per-atlas texture census (carbody-livery structure): road + forest wall

All values read locally from the bound copies, their `.import` sidecars, `assets/BH/tex_by_material.json` (198 entries) and `assets/unity_map.json` (38,230 materials; GUID → source `.mat` path, texture, colour, cutoff, queue).

### 4.1 Road atlas family (track0 racing surface)

| Field | gnd_asphalt_01.png | gnd_asphalt_noskids_01.png |
|---|---|---|
| File (bound at runtime) | `assets/BH/textures/gnd_asphalt_01.png` | `assets/BH/textures/gnd_asphalt_noskids_01.png` |
| tex_by_material entries | `asphalt-_gnd_asphalt_01`, `road_common-track_gnd_asphalt_01` | `asphalt_noskids-_gnd_asphalt_noskids_01` |
| Unity GUIDs (unity_map.json) | `4aa41d5502ba1d9e5b422facc2f1d7b2` (others per track) | `7c2da374db6b00ada893ad68b00af65a`, `6ece1dc15ab547afe8f460e260fbd502`, `a516fc84d72662554bb7d658a14e3fbe` |
| Dimensions | 512x512 | 512x512 |
| Format/compression | PNG source; `.import` `compress/mode=0` (LOSSLESS) → runtime uncompressed RGBA8 in VRAM; `detect_3d/compress_to=1` (would re-import VRAM-compressed on detection; current cache still mode 0) | identical |
| Filtering/mips | `mipmaps/generate=true`, limit -1; sampled `filter_linear_mipmap_anisotropic`, `source_color`, `repeat_enable` | identical |
| Texel density vs world | 512 px / 6.25 m tile (world_scale 0.16 uv/m) = **81.9 texels/m** | identical (NoSkid swaps albedo at bind, same tiling) |
| sRGB/linear | albedo; `source_color` sampling (sRGB→linear automatic); no linear-data textures wired in this family | identical |
| Measured content | mean RGB (54,54,54), 100% alpha (opaque), neutral grey, low internal contrast | (55,55,55), identical character |
| Source material shape | every road GUID references ONE diffuse-class Unity shader (`2a018b57...`), tex + colour only — no normal/specular slots populated in the extraction | same |

Available detail maps: NONE wired — `tex_by_material.json` references albedos only; the only non-albedo asphalt files on disk (`gnd_asphalt_spec.png`, `gnd_asphalt_noskids_spec.png`) are not referenced by any material in the map. The shader's only detail sources are its procedural fbm layers, authored nearly off (asphalt: `detail_scale 90 / detail_amp 0.10`, `macro_scale 0.18 / macro_amp 0.07`, `macro_tint (0.96,0.96,0.98)`).

Defect-taxonomy mapping (road): `mirroredU` no (world-space sampling, no text); `flippedV` no; `upsideDownIslands` no; `wrongAtlas` no (single dedicated albedo); `wrongUVchannel` no (shader ignores UVs when world_scale>0); `reversedNormals` no (`GroundFixer` flips down-facing ground normals at build; counter in logs); `tangentHandedness`/`normalGreenChannel` N/A (no normal maps exist in this family); `lowSourceRes` NO (81.9 texels/m is oversampled at driving distance — see §5); `importLoss` none (lossless import, mipmaps on, no size limit). The actual defects are OUTSIDE the taxonomy: (a) flat shading — no normal/detail map in the source materials; (b) uniform tiling phase — world-anchored sampling with no offset uniform; (c) procedural variation authored nearly off.

### 4.2 Forest-wall atlas (treeline backdrop strips)

| Field | Value |
|---|---|
| File (bound at runtime) | `assets/BH/textures/org_brandshatch_tree_line_1.png` (duplicate copies exist under `Brands_Hatch/textures/` and `RealRacing/{RecoveredNativeTextures,Source}/...`; the binder resolves the BH copy) |
| tex_by_material entry | none (bound via `unity_map.json` GUID path) |
| Unity GUID | `5dd62b94731fd0e3ea302d8d0f1a1dfe` — source `.mat` has `cutoff=0.35`, `queue=2450` (the Unity alpha-test queue band; this is why the strips render AlphaScissor in our build), `color=1,1,1,1` |
| Dimensions | 512x256 |
| Format/compression | `compress/mode=0` lossless → uncompressed RGBA8; `detect_3d/compress_to=1` |
| Filtering/mips | `mipmaps/generate=true`; StandardMaterial3D default linear-mipmap-anisotropic filter |
| Texel density vs world | segment world size not yet measured (old audit JSONs lack `boundsSize`; the fields arrive with run 41818948189's audits) — at backdrop viewing distance (≥100 m) even ~25 texels/m would oversample the screen (≈3.7 px/m at 100 m) |
| sRGB/linear | albedo via StandardMaterial3D (sRGB default) |
| Measured content | mean RGB (51,53,45) — dark foliage; alpha 16.4% holes / **14.9% mid** / 68.7% solid — the same mid-alpha-under-scissor class as the fence wires (threshold 0.35) |
| Structure | `org_brandshatch_treeline_00..24` — 25 separate renderer nodes sharing this ONE texture; no alternative treeline texture exists anywhere on disk (checked `BH/textures`, `Brands_Hatch/textures`, `RealRacing/RecoveredNativeTextures`, `RealRacing/Source` — only `tree_line_1` plus its un-imported Source-side `_alpha` companion) |

Defect-taxonomy mapping (forest wall): `mirroredU` no (`org_`-prefixed nodes are exempt from the UV flip; audit rows confirm `uvScale=(1,1)`); `flippedV` no; `upsideDownIslands` no; `wrongAtlas` no; `wrongUVchannel` no (single UV set); `reversedNormals` N/A (unlit-relevant only; cutout strips render double-sided); `tangentHandedness`/`normalGreenChannel` N/A (no normal maps); `lowSourceRes` not for its viewing distance (backdrop); `importLoss` none (lossless, mipmaps on). The defect is STRUCTURAL and outside the taxonomy: 25 identical strips of one dark texture form the continuous wall with a hard edge; the 14.9% mid-alpha band adds scissor shimmer at the silhouette at closer range.

## 5. ADDENDUM — source-recovery-first; upscale verdict with distance budgets

The brief's rule: candidates come from SOURCE recovery first; cloud-only class-specific upscale ONLY for genuinely low-resolution sources; no 4K/8K blanket upscale; no sharpening-as-detail.

- **Road — upscale NOT justified.** Distance budget at 960-wide, 46° vertical FOV (the reference camera): screen px per metre ≈ 960/(2·tan(23°)·d) → 18.7 px/m at 20 m, 7.5 at 50 m, 3.7 at 100 m. The texture supplies 81.9 texels/m everywhere: texel:pixel ≈ 4.4:1 at 20 m (oversampled), 10.9:1 at 50 m, 22:1 at 100 m. The albedo is resolution-sufficient at every driving distance; upscaling would only spend VRAM and bandwidth (and violate the no-blanket-upscale rule). The flat look is content + near-zero procedural amps + no normal map — addressed by the `MYSIM_ROAD_VARIATION` candidate (existing procedural layers) and, if later proven necessary, an owner-side shader normal/detail-map slot — NOT by upscaling.
- **Treeline — upscale NOT justified.** Backdrop strips are viewed at ≥100 m; even a 25 texels/m strip oversamples the screen there. The defect is structural (25 identical strips, one texture, hard end) — addressed by the `MYSIM_TREE_BREAKUP` candidate; per-instance texture-variant swap was evaluated and REJECTED because the source has exactly one treeline texture (documented above).
- No 4K/8K upscale, no sharpening-as-detail anywhere in this turn's work; both candidates operate purely on existing source data (existing textures, existing shader uniforms, existing scene nodes).

## 6. ADDENDUM — VRAM / load measurement plan

- Current reality: all measured atlases import LOSSLESS (`compress/mode=0`) → uncompressed RGBA8 in VRAM: asphalt 512² ≈ 1.4 MB with mips; grass 1024² ≈ 5.6 MB; treeline 512x256 ≈ 0.7 MB. This is itself a finding for the coordinator: the project's textures are NOT VRAM-compressed, so texture memory is ~4-6x the BC-compressed norm — relevant for the stage-13 optimisation pass, independent of my candidates.
- My candidates' VRAM delta: road variation = parameter-only → **0**; tree breakup = transform-only → **0**; grass (turn-2) = parameter-only → **0**; the fence mip-repair rebuilds three existing textures at identical dimensions/format → **≈0** (re-upload only).
- Measurement plan (cloud, owner-run): in the binding-audit pass, sum `width*height*4*(4/3)` over all sampled textures per render and print `VRAM_ESTIMATE bytes=` next to the existing `TRACKBIND` line; capture wall-time of each pass via the existing per-mode `run()` timing. Acceptance of any candidate includes "no VRAM regression" read from these lines; no separate tooling needed.

## 7. ADDENDUM — same-camera before/after comparison protocol (for both new candidates)

One dispatch per candidate (owner), one commit, env-on/env-off pair through the existing depth plumbing (e.g. `ground_review` plus the env flag, or the paired-runner pattern of the fence depth):

1. Static macro: the `ref-car16` left/right/top camera set at 960x540 and 1920x1080 (same camera code path as 41788844369/41798036351) — regions: road surface macro (centre-left third), treeline wall top edge and base.
2. Driving distance: `ref-lap-near`/`ref-lap-wide`-class frames from the same run — check road shimmer/aliasing at 20-100 m and the wall edge at 100 m+ in motion.
3. Conditions: dry (TOD 0), wet (`MYSIM_WET=1` — the wet path multiplies the same albedo; verify the variation stays tasteful when darkened), night (TOD 3 + floodlights) — three condition pairs per candidate.
4. Checks: no seams at road segment joints (no texture swap by design — verify anyway), no aliasing regression at distance, no treeline base gaps (the bottom-pin), no fence/HUD regression, and the specific intended change visible (procedural contrast on road; skyline variation on the wall).
5. Acceptance: human visual verdict only, per the turn-2 checklist standard; metrics never accept.

