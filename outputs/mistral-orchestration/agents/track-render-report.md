# Track rendering report — fence band diagnosis + one controlled candidate

Author: track-render specialist. Scope: track0 (Hatch/Brands) fence speckle, black foliage/horizon masses, white speckled grandstand band. All work and commits are on `mistral/track-render` in `C:/Users/shash/MySimGodot-wt-track`; the main tree was only read. Acceptance of any candidate remains a human visual review of cloud-rendered matched frames; no metric in this report is an acceptance criterion.

## 1. Diagnosis (evidence-based)

### 1.1 Surface families and material state (per-surface rows, cited)

Source of truth for rows: `outputs/repair-20261029/cloud/cutout-identity-private/review/day0-filtered-fence-baseline/cutout-hatch-baseline.json` (single-line JSON, 1125 surfaces, `mode=baseline`; citations below use JSON paths `$.surfaces[i]`). All rows below share: `sourceTransparency=AlphaScissor`, `sourceAlphaAA=AlphaToCoverage`, `edge=0.3`, `filter=LinearWithMipmapsAnisotropic`, `cull=Disabled`, `actualShadow=On`, `metallic=0`, `specular=0.12`.

| Family / texture | Surfaces | Example row (index, renderer) | Threshold |
|---|---|---|---|
| Fence wire 04 | 234 | `$.surfaces[46]` `/root/Main/World/hatch/fence_brands_hatch_catch_wirefence_01` | 0.35 (216 surf) / 0.5 (18 surf, e.g. `$.surfaces[262]` `fence_brands_hatch_marshal_fence_00`) |
| Fence small wire 01 | 155 | `$.surfaces[281]` `/root/Main/World/hatch/fence_brands_hatch_white_smallwire01` | 0.35 |
| Fence metal catch 01 | 1 | `$.surfaces[280]` `/root/Main/World/hatch/fence_brands_hatch_pit_wall_rail_mm3` | 0.5 |
| Crowd | 30 | `$.surfaces[16]` `/root/Main/World/hatch/crowd_grouped_00` (`common/crowd/2d crowd/crowd_group.png`) | 0.35 |
| Canvas (tent) | 3 | `$.surfaces[1122]` `/root/Main/World/hatch/prop_tent_medium001_mm3` (`mat_canvas_white_medium_base.png`) | 0.35 |
| Grandstand scaffold | 5 | `$.surfaces[1]` `/root/Main/World/hatch/arc_brands_hatch_grandstand_blue` (`mat_metal_scafolding_d.png`) | 0.35 |
| Wall-detail overlays | 33 | `overlay_wall_paint_green_*` renderers (`overlay_wall_details.png`) | 0.5 |
| Foliage: trees/shrubs/bushes incl. billboard cross-planes | ~330 | `$.surfaces[1038]` `/root/Main/World/hatch/org_tree_x_pine_fine_a_type_a_001` (`.../org_tree_pine_fine_a/billboarded/org_tree_pine_fine_a_type_a.png`) | 0.35 |
| Grass tufts | 161 | `$.surfaces[531]` `/root/Main/World/hatch/org_grass_b_type_a_024` (`org_grass_b.png`) | 0.35 |
| Metal rails | 10 | `$.surfaces[0]` `arc_brands_hatch_bridge_small_mm3` (`mat_metal_rail_j.png`) | 0.35 |

Fence family total = 234+155+1 = 390 surfaces — matches `changedSurfaces: 390` of rejected run 41632524155. The 780 of 41647350933 = 2 fields changed on the same 390. Thresholds originate from authored per-material `cutoff` values in `assets/unity_map.json` consumed at `src/UnityMaterialBinder.cs:85` (clip branch sets `AlphaScissorThreshold`, `AlphaToCoverage`, `edge 0.3`, `cull disabled`; UV flip `uv_scale=(-1,-1)`/`offset=(1,1)` applied to non-`org_`/`crowd` names). Binding entry points: `src/Main.cs:199` runs `UnityMaterialBinder.Apply` then `TrackMaterialBinder.Apply` per layer; `src/TrackMaterialBinder.cs:35` calls `TrackCutoutDiagnostic.Apply`.

### 1.2 Texture-level facts (measured from the exact imported textures; measurement method identical in class to the already-shipped `grandstand-scaffold-d-source-alpha.json` evidence)

| Texture | a<0.05 | 0.05..0.95 | a>0.95 | RGB where a<0.05 | RGB where a>0.95 | kept@0.50 | kept@0.35 | kept@0.18 |
|---|---|---|---|---|---|---|---|---|
| `mat_fence_wire_04.png` 512² | 63.5% | 26.1% | 10.5% | **(3,3,3)** | (41,45,47) | 22.1% | 25.1% | 29.7% |
| `mat_fence_small_wire_01.png` 256² | 89.7% | ~2.6% | ~4.6% | (130,127,134) | (112,119,125) | 6.95% | 7.31% | 8.08% |
| `mat_fence_metal_catch_01.png` 512² | 75.9% | ~9.3% | ~13.5% | (165,161,169) | (165,161,168) | 19.2% | 21.2% | 23.3% |
| `crowd_group.png` 1024² | 67.7% | 5.0% | 27.3% | (102,91,93) | (80,73,80) | — | — | — |
| `mat_canvas_white_medium_base.png` 256² | 0.0% | 29.6% | 70.4% | **(0,0,0)** | (154,155,155) | — | — | — |
| `org_tree_pine_fine_a_type_a.png` 512² | 62.3% | 21.7% | 16.0% | (99,111,86) | **(37,43,26)** very dark | — | — | — |

Key readings:
- `mat_fence_wire_04` carries **pure-black RGB in its transparent texels** and a broad mid-alpha population (26%) — bilinear + generated mips blend that black into edge texels that survive the 0.35 cut: dark speckle even where geometry is "kept".
- `mat_fence_small_wire_01` is alpha-binary (kept wires are alpha≈1) and its RGB is a uniform light-grey mottle — **the white speckle component**; threshold is irrelevant to it (kept@0.35→@0.18 moves 7.31%→8.08%).
- `mat_fence_metal_catch_01` is light-grey everywhere with a cutout — same white-speckle class.
- Tree billboard RGB is intrinsically very dark (37,43,26) — under day lighting the billboard walls read as near-black masses; this is a source-content property, not only a shading bug.

### 1.3 Where the visible band comes from (pixel evidence from existing decrypted renders)

Evidence: `fence-a2c-result/plain/review/day0-filtered-fence-{baseline,fence_a2c_candidate}.png`, `fence-true-blend-result/.../review/day0-filtered-fence-{baseline,fence_blend_candidate}.png`, `cutout-identity-private/review/day0-filtered-fence-{baseline,identity}.png` (all 960x540, same camera, day0, track0), plus current full-scene frames `reference-evidence-41788844369/.../review/ref-car16-{left,right,front}.png`.

- Both independently-downloaded baselines are pixel-identical in the band (rows 180-260: black 31%/22%/31% by column third; white 27%/29%/6%) — renders are deterministic.
- Both rejected candidates move the band by ≤2 percentage points in the same statistics (a2c black 31→32%; blend black 31→29%): re-confirming the human verdict that neither changed the defect.
- **Identity-run proof (left half):** at the black-band scanline y=215, the baseline alternates black/mid in 1-20px runs across x=0-447, while the identity render (same geometry, opacity forced, unshaded) is one continuous run x=0-447. So the left-side speckle is **not missing geometry**: the fence band is continuous, and the speckle is produced by per-pixel alpha mask decisions over a dark background/void — material-level, exactly as the audit rows describe.
- Right half (x≈453-680) stays black in spots even in the identity render: either dark source RGB or genuine gaps in that band — ownership of the far-right dark band is **not yet attributed**; the family-isolation diagnostic below resolves it in one run.
- Full-scene current frames: a near-solid white band at rows 120-180 (92-93% white in all three views) and a black band rows ~210-390 (up to 54% black on the right view). The white band's exact owner (canvas/crowd/sky-behind-cutouts) is also unattributed for the same reason.

### 1.4 Why the two rejected candidates failed, and why threshold-free material knobs are exhausted

- True-alpha blend (41632524155) changed compositing only; edge density 7.7%→7.6%; tonal histogram unchanged.
- AlphaToCoverageAndToOne (41647350933) kept scissor depth writes; edge density unchanged in 8/9 cells.
- Both failed because the failure is **sub-pixel coverage collapse**: wire features are 1-2 px on screen; generated box-average mip levels drive block-mean alpha below any fixed threshold, so the surviving texel set per frame is a threshold lottery — no compositing mode or AA mode can restore structure that the mip chain destroyed.
- Threshold changes are quantitatively dead too (table above: ≤ +4.6 pp coverage on wire04, +0.8 pp on small_wire). Per the standing instruction, no further material-AA-mode candidates were attempted.

## 2. What was changed (commit on `mistral/track-render`)

One file, `src/TrackCutoutDiagnostic.cs` (my exclusive file). Default rendering is bit-identical: `Apply` still returns immediately when `MYSIM_TRACK_CUTOUT_DIAGNOSTIC` is unset; all new behaviour sits behind new mode values of the same env var; no source asset, import file, UV, geometry, collision or default material is touched.

1. **New modes `family_isolate_fence` / `family_isolate_stands` / `family_isolate_foliage`** (diagnostic, attribution evidence): every AlphaScissor surface whose texture maps to that family (classifier `CutoutFamilyFor`: fence = `mat_fence_*`; stands = crowd/canvas/scaffold/rails/`overlay_wall*`; foliage = `/org/`) gets a duplicated material with `AlbedoTexture=null`, `AlbedoColor=alpha 0` → zero fragments and zero shadow under scissor; everything else in the scene renders normally. Rendering the reference camera once per mode attributes every band to a family by presence/absence. Audit rows gain `cutoutFamily`, `familyIsolated`, `mipRepaired`; audit JSON gains `isolatedCount`.
2. **One fix candidate, mode `fence_miprepair_candidate`**: for exactly `mat_fence_wire_04.png`, `mat_fence_small_wire_01.png`, `mat_fence_metal_catch_01.png` (regex-named, nothing else), at bind time the material gets a **rebuilt mip chain**: each level is the bilinear (2x2 box at exact halves) downsample of the previous level, then a per-level alpha-histogram remap that forces the level's alpha CDF to equal the level-0 CDF. Effect: every mip level keeps the level-0 keep-fraction for ANY threshold (0.35 and 0.5 both) and the level-0 mean coverage for A2C, while spatial structure at block scale becomes thicker coherent wire strands — the "coverage-preserving mip rebuild / thicker wire mips" direction already recorded as the next step, implemented as a binding-time transform rather than an asset edit. Transparency/threshold/AA mode/cull/filter/UV/RGB channels are untouched; source PNG and `.import` files are never written; the candidate is reversible by construction (env off = nothing happens, env on = three in-memory textures).
   - Guards: POT-square requirement (all three are 512²/256²), format conversion to RGBA8 if imported compressed, graceful skip with `FENCE_MIPREPAIR_SKIPPED` + reason when preconditions fail, per-level keep-fraction anomaly detector (>0.02 deviation from level 0 → `FENCE_MIPREPAIR_ANOMALY` on stderr + `anomaly` flag in evidence), per-texture evidence block (level-0 keep fractions at 0.35/0.5, per-level keep fractions) appended to the audit JSON, and `mipRepairedCount` in the log line.

Not changed: `TrackMaterialBinder.cs`, `TrackSceneAudit.cs`, `WallBuilder.cs`, `GroundFixer.cs`, `RainVolume.cs`, `TrackNightLighting.cs`, `Conditions.cs` (no defect in them belongs to this finding), and no file outside my ownership.

## 3. Proposed dispatch (owner/coordinator to wire and run; exact configs)

The candidate needs one new depth option and one small Python block addition — both in files that are NOT mine (`game-review.yml`, `tools/cloud_game_audit.py`); the edits are specified exactly here so the owner can apply or assign them.

1. `game-review.yml`: add `fence_miprepair_review` to the `depth` choice list; map `CUTOUT_MIPREPAIR_REVIEW: ${{ inputs.depth == 'fence_miprepair_review' && '1' || '0' }}` alongside the existing `FENCE_FILTER_REVIEW` line (line ~201), and include `fence_miprepair_review` in the `FENCE_FILTER_REVIEW`/`NIGHT_CUTOUT_REVIEW` truth lists. No new workflow inputs (stays within the 25-input limit; no HTTP 422 risk).
2. `tools/cloud_game_audit.py` `NIGHT_CUTOUT_REVIEW` block (line ~239): when `CUTOUT_MIPREPAIR_REVIEW == '1'`, run modes `['baseline', 'fence_miprepair_candidate', 'family_isolate_fence', 'family_isolate_stands', 'family_isolate_foliage']` with the exact filtered-branch configuration already used by the rejected candidates (`MYSIM_TRACK='0'`, `MYSIM_CAR='16'`, `MYSIM_TOD='0'`, `MYSIM_WET='0'`, `MYSIM_TRACK_FLOODLIGHTS='0'`, `MYSIM_BINDING_AUDIT=out`, `MYSIM_AI='1'`, `MYSIM_AUTODRIVE='1'`, `MYSIM_NOPIT='1'`, `MYSIM_RIVALS='0'`, `MYSIM_SECONDS='5'`, shot `day0-filtered-fence-<mode>.png`). Invariants to assert (keep all existing ones; add, never weaken):
   - every changed surface in `cutout-*-fence_miprepair_candidate.json` has texture in the three named files and `mipRepaired=true`; `mipRepairedCount` equals the fence surface count (390 on track0);
   - `mipRepair` evidence present with exactly 3 entries, each `anomaly=false`, and per-level `keptAt05`/`keptAt035` equal to the level-0 values within 0.02;
   - baseline rows identical to the previous baseline captures on `threshold`/`actualAlphaAA`/`filter`/`cull` (guards that the default path is untouched);
   - each `family_isolate_*` mode reports `isolatedCount` > 0 and exactly the expected family rows.
3. Dispatch (owner token handling per instructions.md §4; do not rerun on failure without a distinct diagnosis):

```
gh workflow run game-review.yml -R instagramshashank8-bot/mysim-godot --ref mistral/track-render -f depth=fence_miprepair_review
```

All other inputs at workflow defaults; the Python block pins track0/car16/day/camera exactly as the rejected fence runs did, so frames are matched against `fence-a2c-result` baseline. Acceptance test for the candidate: human visual comparison of `day0-filtered-fence-baseline.png` vs `day0-filtered-fence-fence_miprepair_candidate.png` at the fence band — visible reduction of black/white speckle without opaque sheets, holes, or order artefacts. The three isolation PNGs are attribution evidence for the white band and the far-right dark band regardless of the candidate's fate.

## 4. Verification status and open risks

- Verified locally: full-file re-read of the change; brace/paren balance (39/39, 201/201); API signatures used (`Image.CreateFromData(int,int,bool,Format,byte[])`, `GetData`, `Resize(int,int,Interpolation)`, `Interpolation.Bilinear`, `ImageTexture.CreateFromImage`, `HasMipmaps`, `IsCompressed`, `Convert`) confirmed against the installed GodotSharp 4.7.2 XML before use.
- NOT verified: no compile and no rendered frame exist yet — builds and rendering run on GitHub only (project rule). The proposed run is the compile-and-render proof; treat a compile failure as a fast, contained failure of this one change.
- The mip-repair mechanism is a hypothesis about coverage collapse; it cannot fix dark-RGB bleed on wire04 edges (RGB untouched by design) or source-dark foliage (a separate content issue). If visual review rejects it, the same evidence run still delivers the family attribution needed for the next, correctly-targeted candidate.
- The 93%-white band (rows 120-180) and the far-right dark band are not yet attributed to a family; the isolation renders answer this. I did not guess.
- Ancillary observations for other owners (no action taken): crowd/wall-detail threshold 0.35-0.5 on heavily-alpha textures; `mat_canvas_white_medium_base` has 0% fully-transparent texels but 29.6% mid-alpha, so it is being scissor-cut unnecessarily; authored `cutoff` values in `unity_map.json` are inconsistent across the same fence family (0.35 vs 0.5).
