# Independent snapshot review — 2026-11-03
Verdict: CHANGES REQUESTED. Snapshot not ready for runtime acceptance. No source edits performed by reviewer.

## Blocking findings
1. src/SettingsPersistChecksNode.cs calls WriteReport(path) but current uncommitted fixture declares WriteReport(path,contents). Pass report explicitly; handle exceptions so runner exits nonzero and preserves diagnostics.
2. tools/cloud_game_audit.py settings branch checks SETTINGS_PERSIST_REVIEW, but workflow sets MYSIM_SETTINGS_PERSIST. Branch unreachable. Workflow phase env SETTINGS_PERSIST_SHARD differs from script SETTINGS_PERSIST_CORRUPT_SHARD. Align explicit scenario selection and shard coverage.
3. Same Python branch redefines shared run(), uses nonexistent /tmp/mysim/godot and tools/SettingsPersistBoot.cs, ignores actual GODOT_BIN/shared helper. Remove duplicate runner; reuse existing run(label,values,args) or a separate distinctly named tested helper.
4. Unconditional print+sys.exit(0) immediately after settings branch bypasses F1_PAINT_REVIEW and every later mode. Remove; preserve all original modes. Reject changes that silently skip required validation.
5. Fixture outputs structured JSON (SETTINGSPERSIST) but Python expects PASS/FAIL assertions; re not imported, env referenced outside local scope. Standardise actual JSON report: successful process exit, exact scenario, passed>0, failed==0, all required case IDs present. Missing/malformed report/FAIL/nonzero exit must fail. Never accept zero corrupt assertions.
6. No demonstrated user-directory isolation: MYSIM_SETTINGS_USER_DIR is invented/unconsumed. Use real Godot user-data isolation verified at runtime, dedicated test project/user-dir settings or explicit test storage seam defaulting to real production path. Must never touch real user save data.
7. Foreign-version test writes disk AFTER Settings.Load cached legacy values. Subsequent Get never reloads, so it is vacuous. Fresh PROCESS for legacy/foreign/corrupt/malformed-types, not merely fresh ConfigFile.
8. Rename test removes directory write permission BEFORE Settings.Set; this fails TMP CREATION, not rename. Existing tmp expectation is invalid; rename_fails_only always true. Split production-path temp-save and rename failures with an explicit I/O seam or deterministic real fault between successful temp write and rename. Restore permissions in finally; compare captured old bytes; assert actual error stage and recovery tmp only after successful temp save.
9. src/Settings.cs generic Get blindly As<T>, Load blindly reads schema int. Malformed-type defaults claimed by comments/report are not implemented/tested. Specify supported conversions/fallbacks, preserve original file and future schema; don't overwrite foreign schema with2 without an explicit migration policy.

## Other corrections
- Missing Node report arg and duplicate JSON serialisation are NOT the only remaining defects.
- Early _Ready hook with return is the right place, but remove unreachable duplicate later hook. Ensure exceptions and exit code are captured and test only boot accesses no ordinary UI/assets/settings first.
- Old41656712889 proves only that run's source/config compilation, not current uncommitted fixture.
- A2C quantitative rejection is provisional visual evidence, not proof geometry rootcause. Machine screening isn't manual review.

## Answers / acceptance bar
Use separate fresh boots for legacy, foreign schema, corrupt syntax and malformed values. Normal atomic-save tests can share a boot if state setup is explicit; failure scenarios need isolated storage and real production save path. One workflow may orchestrate ALL scenarios: separate dispatch per corrupt case is unnecessary if all boots, nonzero assertions, reports and gates are present. Require Linux AND Windows replacement/failure behaviour before Windows persistence acceptance; Linux-only green does not certify Windows atomic replacement. No magic number of green runs substitutes for these requirements.
After relevant fixture passes at exact new commit and reviewed reports prove coverage, proceed directly to referencecar/track repair. Do not run more fence workflows to test Settings. Preserve previous failures, retry once after distinct fix, full15stage scope remains.
