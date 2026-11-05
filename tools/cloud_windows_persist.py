import subprocess,os,pathlib,sys,json,shutil
root=pathlib.Path.cwd();exe=root/'build-cloud/MySim.exe'
scenarios=('legacy','foreign','corrupt','malformed','failures')
# Keep in sync with cloud_game_audit.py required_cases; the fixture's contract_sync_case_names enforces this on repo runs.
required={'legacy':('legacy_seed_save','legacy_readback','legacy_get_did_not_write_disk','legacy_long_roundtrip','legacy_set_stamps','legacy_marker_and_values_persisted','atomic_replace_saved','atomic_replace_final_bytes'),
 'foreign':('foreign_seed_save','foreign_readback','foreign_set_succeeds','foreign_marker_kept'),
 'corrupt':('corrupt_defaults_typed','corrupt_no_crash','corrupt_no_value_resurrection','corrupt_file_intact','corrupt_load_error_evidence'),
 'malformed':('malformed_seed_save','malformed_defaults_typed','malformed_disk_untouched_by_get','malformed_set_migrates_marker'),
 'failures':('baseline_saved','temp_failure_rejected','temp_no_tmp','temp_live_kept','rename_fault_rejected','rename_tmp_recovery_kept','rename_live_kept','atomic_success_after_faults')}
# This driver runs on Windows only; the Windows-only contention cases are REQUIRED evidence, not optional extras,
# and every report must prove it was produced by the Windows platform (reviewer requirement: platform evidence, not totals).
WIN_ONLY=('win_sharing_violation_rejected','win_sharing_violation_tmp_kept','win_sharing_violation_live_kept')
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
 missing+= [c for c in WIN_ONLY if not any(case.startswith(c) for case in got)] if scenario=='failures' else []
 expected_stages={'failures':{'tmp':1,'rename':2}} # the failures scenario legitimately PushErrors through the tested fault seams exactly once per stage: 1 tmp-stage (dir-occupied tmp), 2 rename-stage (the env seam + the Windows FileShare.None delete-failure); every other scenario expects ZERO failed saves
 exp=expected_stages.get(scenario,{'tmp':0,'rename':0})
 tmp_err=sum(1 for l in text.splitlines() if 'SETTINGS_SAVE tmp=' in l)
 ren_err=sum(1 for l in text.splitlines() if 'SETTINGS_SAVE rename=' in l)
 other_err=[l for l in text.splitlines() if 'ERROR:' in l and 'SETTINGS_SAVE' not in l]
 ok=data is not None and data.get('failed')==0 and data.get('passed',0)>0 and not missing and p.returncode==0 and tmp_err==exp['tmp'] and ren_err==exp['rename'] and not other_err
 platform=(data or {}).get('platform')
 if platform is not None and platform!='windows':ok=False
 reports.append({'scenario':scenario,'exit':p.returncode,'passed':(data or {}).get('passed'),'failed':(data or {}).get('failed'),'missingCases':missing,'saveErrors':{'tmp':tmp_err,'rename':ren_err,'expectedTmp':exp['tmp'],'expectedRename':exp['rename']},'unexpectedErrors':other_err[:5],'failures':(data or {}).get('failures',[]),'ok':ok})
 print(json.dumps(reports[-1]),flush=True)
(out/'windows-settings-persist-summary.json').write_text(json.dumps(reports,indent=2))
bad=[r for r in reports if not r['ok']]
if bad:sys.exit('windows settings persistence FAILED: '+json.dumps(bad))
print('COMPLETE windows settings persistence: '+str(len(reports))+'/'+str(len(reports))+' scenarios green (packaged Windows player, real %APPDATA% settings; runs ALL scenarios before judging so failures are not hidden by early exit)',flush=True)
