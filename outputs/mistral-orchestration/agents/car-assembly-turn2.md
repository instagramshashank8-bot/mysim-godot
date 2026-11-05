# Car-assembly turn 2 — mirrored logos/numbers root cause + detector — 2026-11-05

Branch `mistral/car-assembly` (worktree `C:/Users/shash/MySimGodot-wt-car`), new commits this turn: `80ceb22` (detector), `e41d4f4` (env-name fix). Read-only inputs: `REVIEW_PACKET.md` session-2 status, `VISUAL_REVIEW.md` (incl. the 2026-11-04T09:10Z review of run 41798036351), `JOB_REGISTRY.json`, plus read-only GitHub API queries of run 41798036351's job logs and the e8b80bb content of `data/car_surface_uv_repairs.json`, `src/CarLoader.cs`, `tools/cloud_game_audit.py` (local copies of the three decisive files left untracked in my worktree `outputs/` for inspection).

## 0. Decisive process finding first: run 41798036351's UV-candidate leg was INERT

- The car_evidence depth at e8b80bb wired the environment as `SOURCE_UV_CANDIDATES` -> **`MYSIM_SOURCE_UV_CANDIDATES`** (`cloud_game_audit.py` line 316, verified in the e8b80bb blob; job env confirms `SOURCE_UV_CANDIDATES: 1`).
- My Turn-1 gate in `CarLoader.cs` reads **`MYSIM_SURFACE_UV_CANDIDATES`** (e8b80bb line 50, verified).
- The names differ by one word, so the ten pending V-only candidate entries **never applied** in that run; its mirrored-text verdict was rendered on the pre-candidate state (3 reviewed repairs + everything else at defaults). The twin-hide leg DID run (its name matches: `MYSIM_MISC_TWIN_HIDE_DIAGNOSTIC`), consistent with JOB_REGISTRY's "runtime-3 = variant with twin-hide off".
- Fix committed (`e41d4f4`, my file only): the gate now accepts both spellings, so the coordinator's existing wiring works unchanged. **The "pending UV candidate NOT accepted" verdict is therefore not yet evidence against the candidate hypothesis — the candidate test has still never run.**
- Request: the next car_evidence run should log/verify `SURFACE_UV_REPAIR_CANDIDATE` lines in the per-step game logs (they appear exactly when pending entries apply) — that is the cheap on/off proof that the leg was live. The detector below also emits per-surface state, making the run self-evidencing.

## 1. Which exterior surfaces can show mirrored text, from left vs right views

Surface inventory for car16 from the assembly evidence (`cloud/formula16-density/outputs/cloud-game/car16-channel-0-both-assembly.json`) + `car_map.json`, with each surface's text-bearing risk and current material state:

| Surfaces (car16) | Texture | Effective UV transform | Text at risk | Mirrored-from |
|---|---|---|---|---|
| LOD_A_BODY/FRONTBUMPER/REARBUMPER_mm_ext | livery `ext_16.png` | V-only (reviewed repairs) | numbers/livery lettering | per-island (see H2/H3) |
| LOD_A_DOOR_L+R, EXHAUST, MIRROR_L+R, STRUT_UPPER_FRONT_L/R `_mm_ext` | livery `ext_16.png` | V-only **candidates** (inert in all runs so far) | side numerals, mirror decals | both sides if candidates stay inert (H1) |
| LOD_A_BODY/FRONTBUMPER/REARBUMPER `_mm_badges` | `badges.png` (alpha overlay) | V-only **candidates** (inert so far) | sponsor marks (primary text carrier) | both sides if candidates stay inert (H1) |
| LOD_A_WHEEL_mm / LOD_A_WHEEL_REAR_mm (duplicated per pivot) | `wheel.png` (guid 78fcd2c9) | default, unbound params = (-1,-1)/(1,1) | rim-face branding | one side via the 180 deg pivot yaw (H5) |
| LOD_A_TYRE*_mm_tyre | source tyre texture (8fe5c99) / fallback | default or fallback Standard | sidewall branding (if textured) | one side (H5) |
| WHEELGUARD_FRONT_L/R `_mm_misc`+`_mm_chassis`, CHASSIS_mm_misc | misc/chassis atlases | default | hardware swatches, no prose text | low relevance |

Left vs right mechanics per mechanism:
- **Material-level flip (H1/H2)** mirrors text identically on BOTH sides (both sides sample the same atlas with the same transform).
- **Mirrored instance (H3)** flips exactly ONE side (the negative-scale/180deg-yaw side) while the other stays correct -> left/right ASYMMETRY.
- A back-face view (cull_disabled shader) always mirrors; only the outward-side verdict matters for the exterior critique.

## 2. Numbered, ranked hypotheses (updated by the 41798036351 evidence)

1. **H1 (still lead; untested, not disproven):** the ten pending V-only candidate entries were never applied in ANY run so far (section 0). Prediction if H1 is the whole story: with `MYSIM_SOURCE_UV_CANDIDATES=1` (post-e41d4f4), all flagged surfaces flip to correct text in one run; detector verdicts flip mirrored->ok exactly on the surfaces carrying text.
2. **H2 (co-lead; per-island chirality):** text islands are authored with opposite UV handedness to panel islands on the SAME atlas/surface (decal islands mirrored in the source DCC), so NO single per-surface linear transform fixes both panels and text on the same surface. This is consistent with the historical record: V-only made PANELS coherent (41572315488) while "markings/orientation not accepted" persisted verbatim across 41571290602 -> 41788844369 -> 41798036351. Discriminator: the detector's per-surface `mixed_risk`/fraction values on multi-island surfaces (e.g. BODY_ext partially ok / partially mirrored) and paired closeups per marking. Fix shape if confirmed: per-island, requiring source correspondence per island (split material or shader-level island handling) — per-surface entries alone cannot express it.
3. **H3 (medium; mirrored instances):** left/right sibling meshes carry negative-determinant transforms (or equivalent 180 deg pivot yaw for wheels) with shared UV layout, mirroring exactly one side. Detector signal: `MIRRORED_TEXT_PAIR_RISK` asymmetries and/or negative `basisDeterminant` on one side only. Supporting hint already on record: WHEELGUARD_FRONT_*_LEFT sits at +X in the source (naming/geometry conventions already scrambled once).
4. **H4 (low): FBX-import tangent/normal handedness** flipping the effective frame — would appear as systematic orientation-reversing with X-direction preserved in the detector; low prior because panels render coherently lit.
5. **H5 (low): wheel/rim/sidewall branding mirrored on one side** via the pivot 180 deg yaw mirror (wheel.png / tyre texture). Matches the "rims plain low-detail" note; only matters when branded art or lettering is enabled (`MYSIM_WHEEL_LETTERING`).

## 3. Detector (committed; read-only)

`src/CarAssemblyAudit.cs` (commit 80ceb22):
- Per rendered textured surface with UV0: computes the composed mesh-UV + material UV transform (`uv' = uv*scale+offset`) chirality as seen from the surface's OUTWARD side (per-primitive outward = geometric normal signed away from the mesh's radial reference; upright-viewer screen basis; near-horizontal faces use a longitudinal up hint, documented in the scope string).
- Emits per surface: `verdict` in {ok, mirrored_text_risk, rotated_text_risk, flipped_text_risk, mixed_risk, no_texture, no_uv, no_geometry, degenerate_uv}, area-weighted `flipFraction`/`xFlipFraction`, effective `uvScale/uvOffset`, primitive counts — in the assembly JSON (`mirroredTextTags[]` + per-surface `textOrientation` inside `materials[]`).
- Emits console lines for the coordinator's harness to assert on: `MIRRORED_TEXT_RISK <name> s<n> <verdict>` per flagged surface, `MIRRORED_TEXT_PAIR_RISK <family> left=... right=...` when LEFT/RIGHT members disagree (H3 signature), and always `MIRRORED_TEXT_RISK_SUMMARY car=<n> surfaces=<n> orientationRisk=<k> pairAsymmetries=<m> ...`.
- Integrity: throws on structurally impossible input (position/UV count mismatch); tags degenerate/empty surfaces instead of failing (collapsed-UV islands are known-legitimate on this car); the existing `PASS read-only car assembly evidence` line and all prior fields are unchanged.
- Explicitly NOT: a texture flip, a livery change, a UV rewrite, or a pass/fail verdict — the tags are numeric risk indicators; pixel confirmation stays with the paired renders.

Analytical predictions recorded BEFORE the next run (falsifiable): if H1 is the whole story, one candidate-enabled run flips all flagged surfaces to ok. If H2, surfaces carrying both panels and text survive as `mixed_risk` (or ok panels + mirrored text islands remain visible in closeups) and per-island work becomes necessary. If H3, `MIRRORED_TEXT_PAIR_RISK` fires on the side pairs.

## 4. Proposed next run config (coordinator-owned, no dispatch by me)

Re-run the existing car_evidence depth at a ref containing e41d4f4 + 80ceb22, with the SAME wiring as 41798036351 (`SOURCE_UV_CANDIDATES=1`, `MISC_TWIN_HIDE` A/B legs) — after e41d4f4 the existing env reaches the gate unchanged. PASS/evidence criteria: per-step logs contain `SURFACE_UV_REPAIR_CANDIDATE` lines for the ten pending keys (proves the leg live), `MIRRORED_TEXT_RISK_SUMMARY` present, and the reviewer maps each mirrored logo to a surface+verdict. If mirrored text persists WITH candidates live, H2/H3 discriminate via `mixed_risk`/pair lines, and the fix moves to per-island source correspondence (H2) or per-side handling (H3).

## 5. Risks / open

- I could not verify from metadata alone whether e8b80bb's car_evidence depth passed the env into the game process beyond the workflow-level print (the mapping at line 316 of the e8b80bb `cloud_game_audit.py` is present and correctly named apart from the mismatch fixed here; residual risk: their runtime also needs to map `MYSIM_SOURCE_UV_CANDIDATES` through `run()` — with e41d4f4 both spellings work regardless).
- The detector's outward-side heuristic (radial from mesh centre; up-hint switch for near-horizontal faces) can mislabel rare near-horizontal decal surfaces; the tags carry the raw fractions so the reviewer can override.
- The detector adds per-surface JSON volume to every assembly audit (small; ~51 surfaces on car16).
- Turn-1 items unchanged and still open: rear-assembly R1 (axle-fit A/B), R3 (misc correspondence), the clearance-sweep dispatch, and the tyre-ring/rim detail notes from the 2026-11-04T09:10Z review (TyreAppearance ring width / rim detail are my files; not touched this turn — awaiting the coordinator's priority call).
