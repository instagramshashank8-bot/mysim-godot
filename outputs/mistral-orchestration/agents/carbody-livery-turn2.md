# Carbody & livery — turn 2

Author: carbody-livery specialist. Branch `mistral/carbody-livery` (worktree `C:/Users/shash/MySimGodot-wt-carbody`). Basis: integration HEAD `39cdf33` (= `o_instagramshashank8-bot/codex/repair-20261029` tip, verified by fetch this session). **No dispatches; no workflow edits; no engine/render/test execution.** Canonical inventory files were NOT duplicated on this branch (the staged copies from `dc8e061` were unstaged and deleted; canonicals live on the integration branch).

Corrections from the turn-2 instructions, applied: inventory files unstaged and deleted from my worktree (canonicals are coordinator-owned at `dc8e061`); my earlier fragment suspecting a workflow env mismatch is corrected below — the wiring is sound, with one real coupling consequence that shapes the P1 runbook.

---

## 1. Task 3 — H1 staging verification at `39cdf33` (result: CONFIRMED, with one coupling finding)

- `src/CarLoader.cs` is **byte-identical between my base `2862b04` and `39cdf33`** (`git diff --stat` empty): CarShader defaults (`uv_scale=(-1,-1)`, `uv_offset=(1,1)`), the `SurfaceUV` gate and the twin-hide block are unchanged.
- `data/car_surface_uv_repairs.json` is unchanged between the same commits. The three H1 entries at `39cdf33` are exactly as H1 needs:

| Entry | pending | scale | offset | source_guid | texture |
|---|---|---|---|---|---|
| `16/LOD_A_BODY_mm_badges` | true | [1, -1] | [0, 1] | `a903631edbed0e5cf8f2a64dc5a09eb2` | `res://assets/RealRacing/Source/2019_f1_ferrari_sf90/2019_f1_ferrari_sf90_badges.png` |
| `16/LOD_A_FRONTBUMPER_mm_badges` | true | [1, -1] | [0, 1] | same | same |
| `16/LOD_A_REARBUMPER_mm_badges` | true | [1, -1] | [0, 1] | same | same |

  V-only (`u` untouched, `v'=1-v`) is exactly the transform my H1 requires; `pending:true` keeps them inert in production; the guard throws (`InvalidOperationException`) on any guid/texture mismatch, so a stale entry fails loudly rather than mis-binding.
- **Activation path verified end-to-end** (this corrects my earlier fragment): workflow env uses bare names (`SOURCE_UV_CANDIDATES`, `MISC_TWIN_HIDE` — `game-review.yml` lines 209/210, set to 1 iff `depth == car_evidence`), and `tools/cloud_game_audit.py:359` maps them onto the C# names when launching every game process:
  `car_extra = {'MYSIM_SOURCE_UV_CANDIDATES': os.environ.get('SOURCE_UV_CANDIDATES','0'), 'MYSIM_MISC_TWIN_HIDE_DIAGNOSTIC': os.environ.get('MISC_TWIN_HIDE','0')}`, and `car_extra` is spread into **every** game invocation (views, wheel closeups, steered, HUD, laps — lines 364/368/371/382/402). The gate (`CarLoader.cs:53`) opens on `MYSIM_SURFACE_UV_CANDIDATES=1` **or** `MYSIM_SOURCE_UV_CANDIDATES=1`. Chain verified end-to-end at `39cdf33`; no other consumer of the bare names exists.
- **Coupling finding (drives the P1 design):** `depth=car_evidence` forces `SOURCE_UV_CANDIDATES=1` **and** `MISC_TWIN_HIDE=1` simultaneously (`game-review.yml:209-210`). No existing depth sets one without the other, and no dispatch input overrides them. Any car_evidence run therefore differs from production by **two** variables at once. The P1 runbook (§4) is designed around this: both legs ride `car_evidence` so the twin-hide state is a controlled constant, and the badges variable is isolated by a three-line data toggle commit instead of a workflow change.

## 2. Task (a) — six-field QC of the committed inventories (`dc8e061`)

Verification: the committed `car16.md` is byte-identical to my validated turn-1 run (15 rows; every dimension, byte size, import field, binding and repair state matches my independent measurements); `car6.md` (17 rows) and `track0_brands_hatch.md` (72 rows) match my validation runs. **No factual errors found in anything the tool could measure.** The `.json` sidecars additionally carry `guid` + `slot` per binding (the `.md` table omits the guid column — cosmetic; the json is the machine source; optional tool revision, not done this turn to avoid churning coordinator-owned canonicals).

Gap analysis against the six-field standard (fields the tool cannot derive locally, and how they are now filled):

| Field | car16 | car6 | track0 |
|---|---|---|---|
| atlasGUID + file | **filled** — guid per binding in `.json`; unbound textures (arms, driver_16/5, sha, ext_5, wheel_blur) = `n/a (unbound in car_map)` | filled for bound rows (json) | `n/a per file` — tracks bind at runtime via `TrackMaterialBinder`/`unity_map`, not `car_map` |
| dimensions | filled (tool) | filled (tool) | filled (tool) |
| UV channel | **filled**: every visible surface measured UV0-only, uv2 absent (runs 41306343412 + 41510907196, 51 visible surfaces); hidden INT/BLUR surfaces = `pending-run-P5` | filled only where measured; all else `pending-run-P5` | ground/road set filled from `TRACK_UV_DENSITY_ACTUAL` (run 41502718760); remaining ~65 facades/props = `pending-run-P5` (needs a `track_scene_audit` run) |
| orientation class | **filled** from measured transforms + raw-V signs (table below) | hood measured (`wrongAtlas`+`mirroredU`, fixed); all else `pending-run-P5` | tiled/no-directional-content for ground; rest `pending-run-P5` |
| defect taxonomy | **filled** (table below) | only documented observations; rest `pending-run-P5` | lightmap `mip=false` documented (expected); ground flatness = material-variety (outside taxonomy) |
| correction family | **filled** (table below) | same | same |

### car16 six-field table at surface grain (canonical fill; sources in brackets)

| Surface | atlasGUID + file | dims | UV channel | orientation class | defect (taxonomy) | correction family |
|---|---|---|---|---|---|---|
| `LOD_A_BODY_mm_ext` | `449e4db7…` + `livery/…_ext_16.png` [car_map; run 41306343412] | 1024² | UV0 only (measured, uv2 absent) [41510907196] | authoredDirect, V-fixed (promoted repair) [41572315488] | blur only | `lowSourceRes` |
| `LOD_A_FRONTBUMPER_mm_ext` | same | 1024² | UV0 only (measured) | authoredDirect, V-fixed (promoted) | blur only | `lowSourceRes` |
| `LOD_A_REARBUMPER_mm_ext` | same | 1024² | UV0 only (measured) | authoredDirect, V-fixed (promoted) | blur only | `lowSourceRes` |
| `LOD_A_DOOR_LEFT/RIGHT_mm_ext` | same | 1024² | UV0 only (measured) | mirroredU (default transform) | none visible (2.3 cm stub cubes, hidden) | `mirroredU` (cosmetic) |
| `LOD_A_EXHAUST_mm_ext` | same | 1024² | UV0 only (measured) | mirroredU | potential mirrored detail (rear) | `mirroredU` |
| `LOD_A_MIRROR_LEFT/RIGHT_mm_ext` | same | 1024² | UV0 only (measured) | mirroredU (L/R distinct islands — correct) | mirror-cap marks mirrored | `mirroredU` |
| `LOD_A_STRUT_UPPER_FRONT_L/R_mm_ext` | same | 1024² | UV0 only (measured) | mirroredU | wrong-island look on visible suspension | `mirroredU` |
| `LOD_A_BODY/FRONTBUMPER/REARBUMPER_mm_badges` | `a903631e…` + `…_badges.png` | **256²** | UV0 only (measured) | **mirroredU (default)** | **sponsor marks/numbers mirrored + soft — H1 primary** | `mirroredU` + `lowSourceRes` |
| 14× `*_mm_misc` shells | `aed61c8b…` + `…_misc.png` | 512² | UV0 only (measured, visible ones) | mirroredU | mostly non-directional content; low impact | `mirroredU` (low priority) |
| `LOD_A_WHEELGUARD_*_mm_chassis` | `17b01929…` + `car_chassis.png` (shared) | 512² | UV0 **sliver** (Δ≈0.0007, measured) | degenerate sliver (outside taxonomy) | flat colour speck; likely intentional | (outside taxonomy: `degenerateUV`) — verify vs source before any change |
| `LOD_A_WHEEL/_REAR_mm_wheel` | `04bf1311…`… (car_map) + `…_wheel.png` | 512² | UV0 only (measured) | mirroredU (minor) | visible "plain rims" are the generated candidate parts, not lost textures | `mirroredU` (minor) |
| `LOD_A_TYRE/_REAR_mm_tyre` | no `_BaseMap` in source material | n/a | n/a | n/a | flat rubber = correct fallback | none |
| INT_*/BLUR/DAMAGE (hidden) | various | — | `pending-run-P5` | `pending-run-P5` | hidden by design | none |

car6 and track0 keep their turn-1 tables; their unmeasured fields are explicitly `pending-run-P5` (first `car_assembly_audit`-class run that includes car6, and a `track_scene_audit` run for track0, fill them via the §turn-1 tool's `--assembly` join).

## 3. Task (c) — H2 (cull_disabled backface): **no code changes needed on my branch**

The already-merged `TextTag` detector (`CarAssemblyAudit.cs`, fixed at `5bf41d7`, present at `39cdf33`) suffices for H2's measurement:

- It evaluates, per primitive, the chirality of the **composed** mesh-UV × material transform **as seen from the surface's outward (radial-from-car-centre) side** — exactly the side an outside viewer occupies.
- With `cull_disabled` (CarShader), what a viewer at a given side sees is determined solely by that chirality; a decal quad whose winding faces inward still renders mirrored text to an outside viewer precisely when the composed mapping is mirrored from that side — which is what the tag measures. Winding/front-back labels do not add a second, unmodelled effect for exterior viewers.
- Caveats, stated: the radial heuristic can mislabel concave regions (cockpit interior, wing undersides) — tags are advisory and paired renders remain the confirmation; interior-facing mirrored views (cockpit looking out) are outside its model; and the detector has still never emitted output in a completed run — its first integration run died on the CS0103 that `5bf41d7` fixed. A `car_assembly_audit` depth run at `39cdf33` produces `MIRRORED_TEXT_RISK_*` + per-surface `basisDeterminant`/`metresPerUv` and is the natural companion to P1 (independent depth, can run any time).

## 4. Task (b) — P1 badges A/B runbook (exact dispatch spec; coordinator executes)

Design constraint (verified, §1): `depth=car_evidence` hardwires `SOURCE_UV_CANDIDATES=1` **and** `MISC_TWIN_HIDE=1` (mapped by `cloud_game_audit.py:359` to the `MYSIM_*` names). No input overrides them. The runbook therefore isolates the badges variable with a **data toggle commit**, not a workflow change: both legs run the same depth, same inputs, same capture code; the refs differ by exactly three deleted JSON stanzas.

**Leg B (candidate active) — dispatch at the integration head:**

```
gh workflow run game-review.yml -R instagramshashank8-bot/mysim-godot --ref 39cdf33 -f depth=car_evidence
```

All other inputs default (cars defaults to `16`; `visual_renderer=gl_compatibility`, `physics_backend=source`, `physics_hz=120` — the 41798036351 conditions). `car_evidence` is pinned to shard `[0]` (single shard; no duplicate-shard waste). Expected state: candidates active (three badges entries apply), ext trio active (promoted), twin-hide ON.

**Leg A (baseline; badges at default transform) — zero workflow changes:**

1. Coordinator creates a scratch ref off `39cdf33`, e.g. `carbody/badges-ab-baseline`, containing exactly one commit that **deletes the three stanzas** `16/LOD_A_BODY_mm_badges`, `16/LOD_A_FRONTBUMPER_mm_badges`, `16/LOD_A_REARBUMPER_mm_badges` from `data/car_surface_uv_repairs.json` (nothing else). With the keys absent, `SurfaceUV` returns the unmodified material (default `(-1,-1)/(1,1)`) even though `SOURCE_UV_CANDIDATES=1`. The ref is used only as the A-leg target and never merged. (Note: setting `pending:false` instead would make the entries apply UNCONDITIONALLY — the wrong direction; deletion is the correct baseline.)
2. `gh workflow run game-review.yml -R instagramshashank8-bot/mysim-godot --ref carbody/badges-ab-baseline -f depth=car_evidence`

Both legs: same depth (shard `[0]`), same inputs, same capture code; refs differ by exactly the three deleted stanzas.

**Machine-checkable gates (before any visual review):**

- Leg B, every view/engine log: exactly the three lines `SURFACE_UV_REPAIR_CANDIDATE 16/LOD_A_BODY_mm_badges`, `…_FRONTBUMPER_mm_badges`, `…_REARBUMPER_mm_badges` per car load (printed by `CarLoader` when a pending entry applies), plus the three promoted `SURFACE_UV_REPAIR 16/LOD_A_{BODY,FRONTBUMPER,REARBUMPER}_mm_ext` lines; `MISC_TWIN_HIDE_DIAGNOSTIC hidden=N` present; `CARMAP car=16 bound=` present.
- Leg A: zero `SURFACE_UV_REPAIR_CANDIDATE` lines; the same three promoted lines; `hidden=N` with the **same N** as leg B; same `bound=` count.
- Any `InvalidOperationException` mentioning `Reviewed surface UV repair source mismatch` = the fail-closed guard tripped (stale guid/texture) — fix the data, never bypass the guard.
- Missing PNGs / non-uniform images = capture failure: preserve and retry per standing policy; do not lower gates.

**Expected artifact set (per leg, shard 0)** — naming per run 41798036351 (`encrypted-review-0-attempt-1/game-review-<run>-1-0/review/`), decrypted with the standing `tools/cloud_decrypt.py` + intake-key procedure:

- `ref-car16-front.png`, `ref-car16-left.png`, `ref-car16-right.png`, `ref-car16-rear.png`, `ref-car16-top.png` (five named-region frames),
- `ref-car16-cockpit.png`, `ref-car16-wheel-{fl,fr,rl,rr,fl-steered}.png`, `ref-hud-{640x360,1280x720,1920x1080,1920x1200,2560x1080}.png`, `ref-lap-{near,wide}.png` (context/regression frames; not P1-decisive),
- per-view `*-1.log` / `*-1-process.json` (source of the log gates above).

**Per-marking pass rules (source-anchored; judge each named region on the five framing PNGs, crops at native resolution, leg B vs leg A side by side, each compared against `2019_f1_ferrari_sf90_badges.png` itself):**

| Named region | Frame(s) | Pass requires (leg B) |
|---|---|---|
| Nose number / nose marks | `front`, `top` | reads correctly (not mirrored, not upside-down) vs the source atlas; position unchanged vs leg A |
| Engine-cover sponsor/number strip | `top`, `rear` | same |
| Sidepod sponsor text, left | `left` | same |
| Sidepod sponsor text, right | `right` | same (independent of left — catches L/R asymmetry) |
| Rear-facing marks (bumper/wing endplate area) | `rear` | same |
| Non-badge surfaces regression | all five | livery/carbon/lights identical to leg A (only badges layers may change) |

**Decision rule:** every named marking correct in leg B and no regression elsewhere → promote (coordinator removes `pending` from the three entries in a reviewed commit, making them unconditional); any marking still wrong or newly broken in leg B → keep `pending`, attach the pair, and the remaining suspect moves to H2's measured output (`car_assembly_audit` depth) before any further candidate. Note leg B is not "production+badges-fix": it also carries the twin-hide (forced by the depth). If the reviewer wants the badges fix judged against pristine production appearance instead, use the Primary-alt below.

**Primary-alt (cleaner isolation, one-line workflow change by coordinator):** add one dispatch input, e.g. `surface_uv_candidates` (boolean, default false), and change line 209 to `SOURCE_UV_CANDIDATES: ${{ (inputs.depth == 'car_evidence' && inputs.surface_uv_candidates) && '1' || '0' }}`; then leg A = `car_evidence` with the input unset/false (candidates off, twin-hide still on — unchanged variable), leg B = input true. Same-shape pairs without any data toggle commit. Either primary is acceptable; the toggle-commit variant requires zero workflow changes.

**Explicitly NOT acceptable as the P1 pair:** `reference_evidence` vs `car_evidence` cross-depth comparison (differs in capture set AND both twin-hide and candidates — two confounded variables), and any cross-commit comparison against the existing e41e29b/41788844369 frames (different commit). The pre-existing mirrored frames remain useful only as the "before" context the reviewer already described.

**Coupling flag for the coordinator (beyond P1):** every current and future `car_evidence` run silently carries the twin-hide ON; decisions recorded from car_evidence frames should note that (e.g. the 09:10Z review's "cleaner silhouette" observations).

## 5. Branch state

This turn adds exactly one commit on `mistral/carbody-livery`: this report (`agents/carbody-livery-turn2.md`). No code changes (H2 needs none — §3), no data changes, no workflow changes, no inventory duplicates, no dispatches. Turn-1 commits (`469dd2c` report, `a5bbeb8` tool) unchanged.

## 6. Open items

- `pending-run-P5` marks above await the first successful `car_assembly_audit`-class run at `39cdf33` (first post-`5bf41d7` run; emits per-surface `basisDeterminant`, `metresPerUv`, `MIRRORED_TEXT_RISK_*`, wheel-binding state) and a `track_scene_audit` run for track0's non-ground textures.
- P1 runs wait for the coordinator's queue (two `car_evidence` dispatches + the three-line toggle ref, or the one-line workflow input alternative).
- Optional tool revision (not done): add the guid column to the `.md` tables and an `--orientation` annotation fed by a static per-surface map; the `.json` already carries guids.
- The `arms.psd` inside `int_a/int_b.fbx` remains the only unexamined higher-fidelity source artifact (driver suit, not livery; cloud-only if ever wanted).
