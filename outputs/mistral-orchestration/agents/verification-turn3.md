# verification agent — turn 3 report (2026-11-04, addendum included)

Deliverables: EVIDENCE_INDEX.md (new), REVIEW_PACKET.md session-3 corrections block, this note. All on `mistral/verification` (rebased onto `2862b04`; the branch diverged from the remote copy because the coordinator had merged my turn-1 work but not my turn-2 commit — the push therefore uses `--force-with-lease` on my own branch only). No dispatches; `gh` used only for `gh run view --json` reads.

## 1. What changed

1. **EVIDENCE_INDEX.md (new, the reviewer's index):** claim→(run, commit, artifact path, status) tables for persist / lap / HUD bounds / wheel closeups / cockpit / fences / UV candidates; status vocabulary defined (machine-verified / human-pending / rejected / failed / superseded); artifact paths marked `✓disk` were verified on disk this session; content figures not re-read are marked `(content per packet)`. Rejected/failed rows are never deleted.
2. **REVIEW_PACKET.md:** new session-3 block at the top of CURRENT STATUS (additive; supersedes only the quoted stale lines): 41818948189 finished FAILED (mixed shards; schema fix postdates the run; no fence conclusion possible), 41820839687 finished FAILED (superseded by 41825439700 in flight), 41819498045 FINISHED FAILED (see §2), 41826593001 DV in flight with its fix-series history. Session-2 text preserved untouched below it.
3. **TEXTURE-INVENTORY QC section (addendum):** added to EVIDENCE_INDEX.md — the six required per-row fields (atlas GUID+file, dimensions, UV channel, orientation class, defect taxonomy value, correction family) with controlled vocabularies, explicit hand-waved patterns, six cross-checks, and a QC status table. Pre-QC already ran on the two existing artifacts: track-render-report.md §1.2 (has file+dimensions+measurements; missing GUID/UV-channel/orientation-class/taxonomy/correction-family per row) and car-assembly-turn2.md §1 (has files+2 GUIDs+UV-transform state; missing dimensions/UV channel/orientation class/taxonomy/correction family per row — note its detector DOES emit a valid per-surface taxonomy for its own output). Neither formal inventory has landed yet; when they do, the QC table gets filled and failures flagged by field.

## 2. New verified facts this turn (all via `gh run view --json`)

- **41819498045 (Windows persist, d3629e8): completed FAILURE — in the persist step itself.** Build and native-smoke steps PASSED; the failed step is exactly "Settings persistence assertions on the packaged Windows player". This is the first real Windows persist datum: the packaged player built and smoked on Windows, then the persist assertions failed. WHICH assertion failed is unknown from metadata (logs outside my permitted read scope). Candidates consistent with the record (NO root cause claimed): the predicted `atomic_replace_*` delete-then-rename defect, the temp-fault/`FileShare.None` mechanics, or a harness issue. Required next steps (coordinator): read the run's `windows-settings-persist-*.json` (the fixture's report names every failed assertion), fix with evidence, then the decisive run at `2862b04`+ (32 assertions; driver requires the win cases + `platform=windows`). The run also used the PRE-platform driver, so even success would only have proven build+smoke+8-case persist.
- 41825439700 (ref-evidence @ 5bf41d7): still in progress. 41826593001 (DV @ 9c422a4): still in progress.
- The packet's "in flight" claims for 41818948189 and 41820839687 were stale (both finished, both failed) — corrected in the session-3 block with exact quotes.

## 3. Cross-agent finding incorporated (affects this index's correctness)

car-assembly-turn2.md §0 proves run 41798036351's UV-candidate leg was INERT (env wiring `SOURCE_UV_CANDIDATES` vs gate `MYSIM_SURFACE_UV_CANDIDATES`; fixed at `e41d4f4`, merged). Consequence recorded in EVIDENCE_INDEX: the V-only UV-candidate A/B has NEVER run; the "pending UV candidate NOT accepted" verdict is not evidence against the candidate. The index's UV rows now separate twin-hide (ran; human-pending) from the UV-candidate A/B (never run; requires a run at `e41d4f4`+`80ceb22` showing `SURFACE_UV_REPAIR_CANDIDATE` lines + `MIRRORED_TEXT_RISK_SUMMARY`).

## 4. Open / not verifiable in my scope

- The exact failed assertion(s) of 41819498045 — needs the run logs/JSON (coordinator or an agent with log access).
- Per-shard causes of 41818948189's failed shards 0/3/4 — same.
- Whether the in-flight 41826593001 survives the bake-init issue fixed at 49ece2c (its head predates the fix).
- Both formal texture inventories (carbody-livery, track-render turn deliverables) — QC will run when they land.
