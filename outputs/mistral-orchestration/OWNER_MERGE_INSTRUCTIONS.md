# OWNER MERGE INSTRUCTIONS — MySim rebuild line → main (updated 2026-11-05, coordinator)

**Current integration/pre-merge SHA: `c677943` on `https://github.com/Jdiejei/mysim-godot` branch `codex/repair-20261029`** (verify at pull time). Adds since 40ddb4e: LAN phase-2 + adapters + HOST side + the regression net (card s14 - eight single-player log assertions riding existing runs) + the corridor sweep + seam welds (gated OFF) + the F1 triad + the 24-car cards + the bindings display (+HUD_RECTS) + the tracks index + the audit plan + the batch-A dispatch spec + the D0 checker/manifest/operational note + the wet/night matrix NOW FULLY WIRED (the wetnight_matrix_dry/wet case_set options + the manifest chain entries + the frame-hash hook; inputs stay 25; py_compile/YAML verified) + the env plumbing + the critical self-fix (invalid Python, caught before any run died on it).

## SHORTEST REPRODUCIBLE BATTERY (run these FIRST - ~15-35 min total, fail fast)

```bash
# Q1 (fastest, ~10 min): the fence re-run - the first run that can exercise the miprepair invariants end-to-end
gh workflow run game-review.yml --repo instagramshashank8-bot/mysim-godot --ref codex/repair-20261029 -f depth=fence_miprepair_review

# Q2 (~15-25 min): DV core - car 16 DRIVES or the auto-emitted failure report names the gate
gh workflow run game-review.yml --repo instagramshashank8-bot/mysim-godot --ref codex/repair-20261029 -f depth=driving_validation -f cars=16

# Q3 (~10 min): collision overlay + corridor on track 8 (the corridor fix's visual proof)
gh workflow run game-review.yml --repo instagramshashank8-bot/mysim-godot --ref codex/repair-20261029 -f depth=track_scene_audit
```

## WET/NIGHT MATRIX (D0-first; OPTIONAL - after the quick battery's D0-family gates hold; added 2026-11-05 turn 25 by the verification agent from track-render turn-23's matrix, VERIFYING every knob against the env plumbing landed at `1f4ccf0`; UPDATED turn 26 for the `4179e30` prerequisites)

**Prerequisites CLOSED at `4179e30` (verified):** (1) the 4-case manifest pair EXISTS — `data/native_wetnight_matrix_dry.json` (car16+car0 × tracks 0+8, no weather keys = the default dry lighting; the tool now treats missing weather as the 0-default WITHOUT weakening the wet assert: wet-named manifests still require weather>0) + `data/native_wetnight_matrix_wet.json` (the SAME 4 cases with weather=2); (2) the 11th env port LANDED (`MYSIM_NIGHT_FIXTURE_VIEW` setdefault + the fixture-view run: the tool renders `source-night-fixtures-<track>` at the audit); (3) the py_compile gate ADDED to windows-build.yml (`.github/windows-build.yml`: `python -m py_compile tools/cloud_game_audit.py tools/cloud_windows_persist.py tools/cloud_windows_smoke.py tools/cloud_windows_userpath.py ...`).

**LAST-MILE CLOSED at `c677943` (verified verbatim this turn): the `case_set` choices now include `wetnight_matrix_dry` + `wetnight_matrix_wet` (yml line 28) and the `DRIVING_VALIDATION_MANIFEST` chain (yml line 208) carries both entries mapping to `data/native_wetnight_matrix_{dry,wet}.json` — exactly the two-line shape this agent proposed in turn 26 (the coordinator chose the pair-file-aligned names). The commands below carry the final case_set values, so THE MATRIX IS END-TO-END DISPATCHABLE: prerequisites + selectability + the env ports all verified.** The turn-25 mechanism paragraph stands: each cell (N0/N1/N2/W1/W2) = the branch-yml env-block edit + the push + this dispatch; each push's diff = ONLY the env lines (the confound check; record each cell's HEAD); D0 needs no env edit; D0 first, STOP on the identity-diff failure (the D0 checker landed at `91457d1`; the frame-hash hook at `c677943` = 2 lines, unencrypted hashes only, per track-render turn-25's adoption).

```bash
# PREREQ (data-only commit, BEFORE any matrix dispatch): the shared 4-case manifests
# (car 16 + car 0, tracks 0 + 8, DRY file weather=0 and WET file weather=2).
# LANDED at 4179e30: data/native_wetnight_matrix_dry.json (4 cases, no weather keys = dry default)
# + data/native_wetnight_matrix_wet.json (4 cases, weather=2). SELECTABILITY: PENDING the
# wetnight_dry/wetnight_wet case_set choices + the line-208 chain entries (the last-mile gap above).

# D0 identity guard (dry day, ALL candidate knobs off): establishes the pixel baseline (SHA-pinned run dir).
gh workflow run game-review.yml --repo instagramshashank8-bot/mysim-godot --ref codex/repair-20261029 -f depth=driving_validation -f cars=16 -f case_set=wetnight_matrix_dry

# N0 night base (patch the branch yml env block: TOD: '3' (WEATHER: '0' optional/implicit), push, then:)
gh workflow run game-review.yml --repo instagramshashank8-bot/mysim-godot --ref codex/repair-20261029 -f depth=driving_validation -f cars=16 -f case_set=wetnight_matrix_dry

# N1 night light package (branch yml env += NIGHT_POOL_GAIN: '1.6', NIGHT_AMBIENT_LIFT: '1.5', NIGHT_EXPOSURE: '1.15'):
gh workflow run game-review.yml --repo instagramshashank8-bot/mysim-godot --ref codex/repair-20261029 -f depth=driving_validation -f cars=16 -f case_set=wetnight_matrix_dry

# N2 night + vehicles + anchored (= N1's block + VEHICLE_LIGHTS: 'emissive', LIGHT_SCHEME: 'anchored'):
gh workflow run game-review.yml --repo instagramshashank8-bot/mysim-godot --ref codex/repair-20261029 -f depth=driving_validation -f cars=16 -f case_set=wetnight_matrix_dry
# N2's fixture-view frames: LANDED at 4179e30 (the MYSIM_NIGHT_FIXTURE_VIEW port + the tool's fixture-view runner) — covered by this same dispatch.

# W1 wet day, rain+reflect (branch yml env: TOD: '0', WEATHER: '2', RAIN_VISIBILITY_CANDIDATE: '1', RAIN_VOLUME_PROFILE: 'readable', WET_REFLECT: '1.6'):
gh workflow run game-review.yml --repo instagramshashank8-bot/mysim-godot --ref codex/repair-20261029 -f depth=driving_validation -f cars=16 -f case_set=wetnight_matrix_wet

# W2 wet night FULL package (= N2's env block + W1's wet knobs together) - the money cell:
gh workflow run game-review.yml --repo instagramshashank8-bot/mysim-godot --ref codex/repair-20261029 -f depth=driving_validation -f cars=16 -f case_set=wetnight_matrix_wet
# NOTE for W2: the pair's cases cover tracks 0+8 — W2 tests BOTH packages across BOTH tracks (no track-0 limitation since 4179e30).

# W0 (OPTIONAL, clean wet baseline, all candidate flags off - added if the budget allows):
gh workflow run game-review.yml --repo instagramshashank8-bot/mysim-godot --ref codex/repair-20261029 -f depth=driving_validation -f cars=16 -f case_set=wetnight_matrix_wet
# (branch yml env: TOD: '0', WEATHER: '2' only - all RAIN/NIGHT/LIGHT knobs off)
```

**BATCH-A (carbody's dispatch spec @ `c63d0d8` = `outputs/mistral-orchestration/tracks-index/BATCH-A-DISPATCH.md` + `batch_a_dispatch.json` — FOLDED into this matrix section this turn by the verification agent, with ONE CORRECTION):** the audit-plan's batch A (tracks 1,3,9,16,17,21,23,24,26,54,57 — route-size-only batching) gets its D0-day-dry baselines in ONE dispatch via `depth=weather_review`:

- **Path 1 (zero workflow changes — carbody's verified shape, the repository CORRECTED: carbody's spec wrote `--repo Jdiejei/mysim-godot`, but dispatches run with the OWNER's credential and Jdiejei's fork-side dispatch = 403 (server-side verified 2026-11-04); the correct target = the OWNER repo, where the Option-A push lands the same branch+yml):**

```bash
gh workflow run game-review.yml --repo instagramshashank8-bot/mysim-godot --ref codex/repair-20261029 -f depth=weather_review
# then: gh run watch <id>; gh run download <id> -n "encrypted-review-<shard>-attempt-1" (shards 0..5); decrypt per the established MYSIM_INTAKE_KEY flow (key never printed)
```

- Verified mechanism this turn: `weather_review` IS a `depth` choice; its env line `WEATHER_REVIEW: ${{ inputs.depth == 'weather_review' && '1' || '0' }}` (yml 276); **the shard matrix = the STRATEGY FILTER'S FINAL FALLBACK `[0,1,2,3,4,5]` (verified: `weather_review` appears in NO explicit branch of the yml's 4006-char shard ternary (line 76) → the fallback)**; the tool's WEATHER_REVIEW branch (line 672-675) labels the captures VERBATIM `weather-track-{track:02}-tod-{tod}-wet-{wet}.png` ✓; carbody's per-track shard mapping (21/57/9/3→3, 54/24→0, 26→2, 16→4, 17/23→5, 1→1) = their dispatch.json's advisory division — the ground truth comes from each run's logs (reconcile per run in the ledger).
- **Path 2 (OPTIONAL, the coordinator-owned yml): the two-line `weather_rows` input + the tool's WEATHER_ROWS loop** (carbody's proposal, verbatim in BATCH-A-DISPATCH.md) → a D0-ONLY quick item (`-f weather_rows=0_0`), 60 D0 frames; recommend adopting it if the owner wants the D0 verdicts before ANY full matrix row.
- The D0 target list (the 11 tracks, names verified by carbody against the per-track files; the shard column = advisory): 21 RED BULL RING, 54 LEMANS SARTHE, 57 REDBULL SOUTH, 17 NEW YORK, 9 HONG KONG, 26 SPA, 3 BATHURST, 23 SEBRING, 24 SILVERSTONE, 16 MONZA, 1 LAGUNA SECA — the expected file per track = `weather-track-<NN>-tod-0-wet-0.png`.
- **Ledger filing (the verification side = me):** on each run's artifacts, one row per track: `| track<N> D0 day/dry baseline captures (batch A, audit plan) | <run> | <dispatch head> | <decrypted weather-track-<NN>-tod-0-wet-0.png> | open |` — the reviewer's D0-first pass = these 11 BEFORE any night/wet row; every defect row needs its capture; nothing accepted from the dispatch itself.

Reading per pair (from track-render turn-23 §4, checkable): rain streaks >= 2 px at 10-40 m on >= 5 frames (base profile = sub-pixel at those ranges); the wet-road specular responds under light while the DRY cells show ZERO road delta; night readability (the body/near barriers/kerbs discernible, no hue inversion); `NIGHT_VEHICLE_LIGHTS ... poolBudgetUntouched=true` present for N2; the anchored scheme cross-checked via the audit JSON; W2 must not black-hole the road (>= N1 pool visibility). Fence regression watch: the fence wire stays visible (not black-crushed) in W2 (the turn-1 reject DOES NOT re-enter through a wet/night flag).

## FULL BATTERY (after the quick three, or post-merge on main) (API-verified pushed; the full integration line: corridor fix, axle candidate + A/B, six-field inventories, touch-strip/cockpit contract, lapValidity, forest-tone candidate, the bisect-verified 25-input workflow). All commands below run on the OWNER's machine with the OWNER's credentials; `--ref main` variants apply after the merge, or substitute `--ref codex/repair-20261029` to run them pre-merge against the branch.

No secrets in this file. All commands run on the OWNER's machine with the OWNER's credentials.
Current auth on the shared workstation is `Jdiejei` (verified by the reviewer: push to the owner repo = 403, MergePullRequest = denied, collaborator-query = 403 Must have push access). The OWNER performs the merge; nothing here requires sharing tokens or granting admin.

## State being merged (verified this session)

| Item | Value |
|---|---|
| Owner repo | `instagramshashank8-bot/mysim-godot` |
| PR #1 | `codex/repair-20261029` → `main`, state OPEN, MERGEABLE/CLEAN, 243 files |
| PR #1 head (owner repo copy) | `a313492e7458e5733c6933d550b7eee803b4b6f1` |
| Fork with NEWER commits | `https://github.com/Jdiejei/mysim-godot` branch `codex/repair-20261029` |
| Fork tip (UPDATED 2026-11-05 audit) | `c4fb876` — the integration head (verified by ls-remote + fetch; the 2026-11-04 text below said `1f4ba0c`, which is now 5 commits behind) |
| Commits ahead of `a313492` | `1f4ba0c` windows-persist single-source saveErrors count + smoke MYSIM scrub (still NO cloud run at ANY of these heads — UNVERIFIED, do not claim Windows fixed) · `0e0da0c` fence miprepair one-liners (stale a2c/blend block no longer captures the miprepair depth; per-level tolerance max(0.02, 2/sqrt(texels)) with safe fallback) · `9006a1a`/`65bbef6` owner handoff + chart · `c4fb876` shard-matrix fix (the chiron grouping bug had swallowed fleet..f1_wheel_diagnostic and ANDed it with [0]; fleet_evidence restores 6 shards; verified by expression parser across 10 depths) |
| What the branch delivers | The full rebuilt line: `windows-game-build.yml` (persist+userpath+smoke steps), `Main.cs` userpath hook, gate battery, hull/mount fixes, HUD/fence/night evidence tooling |

## Option A (recommended): update PR #1 to the fork tip, then merge once

```bash
# from the owner's clone of instagramshashank8-bot/mysim-godot
git checkout codex/repair-20261029
git pull https://github.com/Jdiejei/mysim-godot.git codex/repair-20261029
git push origin codex/repair-20261029        # PR #1 head moves a313492 -> the fork's CURRENT tip (c4fb876 at the 2026-11-05 audit; confirm with `git rev-parse FETCH_HEAD` right after the pull — this file's older text said 1f4ba0c, now 5 commits behind)
gh pr merge 1 --repo instagramshashank8-bot/mysim-godot --merge
```

## Option B (equivalent): merge PR #1 at a313492 first, then fast-forward main

```bash
gh pr merge 1 --repo instagramshashank8-bot/mysim-godot --merge   # merges a313492
git checkout main && git pull
git merge https://github.com/Jdiejei/mysim-godot.git codex/repair-20261029  # or cherry-pick 1f4ba0c
git push origin main
```

## Post-merge verification the OWNER can run (this is what makes 1f4ba0c "verified")

> **UPDATE 2026-11-04 late:** the workstation's dispatch capability changed server-side — `gh workflow run` now returns `HTTP 403: Must have admin rights to Repository` for Jdiejei (dispatches worked earlier the same day). The validation battery below therefore runs from the OWNER's side (their own credentials), either on the merged main or directly on the branch. Commands are ready as-is.

> **AUDIT 2026-11-05 (verification agent) — battery is SIX commands; three corrections applied:**
> (1) command 1's workflow name was `windows-game-build.yml` — that string matches NEITHER the workflow file (`windows-build.yml`) NOR its name (`windows-game-build`, no `.yml`): **this was the real cause of the Q4 "404 workflow not found"**, not merely the stale default branch;
> (2) command 1 was missing the REQUIRED `publication_tag` input (the workflow's first step hard-fails without it; create the tag at the post-merge main head first);
> (3) the sixth command added: the fleet_evidence re-run, unblocked by the shard-matrix fix at `c4fb876` (expression-parser-verified across 10 depths). Commands 1-5 are POST-MERGE-ONLY: main's October-era workflows lack the new depth/case_set choices and reject the inputs with HTTP 422 before the merge; command 0 runs on the branch AFTER the Option-A push (which carries `0e0da0c`).
> (4) **the collision_overlay INPUT removed — the 2026-11-05 workflow-parse bisect proved GitHub's hard limit: you may only define up to 25 inputs for a workflow_dispatch event** (verbatim API 422); the 26-input file fails to PARSE (the Q4 "workflow file issue" run's true cause), while every OTHER change parses (bisect: change 1 green via run 41907880155; change 2 green via 41908032898; the env-line-only form green via 41908499825; the 26-input form rejected twice — push-run 41908612608 + the dispatch 422). The working form (verified green as run 41908968766, cancelled after the verdict): NO new input; COLLISION_OVERLAY derives from depth=track_scene_audit — the overlay stays bound to the only depth that consumes it; the 10-depth shard mapping is untouched.

```bash
# 0) Fence re-run — pre-merge OK only AFTER the Option-A push (the branch must carry 0e0da0c's one-liners);
#    all flags verified against the c4fb876 workflow:
gh workflow run game-review.yml --repo instagramshashank8-bot/mysim-godot --ref codex/repair-20261029 -f depth=fence_miprepair_review
# EXPECT (artifacts): game-runtime-<shard>-attempt-1 + encrypted-review-<shard>-attempt-1 (6 shards — 41830995246's
#        shard 0/4 failures were the GENERIC track block, not these invariants; this is the first run that can
#        exercise the miprepair invariants end-to-end at the fixed tolerance).

# 1) Windows build + persistence + userpath + smoke at the merged HEAD — POST-MERGE ONLY; CORRECTED name + tag:
gh workflow run windows-build.yml --repo instagramshashank8-bot/mysim-godot --ref main -f publication_tag=windows-candidate-<40-hex-of-post-merge-main-head>
#   (create the tag first, pointing at the post-merge main head: git tag windows-candidate-<sha> <sha> && git push origin windows-candidate-<sha>)
# EXPECT (artifacts): windows-build-diagnostics-<run>-attempt-1 → windows-settings-persist-summary.json (all five
#        scenarios ok; failures scenario tmp=1 / rename=2 — the UNCHANGED expectations; the old double-count showed
#        tmp=2/rename=4), the three win_sharing_violation cases present; userpath-summary.json (9 profiles x 2 passes
#        incl. the determinism re-injection check) green; windows-smoke-results.json + windows-render-results.json green.

# 2) Driving validation (collider restore + mounted bake + userpath hook) at the merged head — POST-MERGE ONLY
#    (main's old workflow lacks the driving_validation depth choice → HTTP 422 before the merge):
gh workflow run game-review.yml --repo instagramshashank8-bot/mysim-godot --ref main -f depth=driving_validation -f cars=16
# EXPECT: car DRIVES (distance > 0, no beach) — the prior DV runs failed no-travel/beach; DV_TRACE telemetry in the
#        report; encrypted-report-*/runtime artifacts as for the depth.

# 3) Force-driven wall/contact battery (physics c059e2c spec; jolt first, source pair after) — POST-MERGE ONLY
#    (case_set driving_wall verified present in the choices + the DRIVING_VALIDATION_IMPACT wiring; manifest
#    data/native_driving_validation_wall.json verified present in the tree):
gh workflow run game-review.yml --repo instagramshashank8-bot/mysim-godot --ref main -f depth=driving_validation -f case_set=driving_wall -f cars=16 -f physics_backend=jolt -f physics_hz=240 -f native_damage=true -f rotational_tyres=true
# EXPECT: encrypted-review-* + the DV report covering the wall cases (contact, impulse identity, wall hits, recovery;
#        no bound relaxation); the source-backend pair runs after this one with the same case_set.

# 4) Collision-overlay evidence (track-render 40c7df5; overlay + per-collider audit JSON) — POST-MERGE ONLY.
#    CORRECTED by the 2026-11-05 bisect: the earlier form (-f collision_overlay=true) is INVALID — that input was the
#    26th workflow_dispatch input and GitHub rejects the file outright (verbatim: HTTP 422 failed to parse workflow:
#    you may only define up to 25 inputs for a workflow_dispatch event). The overlay is DEPTH-DRIVEN now:
#    every track_scene_audit dispatch renders it (COLLISION_OVERLAY derives from the depth).
gh workflow run game-review.yml --repo instagramshashank8-bot/mysim-godot --ref main -f depth=track_scene_audit
# EXPECT: encrypted-review-* carrying the overlay renders + the per-collider audit JSON.

# 5) NEW (2026-11-05 audit): fleet_evidence re-run — unblocked by the shard-matrix fix at c4fb876 (the chiron grouping
#    bug had swallowed fleet..f1_wheel_diagnostic and ANDed the shard set with [0]; the fix restores 6 shards;
#    expression-parser-verified across 10 depths) — POST-MERGE ONLY (not in main's old depth choices):
gh workflow run game-review.yml --repo instagramshashank8-bot/mysim-godot --ref main -f depth=fleet_evidence -f cars=16
# EXPECT: SIX shards in the run's matrix (the fix's visible effect); per-shard encrypted-review-* + the fleet manifest
#        coverage from the authoritative 24-car/60-track manifest (74017c6); per-car evidence dirs.

# 6) NEW (2026-11-05): axle-candidate A/B (car-assembly 063620d) — POST-MERGE ONLY. One dispatch yields the raw AND
#    fitted sweep tables side by side (the candidate rides the EXISTING source_axle_fit input — no new input; the
#    25-input limit stands). Front bodywork must move -0.0455 -> ~+0.20/+0.25; front suspension >= -0.002 (expected
#    +0.01..+0.09 post-fit); wheelguard numbers collapse to arch-normal (the restore's signature).
gh workflow run game-review.yml --repo instagramshashank8-bot/mysim-godot --ref main -f depth=wheel_fit_review -f cars=16 -f source_axle_fit=true
# EXPECT: encrypted-review-* wheel-fit sweep with per-shift-state rows (axleShift 0 = raw, fitted = candidate).
```

Artifacts are encrypted (`encrypted-review-*`); the owner's local key `C:/Users/shash/.codex/private/mysim-review/review-20261031.key` decrypts them via `tools/cloud_decrypt.py` (pattern: `outputs/mistral-orchestration/decrypt-reports.ps1`).

## Explicitly NOT claimed

- `1f4ba0c` is NOT verified until a cloud run at that SHA (or the merged main) passes the Windows persistence suite.
- Physics is NOT accepted: the wheelguard/caliper collision coverage restoration + force-driven contact evidence are still open (physics-gameplay is working them).
- The track-8 legality failure (AI line vs corridor at progress ≈0.195) is diagnosed but NOT fixed; track-render's correction proposal is pending.
- DV 41871263005 failed with cause UNDETERMINED (log retention failed; artifact downloads stalled) — its artifact remains on GitHub for later retrieval.
