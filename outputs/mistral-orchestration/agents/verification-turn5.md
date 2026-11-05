# verification agent — turn 5 report (2026-11-04)

Deliverables: EVIDENCE_INDEX.md updated (wheel_fit sweep v2 SUCCESS; product/DV/miprepair failure records with evidence; new P1/RIDE dispatch-queue tracking section), REVIEW_PACKET.md session-5 note, this note. Branch rebased onto `c057ce0` (current mainline tip) before committing. No dispatches performed by me; the coordinator's watcher owns dispatches.

## 1. wheel_fit sweep v2 — SUCCESS recorded

- `41837315358` at `e17fdf5` (`e17fdf57ad5e4454cf88c35cd7039a6ea0fb53c8`) = success, verified via `gh run view --json`.
- Recorded artifact form: per-car `41837315358` wfr dirs; evidence = `sweep.perWheel` worsts + `WHEEL_CLEARANCE_WORST` lines; the audit asserts the per-wheel sweep evidence exists (per `e17fdf5`).
- The failed first attempt `41832997990` (died mid-sweep at the 420 s default) is preserved WITH its finding: real worsts including -45 mm bodywork penetrations at full lock (content per commit `e17fdf5`) — those worsts await human review; no clearance acceptance claimed.

## 2. P1/RIDE dispatch-queue tracking (new standing section in EVIDENCE_INDEX)

- Read the watcher (`p1-queue-watcher.ps1` @ `5f2b9ee`): 4 queue entries (P1 leg A on `mistral/p1-badge-baseline`=adf89ca, leg B on `39cdf33`, both car_evidence/cars=16; RIDE-A/RIDE-B on `codex/repair-20261029`, native_geometry_recovery, cars=16, jolt/240/rotational, B adds native_ride_height=true). Dispatches only when <2 runs in flight; appends `dispatched idx=N id=ID tag=...` to p1queue-log.txt; state in p1queue-state.json.
- At recording time NO dispatch had occurred (neither state file exists).
- **Confound check leg A vs leg B — VERIFIED CLEAN from the committed trees (pre-flight):** the diff touches exactly one file (`data/car_surface_uv_repairs.json`; the 300/315 line churn is re-serialization). Semantic diff: legA 22 entries vs legB 25; only-in-B = exactly the three badges entries (all pending:true); nothing only-in-A; ZERO payload differences on all 22 shared entries; badges mentions 0 vs 9. The pair differs ONLY in the intended variable.
- **RIDE pair pre-checked at queue level** (identical except native_ride_height) with two recorded risks to verify at landing: (1) both legs use the MOVING ref `codex/repair-20261029` — a commit between dispatches would make the pair differ by code; compare headSha at recording; (2) the watcher captures the run id via `gh run list --limit 1` 15 s after dispatch — a race that can bind the wrong id; every recorded id gets verified (headSha + workflow + title) before entering the index.
- The dispatch log table is live in EVIDENCE_INDEX and will be filled as the queue advances.

## 3. miprepair 41830995246 — FAILED, evidence Shipped

- Verified via `gh run view --json jobs` + `--log-failed` + the artifacts API (explicitly tasked this turn): shards 0 and 4 FAILED; shards 1/2/3/5 succeeded. Shard 4's exception: `RuntimeError: track-28 failed; see saved logs` raised at cloud_game_audit.py:710 — the audit's GENERIC track block, NOT the miprepair invariant code. Cause of the track-0/28 engine failures: TBD (saved logs; outside my remaining scope this turn).
- Artifacts listed: `game-runtime-0..5-attempt-1` + `encrypted-review-{0,1,2,4,5,...}-attempt-1` — the fence evidence DID ship via `if: always()` and awaits extraction/viewing; the successful shards' encrypted review envelopes are the candidate's evidence.
- Recorded in the index with the exact failure signature so nobody misreads this as a fence-repair failure.

## 4. product re-run recorded

- `41835648940` at `f2eba7e6a6639ea4ffea239123185fa85db55132` = failed (verified). Context recorded: 41830853569's root cause was the BEST-lap assertion precision (fix `5305458`, merged via `99964a4`/`f2eba7e`); the re-run failed on the standings header-row bug instead; hud-camera's root cause + by-name fix landed at `6ffdf63` AFTER the run. Next product run should carry it.

## 5. Open

- Track-0/28 engine-failure causes in 41830995246 (saved logs).
- The P1/RIDE queue dispatches (watcher-owned; I record + verify as they land).
- The -45 mm-class penetration worsts and the fence candidate evidence await human review/extraction.
