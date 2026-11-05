# DELIVERY_PLAN — MySim rebuild

Updated: 2026-11-04 ~17:55 Asia/Dubai (10:55Z window; timestamps re-stamped every batch from actual run/git data).
Target: coherent, tested, PLAYABLE improvement by 2026-11-05 evening (Asia/Dubai). No full-game completion claim will be made; remaining scope stays explicitly listed.


# BLOCK: release-defining facts (replaces the earlier single-fact header; re-stamped 2026-11-04 18:20 Asia/Dubai / 14:20Z)

1. The published Windows build (41828819144/e82a362) boots the LEGACY drive path: MYSIM_NATIVE_GAMEPLAY is not set, so the user played the legacy physics while every green native result validated the opt-in path. The default flips to native ONLY through the gate battery below.
2. BLOCK A CORRECTION (reviewer-verified): the 0de3123 WHEELGUARD/CALIPER hull ablation did NOT fix the drive - the re-run at that commit (41854714029) still failed: travelled 0.2004 m in 30 s, bodyContacts=2/tyreContacts=2. Block A status = FAILED (not in-flight green). The exclusion also removed the wheel-band lateral collision protection - it is EXPERIMENTAL/UNACCEPTED standing alone: the legitimate wheel-band coverage must be RESTORED at the CORRECTED geometry (the mounts/derivation fix), or the ablation reverts. No preload-bound increase.
3. The active investigations: the physics stance-datum chain (the guards measured lowest point -0.81 vs the tyre plane -0.61; the real chassis floor -0.60) and the car-assembly source-part audit (the mount-vs-source-geometry split). The next DV (41860220647 at 4e11bba) carries the full failure-report patch (telemetry + contact names on any failure).

## 1) Live agents (GLM subagents of this coordinator; names = agent IDs; states read via agents.list this batch)

The published Windows build (run 41828819144 / e82a362) boots `Main` WITHOUT the native force-driven mode: the default user path is the legacy DriveController physics, while every green native lap/steering/physics result validated `MYSIM_NATIVE_GAMEPLAY=1` (jolt/240 class) paths. USER_STEERING_BLOCKER records the resulting user experience failure (no real steering/look feel) and the release rule: the native mode becomes the SHIPPED DEFAULT only through the required gate battery (contact/impact/grid/wet/fleet + human-path input evidence), never by silent flag flip. Current integration HEAD (lineage through 0de3123/6263c51/a613a9a/ccca290/6263c51... read `git log`) carries the fixes that make the native path viable by default (the WHEELGUARD/CALIPER hull exclusion at 0de3123 — see risk note below).

## 1) Live agents (GLM subagents of this coordinator; names = agent IDs; states read via agents.list this batch)

| Agent (ID) | State | Latest work (commit) | Current task (turn) | Owns | Next artifact |
|---|---|---|---|---|---|
| physics-gameplay | running (turn-8 assigned) | 6f174bc (turn-7: stance datum analysis; guard-fix proposal) | input-chain trace + steering feel + the DV un-block evaluation | src/Logic/*, data/native_*.json | agents/physics-gameplay-turn8.md + steering patches |
| track-render | running (turn-8 assigned) | 9e123c0 (night candidates merged 125811d) | invisible-walls P0: collision overlay + contact audit + WallBuilder review | src/TrackNightLighting.cs, src/TrackMaterialBinder.cs, src/TrackCutoutDiagnostic.cs, src/WallBuilder.cs, TrackNightLighting | agents/track-render-turn8.md + overlay patch |
| car-assembly | running (turn-8 assigned) | ab28e50/8092aea (turn-6 protocol/verdict) | floating root cause: per-car contact planes vs road + the mount-corrected guard fix (must RETAIN collision) + the 41843317303 fit-leg verdict | src/CarLoader.cs, src/CarAssemblyAudit.cs, WheelFitAudit, f1_wheel_profiles, component_mount_profiles | agents/car-assembly-turn8.md + mount fix |
| hud-camera | running (turn-8 assigned) | 6ffdf63 (turn-5 fixture fixes; merged c057ce0) | sense-of-speed: unit/scale verification, camera, wheel-rotation/distance, real-state audio; the running product gate 41852901703 watches | src/Hud.cs, src/RaceStandingsPanel.cs, ProductControlsScenario, TouchControls, DriveController(UI) | agents/hud-camera-turn8.md + audio/camera patches |
| carbody-livery | idle → reimperative tasking this batch | 74017c6 (fleet manifest merged 6505f16-era) | source-quality repairs continue: the fleet inventory rows completion + per-car UV/livery repair candidates (NOT blocking P0) | data/car_surface_uv_repairs.json (coordinator merges), the inventory tool | agents/carbody-livery-turn4.md + repair candidates |
| verification | running (turn-8 assigned) | 1a204b2 (EVIDENCE_INDEX current) | the integrated default-Windows verification + the user-path recording spec + index upkeep | EVIDENCE_INDEX.md, REVIEW_PACKET (shares) | agents/verification-turn8.md |

## 2) Critical path to the deliverable (ordered; each block = 60-90 engineering minutes)

1. **BLOCK A (in flight): the native-drive un-block proof.** Runs: 41854714029 (DV at 0de3123: the WHEELGUARD/CALIPER hull exclusion; the rider... no ride flag = the sane default) + the RIDE-A/B pair queued by the watcher. EXIT CRITERIA: the DV core completes with the car DRIVING (the thrust path live) + the hull's ground contact at the sane +10 mm-level stance + no NEW contact class. If green: the native default flip becomes gateable. NEW: the exclusion collision risk (below) must be closed in the same window.
2. **BLOCK B (in flight): the collision/contact verification.** The axle-fit leg (41854428291 at 6263c51: the gate fix landed) vs the no-fit baselines (41837315358/41849732286): the -45 mm class bodywork penetrations must move positive with the fit ON (the car-assembly verdict protocol). The 0de3123 ablation NOTE: the GUARD/CALIPER hull removal = lateral-wall protection sacrifice — REQUIRED compensation per the review: re-add the wheel-band collision at the CORRECTED positions (the guard mount fix = car-assembly's next patch; the ablation CURRENT state stays flagged EXPERIMENTAL/UNACCEPTED until the guarded fix lands).
3. **BLOCK C (in flight): the mirror-sponsor A/B (P1).** Leg A in flight (41847896437 at the adf89ca scratch ref), leg B queued; the A/B + the human review = the mirror determination; then the promotion or the rejection at the trio.
4. **BLOCK D (in flight): the night lighting candidates.** 41849395054 (the 5-leg × dry/wet × track0/25 grid, root-caused from the night-burn defect map). Human review → the flag adoption decision.
5. **BLOCK E (in flight): the fleet renders.** 41848086075 (all 24 cars, the same-HEAD batches) → the fleet triage + the manifest refresh; the fleet wheel sweeps batch-by-batch (0-3 next via 41849732286/41854428291, then 4-7, 8-11, 12-15, 16-19, 20-23).
6. **BLOCK F (the merge target): the integrated Windows candidate.** Requirements (all machine gates + human-relevant review): the compiled merged HEAD; the DV core COMPLETE (the input → car moves; the 9-target contract); the wheel sweep clearances ≥ the diagnostic tolerances with the mount-corrected guards (collision intact); the night/basic frames judged by the human reviewer; the user-path input harness (NEW: the keyboard input driven by the actual input injection, not AI lines) recorded on the packaged player; the P1 mirror verdict executed. THEN: the release default = the native mode IF (and only if) the gates pass; the Windows publish flow re-runs with the tag candidate; the user plays it.

## 3) P0/P1 board

- **P0** (blocks the delivery): the invisible walls (track-render turn-8 in progress); the car contact/float/ride geometry (car-assembly/physics turn-8 in progress; the exclusion-risk fix pending); the user-path steering feel (physics turn-8); the shipped-default integration route (this plan's Block A/B/F); the guard-collision restoration (car-assembly).
- **P1** (this delivery should carry): the mirror logos (the P1 pair in flight); the忘 night visibility (the candidates in flight); the sense-of-speed package (hud turn-8); the fleet triage (the fleet renders in flight); the speckled fences/stands (the gate-fix run 41843204651 in flight = the FIRST properly-gated fence evidence; the judge pack after); the graphics un-finished pass (the material/UV batch).
- **Backlog (the 15-stage scope)**: all cars/tracks polish, weather/wet/night depth, racing AI/rules, pits+crew, menus/settings depth, championship/saves, LAN, audio full pass, performance budgets, exhaustive asset acceptance, the final Windows verification. Tracked in FLEET_MANIFEST + EVIDENCE_INDEX.

## 4) Completion gates (objective; no CI-green = done claims)

1. The packaged player with the shipped default path = the mode the human plays; the launch metadata + the config recorded.
2. The continuous human-path recording (keyboard, real inputs) reviewed by the human reviewer: steer/brake/accel/reverse/countersteer SAFE; no drive-through walls; no floating.
3. The DV core + the extended battery green at the release default path … plus the P1 mirror verdict + the night verdict + the fleet triage rows + the collision-overlay survey.
4. The top-down review: every defect in VISUAL_REVIEW has a row: fixed (evidence) / accepted-as-character (justified) / open (planned).

## 5) Session 6 addendum (coordinator, 2026-11-04 ~19:10Z) — corrected state and the exact open items

- **PR #1 OPEN at `a313492`** (verified `"state":"OPEN","mergeable":"MERGEABLE"`); fork tip `1f4ba0c` (the single-source saveErrors count + the smoke MYSIM_* scrub) has **no cloud run — the Windows fix is NOT verified**. No further blocked dispatches; the merge or an equivalent write grant is the reviewer's/owner's action.
- **Geometry recovery 41874907855 FAILED** at `tools/cloud_game_audit.py:177` (rows count/ok gate; shard 8; NATIVE_CASE_SET=shutdown; collapsed 2m10s in, before any captures). NO retry until its artifacts are fetched and a distinct documented fix exists.
- **Adopted runs:** DV 41871263005 still in_progress (~1h; outcome to be reported exactly when terminal); car_evidence 41874911549 COMPLETED SUCCESS (prepare 14s + render(0) 33m53s).
- **Physics: UNACCEPTED** pending the reviewer's NativeCarColliders.cs preserve-order inspection + force-driven contact evidence; Blocks A/B wording above stays authoritative.
- **Environment:** local shell tools resolve to the WSL stub (no distro); working path = desktop MCP with the explicit shell `C:\\Program Files\\Git\\bin\\bash.exe`.

## 6) Reviewer-assigned specialist tasks (2026-11-04 ~23:30Z; jobs dispatched by specialists, IDs recorded)

| Agent | Assigned task | Job IDs (adopted) | Status |
|---|---|---|---|
| track-render | Hockenheim progress~0.195 corridor trace -> evidence-based AI-line or corridor correction (patch proposal to coordinator) | 41874907855 / 41880754125 / 41880755936 (adopted read-only) | analysis in progress |
| physics-gameplay | NativeCarColliders preserve-order review -> restore justified wheelguard/caliper coverage -> plan force-driven wall/contact validation | (none; source work first) | pending start |
| verification | PR owner/write blocker documentation; Windows assertions stay open (tmp=1/rename=2) | (none) | assigned |

**Unadopted/flagged runs:** 41880754125 (FAILED — shard-8 legality assert, 2nd identical failure, NOT coordinator-dispatched) and 41880755936 (in flight, 3rd identical attempt — flagging here; not counted as evidence). Both dispatched 1s apart at a313492 — duplicate dispatch, not an A/B. Retry budget for the shard-8 path: EXHAUSTED (2 failures, no intervening fix). The distinct-fix requirement stands.

**Reminder (reviewer-confirmed):** 1f4ba0c is NOT on PR #1's head; no Windows verification claims. PR #1 merge/write on instagramshashank8-bot/mysim-godot remains blocked by repo permissions (Jdiejei).

## 7) Owner handoff (2026-11-04 ~23:45Z)

User authorizes repo access and will merge/test from their side. Exact copy-pasteable instructions written to **`outputs/mistral-orchestration/OWNER_MERGE_INSTRUCTIONS.md`**: Option A = update PR #1 to the fork tip (`git pull` from the Jdiejei fork + push, then `gh pr merge 1`), Option B = merge at a313492 then fast-forward; post-merge verification commands (Windows persistence reload expecting tmp=1/rename=2; DV re-run at merged HEAD). No tokens shared; no auth changes; 1f4ba0c stays UNVERIFIED until the post-merge run passes. Track-8 corridor + collider-preservation work continues on the fork branches independently of the merge.

## 6) Geometry-recovery root cause (2026-11-04 ~22:25Z, from decrypted artifacts — EVIDENCE_INDEX session-5 addendum 2)

Track 8 (Hockenheim) case `{car:13}`: the mounted native lap is HEALTHY (finished, 4646m/4331m, 0 impacts, hulls mounted, clearance 30.9mm) but the **AI line exceeds the legal corridor at progress ~0.195** (lateral 6.00–6.02 vs half-width 6.0, twice, same spot) → legality gate → FAIL. Fix path: corridor-vs-visible-track source review (same family as the invisible-walls P0), then either an AI-line correction for that corner or an evidence-backed corridor correction. No retry on unchanged inputs; 41880755936 (third identical attempt, in flight) is not evidence.
