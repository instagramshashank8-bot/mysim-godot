# verification agent report — 2026-11-04

Role: verification/persistence specialist. Branch `mistral/verification` (worktree `C:/Users/shash/MySimGodot-wt-verify`), parent `c87ae00`. Only owned files touched: `src/SettingsPersistChecks.cs`, `outputs/mistral-orchestration/REVIEW_PACKET.md`, `outputs/mistral-orchestration/REVIEW_REQUEST.json`, this report. `src/Settings.cs` and `src/SettingsPersistChecksNode.cs` deliberately unchanged (no change was needed for Windows support; production logic is platform-neutral by design).

## 1. What changed

### src/SettingsPersistChecks.cs (the only code change)
1. **Fixed two vacuous assertions (corrects the record for run 41710482854@dfe0ed3, 28/28).** In the `failures` scenario, `temp_live_kept` and `rename_live_kept` read the un-globalized constant `user://settings.cfg` via `System.IO.File` (lines 66/72/78 before this change; introduced when `c1701d9`/`dfe0ed3` globalized reads — this call site was missed). Both reads threw, `Read()` returned `""`, and the assertions compared `"" == ""` — the same tautology class the reviewer flagged at `9bed931`, surviving in a new form. They now read the globalized path and compare real pre-captured bytes, so they can genuinely fail. The other 26 assertions of the 28 were verified real (return values, globalized `File.Exists`/absence checks, reloaded-content reads, `corrupt_file_intact` byte equality). **A Linux re-run is required to re-prove the strengthened 28; until then read "28/28 at dfe0ed3" as "26 real + 2 vacuous".** No assertion was weakened anywhere.
2. **Windows-compatible temp-write fault (was the POSIX-only blocker).** Linux keeps the proven read-only-directory mechanism (`File.SetUnixFileMode` is POSIX-only). Every other OS (incl. Windows) now occupies the tmp path (`settings.cfg.tmp`) with a directory, which makes `cf.Save(tmp)` fail identically (open-for-write on a directory fails on Win32 and POSIX alike). The blocker is created after a stale-tmp cleanup and removed before the `temp_*` assertions, so `temp_failure_rejected` / `temp_no_tmp` / `temp_live_kept` hold with identical semantics on both platforms.
3. **Windows-only real contention fault (new, 3 cases).** The live file is held open with `FileShare.None` while `Settings.Set("sharing-violation",8)` runs. Verified against Godot 4.7.2 source (`godotengine/godot` `drivers/windows/dir_access_windows.cpp` @ tag `4.7.2-stable`, fetched this session): Windows `DirAccess.Rename` onto an existing target is delete-then-rename (`if (file_exists(new_path)) { if (remove(new_path) != OK) return FAILED; }` then `MoveFileW`), so the exclusive handle (no `FILE_SHARE_DELETE`) makes the delete step fail with a sharing violation → save rejected, tmp recovery copy kept (tmp write is unaffected by the lock). Live-content equality is asserted AFTER the lock is released so the comparison reads real bytes — this specifically also catches "deleted but not renamed", the non-atomic hazard of that Windows path. Cases: `win_sharing_violation_rejected`, `win_sharing_violation_tmp_kept`, `win_sharing_violation_live_kept`. This fault class cannot exist on POSIX (POSIX rename over an open file always succeeds), which is why it is Windows-only rather than a port of the Linux chmod.
4. **Contract-sync regression guard (task 3), in-fixture so no foreign file is touched.** Every scenario now ends with `contract_sync_case_names`: (a) the emitted case names must equal the fixture's per-platform contract exactly (base 28 + Windows extras; also catches duplicate emission); (b) on repo runs (`res://tools/cloud_game_audit.py` present — packaged exports do not contain it) the audit's `required_cases[scenario]` must equal the 28-name base contract in BOTH directions, parsed from the actual python source. Drift in either file fails the run. On packaged runs the audit comparison is skipped and recorded in the new report-JSON `contractSync` field (`{"checked":false,...}`) — visible, never a silent pass. The JSON shape change is additive; `cloud_game_audit.py` reads only `passed`/`failed`/`cases`/`failures`.

### outputs/mistral-orchestration/REVIEW_PACKET.md (rewritten per the 2026-11-04 review directive)
New authoritative **CURRENT STATUS — 2026-11-04** section: Linux verified at dfe0ed3/41710482854 with the vacuous-assertion correction stated plainly; Windows explicitly OPEN with the readiness + plan + risk; reference run 41788844369@e41e29b recorded as capture-success with visual acceptance NOT granted; exact fixture contract (28 base names + Windows extras + contract-sync). The entire 2026-11-03T~18:40Z snapshot is preserved verbatim under `## HISTORY`, in a four-backtick fenced block, with an erratum note naming which of its claims are obsolete (it still said "compile FAILED / no successful run / HEAD 9bed931" while the branch tip title claimed 28/28 — the contradiction the review flagged).

### outputs/mistral-orchestration/REVIEW_REQUEST.json (rewritten)
Now reflects the true state: branch/parent, verification state per platform (including the vacuous-assertion caveat and the Windows risk), full run table (5 preserved failures + 2 successes), proposed dispatches, changed files, open questions.

## 2. Windows verification design (task 1 deliverable)

Minimal Windows-side assertion set — no new C# surface needed; the fixture already branches per OS:

| Assertions on Windows | Mechanism |
|---|---|
| Atomic save + reload correctness on real `%APPDATA%\Godot\app_userdata\MySim\settings.cfg` | existing `legacy` scenario, unchanged |
| Legacy/foreign/corrupt/malformed on Windows paths | existing scenarios, unchanged (all file access already goes through `ProjectSettings.GlobalizePath`) |
| Temp-write failure → reject, no stray tmp, live intact | NEW: directory occupies the tmp path |
| Rename failure → reject, tmp recovery kept, live intact | existing env seam `MYSIM_PERSIST_FAULT=rename` (OS-neutral) |
| Real Windows sharing violation → reject, tmp kept, live bytes intact (incl. not deleted) | NEW: `FileShare.None` handle on the live file |
| Typed defaults on corrupt/malformed input | existing scenarios |

**Where it runs (proposal):** one new step in `windows-build.yml` on `windows-latest`, after `Export current source and run the native player` (so the packaged `MySim.exe` exists), running the PACKAGED player — a real Windows settings touch, not an editor harness — once per scenario with the user-data directory deleted between scenarios (the Windows equivalent of the audit's per-scenario `HOME` swap). Driver script proposed below (coordinator-owned file; mirrors `cloud_windows_smoke.py` conventions: headless, `CREATE_NO_WINDOW`, per-scenario logs, results JSON, non-zero exit on failure).

### Proposed new file: tools/cloud_windows_persist.py (coordinator-owned — provided for verbatim adoption)

```python
import subprocess,os,pathlib,sys,json,shutil
root=pathlib.Path.cwd();exe=root/'build-cloud/MySim.exe'
scenarios=('legacy','foreign','corrupt','malformed','failures')
# Keep in sync with cloud_game_audit.py required_cases; the fixture's contract_sync_case_names enforces this on repo runs.
required={'legacy':('legacy_seed_save','legacy_readback','legacy_get_did_not_write_disk','legacy_set_stamps','legacy_marker_and_values_persisted','atomic_replace_saved','atomic_replace_final_bytes'),
 'foreign':('foreign_seed_save','foreign_readback','foreign_set_succeeds','foreign_marker_kept'),
 'corrupt':('corrupt_defaults_typed','corrupt_no_crash','corrupt_no_value_resurrection','corrupt_file_intact','corrupt_load_error_evidence'),
 'malformed':('malformed_seed_save','malformed_defaults_typed','malformed_disk_untouched_by_get','malformed_set_migrates_marker'),
 'failures':('baseline_saved','temp_failure_rejected','temp_no_tmp','temp_live_kept','rename_fault_rejected','rename_tmp_recovery_kept','rename_live_kept','atomic_success_after_faults')}
udir=pathlib.Path(os.environ['APPDATA'])/'Godot'/'app_userdata'/'MySim'
out=root/'build-cloud';reports=[]
for scenario in scenarios:
 if udir.exists():shutil.rmtree(udir) # fresh user data per scenario: the Windows equivalent of the audit's per-scenario HOME swap
 rep=out/f'windows-settings-persist-{scenario}.json'
 env={k:v for k,v in os.environ.items() if not k.startswith('MYSIM_')}
 env.update(MYSIM_SETTINGS_PERSIST='1',MYSIM_SETTINGSPERSIST_SCENARIO=scenario,MYSIM_SETTINGS_REPORT=str(rep))
 stdout=root/f'windows-settings-persist-{scenario}-stdout.log';engine=root/f'windows-settings-persist-{scenario}-engine.log'
 with stdout.open('w') as f:
  p=subprocess.run([str(exe),'--headless','--fixed-fps','60','--log-file',str(engine)],env=env,stdout=f,stderr=subprocess.STDOUT,timeout=300,creationflags=subprocess.CREATE_NO_WINDOW)
 text=stdout.read_text(errors='replace')+'\n'+(engine.read_text(errors='replace') if engine.exists() else '')
 (root/f'windows-settings-persist-{scenario}.log').write_text(text)
 data=None
 try:data=json.loads(rep.read_text()) if rep.exists() else None
 except Exception:data=None
 got=(data or {}).get('cases') or []
 missing=[c for c in required[scenario] if not any(case.startswith(c) for case in got)]
 ok=data is not None and data.get('failed')==0 and data.get('passed',0)>0 and not missing and p.returncode==0 and 'ERROR:' not in text
 reports.append({'scenario':scenario,'exit':p.returncode,'passed':(data or {}).get('passed'),'failed':(data or {}).get('failed'),'missingCases':missing,'failures':(data or {}).get('failures',[]),'ok':ok})
 print(json.dumps(reports[-1]),flush=True)
(out/'windows-settings-persist-summary.json').write_text(json.dumps(reports,indent=2))
bad=[r for r in reports if not r['ok']]
if bad:sys.exit('windows settings persistence FAILED: '+json.dumps(bad))
print('COMPLETE windows settings persistence: '+str(len(reports))+'/'+str(len(reports))+' scenarios green (packaged Windows player, real %APPDATA% settings; runs ALL scenarios before judging so failures are not hidden by early exit)',flush=True)
```

### Proposed workflow step (coordinator-owned windows-build.yml)

Insert after the `Export current source and run the native player` step (before packaging, so a persistence failure blocks publication — coordinator's call, see open questions):

```yaml
      - name: Settings persistence assertions on the packaged Windows player
        run: python tools/cloud_windows_persist.py
```

and add `windows-settings-persist-*.json` to the `windows-build-diagnostics-*` artifact paths so failures are always preserved.

## 3. Evidence (all re-verified via `gh run view` this session)

| Run | Head | Verified result |
|---|---|---|
| 41710482854 | dfe0ed3 | success — settings_persist_review, 28/28 (26 real + 2 vacuous, fixed here) |
| 41788844369 | e41e29b | success — reference_evidence, 9 PNGs; visual acceptance NOT granted (the brief's "44788844369" is a typo: that ID returns HTTP 404) |
| 41707159183 | c1701d9 | failure (preserved) |
| 41703220126 | 1d6bd4d | failure (preserved) |
| 41699253306 | e1f6d21 | failure (preserved) |
| 41688779865 | 9bed931 | failure (preserved) |
| 41675023687 | 18f69a1 | failure (preserved) |

Primary sources read this session: `instructions.md` (§5-10), `VISUAL_REVIEW.md`, `feedback.md`, `CONTINUE.md` (tail), `TASK_BOARD.json`, `JOB_REGISTRY.json`, `REVIEW_PROTOCOL.md`, godot 4.7.2-stable `drivers/windows/dir_access_windows.cpp` (rename/remove semantics).

## 4. Proposed dispatch configs (coordinator executes; I dispatch nothing)

1. Linux re-proof of the strengthened fixture (also the first live run of the contract-sync guard):
```
gh workflow run game-review.yml -R instagramshashank8-bot/mysim-godot --ref <ref-containing-this-commit> -f depth=settings_persist_review
```
All other inputs default (matches the 41710482854 shape: cars 16, backend source, 120 Hz, rotational false; `settings_persist_review` maps to shard `[0]`).
PASS criteria: `settings-persist-summary.json` `all_ok=true`; per-scenario passed/failed 8/0, 5/0, 6/0, 5/0, 9/0 (28 base + 5 contract_sync on Linux repo runs); `contract_sync_case_names` present in every scenario with the audit-comparison detail; the 28 base case names unchanged.

2. Windows persistence verification (after the coordinator adds the proposed script + step and creates the publication tag):
```
gh workflow run windows-build.yml -R instagramshashank8-bot/mysim-godot --ref <ref-containing-this-commit-and-the-new-script> -f publication_tag=windows-candidate-<40-hex-of-that-ref>
```
PASS criteria: `windows-settings-persist-summary.json` all five scenarios ok (exit 0, passed>0, failed==0, no missing cases, no `ERROR:` lines); the `failures` scenario reports 31 assertions (28 base + 3 win) on Windows.

## 5. Remaining risks / open items

1. **Compile risk (honest):** no local build is permitted, so `SettingsPersistChecks.cs` changes are verified by careful review only (two defects were caught and fixed during that review: a paste artifact `\"places[]\"` and the C# keyword `checked` as an anonymous-member name, resolved with `@checked`). The dispatch in §4.1 is the compile gate.
2. **Windows atomicity:** Godot's Windows rename is delete-then-rename, so save-over-existing has a non-atomic window on Windows (crash between delete and rename loses the live file; tmp recovery copy survives). If the first Windows run fails `atomic_replace_*`/`foreign_marker_kept`, that is a REAL `Settings.Set` defect on Windows requiring an evidence-backed fix (e.g. `File.Replace`-based path), not a fixture weakening.
3. **Contract-sync parsing** depends on `cloud_game_audit.py` keeping the literal `required_cases={...}` block; if the coordinator moves/renames it, the fixture fails loudly with a pointer to update the check (intended forcing function; the file is not mine to edit).
4. **Packaged Windows runs** cannot compare against the audit source (no `.py` inside the export); there the fixture still enforces its own contract exactly and records `contractSync.checked=false` — documented scope, not a silent pass.
5. `Main.cs` contains a now-dead second `MYSIM_SETTINGS_PERSIST` hook further down `_Ready()` (unreachable after the early-exit guard). Not my file; flagged for the owner. No functional impact.
6. Reference visual acceptance remains open per `VISUAL_REVIEW.md` (other agents' files); nothing in this packet claims it.
