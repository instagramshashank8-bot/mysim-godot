# Track rendering — turn 5: run 41830995246 (fence_miprepair_review @ d9940a2) — FAILURE diagnosis

Author: track-render specialist. Read-only diagnostics: `gh run view --log-failed` + artifact download to `outputs/repair-20261029/mip2-41830995246/` (owner token used inside the process env only, never printed; no dispatches, no repo mutations). Artifacts retained at that path for the coordinator.

## Answers to the three questions

### 1. Did shard 0 run the fence branch this time? — NO.

The shard-0 job log's env block shows `FENCE_FILTER_REVIEW: 1` and `CUTOUT_MIPREPAIR_REVIEW: 1` but **`NIGHT_CUTOUT_REVIEW: 0`**. The entire fence/miprepair branch in `tools/cloud_game_audit.py` (mode list, five renders, all invariants) lives inside `if os.environ.get('NIGHT_CUTOUT_REVIEW')=='1':` — with the gate at 0 the branch was never entered on any shard. Shard 0 fell through to the default startup-render-probe sweep (tracks 0/6/12/18/24 passed as probes; track-30 stalled → job failure).

Root cause (owner file, one line): `.github/workflows/game-review.yml` **L212 at d9940a2** —

```
NIGHT_CUTOUT_REVIEW: ${{ (inputs.depth == 'night_cutout_review' || (inputs.depth == 'fence_filter_review' || inputs.depth == 'fence_a2c_review' || inputs.depth == 'cutout_identity_review')) && '1' || '0' }}
```

— `fence_miprepair_review` is missing from the truth list, while L201 (`FENCE_FILTER_REVIEW`) and L202 (`CUTOUT_MIPREPAIR_REVIEW`) DO include it. The same omission is verified at `5288329` (L206) and `9ff443c` (L208): **every `fence_miprepair_review` dispatch so far (41795630650, 41818948189, 41830995246) was structurally unable to reach the fence branch.** This is the same wrong-mode failure class as run 41785711764 recorded in feedback.md (2026-11-04T06:55Z), now in the opposite direction (gate closed instead of leaking modes in).

### 2. Did the flat-key invariant pass? — NOT REACHED.

The invariant code is present and correct at d9940a2 (`tools/cloud_game_audit.py` L302: `base5=entry.get('level0KeptAt05');base35=entry.get('level0KeptAt035')`; L303: strengthened missing-key assertion with a key-listing diagnostic; per-level ±0.02 constancy and the three `isolatedCount>0` assertions follow) — but it has never executed, because the branch above it never runs. It remains unvalidated by any run; the fix itself is not in doubt (it reads exactly the schema my committed C# emits), only unexercised.

### 3. What failed?

Two stacked, independent failures:

(a) **This run's red:** the gate mapping bug above. With the fence branch skipped, the python fell through to the default render sweep; shard 0's sweep hit `track-30` and shard 4's hit `track-28`.

(b) **A real, separate defect the fall-through exposed (not fence-related, not caused by any of my code — my passes are env-gated off in this run):** deterministic post-spawn stall. Both track-30 (Circuit de Catalunya / national, shard 0) and track-28 (shard 4) show the identical signature: boot OK → `RACE_READY track=N car=16` → `SPAWN_GROUND` → `PITSTATE t=0.0 --- progress=0.988 speed=0.0 lap=0 wear=0.00` → **no further output until the wall-time kill**; the logged retry (track-30-2 / track-28-2) reproduces byte-for-byte the same stall. Both stall at exactly `progress=0.988` — a shared systemic cause, not track geometry. Preserve as failure evidence; hand to physics-gameplay: `outputs/repair-20261029/mip2-41830995246/game-runtime-0-attempt-1/outputs/cloud-game/track-30-{1,2}.log` and `game-runtime-4-attempt-1/outputs/cloud-game/track-28-{1,2}.log` (plus `track-status.json`, which lists only the five passed probes).

## Judge pack status: NOT ASSEMBLED — no fence evidence exists in this run

Verified by listing the full downloaded tree: **no** `day0-filtered-fence-*.png`, **no** `cutout-hatch-*.json`, **no** `miprepair-summary.json` exists in any of the six shards' artifacts (only the default-path geometry/track/car outputs). No baseline-vs-candidate comparison is ready; the turn-2 checklist (`agents/track-render-turn2.md`) remains ready and unchanged but has nothing to consume from 41830995246. Nothing from this run may be quoted as fence/mip-repair evidence.

## Coordinated fix (owner; minimal; strengthens, never weakens)

1. **One line**, `game-review.yml` L212: insert `inputs.depth == 'fence_miprepair_review' ||` into the `NIGHT_CUTOUT_REVIEW` truth list. This makes the guarded fence branch — with every one of its invariants — actually run; it removes no check.
2. **Fail-closed wrong-mode tripwire** (the standing "intended-mode completion contract" requirement from feedback.md 2026-11-04T06:55Z), owner file `tools/cloud_game_audit.py`, near the top of the audit flow: if `CUTOUT_MIPREPAIR_REVIEW=='1'` and `NIGHT_CUTOUT_REVIEW!='1'` → print `WRONG_MODE miprepair requested but fence gate closed` and `sys.exit(2)`. A missed gate then fails loudly in the prepare phase instead of silently burning a six-shard matrix.
3. **Shard economy:** the fence depth only needs shard 0 (`track0 % 12 == 0`). At d9940a2 the depth runs a six-shard matrix; five shards rendered unrelated content and two of them failed inside that unrelated work. Add `fence_miprepair_review` to the single-shard `[0]` mapping (L76 pattern, same as `chiron_atlas_recovery`).
4. **Track-28/30 stall** goes to physics-gameplay as its own defect (paths above); it must not be fixed inside the fence run and must not block the fence re-dispatch once the shard mapping is fixed (with shard `[0]`-only, track-28/30 are not exercised by the fence run at all).

## Next dispatch (coordinator, after items 1-3 land at one ref)

```
gh workflow run game-review.yml -R instagramshashank8-bot/mysim-godot --ref <ref-with-gate-fix> -f depth=fence_miprepair_review
```

All other inputs at workflow defaults (same shape as this run: cars 16, backend source, 120 Hz). Expected on shard 0: the five renders (`day0-filtered-fence-baseline` / `-fence_miprepair_candidate` / `-family_isolate_{fence,stands,foliage}`), `cutout-hatch-*.json` now WITH `worldOrigin`/`boundsSize` per row, `miprepair-summary.json`; invariants L296-311 exercise for the first time; log markers `TRACK_CUTOUT_DIAGNOSTIC ... mipRepaired=390` and `FENCE_MIPREPAIR texture=... x3`. The moment that artifact lands I assemble the turn-2 judge pack (baseline vs candidate pair + three isolation frames + the audit checks) and hand the human review packet over with the exact ready comparisons listed.

## Verification status of this turn

Read-only: run metadata, failed-log retrieval, artifact download + local inspection (file listings, log reads). No local engine/render/asset processing; no dispatches; no repo mutations outside my worktree's agent-report commit. Temp analysis files created and deleted.
