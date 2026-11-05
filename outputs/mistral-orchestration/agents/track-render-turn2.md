# Track rendering — turn 2: mip-repair review packet + grass colour-balance candidate

Author: track-render specialist. Worktree `C:/Users/shash/MySimGodot-wt-track`, branch `mistral/track-render`. Basis: `REVIEW_PACKET.md` session-2 status, `VISUAL_REVIEW.md` (incl. the 2026-11-04T09:10Z car_evidence review), `JOB_REGISTRY.json` (2026-11-04T11:20Z). No dispatches, no workflow edits, no runs performed. The integration branch was fetched read-only (`FETCH_HEAD = 076363b`); `f8f39cf` (my turn-1 commit) is an ancestor of it, and `git diff f8f39cf 076363b` over `TrackMaterialBinder.cs` / `GroundFixer.cs` / `TrackCutoutDiagnostic.cs` / `TrackSceneAudit.cs` is empty — my worktree holds the merged state of my files verbatim, so both deliverables below are grounded in the merged code exactly as integrated.

## (a) Human-review checklist for the in-flight mip-repair candidate (run 41818948189 at 9ff443c)

### READ FIRST — expected red-run cause (owner-side schema mismatch, NOT a mip-repair failure)

The merged invariant `tools/cloud_game_audit.py` L296 reads the per-texture evidence as
`base5=entry.get('level0',{}).get('keptAt05');base35=entry.get('level0',{}).get('keptAt035')`
but the committed C# evidence schema (`src/TrackCutoutDiagnostic.cs`, unchanged by the merge — `git diff f8f39cf 076363b` over my four files is empty) emits **flat** `level0KeptAt05` / `level0KeptAt035` and no nested `level0` object. L296 therefore yields `None` and L298 (`abs(level['keptAt05']-base5) <= 0.02`) raises `TypeError` after all five modes have already rendered and all audit JSONs are written. Expect run 41818948189 to finish red with a Python `TypeError` at ~L298 even if every render and every other invariant is perfect. The workflow's encrypt/publish/upload steps run `if: always()` (game-review.yml L291/302/309 at 076363b), so the five PNGs and all audit JSONs ship in `encrypted-review-0-attempt-1` despite the red conclusion.

- Do NOT read that red as a mip-repair failure, and do not re-dispatch on it.
- For the next dispatch the owner should align the key names at L296-298 (`entry.get('level0KeptAt05')` / `entry.get('level0KeptAt035')`) or the C# side should additionally nest a `level0` object; either restores the assertion at full strength (the per-level constancy assertion itself stays exactly as strict). I did not touch the owner file.

### Step 0 — mechanical pre-checks (all must pass before any image is opened)

Download/decrypt per the established recipe; artifact name `encrypted-review-0-attempt-1` (upload is `if: always()`). Expected relative paths under the decrypted `game-review-41818948189-1-0/review/`:

1. Five PNGs, each 960x540 (same camera family as prior fence runs):
   - `day0-filtered-fence-baseline.png`
   - `day0-filtered-fence-fence_miprepair_candidate.png`
   - `day0-filtered-fence-family_isolate_fence.png`
   - `day0-filtered-fence-family_isolate_stands.png`
   - `day0-filtered-fence-family_isolate_foliage.png`
2. `cutout-hatch-fence_miprepair_candidate.json`: log line `TRACK_CUTOUT_DIAGNOSTIC mode=fence_miprepair_candidate surfaces=1125 isolated=0 mipRepaired=390`; `mipRepairedCount=390`; `mipRepair` has exactly 3 entries (one per texture); every entry `anomaly=false`; every per-level `keptAt05`/`keptAt035` equal to the level-0 values within 0.02 (read the flat `level0KeptAt05`/`level0KeptAt035` fields until the L296 key mismatch is fixed).
3. Baseline identity: this run's `day0-filtered-fence-baseline.png` must be visually identical to the baseline of the rejected fence runs (`fence-a2c-result/plain/review/day0-filtered-fence-baseline.png`). Both prior baselines were pixel-consistent; any visible difference = something other than the candidate changed the scene — stop and report before judging.

### Step 1 — visual acceptance (human; side-by-side; same-run pair)

Open `day0-filtered-fence-baseline.png` vs `day0-filtered-fence-fence_miprepair_candidate.png` side by side (same run, same camera/car16/day0). Inspect at 100% and 200% zoom:

- Region A — left fence band: rows 180-260 of 540, x 0-447 (the proven continuous-geometry zone).
- Region B — right fence/grandstand band: rows 180-260 plus rows 300-390, x 450-960.
- Region C — grandstand band context: rows 120-180 full width.

ACCEPT only if ALL hold:

1. In A and B, the black/white salt-and-pepper along fence lines visibly organizes into coherent grey mesh strands at 100% zoom (structure, not just tone).
2. The fence stays a see-through mesh — the scene behind remains visible in all regions; any opaque-sheet/grey-wall appearance fails (documented failure mode of the earlier depth-prepass/blend attempts).
3. No new holes, blank rectangles or depth-order breaks in the band.
4. Fence pattern scale unchanged up close; the band did not merely move or change hue.
5. Isolation cross-checks agree with the turn-1 attribution: `family_isolate_fence` removes the speckle band; `family_isolate_foliage` removes the dark horizon masses; `family_isolate_stands` removes/attenuates the white band. A disagreement means the turn-1 attribution was wrong — record it; do not accept the candidate on appearance alone.

What does NOT count: any metric (edge density, histograms, tonal shift), improvement visible only at zoom, or "the images differ". Minimum visible change = rule 1 at 100% zoom in BOTH regions A and B with rules 2-5 intact; anything less is rejected like its predecessors.

### Reference (rejected) baseline set for comparison

- `cloud/reference-evidence-41788844369/encrypted-review-0-attempt-1/game-review-41788844369-1-0/review/ref-car16-left.png` and `ref-car16-right.png` — the full-scene defect statement (white band rows 120-180; black band rows 210-390 at 960x540), reviewed and rejected 2026-11-04.
- `outputs/repair-20261029/car-ev-41798036351/encrypted-review-0-attempt-1/game-review-41798036351-1-0/review/ref-car16-left.png`, `ref-car16-right.png`, `ref-lap-near.png`, `ref-lap-wide.png` — latest reviewed state ("white/speckled stands/fences persist severely"; "road/grass remain repetitive and stark"); reviewer's private copy at `cloud/reviewer-car-41798036351/plain/review`.
- If 41818948189's own baseline differs visibly from these, something between e41e29b/e8b80bb and 9ff443c changed the scene — identify it before judging the candidate.

## (b) Grass colour-balance candidate — prepared, NOT dispatched (per the one-change-per-evidence-cycle rule)

**Grounding.** Defect: "grass brightyellowgreen/streaked" (VISUAL_REVIEW 2026-11-03), "Road/grass remain repetitive and stark" (VISUAL_REVIEW 2026-11-04T09:10Z). Measured source: track0's ground grass `assets/BH/textures/gnd_grass_01.png` 1024x1024, mean RGB (76,83,38), green-blue gap +45 (already a deep yellow-green hue), brightShare 0% — the garishness is produced in rendering: the ground shader multiplies it by the authored warm `macro_tint (1.10,1.04,0.78)` (blue crushed to 0.78x) and modulates with `macro_amp 0.30` / `detail_amp 0.22` (the streaks/patches), e.g. with a neutral tint the shaded base is approx (84,86,30) before lighting.

**Patch (committed on this branch, `src/TrackMaterialBinder.cs` — my file).** New env-gated pass `GrassBalancePass`, called at the end of `TrackMaterialBinder.Apply` (post-binding, which is where the material parameters exist; `GroundFixer.Fix` runs pre-binding at `src/Main.cs:199` and is geometry-only, so the material hook correctly lives here):

- Gate: `MYSIM_GRASS_BALANCE=1`; unset = return immediately (bit-identical default). Skips with a log line when `MYSIM_AUTHORED_GROUND=1` so it never fights the existing authored-ground mode.
- Scope: ONLY `ShaderMaterial` overrides whose `tex` filename starts with `gnd_grass` — exactly the set the ground shader's own grass branch keys on (`gnd_grass_01.png` / `gnd_grass_patchy*.png` / etc.). Road (`gnd_asphalt*`/`gnd_road*`), fence (StandardMaterial3D — not a ShaderMaterial) and foliage (`org_*` StandardMaterials) are structurally unreachable from this pass. Materials deduped per instance (UnityMaterialBinder shares cached materials).
- The one bundle (existing shader uniforms only — no shader text change, no other file): `macro_tint (1.10,1.04,0.78) -> (0.99,1.02,0.96)` (removes the yellow bias, restores blue), `macro_amp 0.30 -> 0.15` and `detail_amp 0.22 -> 0.15` (halves blotch/streak contrast), tile `tint x(0.92,0.96,0.94)` (slight desaturating darken).
- Evidence: per-material before/after record written to `MYSIM_BINDING_AUDIT` as `grass-balance-<layer>.json`; log line `GRASS_BALANCE materials=N ...`.
- Its dispatch belongs to a LATER evidence cycle: the coordinator's own rule ("grass color balance ... AFTER the fence mip-repair verdict") — preparing the patch now does not schedule it. When cleared, the matched pair is the same camera with/without `MYSIM_GRASS_BALANCE=1` at one commit; acceptance is the human visual verdict on grass colour/streaking.

**Verification of the patch itself:** full re-read in place; C# pattern-match syntax (`is not ShaderMaterial sm`) matches existing compiled-and-green usage (`TrackCutoutDiagnostic.cs`); `Variant` member use (`VariantType`, `Type.Color`, `AsColor`) is the same generated family as the `AsString`/`AsBool`/`AsInt32` uses compiling green in CI; the tex read uses the exact proven `.AsGodotObject() as Texture2D` pattern from `TrackSceneAudit.cs` (an earlier draft used `as Texture2D` directly on the Variant struct — CS0077 — caught and fixed during self-review). No compile and no render exist yet (cloud-only rule); the patch rides the next fence-cycle run or its own minimal run at the coordinator's discretion.

## Commit

One commit on `mistral/track-render` for this turn: `src/TrackMaterialBinder.cs` (grass candidate pass) + `outputs/mistral-orchestration/agents/track-render-turn2.md` (this file). No dispatches, no workflow edits, no runs; the integration branch was only fetched for reading; temp analysis files were created and deleted.
