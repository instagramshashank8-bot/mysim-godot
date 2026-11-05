# verification agent — turn 4 report (2026-11-04)

Deliverables: EVIDENCE_INDEX.md updated (Windows VERIFIED with exact artifact data; new DV/product section; in-flight actuals; QC of the formal inventories), REVIEW_PACKET.md session-4 block, this note. Branch `mistral/verification` rebased onto `39cdf33` (current mainline tip) before committing; push used `--force-with-lease` (own branch only). No dispatches.

## 1. Windows persistence VERIFIED — evidence firsthand

Downloaded `windows-build-diagnostics-41828819144-attempt-1` from run 41828819144 (authorized this turn) and read the JSONs directly:

- Summary: all five scenarios `ok:true`, exit 0, `missingCases: []`, `unexpectedErrors: []`.
- EXACT per-scenario passed/failed: **legacy 9/0, foreign 5/0, corrupt 6/0, malformed 5/0, failures 12/0 — total 37** = 29 base + 3 win + 5 contract_sync.
- `platform: "windows"` present on ALL five per-scenario reports.
- `win_sharing_violation_rejected`, `win_sharing_violation_tmp_kept`, `win_sharing_violation_live_kept` all present in `failures` and PASSED (their detail strings match the fixture source).
- Log fingerprint: exactly 1 `SETTINGS_SAVE tmp=FileCantOpen` + 2 `SETTINGS_SAVE rename=Failed` per captured output in `failures` (dir-occupied tmp; env-seam rename; FileShare.None delete-block) — matches the dc8e061 per-stage expectation; paths confirm the real runner `%APPDATA%\Roaming\Godot\app_userdata\MySim\` location.
- Discrepancy noted: dc8e061's commit message says "8/5/6/5/9=33 totals" — the artifact shows 9/5/6/5/12=37 (difference = the legacy contract-sync entry + the three win cases). The artifact is authoritative; flagged in the packet.
- Scope statement (kept explicit): this verifies save/load/fault semantics as tested on a Windows Server runner with a packaged player. It does NOT cover crash-consistency and does not close stage-10.

## 2. Open item recorded (kept explicit per instructions)

**Crash-consistency through the delete-then-rename window: OPEN.** No crash-injection test exists. Godot's Windows rename deletes the live file before `MoveFileW`, so a crash in that window leaves no live settings file (tmp recovery copy survives). `temp_live_kept`/`win_sharing_violation_live_kept` do not cover it. Closing needs a crash-injection harness or a `File.Replace`-based Windows path in `Settings.cs`. Recorded as its own row in EVIDENCE_INDEX.

## 3. Run-status updates (all verified via `gh run view --json`)

| Run | Claimed in tasking | Verified actual |
|---|---|---|
| 41828819144 (windows persist) | success @ e82a362 | success @ `e82a3620702d13155cdee6d3d5cefb80411216e7` ✓ |
| 41828824418 (DV) | FAILED (grid-hold; fixed 39cdf33) | failure @ e82a362 ✓ |
| 41830853569 (product controls) | FAILED (runtime; under diagnosis) | failure @ `d9940a2` ✓ (title: product_controls_review) |
| 41834771421 (DV boot fix) | in flight | in_progress @ `39cdf33` ✓ |
| 41832997990 (wheel_fit sweep v2) | in flight | in_progress @ `71dbbe1` ✓ |
| 41831318759 (car_evidence sweep v2) | in flight | **completed FAILURE** @ `4d0622f` — not in flight; recorded |
| 41830995246 (miprepair) | in flight | in_progress @ `d9940a2` ✓ |
| 41830989890 (reference evidence) | in flight | **completed FAILURE** @ `d9940a2` — not in flight; recorded |

## 4. Texture-inventory QC (inventories landed at dc8e061; full results in EVIDENCE_INDEX)

Read the committed `outputs/mistral-orchestration/inventory/{car16,car6,track0_brands_hatch}.{md,json}` (15/17/72 rows — counts verified). Verdict: **none satisfies the six-field contract.**

- **track0_brands_hatch:** most deficient — a file listing, not an inventory: 0/72 rows have ANY binding (verified: 0 non-empty `boundTo`; 72/72 md rows `- | -`), zero GUIDs, zero UV-channel/orientation/taxonomy/correction data. Cannot support any acceptance claim in this form.
- **car16 (15 rows) / car6 (17):** strong on file paths, exact dimensions, import state, per-binding GUIDs (125/148 binding entries, e.g. `LOD_A_BODY_mm_badges` → `a903631edbed0e5cf8f2a64dc5a09eb2`) and repair state (`scale=[1,-1]`, `offset`, `pending`). GAPS: no UV-channel field anywhere (0 `uv*` fields in either JSON); no defect-taxonomy field (unoccupied `repairs: []` is ambiguous between "no defect" and "not assessed"); orientation class only implicit via repair scale on the few repaired rows; unbound rows (e.g. `arms`, `driver_16/5`, `sha`, `wheel_blur`, `ext_5`) carry no GUID at all.

Requested additions (owner of the inventory generator): per-row `guid` (resolvable via the source maps even when unbound), `uvChannel`, `orientationClass`, `defectTaxonomy`, `correctionFamily`; for track0 additionally the material/surface linkage. Until then, no inventory row is citation-eligible for acceptance.

## 5. Housekeeping

- The downloaded artifact was extracted to `%TEMP%\mysim-winpersist` and deleted after the data was recorded; re-download anytime via `gh run download 41828819144 -R instagramshashank8-bot/mysim-godot -n windows-build-diagnostics-41828819144-attempt-1`.
- PublicKey note: the coordinator's `PlatformExtra(scenario)` scoping fix (372f92f) fixed a real bug in my turn-1 fixture (win cases were expected in EVERY scenario's contract on Windows; only `failures` emits them) — acknowledged as my bug; the 41819498045 failure was the joint effect of that and the driver's blanket ERROR check, with the assertions themselves 12/12 green.
