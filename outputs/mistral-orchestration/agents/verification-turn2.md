# verification agent — turn 2 report (2026-11-04)

Scope: claim-by-claim audit of `outputs/mistral-orchestration/REVIEW_PACKET.md` session-2 status against `JOB_REGISTRY.json`, actual run IDs and artifact paths; three-way `legacy_long_roundtrip` contract-sync check. Method limits honoured: `gh` used ONLY for `gh run view --json` reads; no dispatches, no workflow edits, no runs; main repo checkout read-only; local work = source reading, metadata, temp analysis scripts (deleted after use).

## 0. Environment corrections (coordinator should note)

1. **Worktree path:** the tasking says `C:/Users/shash/MySimGodot-wt-verification`; that path does not exist. The real worktree is `C:/Users/shash/MySimGodot-wt-verify` (branch `mistral/verification`). All work was done there.
2. **Branch sync:** my branch (`539f34f`, turn 1) is an ancestor of `076363b` (verified `git merge-base --is-ancestor`; my work was merged at `537feb7 Merge verification specialist`). I fast-forwarded `mistral/verification` to `076363b` (ff-only, no content change) so this audit and any future patch sit on the current mainline. The only changes to my three owned source files between `539f34f` and `076363b` are exactly the long-fix diff (`src/Settings.cs` +long branch; `src/SettingsPersistChecks.cs` +`legacy_long_roundtrip` Check + BaseCases entry) — nothing else touched my files, nothing to revert.
3. **The session-2 packet status is UNCOMMITTED:** `git show 076363b:outputs/mistral-orchestration/REVIEW_PACKET.md` contains no "session 2" section; the session-2 text exists only in the main tree's uncommitted working copy (same for the updated `JOB_REGISTRY.json`). This audit therefore cites the working-tree text of 2026-11-04 (~11:20Z registry timestamp). If that working copy is lost, the corrections below (quoted verbatim) preserve the diffs.

## 1. Three-way `legacy_long_roundtrip` contract sync at 076363b — RESULT: IN SYNC

Method: all three sources read at HEAD `076363b` (working tree == HEAD for these files; none appear in `git status`), names extracted by regex per scenario and compared as sets (script run locally, then deleted; no repo files touched):

| Scenario | fixture `BaseCases` | audit `required_cases` | windows driver `required` | set-equal |
|---|---|---|---|---|
| legacy | 8 (incl. `legacy_long_roundtrip`, position 4 in all three) | 8 | 8 | YES |
| foreign | 4 | 4 | 4 | YES |
| corrupt | 5 | 5 | 5 | YES |
| malformed | 4 | 4 | 4 | YES |
| failures | 8 | 8 | 8 | YES |

Totals: base 29 (8+4+5+4+8) — matches the packet's "Linux now expects 29"; Windows 32 = 29 + the 3 `win_sharing_violation_*` cases in the fixture's `PlatformExtra()` — matches "Windows 32". No duplicates. `CONTRACT_SYNC_RESULT=IN_SYNC`.

Protective-chain analysis (why this stays in sync at runtime):
- fixture emitted cases <-> fixture `BaseCases`+platform extras: enforced by `contract_sync_case_names` inside EVERY run (both OSes, repo and packaged).
- fixture `BaseCases` <-> audit `required_cases`: enforced both directions on every Linux repo run by the same check (run `41818941902` passed with this on).
- Windows driver `required` -> fixture emitted: enforced by the driver's missing-case check on Windows runs.
- Residual uncovered direction: the driver's dict could silently DROP a requirement without failing (nobody re-checks driver-vs-fixture in the reverse direction). Harmless today because the fixture's own contract check enforces the full per-platform set independently of the driver, but if the coordinator wants it closed, a two-line addition to `tools/cloud_windows_persist.py` after the `missing=` line (coordinator-owned; proposed, not applied by me):
  `extra=[c for c in got if not any(c.startswith(req) for req in (required[scenario]+(('win_sharing_violation_rejected','win_sharing_violation_tmp_kept','win_sharing_violation_live_kept') if scenario=='failures' else ())))]` and adding `and not extra` to `ok` (the win names must be expected on Windows, otherwise this would false-fail).

## 2. Claim-by-claim audit — session-2 packet vs registry vs actual runs

Legend: VERIFIED = I independently confirmed via `gh run view --json` (existence/conclusion/headSha), `git show`/`rev-parse`, or on-disk check. CORROBORATED = consistent with evidence I can see, but the specific content was not independently readable within my permitted scope.

| # | Claim (packet/registry) | Verdict | Evidence |
|---|---|---|---|
| 1 | Run `41798040383` at `e8b80bb` = success, settings_persist_review | VERIFIED | gh: success, headSha `e8b80bbf801e9263929aec6caa0a3ddd969a7f6` |
| 2 | "28/28, five isolated scenarios" at that run | CORROBORATED | `git show e8b80bb:src/SettingsPersistChecks.cs` has zero `legacy_long` matches (28-case contract at that commit); run green. Artifact content (per-scenario counts) not readable within permitted gh scope |
| 3 | Regression found by run `41795630650` at `5288329` (fence_miprepair_review), failed on controls probe not mip invariants | VERIFIED (failure+head); CORROBORATED (reason) | gh: failure @ `5288329d518c4fc4985b91610e163526a4c3aa77`; failed step = `render (0)` → "Render layouts and cars; test real scene transitions" (the audit-script step where the probe runs). The exact probe text "new throttle key not stored" needs a log read — outside permitted scope |
| 4 | Root cause: `Settings.TypedValue<T>` had no `long` branch; broke `ControlBindings` rebinding | VERIFIED | `src/ControlBindings.cs:6`: `(Key)Settings.Get("key_"+action,(long)Defaults[action])` — production path really uses `Get<long>`; `git show 076363b:src/Settings.cs` now has `if (t == typeof(long)) ... v.As<long>()` (added at `9ff443c`, diff verified: exactly 3 files) |
| 5 | Fix at `9ff443c` + `legacy_long_roundtrip` in fixture AND audit AND Windows driver | VERIFIED | `git show --stat 9ff443c`: exactly `src/Settings.cs` `src/SettingsPersistChecks.cs` `tools/cloud_game_audit.py`; all three files carry the case (§1) |
| 6 | "the verification agent's step was NOT actually in the committed workflow (handoff claim 'windows step wired at e8b80bb' was wrong)" | VERIFIED | `git show e8b80bb:.github/workflows/windows-build.yml | grep persist` = empty; e8b80bb touched only `game-review.yml` + `cloud_game_audit.py`. Note e8b80bb's own commit TITLE ("...Windows persistence step into workflows") overclaims; the script first appears at `8eeb6e4`, the workflow step only at `d3629e8` |
| 7 | Coordinator added the real step at `d3629e8` (+diagnostics artifact paths) | VERIFIED | `git show d3629e8:.github/workflows/windows-build.yml` contains the step (`python tools/cloud_windows_persist.py`) + `windows-settings-persist-*.json/.log` artifact paths; d3629e8 also updated the driver's contract (the "2 +-" line) |
| 8 | Tag `windows-candidate-d3629e8430fcddcd5544153221e07467f7499120` | VERIFIED (locally) | `git rev-parse <tag>^{commit}` = `d3629e8430fcddcd5544153221e07467f7499120` — tag name and target match the packet exactly. Remote tag existence not checkable within permitted gh scope |
| 9 | First Windows persist verification = run `41819498045`, pending | VERIFIED (still current) | gh: status `pending`, headSha `d3629e8430f...`, workflow `windows-game-build` |
| 10 | "Dispatch `41818941902` (Linux persist at `9ff443c` with the long case) queued" | **STALE — finished and PASSED** | gh: `completed`, **success**, headSha `9ff443cbf99ab00626ddd5e2014edec0e6abda98`. See §3 replacement A |
| 11 | Run `41799538756` at `5d21c56` success (jolt/240/CCD/rotational continuous lap) | VERIFIED (run-level) | gh: success @ `5d21c566d0c1b114c27128209e4b94d7cbeb1d02`, title "native_geometry_recovery cars 16 backend jolt 240Hz rotational true". Content claims (finished=true, bestLap=87.52s, damage=0...) CORROBORATED only — artifact contents not readable in scope; on-disk evidence dir exists (41 files) |
| 12 | `outputs/repair-20261029/lap-jolt-41799538756/encrypted-review-0-attempt-1/game-review-41799538756-1-0/review/` exists | VERIFIED (path) | Test-Path true, 41 files |
| 13 | car_evidence `41798036351` at `e8b80bb` success; 18 PNGs/shard at `outputs/repair-20261029/car-ev-41798036351/` | VERIFIED (run + path) | gh: success @ e8b80bb; dir exists (684 files). Content claims (corners=376, cockpit metadata) CORROBORATED only |
| 14 | HUD dispatch `41820839687` in flight at `076363b` | VERIFIED (still current) | gh: in_progress @ `076363b3d56ff03185b7779076e3150a94ef88e7` |
| 15 | Fence retry `41818948189` in flight at `9ff443c` | VERIFIED (still current) | gh: in_progress @ 9ff443c |
| 16 | (registry) `41794252920` failure @ `71dfc6c` (17 impacts, source/120); `41797648627` failure @ `d195794` (precondition error) | VERIFIED | gh: failure @ `71dfc6c749a68a556327281a5b06ad0e1f718022`; failure @ `d195794dde364e386098b54e5869d6af7c1c7950` |
| 17 | (registry) two discarded Windows dispatches | VERIFIED, with a sharpening | `41818937383`: failure, headSha **`a6416fb...`** — it ran against the STALE October head, so it tested nothing relevant; discard is correct and it must never be cited as Windows evidence. `41819339436`: cancelled @ `9ff443c` — matches "executed before branch push landed; tag pointed at d3629e8 while ref checkout was 9ff443c" |
| 18 | (registry) `"remote_tip": "d3629e8 ..."` | **STALE** | `git ls-remote` during this audit: `refs/heads/codex/repair-20261029` = `076363b3d56ff03185b7779076e3150a94ef88e7` (two commits ahead: `4afae34`, `076363b`). See §3 replacement C |
| 19 | (registry) "GitHub push API returned transient 500s (3x)" | NOT VERIFIABLE in scope | Needs API/log access beyond `gh run view --json`; documented by coordinator, plausible, unverified by me |

## 3. Contradictions / stale claims — exact quotes and proposed replacements

**A. Packet, session-2 bullet (STALE — stale in the good direction):**
> "Dispatch `41818941902` (Linux persist at `9ff443c` with the long case) queued; `41798040383` (at `e8b80bb`, without the long case) already success."

Proposed replacement:
> "Dispatch `41818941902` (Linux persist at `9ff443c` with the long case) = success (verified via `gh run view` in the 2026-11-04 turn-2 audit) — the current Linux proof is 29 base cases + 5 `contract_sync_case_names` at `9ff443c`; `41798040383` (at `e8b80bb`, 28-case contract, pre-long-fix) also success and remains the re-proof of the strengthened non-vacuous reads."

Optionally append to the "Linux re-proof DONE" bullet: "Superseded same day by run `41818941902` at `9ff443c`: 29/29 including `legacy_long_roundtrip`."

**B. JOB_REGISTRY.json entry (same fact):**
> `"41818941902": { "head": "9ff443c", "status": "queued", "depth": "settings_persist_review with legacy_long_roundtrip (29 Linux cases expected)" }`

Proposed replacement:
> `"41818941902": { "head": "9ff443c", "status": "completed success (29 base + 5 contract-sync; verified 2026-11-04 turn-2 audit)", "depth": "settings_persist_review with legacy_long_roundtrip (29 Linux cases expected)" }`

**C. JOB_REGISTRY.json header (stale tip):**
> `"remote_tip": "d3629e8 (settings long-fix 9ff443c + windows persist step d3629e8)"`

Proposed replacement:
> `"remote_tip": "076363b (long-fix 9ff443c + windows persist step d3629e8 + driving validation 4afae34 + HUD bounds assertions 076363b; ls-remote verified 2026-11-04 turn-2 audit)"`

**D. Optional erratum for the packet history (the packet already narrates this correctly; adding the hard evidence):**
> "Commit e8b80bb's own title ('Wire car_evidence depth + Windows persistence step into workflows') overclaims: `git show e8b80bb:.github/workflows/windows-build.yml` contains no persist step (verified in the turn-2 audit); the script arrived at 8eeb6e4 and the workflow step at d3629e8. The turn-1 verification deliverable consistently labelled the step a PROPOSAL, never wired."

## 4. No code patch required this turn

The three-way contract is in sync at `076363b` (§1); the long-fix diff to my files is correct and minimal; run `41818941902` proves the 29-case fixture compiles and passes on Linux with the contract-sync guard active. The only code-level suggestion (the driver's reverse-direction `extra` check, §1) touches `tools/cloud_windows_persist.py`, which I do not own — text proposed above for the coordinator.

No assertion was weakened anywhere; the stale statuses found are stale in the conservative direction (a "queued" run that actually PASSED, and a stale remote-tip pointer), plus one sharpening of the discarded-run record (41818937383 ran at the stale `a6416fb` head — discard justified, never cite it).

## 5. State changes made by this turn

1. `mistral/verification` fast-forwarded `539f34f` → `076363b` (ff-only, own worktree, no content change; verified clean tree).
2. This report committed on `mistral/verification` (only this file) and pushed once.
3. Temporary analysis scripts and the askpass helper created under `%TEMP%` and deleted after use; no secrets printed; no repo files touched outside my worktree.
