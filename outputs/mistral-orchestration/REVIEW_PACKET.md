# REVIEW_PACKET — MySim verification and evidence (coordinator-maintained; specialists contribute sections)

Maintained on branch `codex/repair-20261029`. The **CURRENT STATUS** sections below are the authoritative snapshot; superseded material is preserved under dated **HISTORY** headings. No whole-game claim and no "persistence stage closed" claim is made anywhere in this packet.

## CURRENT STATUS — 2026-11-04, session 5 (verification agent; the live run-state record is now EVIDENCE_INDEX.md)

- **wheel_fit clearance sweep v2 VERIFIED: run `41837315358` at `e17fdf5` = success** (the v2 sweep at the 1500 s timeout; per-car `41837315358` wfr dirs; `sweep.perWheel` worsts + `WHEEL_CLEARANCE_WORST` lines; per-wheel sweep evidence asserted by the audit). The failed first attempt `41832997990` died mid-sweep at the 420 s default AFTER emitting real worsts incl. **-45 mm bodywork penetrations at full lock** (content per `e17fdf5`). Machine-verified; the negative-penetration worsts await human review — no clearance acceptance.
- **miprepair `41830995246` FAILED — but NOT on the fence invariants:** shards 0/4 failed, 1/2/3/5 succeeded; shard 4's exception is `RuntimeError: track-28 failed; see saved logs` in the audit's GENERIC track block (cloud_game_audit.py:710), not the miprepair code; the evidence shipped via `if: always()` (`game-runtime-0..5` + `encrypted-review-*` artifacts listed) and awaits extraction/viewing. Cause of the track-0/28 failures TBD.
- **DV state:** `41834771421` FAILED — boot FIXED (race ready, hulls) but no travel (10 m gate); shares the symptom with `41828824418`; `DV_TRACE` telemetry added at `95f69c6`; `41826593001` cancelled. The next DV run must carry DV_TRACE.
- **Product controls:** `41830853569` root-caused (BEST-lap assertion precision; fix `5305458`); re-run `41835648940` at `f2eba7e` FAILED on the standings header-row bug; root cause + by-name fix landed at `6ffdf63` after that run.
- **P1/RIDE A/B queue armed (watcher live, `5f2b9ee`), no dispatches at this recording.** Leg A↔B confound check already VERIFIED CLEAN from the committed trees: the pair differs ONLY by the three badges candidate entries (semantic diff of `data/car_surface_uv_repairs.json`: 22 vs 25 entries, only-in-B = the 3 badges, zero payload changes on shared entries). RIDE pair risks: moving-ref drift (both legs on `codex/repair-20261029` — compare headSha at recording) and the watcher's `gh run list --limit 1` id-capture race — every recorded id will be verified against its expected ref/depth. Live tracking table: `EVIDENCE_INDEX.md` §P1/RIDE.

## CURRENT STATUS — 2026-11-04, session 4 (verification agent; Windows persistence VERIFIED — supersedes the session-3 lines where they differ)

- **WINDOWS PERSISTENCE VERIFIED: run `41828819144` at `e82a362` (`e82a3620702d13155cdee6d3d5cefb80411216e7`) = SUCCESS** (build + smoke + persist, all five scenarios). Verified from the actual artifact (`windows-build-diagnostics-41828819144-attempt-1`, downloaded and read this session): per-scenario passed/failed = **legacy 9/0, foreign 5/0, corrupt 6/0, malformed 5/0, failures 12/0 — total 37** (29 base + 3 win + 5 contract_sync), exit 0, zero missingCases, zero unexpectedErrors; **`platform:"windows"` on all five reports**; the three cases `win_sharing_violation_rejected`/`win_sharing_violation_tmp_kept`/`win_sharing_violation_live_kept` present and PASSED; the `failures` logs show exactly the expected error fingerprint (1 `SETTINGS_SAVE tmp=FileCantOpen` + 2 `SETTINGS_SAVE rename=Failed`, real `%APPDATA%` paths on the runner). Scope: Windows Server runner + packaged player, real I/O. This closes the "Windows open" item for RENAME/ATOMIC-REPLACE semantics as tested — it does NOT close stage-10 and does NOT cover the item below. Note: dc8e061's commit-message totals ("8/5/6/5/9=33") do not match the artifact (the difference is exactly the legacy contract-sync entry + the three win cases); the artifact is authoritative.
- **STILL OPEN (explicit): crash-consistency through the delete-then-rename window.** No crash-injection test exists. Godot's Windows rename deletes the live file before `MoveFileW` (4.7.2 source, verified), so a crash/power-loss inside that window leaves NO live settings file (the tmp recovery copy survives). The verified assertions (`temp_live_kept`, `win_sharing_violation_live_kept`) do NOT cover this window. Closing it needs a crash-injection harness or an OS-atomic `File.Replace`-based path in `Settings.cs`.
- **Run-status updates (verified via `gh run view --json` this session):** DV `41828824418` FAILED (grid-hold: session never started; fixed `39cdf33`); product_controls `41830853569` FAILED (runtime; cause under diagnosis); in flight at this refresh: `41834771421` (DV boot fix), `41832997990` (wheel_fit sweep v2), `41830995246` (miprepair at the fixed schema), plus — corrections to the in-flight set: `41831318759` (car_evidence sweep v2) and `41830989890` (reference evidence) have ALREADY terminally FAILED, they are not in flight. Full claim→evidence state: `EVIDENCE_INDEX.md`.
- **Texture-inventory QC (inventories landed at `dc8e061`):** NONE of the three (car16 15 rows, car6 17, track0 72) satisfies the six-field contract — track0 has NO material/surface linkage at all (0/72 rows with bindings; no GUID/UV/orientation/taxonomy/correction fields), the car inventories lack UV-channel and defect-taxonomy fields entirely and GUIDs on unbound rows. Details and per-field verdicts in `EVIDENCE_INDEX.md` §TEXTURE-INVENTORY QC. The inventories' existing data is accurate as far as it goes; rows must not be cited as acceptance evidence until the gaps close.

## CURRENT STATUS — 2026-11-04, session 4 (six-specialist programme; fleet renders + ride A/B live)

### Programme state (supersedes session 3.5 where it differs)

- **Windows persistence: PASSED end-to-end at `e82a362` (run 41828819144)**: 9/5/6/5/12 = 37 assertions (29 base with the long-roundtrip + 3 win_sharing_violation + 5 contract-sync), `platform:windows` on every report, real `%APPDATA%` paths. The two "atomic-replace" cases + the sharing-lock cases prove those paths hold in the packaged player; crash-consistency through Godot's Windows delete-then-rename window remains an open design risk (no crash-injection test; tmp-recovery copy is the mitigation).
- **Fleet renders (programme item 1)**: new `fleet_evidence` depth at `b5ca3e0` = all 24 cars in labelled same-HEAD batches (5 views + 4 wheel closeups + cockpit per car, split over 6 shards by car index) + track0 day/night pairs; survey semantics (collect-then-fail with a machine manifest of defects). Run 41848086075 in flight.
- **Driving validation root cause (trace-verified, DV_TRACE 41840755243)**: race started, grid released, validation throttle=1.00 from t≈2s — the car never moves the whole session (speed 0.00; hull rides the floor: bodyContacts=2/tyreContacts=1). Command path + sequencing cleared; the frozen stage = the thrust path with the hull beached on its floor at the source/120 solver. Physics turn-6 adds the REAL spawn-datum analysis for the track-28/30 stalls: track 30's road mesh sits +5.85 m ABOVE the legacy ground-seed height → the seed's downward ray can never find it (track 0's road is 7.8 m below and works); the native spawner's y=200 rays are datum-safe by construction. Fixes split: the legacy DriveController spawn datum (shared files, coordinator merges) and the ride-height candidate.
- **Ride-height A/B (Q1(a) authorised, 35 mm labelled EXPERIMENTAL)**: the direct un-block experiment dispatched = 41848766195 (DV core with `native_ride_height=true`) — if it moves, the ride candidate is proven as the DV unblock with the handling re-baseline plan (physics turn-6 rubric: Group 1 fix-proof lines + Group 2 DV/core on the unchanged target bands + the re-run list) before ANY default flip.
- **Night lighting (programme item 3)**: track-render turn-6 candidates merged at `125811d` — `MYSIM_NIGHT_POOL_GAIN` / `MYSIM_NIGHT_AMBIENT_LIFT` / `MYSIM_NIGHT_EXPOSURE`, each flag-gated with bit-identical defaults. New `night_evidence` depth (e76a1e1-ish lineage): same-camera paired frames across {off,pool,ambient,exposure,all} × {night-dry, night-wet} × {track 0, track 25}; run 41849395054 in flight.
- **Mirrored-sponsor P1 A/B (Q3(a) authorised)**: leg A dispatch = 41847896437 at the scratch ref `mistral/p1-badge-baseline` (adf89ca, ONLY the three `_mm_badges` candidate entries removed, verified pre-flight); leg B (39cdf33, candidates active) queues behind it via the serial watcher.
- **Wheel clearance v2 fleet sweeps**: the no-fit baseline (41837315358) = ALL-FOUR-cars FAIL: wheel-vs-bodywork −45.0…−45.5 mm at/near rest (FRONTBUMPER/CHASSIS misc), rear-vs-suspension −20.3/−11.9/−5.2 mm (car 15 = WATCH −2.1); the axle-fit leg (rung 1 of the repair ladder) dispatched = 41843317303 (`source_axle_fit=true`); the fleet batch 1 (cars 0-3 + fit) at 6505f16 queued (41849732286). Q2's bands apply as diagnostics (PASS<2 mm = within tolerance, NOT a physical clearance certificate).
- **Night evidence + fleet + P1 + ride runs all in flight**: 41849395054 (night), 41848086075 (fleet), 41847896437 (P1-A), 41848766195 (DV+ride), 41849732286 (fleet wheel sweep 0-3).

### Earlier phases (dated; superseded where session 4 contradicts)

## CURRENT STATUS — 2026-11-04, session 3.5 (DV root cause + wheel-clearance verdict + fence gate fix)

- **Driving-validation root cause CHAIN (DV_TRACE, run 41840755243 at 95f69c6):** race STARTED, grid RELEASED, validation input ENGAGED (throttle 1.00 from t≈2 s) — and the car still cannot move: speed 0.00 with `bodyContacts=2, tyreContacts=1` for the whole session on source/120. Verdict per the physics specialist's decision table: **thrust path dead = the spawn beaches the hull on its floor** (the measured floor-scrape: chassis hull touching the road in ~99.6% of the clean-lap samples). Same mechanism suspected for the track-28/30 post-spawn stalls (progress 0.988 = spawn, no motion until the kill). The authorised fix path = the ride-height A/B (Q1(a)): RIDE-A baseline / RIDE-B `native_ride_height=1`, both at the clean-lap config (jolt/240/rotational), queued in the serial watcher behind the P1 pair. DV completion is blocked on the ride adoption decision — this is the recorded dependency, not a separate bug hunt. The physics specialist's launch-failure reporting patch (4129dc4, merged d271b64) now leaves full per-tick evidence on any recurrence.
- **Wheel-clearance sweep v2 FIRST SUCCESS (run 41837315358 at e17fdf5, cars 15-18):** systematic FAIL on all four cars — wheels sit ~0.45 m forward of the source hub anchors at the raw pivot positions; wheel-vs-bodywork −45.0…−45.5 mm at/near rest (FRONTBUMPER/CHASSIS misc), rear-vs-suspension −20.3/−11.9/−5.2 mm; front suspension +48…+132 mm PASS-in-tolerance; wheelguard shells +1.2…+1.6 m off (separate defect, on record). Verdict semantics per Q2: diagnostic bands only (PASS<2 mm = within measurement tolerance, NOT a physical clearance certificate; FAIL>5 mm; rest-state FAIL>2 mm). Repair ladder rung 1 = the axle-line configuration fix; the WITH-FIT leg dispatched as 41843317303 (a996a9d, `source_axle_fit=true`) against the no-fit baseline 41837315358. Sweep instrumentation hardened (671842a merged fa2459a): mode offsets clamped to the reachable suspension envelope.
- **Fence mip-review ROOT CAUSE (track-render turn-5):** the depth's fence branch was gated on `NIGHT_CUTOUT_REVIEW` which did NOT include `fence_miprepair_review` — ALL THREE prior fence dispatches (41795630650, 41818948189, 41830995246) fell through to the generic suite and never rendered fence evidence (the flat-key invariant fix at 49ece2c was correct but never executed). Fixed at `a996a9d`: gate includes the depth, shard pinned to [0], and a wrong-mode tripwire (exit 2) now refuses the fall-through. First properly-gated fence run = 41843204651 (in flight).
- **In flight at this update:** 41843204651 (fence, properly gated), 41843317303 (wheel_fit + axle fit), 41841959304 (product, third attempt at c057ce0), 41840755243 (DV trace — completed: verdict above). The serial watcher queues P1-A (adf89ca), P1-B (39cdf33), RIDE-A, RIDE-B as pressure allows.

## CURRENT STATUS — 2026-11-04, session 3 (verification findings merged; count correction)

- **Count correction (reviewer-verified, direct totals):** run `41798040383` (Linux persist re-proof at `e8b80bb`) passed **8/5/6/5/9 = 33 assertions** (legacy/foreign/corrupt/malformed/failures; the 5 contract_sync checks included). Later runs change the totals: `9ff443c`+ adds `legacy_long_roundtrip` (legacy becomes 9 → 34 total; proven by `41818941902`), Windows `failures` adds the 3 win cases (37-window base; per-scenario 9/5/6/5/12 at `e82a362`+ plus contract sync per scenario).
- **Windows run `41819498045` (at `d3629e8`) FAILED in the persist step itself** (build + native smoke PASSED): per-scenario reports showed `legacy/foreign/corrupt/malformed` each failing exactly `contract_sync_case_names` (the fixture's `PlatformExtra()` added the three win cases to EVERY scenario's expected set on Windows — fixed at `e82a362` by scoping it to the `failures` scenario) while `failures` itself ran **12/12 green including all three `win_sharing_violation_*` cases plus the normal-replace and sharing-lock cases — demonstrating exactly those tested cases on the packaged Windows player, NOT crash-consistency: Godot's Windows rename is delete-then-rename, the crash window between the delete and the rename remains open (no crash-injection evidence exists), old-file preservation on abrupt termination is still an open design risk and must stay explicit until a suitable fault test resolves it**. The run's `ok:false` came from the driver's blanket `'ERROR:' not in text` check false-positiving the EXPECTED `SETTINGS_SAVE` PushError lines (fixed: the driver now requires the precise per-stage counts — `failures`: exactly 1 tmp-stage + 2 rename-stage SETTINGS_SAVE errors; other scenarios: zero; anything else INCLUDING any other SETTINGS_SAVE line fails the run). The decisive Windows run (platform field in the report + win-case requirement in the driver, both at `2862b04`+) is dispatched upon these fixes.
- **EVIDENCE_INDEX.md** (new, from the verification specialist) is the claim→(run, commit, artifact, status) index for reviewers, with the controlled status vocabulary and rejected/failed rows preserved.

## CURRENT STATUS — 2026-11-04, session 2 (coordinator; supersedes the same-day snapshot below where they differ)

### Settings persistence: Linux re-proved; long-typed regression found and fixed; Windows dispatch in flight

- **Linux re-proof DONE:** run `41798040383` at `e8b80bb` = success — the strengthened non-vacuous `temp_live_kept`/`rename_live_kept` reads + `contract_sync_case_names` are re-proven at a later commit (28/28, five isolated scenarios). The "26 real + 2 vacuous" note below applies only to `dfe0ed3` (run `41710482854`).
- **New regression found 2026-11-04 by the cloud controls probe (run `41795630650` at `5288329`)**: `Settings.TypedValue<T>` had no `long` branch, so `Settings.Get(key,(long)def)` returned the default unconditionally — every key rebinding was silently ignored in play (probe: "new throttle key not stored"). Root cause of the regression is the strict-typed-Get change at `c1701d9`; `41795630650` was the first CI probe battery after it. **Fix at `9ff443c`**: `long`↔Int natively matched; new fixture case `legacy_long_roundtrip` (roundtrips 75 through the production Set/Get exactly as ControlBindings does) added to the fixture contract AND `cloud_game_audit.py required_cases` AND the Windows driver contract. Linux now expects **29** base cases; Windows **32** (29 + 3 win_sharing_violation). The earlier strict-typed failures at `c1701d9` were a DIFFERENT defect (over-strict on wrong types) — both directions of the policy are now covered by the roundtrip case.
- **Windows persistence: the verification agent's step was NOT actually in the committed workflow** (the handoff claim "windows step wired at e8b80bb" was wrong for the committed file — caught 2026-11-04 while preparing the dispatch). Coordinator added the real step at `d3629e8`: `windows-build.yml` now runs `tools/cloud_windows_persist.py` (committed) on the packaged player after smoke/render, with `build-cloud/windows-settings-persist-*.{json,log}` added to the diagnostics artifact. First Windows persist verification = run `41819498045` (tag `windows-candidate-d3629e8430fcddcd5544153221e07467f7499120`; dispatch details in JOB_REGISTRY.json, including two discarded misconfigured windows dispatches and GitHub-side 500s on pushes — all preserved). Fresh `%APPDATA%\...\MySim` per scenario; expects 32 assertions; any `atomic_replace_*` failure = the documented delete-then-rename Windows defect, to be fixed in `Settings.cs`, never suppressed.
- **Linux 29-case proof DONE (supersedes the queued status below):** dispatch `41818941902` at `9ff443c` completed SUCCESS — `legacy_long_roundtrip` green in the packaged Linux fixture flow with contract sync; the 29-case Linux basis (28 original + long roundtrip) is now proven, not pending. `41798040383` (at `e8b80bb`, without the long case) remains the 28/28 re-proof.

### Reference car/track: clean continuous lap PROVEN; visual acceptance still open

- **Continuous lap, clean (run `41799538756` at `5d21c56`, jolt/240Hz/CCD/rotational):** `finished=true`, `laps=1`, `bestLap=87.52s`, `damage=0`, `impactEvents=0`, `ruleResets=0`, 4 tyre contacts on DriveSurface, no chassis contacts in samples. This REPLACES the earlier failure line (17 impacts/damage 0.27 at `71dfc6c` on the source solver — preserved below in HISTORY and in JOB_REGISTRY.json). Evidence on disk: 89.6s encrypted+decrypted video + full telemetry at `outputs/repair-20261029/lap-jolt-41799538756/encrypted-review-0-attempt-1/game-review-41799538756-1-0/review/`. NOT yet watched by the human reviewer; solver changes (source vs jolt) are recorded in the run config.
- **car_evidence run `41798036351` at `e8b80bb` = success** — engine gates PASS: 5 car views full framing (`corners=376`), all 4 wheels + steered wheel closeups (`corners=32`, real closeups not whole-car), genuine cockpit (`COCKPIT_VIEW mode=2 fov=75 seatLocal=(0,0.88,0.05) seatOffsetFromAuthored=0.000 wheelsHidden=True` = authored seat, not overhead). 18 PNGs/shard decrypted at `outputs/repair-20261029/car-ev-41798036351/`. Human visual review pending; the earlier capture defects (mirrored logos, black foliage, speckled fences) are NOT dispositioned by these runs.
- **HUD fix verification made objective at `076363b`**: `Hud.EmitRects()` prints every HUD element's global rect at capture time; `reference_evidence` now asserts per-resolution that speed/gear/info/minimap/standings/hybrid rects stay inside the viewport (640x360 … 2560x1080; 960x540-class covered by 16:9 scaling). Dispatch `41820839687` in flight; the previous car_evidence set had NO bounds asserts (existence-only) — superseded by this.

### Fence/alpha work: mip-repair retry in flight; scale of claims unchanged

- Run `41795630650` (fence_miprepair_review at `5288329`) failed on the controls probe above, NOT on the mip-repair invariants — the fence evidence was never produced; retry `41818948189` dispatched at `9ff443c` (in flight). Scale unchanged: candidate evidence only, no visual acceptance, no geometry root-cause claim until the human reviewer compares repaired vs baseline PNGs.

### STILL OPEN (unchanged by this session)

- Mirrored logos/numbers root cause (UV/texture orientation per surface) — awaiting human review of `41798036351` closeups before any livery change.
- Road fairness, grass color balance, vegetation backdrop — one controlled change per evidence cycle, AFTER the fence mip-repair verdict.
- Windows run results, driving_validation runs (core/impact/wet), and the 15-stage remaining scope (all cars/tracks, weather/night, racing AI, pits, menus, championship, LAN, audio, performance, asset acceptance, final Windows e2e).

## CURRENT STATUS — 2026-11-04, earlier same-day snapshot (verification agent; superseded where the session-2 update contradicts)

### Settings persistence: verified on Linux; Windows explicitly OPEN

- **Linux: PASS (with one correction, below).** Run `41710482854` (game-review.yml, `depth=settings_persist_review`) at `dfe0ed3` = success: five fresh-process scenarios on Ubuntu (`legacy`/`foreign`/`corrupt`/`malformed`/`failures`) with per-scenario isolated `HOME`/`XDG_DATA_HOME`, 28/28 assertions green, audit summary `all_ok`. Scope: Linux runner, headless Godot 4.7.2, real `Settings` statics, real I/O. This is NOT Windows coverage and NOT closure of the stage-10 persistence acceptance.
- **Correction found 2026-11-04 (honesty note, fixed on this branch):** inside `failures`, the two assertions `temp_live_kept` and `rename_live_kept` read the un-globalized constant `user://settings.cfg` through System.IO (introduced when `c1701d9`/`dfe0ed3` globalized reads; this call site was missed). Both reads threw, `Read()` returned the empty string, and the assertions compared `"" == ""` — they passed **vacuously** (the same tautology class the reviewer flagged at `9bed931`). The other 26 assertions were real (return values, `File.Exists`/absence on globalized paths, reloaded-content reads, `corrupt_file_intact` byte comparison). Fixed in this commit: both reads use the globalized path, so they now compare real captured bytes and can genuinely fail. **A Linux re-run is required to re-prove the strengthened 28; until then, read "28/28 at dfe0ed3" as "26 real + 2 vacuous".**
- **Windows readiness (this commit; only `src/SettingsPersistChecks.cs` changed — `Settings.cs` and `SettingsPersistChecksNode.cs` untouched):**
  1. Temp-write fault is now cross-platform: Linux keeps the proven read-only-directory chmod (`File.SetUnixFileMode` is POSIX-only); every other OS (incl. Windows) occupies the tmp path with a directory, which makes `cf.Save(tmp)` fail identically (open-for-write on a directory fails on both Win32 and POSIX). Same three assertions (`temp_failure_rejected`, `temp_no_tmp`, `temp_live_kept`) hold on both; the blocker is removed before the tmp/live assertions.
  2. NEW Windows-only real contention fault: the live file is held open with `FileShare.None`. Godot's Windows `DirAccess.Rename` is delete-then-rename (verified in `godotengine/godot` `drivers/windows/dir_access_windows.cpp` @ `4.7.2-stable`: `if (file_exists(new_path)) { if (remove(new_path) != OK) return FAILED; }` then `MoveFileW`), so the delete hits the sharing violation, the save is rejected and the tmp recovery copy stays (the tmp write is unaffected by the lock). Live-content equality is asserted AFTER the lock is released, which also catches "deleted but not renamed". Three new cases, Windows repo runs only: `win_sharing_violation_rejected`, `win_sharing_violation_tmp_kept`, `win_sharing_violation_live_kept`.
  3. The env-seam rename fault (`MYSIM_PERSIST_FAULT=rename`, retargets the rename to a missing directory after a successful tmp write) was already OS-neutral; unchanged.
- **Known Windows risk (stated BEFORE the first Windows run):** because Godot's Windows rename deletes the existing target first, a save-over-existing on Windows has a non-atomic window (crash between `DeleteFileW` and `MoveFileW` leaves no live settings file; the tmp recovery copy survives and the failed path is reported). If the first Windows run shows `atomic_replace_*` or `foreign_marker_kept` failures, that is a REAL production defect of `Settings.Set` on Windows and needs an evidence-backed fix in `Settings.cs` (e.g. a `File.Replace`-based Windows path), not a weaker fixture.
- **Contract sync (new regression guard, this commit):** every scenario now ends with `contract_sync_case_names`: the emitted case names must equal the fixture's per-platform contract exactly (catches accidental renames/additions in the fixture), and on repo runs the `required_cases` table in `tools/cloud_game_audit.py` must equal the shared 28-name base contract in BOTH directions (catches drift in the coordinator-owned audit). Packaged exports do not contain the `.py`; there the audit comparison is skipped and recorded in the report JSON `contractSync` field — visible, never a silent pass.
- **Fixture contract (exact).** Base 28 (must stay identical to `cloud_game_audit.py` `required_cases`; enforced both by the audit's prefix match + `passed>0 && failed==0 && exit 0` and by the in-firmware contract check):
  - `legacy` (7): legacy_seed_save, legacy_readback, legacy_get_did_not_write_disk, legacy_set_stamps, legacy_marker_and_values_persisted, atomic_replace_saved, atomic_replace_final_bytes
  - `foreign` (4): foreign_seed_save, foreign_readback, foreign_set_succeeds, foreign_marker_kept
  - `corrupt` (5): corrupt_defaults_typed, corrupt_no_crash, corrupt_no_value_resurrection, corrupt_file_intact, corrupt_load_error_evidence
  - `malformed` (4): malformed_seed_save, malformed_defaults_typed, malformed_disk_untouched_by_get, malformed_set_migrates_marker
  - `failures` (8): baseline_saved, temp_failure_rejected, temp_no_tmp, temp_live_kept, rename_fault_rejected, rename_tmp_recovery_kept, rename_live_kept, atomic_success_after_faults
  - Windows adds the 3 `win_sharing_violation_*` cases; every scenario adds `contract_sync_case_names` on repo runs (packaged runs: recorded in `contractSync` instead). Session-2 update: the base contract is now 29 (`legacy_long_roundtrip` added); this snapshot's 28-name list is the pre-`9ff443c` contract, kept for the record.

### Windows verification plan (PROPOSAL — coordinator owns `windows-build.yml` and `tools/*`; this agent does not dispatch)

Minimal set; every assertion already exists in the fixture — the Windows job only needs to drive the packaged player once per scenario:

| # | What Windows must prove | Where it runs |
|---|---|---|
| 1 | Atomic save + legacy/foreign/corrupt/malformed behaviour on real Windows paths (`%APPDATA%\Godot\app_userdata\MySim\settings.cfg`) | New step in `windows-build.yml` after the existing smoke/render step, running the packaged `MySim.exe` (real player, not editor) once per scenario |
| 2 | Failure preservation on Windows: temp-write failure (dir-occupied tmp), rename failure (env seam), Windows sharing violation (`FileShare.None`) — live file intact, tmp recovery semantics | Same step, `MYSIM_SETTINGSPERSIST_SCENARIO=failures` (the fixture branches per OS itself) |
| 3 | Real settings touched exactly once per scenario with fresh state | The driver deletes `%APPDATA%\Godot\app_userdata\MySim` before each scenario (equivalent of the Linux per-scenario `HOME` swap) |

Mechanism: one new coordinator-owned file `tools/cloud_windows_persist.py` (full proposed content in `outputs/mistral-orchestration/agents/verification-report.md` for verbatim adoption) mirroring the existing `cloud_windows_smoke.py` conventions (headless run, `CREATE_NO_WINDOW`, marker/JSON checks, non-zero exit on failure, results JSON), plus one workflow step `python tools/cloud_windows_persist.py` and `windows-settings-persist-*.json` added to the diagnostics artifact paths. The first run doubles as the probe for the delete-then-rename risk above. STATUS 2026-11-04 session 3: implemented (script at `8eeb6e4`, workflow step at `d3629e8`, platform-enforcement at `2862b04`) — this section is historical; see the session-3 block for run states.

### Reference evidence snapshot (NOT visual acceptance)

- Run `41788844369` at `e41e29b` = success (note: the dispatch brief's "44788844369" is a typo; `gh run view 44788844369` returns 404, `41788844369` verified). 9 PNGs (7 static views + 2 drive frames) captured, decrypted under `outputs/repair-20261029/cloud/reference-evidence-41788844369/`. Manually reviewed 2026-11-04 (see `VISUAL_REVIEW.md`): capture success accepted; **visual acceptance NOT granted** — blocking: black foliage/horizon masses, white/speckled grandstand/fence surfaces, HUD right-edge clipping + half-width top bar at 960x540, mirrored logos/numbers, `wheel-fl` not a wheel closeup, cockpit frame not cockpit evidence. No continuous-lap/motion, collision, input-direction or handling acceptance exists in this set.

### Run index (this work line; statuses re-verified via `gh run view` this session)

| Run | Head | Result | Note |
|---|---|---|---|
| 41710482854 | `dfe0ed3` | success | settings_persist_review 28/28 (26 real + 2 vacuous, now fixed on this branch) |
| 41788844369 | `e41e29b` | success | reference_evidence: 9 PNGs captured; capture only, NO visual acceptance |
| 41707159183 | `c1701d9` | failure | strict typed fallback + case sync; preserved |
| 41703220126 | `1d6bd4d` | failure | observed-evidence assertions round; preserved |
| 41699253306 | `e1f6d21` | failure | scenario boot isolation + real fault seams; preserved |
| 41688779865 | `9bed931` | failure | fixture compile errors (missing helpers, Variant.Text, private SchemaVersion); preserved |
| 41675023687 | `18f69a1` | failure | fixture compile errors (UnixFileMode etc.); preserved |

### Proposed dispatch configs (coordinator executes; this agent does not dispatch)

1. **Linux re-proof of the strengthened fixture** (after this branch's commit is available at the dispatch ref):
   `gh workflow run game-review.yml -R instagramshashank8-bot/mysim-godot --ref <ref-containing-this-commit> -f depth=settings_persist_review`
   All other inputs default — this matches the `41710482854` shape exactly (cars 16, backend source, 120 Hz, rotational false; `settings_persist_review` maps to shard `[0]`).
   PASS criteria: `settings-persist-summary.json` `all_ok: true`; per-scenario passed/failed = 8/0, 5/0, 6/0, 5/0, 9/0 (28 base + 5 `contract_sync_case_names` on Linux repo runs); every scenario's case list contains `contract_sync_case_names` with the audit-comparison detail; the 28 base case names unchanged. STATUS: executed — `41798040383` (28) and `41818941902` (29, with `legacy_long_roundtrip`) both success.
2. **Windows persistence verification** (after the coordinator adds the proposed step/script and the publication tag):
   `gh workflow run windows-build.yml -R instagramshashank8-bot/mysim-godot --ref <ref-containing-this-commit-and-the-new-script> -f publication_tag=windows-candidate-<40-hex-of-that-ref>`
   PASS criteria: `windows-settings-persist-summary.json` all five scenarios ok (exit 0, `passed>0`, `failed==0`, all required cases present, no `ERROR:` lines); on Windows the `failures` scenario reports 31 assertions (28 base + 3 win) + `contract_sync` recorded; any `atomic_replace_*` failure is a REAL Windows defect finding to be fixed in `Settings.cs`, not suppressed. STATUS: `41819498045` dispatched at `d3629e8` (pre-platform driver — see the session-3 caveat); the decisive run must be at `2862b04`+ (32 assertions; driver requires the win cases + `platform=windows`).

### Open items

1. Linux re-run to re-prove the strengthened `temp_live_kept`/`rename_live_kept` (dispatch proposal 1). STATUS: DONE — `41798040383` (28) and `41818941902` (29).
2. First Windows run (dispatch proposal 2) — expects either full pass or the documented delete-then-rename finding. STATUS: `41819498045` in flight (pre-platform caveat applies); decisive run at `2862b04`+ still needed.
3. Optional audit-side hardening (coordinator-owned, not required): mirror the fixture's exact-set check in `cloud_game_audit.py` (currently prefix-match only). The in-fixture check already covers both directions on repo runs. STATUS: partially addressed at `2862b04` on the WINDOWS driver (win cases + platform required); the Linux audit still uses prefix-match.
4. Reference visual acceptance remains open per `VISUAL_REVIEW.md` (not this agent's files).

## HISTORY (preserved verbatim; superseded — read only for the record)

### 2026-11-03T~18:40Z snapshot — "READY FOR INDEPENDENT_REVIEW" (superseded by the 2026-11-04 section above)

> Erratum 2026-11-04: this snapshot predates the successful dfe0ed3 run. Its "compile FAILED" / "no successful persisted-behavior run exists" / "Current HEAD 9bed931" statements were true at 9bed931 and are OBSOLETE now; the branch tip title ("persistence verified 28/28") was also ahead of this file's content, which is exactly the contradiction this rewrite removes. Kept verbatim for the failure/decision record.

````text
# REVIEW_PACKET — MySim settings-persistence work + session status
Snapshot frozen 2026-11-03T~18:3xZ by the sole worker. **READY FOR INDEPENDENT REVIEW.** No game-completion claim. This packet contains the actual state including unresolved defects; nothing here should be read as acceptance.

## 1. BASELINE AND CURRENT STATE
- Session baseline: `a6416fb` ("Bind authored car UV defaults…", the handoff HEAD). Remote/protocol: repo `instagramshashank8-bot/mysim-godot`, branch `codex/repair-20261029`, remote name `o_instagramshashank8-bot`, owner credential = `gh auth token -u instagramshashank8-bot` (identity + `admin:true` verified via `gh api user` / collaborators-permission this session; earlier 403s were self-inflicted unset-`GH_TOKEN` shell ordering).
- **Current HEAD (local and remote match): `9bed931`** ("Settings persist fixture: typed fallback, corrupt-branch order, node exit contract").
- **Uncommitted changes** (working tree, verified via `git status`): `src/SettingsPersistChecks.cs` only (see below). No other modified tracked files besides local non-project noise (`shots/log.txt`, untracked tooling/outputs from previous sessions; not staged per the no-`git add .` rule).
- Chain of committed sessions on this branch (this worker): `2c3fd66` (a2c enum fix) → `74dc306`/`ec24272` (ledger + task board) → `e4d1418` (wire + load guards) → `1d5aed7` (a2c naming/corrections) → `bb4c9f8` (a2c naming/issues…) → `0fdc05b` → `4b2b049` (A2C verdict) → `cda1e21` → `72cf0b5`/`01b5500`/`62c096e` (ledger compaction) → `534d2cf` → `18f69a1` (fixture v2 wiring) → `fafaf57` (report) → `9bed931` (fixture v3 commit).

### File-by-file (current work, settings persistence)
| File | State | What it does |
|---|---|---|
| `src/Settings.cs` (committed `b0fa604`) | implemented, compile-verified by CI runs ≥ `bb4c9f8`… (compiles; behavior tests pending) | Atomic-save contract: `cf.Save(tmp)` → `DirAccess.Open(dir).Rename(tmp, live)`; NO direct-write fallback; old file preserved on temp or rename failure; errors pushed via `GD.PushError`; legacy (missing `schema_version`) preserved + stamped on next save; corrupt/unloadable file alone falls back to fresh defaults |
| `src/SettingsPersistChecks.cs` (committed `9bed931`, **further uncommitted fixes present**) | implemented, **compilation FAILED on CI** (see §3), fixes not yet committed | Fixture: legacy / foreign / corrupt / rename-failure / atomic-replace / typed round-trip cases over REAL `Settings` statics; structured JSON report |
| `src/SettingsPersistChecksNode.cs` (committed `9bed931`) | implemented, referenced in same failing CI build; **one real defect in current tree: calls `WriteReport(path)` with one arg while fixture now requires `(path, contents)` — CS1503-class mismatch; must be updated to pass `report` string** | Godot Node host: runs fixture, prints `SETTINGSPERSIST <json>`, writes report file, quits non-zero on failures |
| `src/Main.cs` (committed `18f69a1`) | implemented, compiles | `_Ready()` first line: `MYSIM_SETTINGS_PERSIST=="1"` → `AddChild(new SettingsPersistChecksNode()); return;` — isolated early-exit boot before any world/UI/asset work |
| `tools/cloud_game_audit.py` (committed `18f69a1`/`9bed931`) | implemented; assert contract **currently dead code unless its env is exported** (see §5) | Depth `settings_persist_review`: asserts the fixture's PASS/FAIL contract line and writes `settings-persist-report.cfg` |
| `.github/workflows/game-review.yml` (committed `18f69a1`) | implemented | Depth option `settings_persist_review` + env `MYSIM_SETTINGS_PERSIST: 1` when that depth is dispatched |

Clear separation: **implemented** = Settings atomic-save fixture + wiring; **failed** = CI compile of fixture at `18f69a1` and `9bed931` (details §3); **unverified** = all persistence behavior on real Godot (no successful fixture run exists yet); **failed/adopted** = A2C fence candidate (rejected for adoption, artifacts kept); **not started** = everything else in the 15-stage plan beyond what previous sessions recorded.

## 2. IMPLEMENTATION
### `src/Settings.cs` — production save path (the fix that matters)
```
src/Settings.cs:11-26 (current tree; committed at b0fa604)
- static void Load(): cf.Load(P) != Ok  → cf = new ConfigFile()        // corrupt file: fresh defaults, no erasure of disk
- else if value "schema_version" reads 0 (absent) → stamp marker in MEMORY ONLY (persisted by next Set)
- public static void Set<T>():
    live = ProjectSettings.GlobalizePath(P)                            // ABSOLUTE path
    tmp  = live + ".tmp"                                               // same-directory sibling ⇒ same-volume atomic rename
    Error tmpSave = cf.Save(tmp)                                       // never writes P directly
    if (tmpSave != Ok) { GD.PushError(SETTINGS_SAVE tmp=… old file kept…); return; }
    using var d = Godot.DirAccess.Open(Path.GetDirectoryName(live));
    if (d == null) { GD.PushError(SETTINGS_SAVE dir_open_failed…); return; }
    Error rename = d.Rename(tmp, live)
    if (rename != Ok) { GD.PushError(SETTINGS_SAVE rename=… old file kept; recovery copy=…); return; }
```
Why: the old code wrote `cf.Save(P)` on temp-save failure **and** on rename failure, so any failure near the end of a save could destroy the previous good file mid-write. The new code has exactly ONE write to disk for new state (the tmp file), and the live file's only mutation is the atomic rename. Legacy files (pre-schema) are never reset; only an unreadable file is replaced by defaults, and even then only in memory until a Set happens.

### `src/SettingsPersistChecks.{cs, Node}` — fixture
- B (+corrupt boot) writes `"{not a config"` into `user://settings.cfg`, then asserts `Settings.Get` returns the exact typed defaults (int `-1`, bool `true`, string default `x` now correctly asserted — reviewer point 1) and that `ConfigFile.Load` reports non-Ok while the file still exists (point 4 partially; see "remaining").
- Boot A asserts: legacy versionless file (int/bool/string/double) survives `Get` untouched; foreign `schema_version:99` is KEPT; first `Set` stamps the marker without disturbing other keys; typed round-trip over `Set/Get`; REAL rename-failure injection: `System.IO.File.SetUnixFileMode(dir, UserRead|UserExecute)` **before** `Settings.Set("blocked",1)` (production save path fails; the pre-capture `beforeLive` string is compared to the file AFTER the attempt — no self-comparison), tmp recovery copy must exist; then atomic-replace: `Set("final",1)` renames over the live file, tmp consumed, stray tmp absent.
- Fixture output: single JSON `{passed,failed,failures[],cases[],probe}`; `SettingsPersistChecksNode` prints it and `Quit(HasFailures?1:0)` so CI fails on any failed assertion.

### Integration / regressions
- `Settings` API surface unchanged (same `Get/Set/ApplyAudio/ScreenControlsEnabled` signatures) → all existing callers compile unchanged.
- Risk: rename-based save changes file-inode identity; anything holding an open handle to `settings.cfg` across saves would see the old inode. No known caller holds such a handle (readers re-open per call).
- Temporary diagnostic flags: `MYSIM_SETTINGS_PERSIST` (entry hook), `MYSIM_SETTINGSPERSIST_CORRUPT` (corrupt-boot phase). No others added.
- The corrupted `user://settings.cfg` on a runner is harmless (per-boot isolation).

## 3. VERIFICATION
### GitHub runs (all `instagramshashank8-bot/mysim-godot`, branch `codex/repair-20261029`)
| Run | Commit (short) | Result | What it proved |
|---|---|---|---|
| 41632524155 | `5492d90` | success | fence true-Alpha candidate ran; artifacts downloaded/decrypted; verdict: no speckle reduction → rejected (evidence kept) |
| 41633324944 etc. (5 earlier worker dispatches) | `a6416fb` era | success | adopted, not re-run by this worker |
| 41645599180 | `1d5aed7` | **failure** | `CS0120 DirAccess.Rename static` — Settings atomic-rename first attempt; distinct fix committed (`e4d1418`→`bb4c9f8`) |
| 41645851285, 41646636631, 41656712889 | `6d2069a`/earlier | success / failure mixed | A2C fence runs; artifact comparisons recorded |
| 41675023687 | `18f69a1` | **failure** | `CS0103 UnixFileMode` ×6+, `CS0117 WriteReport/HasFailures missing`, `CS1061 Variant.Text`, `CS0122 Settings.SchemaVersion private`, `CS0019`, `CS0266` — fixtures failed compile |
| 41688779865 | `9bed931` | **failure** | same error classes: Node references `WriteReport`/`HasFailures` that didn't exist in that commit's fixture; `Variant.Text`; private `SchemaVersion`; `CS0266 Variant→int` |
| 41656712889 | `4b2b049` | success | last fully-green full-project compile (before fixture v2/v3) |

### Distinct-fix audit trail (no repeated blind retries)
1. `CS0117 AlphaToCoverageAndHash` → verified actual enum in GodotSharp 4.7.2 XML (`AlphaToCoverageAndToOne` exists; `AlphaToCoverageAndHash` does not) → fixed once (`2c3fd66`).
2. `CS0120 static DirAccess.Rename` → verified rename is an instance method → `DirAccess.Open(dir)` handle (`e4d1418`).
3. `CS0117 WriteReport/HasFailures` (+ Text/Schema/casts) → added `HasFailures`, `WriteReport(path, contents)`, replaced `Variant.Text` with `As…()`/`VariantType` chain, removed private `Settings.SchemaVersion` references from the fixture (current **uncommitted** tree) → **needs the next commit + re-dispatch (not yet done)**.

### What current verification does NOT cover
- **No successful persisted-behavior run exists.** The `settings_persist_review` fixture has never executed its assertions on CI (all its runs failed at compile). Nothing about Settings runtime behavior is verified yet.
- `tools/cloud_game_audit.py`'s `settings_persist_review` branch has never executed on CI either; its assert-contract path is untested code until the fixture runs.
-(`CS0219`-grade sloppiness acknowledged: the fixture currently builds the JSON report **twice** — a local `report` variable (unused) + the return expression. Compiles with a warning, but is ugly and should collapse to one construction in the next commit.)

### Honesty checks requested by reviewer — current answers
- Missing reports: **none fabricated**; all reported runs have runner logs; the two fixture runs failed at compile (log captured above).
- Zero-assertion passes: the fixture's PASS line requires `passed>0` AND prints individual case names; no pass is claimed from zero cases.
- Ignored failures: none; every CI failure above produced either a distinct fix or an explicit observed-defect record.
- Self-comparisons: `rename_failure_preserves_live` compares the live file against `beforeLive` **captured before the faulting call** (the earlier tautological compare `Read(live) == Read(live)` was found and is fixed in the uncommitted tree).
- Production-path tests: rename/temp failures drive the REAL `Settings.Set` (static `Settings.cf`), not a cloned algorithm; the fixture's own `ConfigFile` is used only for legacy/foreign seeding files.
- Dispatch/compile ≠ end-to-end verification: correct — nothing in this packet claims runtime persistence verification.

## 4. VISUAL/GAMEPLAY EVIDENCE (all GitHub-run artifacts, decrypted locally for MANUAL inspection; no local engine work)
- `outputs/repair-20261029/cloud/fence-true-blend-result/…/day0-filtered-fence-{baseline,fence_blend_candidate}.png` — **manually inspected via image statistics AND decrypted artifact viewing**: candidate changes fence band but speckle persists; REJECTED.
- `outputs/repair-20261029/cloud/fence-a2c-result/plain/review/day0-filtered-fence-{baseline,fence_a2c_candidate,fence_blend_candidate}.png` — same treatment; verdict recorded in `outputs/repair-20261029/FENCE_A2C_VERDICT_QUANT.json` (machine metrics + act images reviewed by me; **manual independent review still pending**).
- Mesh overview sheet batches for parts 72/73 (decrypted rewrap `41618971031`): `outputs/repair-20261029/cloud/mesh000{72,73}-review/review/sheets/*.jpg` — **machine-screened only** (black-count/edge metrics); per-item black-card flags: `outputs/repair-20261029/MESH000{72,73}_QUANT_OVERVIEW.json`; **not** counted as manual review anywhere (relabeled in the same files + `CONTINUE.md`/`REPAIR_REPORT.md`).
- Prior accepted-context evidence (pre-session, untouched): `outputs/repair-20261029/RECORDING_SAMPLE_REVIEW_ACTUAL.json` (13 seconds), native campaign reports, mesh parts 1–71 (23,296 sheet meshes overviews).
- Known open visual defects (from the session evidence): fence speckle (unfixed; root cause hypothesis = sub-pixel wires + AlphaScissor over generated mips; geometry-level fix still REQUIRED), bright grass, bland roads, plain HUD, unrepaired corona black-cards (`MESH00072` flags), no full-motion race acceptance.

## 5. BLOCKERS AND NEXT WORK
### Blocker 1 (active, concrete): `src/SettingsPersistChecks.cs`(+Node) do not compile together
- Run per the latest fail log: run `41688779865` at `9bed931` — `CS0117` (missing `WriteReport`/`HasFailures`), `CS1061 Variant.Text`, `CS0122` (private `Settings.SchemaVersion`), `CS0019 bool&&string`, `CS0266 Variant→int`.
- Attempted resolution: fixture rewritten on the working tree (uncommitted): `HasFailures` + `WriteReport(path, contents)` added; corrupt-branch reordered so corrupt bytes stay until after assertions; `Variant.Text` removed; schema reads gated via `VariantType` checks.
- **Remaining concrete code defects in the uncommitted tree (must be fixed in the next commit):**
  1. `src/SettingsPersistChecksNode.cs:12` — `WriteReport(env??path)` with one argument; must pass `report`.
  2. `src/SettingsPersistChecks.cs:~58-59` — duplicate JSON build (dead `string report=…` before final `return …`).
  3. my `Settings.cs` a `Settings.SchemaVersion` remains `const int` internal to the class (+`Settings` is a separate `static class`); there is no longer any reference to it from the fixture, so **no access modifier change is needed** — but re-verify no other fixture file cites it.
  4. No local C# compiler: compile risk remains; next run must be compiled and inspected before any acceptance (its PASS line is the contract).
- No other blocks: dispatch capability confirmed (with `GH_TOKEN` exported around every call); render capacity restored (previous A2C runs succeeded).

### Next work (prioritized, complete 15-stage scope preserved)
1. Commit the fixture fixes (including Node's 1-arg `WriteReport`) + dedup the JSON build; push; dispatch `settings_persist_review` once via `gh workflow run … -f depth=settings_persist_review`; require the run's `settings-persist-report.cfg` to assert `passed>0, failed=0` and read the printed case names. (Retry-once policy applies with a distinct diagnosis if it fails.)
2. Wire `cloud_game_audit.py`'s corrupt-phase env var (`SETTINGS_PERSIST_CORRUPT_SHARD`) so a single run covers BOTH corrupted and legacy/foreign/atomic boots cleanly.
3. A2C/beyond: leave `settings/a2c` artifacts as accepted history (no more A2C runs); move to the next unstarted reference-experience defect list: verify actual fence geometry-corruption driver (not via new AAA renders), then reference car `16`/track `0` boot + drive-level gates under the same CI root cause analysis loop.
4. Resume open visual evidence collection per the standing 15-stage list (gates: physics realism, race flow, pits, visual hierarchy).

### Indicators and evidence integrity commitments
- The next packet update will only mark persistence "verified" when a real GitHub run shows `passed>0 && failed==0` and the case list is spelled out.
- No `git add .` was used at any point; only the five files explicitly enumerated above were staged.
- Failure history (all four failed runs) is preserved in this packet and in `outputs/repair-20261029/REPAIR_REPORT.md` / `CONTINUE.md` rather than deleted.

---

## REVIEWER SUMMARY (paste-ready)
- **Current HEAD:** `9bed931` (local & remote identical; branch `codex/repair-20261029`, commit "Settings persist fixture: typed fallback, corrupt-branch order, node exit contract"); one modified file uncommitted (`src/SettingsPersistChecks.cs` — post-`9bed931` corrections).
- **Changed files (this work block):** `src/Settings.cs` (atomic rename, legacy preservation, no direct write), `src/SettingsPersistChecks.cs` (+ `SettingsPersistChecksNode.cs`), `src/Main.cs` (early-exit fixture hook), `tools/cloud_game_audit.py` (settings_persist_review depth + assert contract + `settings-persist-report.cfg`), `.github/workflows/game-review.yml` (depth option + env wiring); plus evidence (`MESH000{72,73}_QUANT_OVERVIEW.json` machine-only flags, `FENCE_A2C_VERDICT_QUANT.json`, ledger updates, `TASK_BOARD.json`).
- **Verified:** CI compile-past-fixture at earlier actions (Settings atomic-save `b0fa604` compile green via `41656712889`'s project build); 4 failed CI runs captured with exact compiler errors; owner-credential dispatch works (`41688779865`, `41675023687`); A2C artifacts reviewed (rejected for adoption, kept); a2c naming/`MSAA 8×` semantics corrected against the 4.7.2 API; mesh 72/73 relabeled **machine-only**.
- **Failed/unverified:** persists fixture compilation (both `18f69a1` and `9bed931` run attempts — full error list preserved); **no successful settings-persistence execution yet**; corrupt-branch and Node-call defects still present in the uncommitted tree; reference-game visual/physics acceptance untouched (fence speckle, grass, roads, HUD defects open; none claimed fixed).
- **Evidence paths:** `outputs/repair-20261029/cloud/{fence-true-blend-result,fence-a2c-result,settings-persist-result}/`, `outputs/repair-20261029/MESH000{72,73}_QUANT_OVERVIEW.json`, `outputs/repair-20261029/FENCE_A2C_VERDICT_QUANT.json`, `outputs/mistral-orchestration/{BATCH_REPORT.md,REVIEW_REQUEST.json,TASK_BOARD.json}` (task board at `…/repair-20261029/TASK_BOARD.json`), `outputs/repair-20261029/{CONTINUE.md,REPAIR_REPORT.md}`.
- **Questions requiring reviewer judgment:** (1) Is the two-phase (legacy/foreign/atomic) + corrupt-boot split the right fixture shape, or should corrupt + atomic live in one boot? (2) Confirm `MYSIM_SETTINGS_PERSIST` early-exit hook placement is acceptable inside `Main._Ready()` vs a standalone scene entry. (3) Sign-off criteria for calling the persistence WORK "verified" — is a single green `settings_persist_review` GitHub run with PASS enough, or do you want CI to run my corrupt-boot phase separately as well? (4) Proceed to reference car/track repair next session as planned?

*Snapshot ends. No completion claims. READY FOR INDEPENDENT REVIEW.*
````

*History block ends. The Current Status sections at the top of this file supersede everything above.*
