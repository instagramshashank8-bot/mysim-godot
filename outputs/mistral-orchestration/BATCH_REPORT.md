# BATCH_REPORT — MySim rebuild (session-2 update)

Maintained per REVIEW_PROTOCOL.md. Current phase first; earlier material under dated headings. Exact commits, run IDs and evidence paths; failures preserved.

## PHASE — 2026-11-04 12:4xZ, coordinator session 2 ( six-specialist structure active )

### Newest state (after the human review feedback at 09:10Z + the supervisor's six-role directive)

- **Role structure live (JOB_REGISTRY.json `role_structure_2026_11_04_user_update`)**: hud-camera (HUD/product controls, turn 3), car-assembly (wheel/axles/contact, turn 3 — the top visible priority), NEW carbody-livery agent spawned (carbody assembly + livery/texture inventory, turn 1), track-render (forest wall + road + fence checklist, turn 3), physics-gameplay (ride-height A/B pack + per-backend impact protocol + DV judgement, turn 3), verification (EVIDENCE_INDEX + packet freshness, turn 3). Coordinator = sole dispatcher/merger + integration/evidence role.
- **Windows hardening per reviewer**: cloud_windows_persist now REQUIRES the three win_sharing_violation_* cases and `platform:windows` proof per report (fixture emits `platform`); the running 41819498045 uses the pre-platform driver, so its pass proves build+smoke+8-case persist only — the decisive Windows persist run must be at `2862b04`+ (will be dispatched once the in-flight one lands; no duplicate while running).
- **car_evidence pinned to one shard** (`2862b04`) after the reviewer flagged six redundant evidence shards.
- **driving_validation retried with the real root causes fixed**: failure #1 = hull bake never covered the depth (missing bake, not a handoff bug) → step added; failure #2 = with the step present the bake script still selected car 0 via the legacy [[0,16][shard]] fallback (41823772505 "hull cache missing for car 16") → manifest-aware bake (9c422a4). Live DV attempt = 41826593001 (9c422a4, includes the merged hardened profiles a5215ef). A duplicate DV dispatch (41826855508) was created by mistake and CANCELLED immediately (dedupe rule); retained in history.
- **Fence mip-repair**: 41818948189 still finishing shards 0/1 (the fence evidence is shard-0-only; shards 2..5 ran the standard suite; 3/4 failed at as-yet-unidentified probe — full job logs only readable after the run completes). The invariant schema mismatch (python read nested `level0.keptAt05`, C# emits flat `level0KeptAt05/035` — found by the track-render specialist, verified against TrackCutoutDiagnostic.cs:58) is FIXED at `49ece2c`; the mip-repair retry will follow after the in-flight run completes if its shard-0 fails on that exact mismatch.
- **Human-review state (09:10Z)**: speed clipping FIXED at 5 resolutions; genuine wheel closeups + cockpit ACCEPTED narrowly; mirrored logos, speckled stands/fences, wheel detail/clearance, forest wall, cockpit usability, road/grass remain rejected/provisional. All six roles above are aimed at exactly these.

### Prior phase summary (same day)

### Landmarks achieved this session (all in repo, branch codex/repair-20261029)

1. **Settings long-typed regression fixed (`9ff443c`)** — found via the integrated CI probe battery (run 41795630650 caught "new throttle key not stored"): `Settings.TypedValue<T>()` had no `long` branch, so `Settings.Get(key,(long)def)` always returned the default and every key rebinding was dead. Regression was introduced by the strict-typed-Get change at `c1701d9`. Fixed: `long`↔Int natively matched; new `legacy_long_roundtrip` fixture case added to the triple contract (fixture BaseCases / audit required_cases / windows driver), Linux 29-case proof green (run 41818941902 SUCCESS at 9ff443c).
2. **Windows persistence now wired and running (`d3629e8`)** — the earlier "wired" claim was wrong for the committed tree (caught while preparing the dispatch; disclosed in REVIEW_PACKET). `windows-build.yml` now runs `tools/cloud_windows_persist.py` on the packaged player after smoke/render; diagnostics artifact extended. Runs: 41819114117 (build+smoke only, 9ff443c) **SUCCESS**; 41819498045 (d3629e8, includes persist, 5 fresh scenarios, expects 32 assertions incl. 3 win_sharing_violation_*) in flight at report time. Dispatch used tag-ref `windows-candidate-d3629e8430fcddcd5544153221e07467f7499120` while branch pushes hit GitHub 500s (3x, documented); two misconfigured windows dispatches (41818937383, 41819015228) retained as failures.
3. **Clean continuous lap proven (run 41799538756 at 5d21c56, jolt/240Hz/CCD/rotational)** — finished, 1 lap, bestLap 87.52s, 0 impacts, 0 damage, 0 rule resets, 4 tyre contacts. Video (89.6s MP4) + 9MB telemetry decrypted for review. Earlier source/120 failure (17 impacts) retained (41794252920).
4. **Reference/car evidence harness proven (41798036351 at e8b80bb)** — engine gates PASS: 5 full car views (corners=376), 4 wheel closeups + steered (corners=32), genuine cockpit (mode=2, seatOffsetFromAuthored=0.000, wheelsHidden=True). PNGs decrypted; human visual review pending; mirrored-logo/foliage/fence defects NOT dispositioned by this run.
5. **HUD bounds made objective (`076363b`)** — `Hud.EmitRects()` prints every element rect at capture; reference evidence asserts per-resolution containment. Run 41820839687 in flight. HUD-camera specialist also corrected the design-space assumption (1280x720 CanvasItems Expand base — the assertion now checks design-coverage + aspect, not raw pixels).
6. **Driving validation depth landed (`4afae34` + integration chain)** — orchestrator branch, hull-bake inclusion, report-path env hook, `driving_core/driving_impact/driving_wet` case sets. Continuous-lap profiles (8) with per-profile assertions now executable on cloud. Attempts: 41820348686 (init failed: hull cache absent → hull-bake step added), 41823396466 (compile failed: merged detector CS0103 → fixed 5bf41d7), 41823566679 (pre-fix duplicate; discarded), 41823772505 (current, at 5bf41d7) in flight.
7. **Specialist integration (turn 2)** — physics a5215ef merged (hardened profiles; new `nonSurfaceContactSamples` metric; grounded-only suspension; wet decel floor widened −22→−25 with model math). Car-assembly detector + candidate-gate fix merged (its agent's final message degenerated into a repetition loop; its commits were reviewed line-by-line — the compile catch above came from that review chain). Track-render turn-2 deliverables: human-review checklist for the mip-repair candidate + ONE controlled grass-balance candidate committed on its branch (f74a98a, **deliberately not merged/dispatched until the fence mip verdict**).
8. **Fence mip-repair** — first run (41795630650) died on the controls probe (the long bug); retry 41818948189 in flight; the python invariant read a nested schema the C# never emitted (flat `level0KeptAt05/035`) — fixed by the track-render specialist's report + my L296-298 correction (+ the plan: artifacts ship `if:always()` so the five mode renders remain reviewable even on invariant failure).

### Verification discipline this session

- Dispatch ≠ verification: every dispatch above has its own run ID + commit; conclusions read from `gh run view` before any claim.
- Compile breakage caught by integration (not specialist branch) retained as evidence; fixes carry the catching run ID in the commit message.
- No assertion weakened; one band (wet decel) widened with model math and documented reasoning; one metric class replaced (chassis-contact assertions → nonSurfaceContactSamples) because the clean lap PROVED the old premise wrong.
- All engine/render/test/simulation execution on GitHub; local work = source edits, evidence decryption (existing key flow), report writing; no local builds/tests/metrics.

### Open / next

- Mip-repair verdict (run 41818948189 + human checklist) → fence family fix decision.
- DV core result (41823772505) → impact/wet DV dispatches; ride-height A/B decision (chassis-hull rides the road constantly per the clean-lap telemetry — needs a defaults decision vs candidate dispatch).
- Windows persist result (41819498045) → stage-10 Windows closure decision (delete-then-rename non-atomicity risk documented).
- Reference HUD/manual-reviews: mirror-logo root cause (detector evidence lands with next car_evidence), road/grass/vegetation single-change cycles, HUD hierarchy integration (hud-camera turn-2 patch on branch bf5b598, review before merge).
- Full 15-stage scope unchanged; no game-completion claim.
