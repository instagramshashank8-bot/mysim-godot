# User-path recording driver for the PACKAGED player (windows-build; per the verification agent's spec in
# agents/verification-turn8.md). Mirrors cloud_windows_smoke.py / cloud_windows_persist.py conventions:
# CREATE_NO_WINDOW, fresh %APPDATA% per run, report verification, non-zero exit on any failure, results JSON.
# Per profile it runs TWO passes and compares them (the determinism re-injection check from the spec):
#   pass 'script' = the profile's own event list (from data/userpath_profiles.json)
#   pass 'replay' = the RECORDED event stream of pass 1 (converted back to a script) - the telemetry of the two
#   passes must match within tolerance; a mismatch means the input path or the sim is not deterministic.
# Requires the Main.cs hook (MYSIM_USERPATH -> UserPathScenario{Drive}) - see agents/verification-turn9.md for the
# exact request INCLUDING the DrivingScenario guard on the MYSIM_INPUTCHECKS line; this driver fails with a clear
# message if the reports are missing so an unwired build cannot pass silently.
import subprocess,os,pathlib,sys,json,shutil
root=pathlib.Path.cwd();exe=root/'build-cloud/MySim.exe'
profiles=json.loads((root/'data/userpath_profiles.json').read_text())['profiles']
udir=pathlib.Path(os.environ['APPDATA'])/'Godot'/'app_userdata'/'MySim'
out=root/'build-cloud';out.mkdir(exist_ok=True)
TOL={'speed':0.15,'heading':0.03,'steering':0.02,'yawRate':0.05}
POS_TOL=0.6
def load(p):
 try:return json.loads(p.read_text()) if p.exists() else None
 except Exception:return None
def tele_diff(a,b):
 if not a or not b or not a.get('samples') or not b.get('samples'):return ['telemetry missing for determinism compare']
 sa,sb=a['samples'],b['samples'];bad=[]
 if abs(len(sa)-len(sb))>2:bad.append('sample count %d vs %d'%(len(sa),len(sb)))
 n=min(len(sa),len(sb))
 for k,tol in TOL.items():
  worst=max((abs(float(sa[i][k])-float(sb[i][k])) for i in range(n)),default=0.0)
  if worst>tol:bad.append('%s max delta %.4f > %s'%(k,worst,tol))
 dx=max((abs(float(sa[i]['x'])-float(sb[i]['x'])) for i in range(n)),default=0.0)
 dz=max((abs(float(sa[i]['z'])-float(sb[i]['z'])) for i in range(n)),default=0.0)
 if max(dx,dz)>0.6:bad.append('position max delta %.3f > %s'%(max(dx,dz),POS_TOL))
 return bad
results=[]
for p in profiles:
 pid=p['id'];telemetry={}
 for mode in ('script','replay'):
  if udir.exists():shutil.rmtree(udir) # fresh user data per run: deterministic default boot for BOTH passes
  if mode=='script':script=p
  else:
   prev=out/('userpath-%s-script-events.jsonl'%pid)
   rec=[json.loads(l) for l in prev.read_text().splitlines() if l.strip()]
   script={'profile':pid+'-replay','durationSeconds':p.get('durationSeconds',10),'startOnRaceStart':p.get('startOnRaceStart',True),
           'events':[{'t':e['t'],'device':e.get('device','key'),'key':e.get('key',''),'axis':e.get('axis',''),'action':e.get('action','press'),'value':e.get('value',0)} for e in rec],
           'asserts':p.get('asserts',[])}
  sp=out/('userpath-%s-%s-script.json'%(pid,mode));sp.write_text(json.dumps(script))
  env={k:v for k,v in os.environ.items() if not k.startswith('MYSIM_')}
  env.update(MYSIM_USERPATH=pid,MYSIM_INPUTCHECKS='1',MYSIM_RIVALS='0',MYSIM_NOPIT='1',
   MYSIM_USERPATH_SCRIPT=str(sp),MYSIM_USERPATH_REPORT=str(out/('userpath-%s-%s-report.json'%(pid,mode))),
   MYSIM_USERPATH_TELEMETRY=str(out/('userpath-%s-%s-telemetry.json'%(pid,mode))),
   MYSIM_USERPATH_EVENTS=str(out/('userpath-%s-%s-events.jsonl'%(pid,mode))))
  stdout=out/('userpath-%s-%s-stdout.log'%(pid,mode));engine=out/('userpath-%s-%s-engine.log'%(pid,mode))
  timed_out=False
  with stdout.open('w') as f:
   try:
    p2=subprocess.run([str(exe),'--rendering-method','gl_compatibility','--resolution','960x540','--fixed-fps','60','--audio-driver','Dummy','--log-file',str(engine)],env=env,stdout=f,stderr=subprocess.STDOUT,timeout=1200,creationflags=subprocess.CREATE_NO_WINDOW)
    code=p2.returncode
   except subprocess.TimeoutExpired:code=-1;timed_out=True
  text=stdout.read_text(errors='replace')+'\n'+(engine.read_text(errors='replace') if engine.exists() else '')
  data=load(out/('userpath-%s-%s-report.json'%(pid,mode)))
  ev=out/('userpath-%s-%s-events.jsonl'%(pid,mode))
  events_count=sum(1 for l in ev.read_text().splitlines() if l.strip()) if ev.exists() else 0
  ok=data is not None and data.get('failed')==0 and data.get('passed',0)>=1 and code==0 and not timed_out and events_count==len(script['events']) and 'ERROR:' not in text
  rec={'profile':pid,'pass':mode,'exit':code,'timedOut':timed_out,'passed':(data or {}).get('passed'),'failed':(data or {}).get('failed'),'eventsInjected':events_count,'eventsExpected':len(script['events']),'failures':(data or {}).get('failures',[]),'ok':ok}
  results.append(rec);print(json.dumps(rec),flush=True)
 te1=load(out/('userpath-%s-script-telemetry.json'%pid));te2=load(out/('userpath-%s-replay-telemetry.json'%pid))
 bad=tele_diff(te1,te2)
 rec={'profile':pid,'check':'determinism_reinjection','ok':len(bad)==0,'deviations':bad}
 results.append(rec);print(json.dumps(rec),flush=True)
summary={'runs':results,'all_ok':all(r['ok'] for r in results)}
(out/'userpath-summary.json').write_text(json.dumps(summary,indent=1))
if not summary['all_ok']:sys.exit('userpath FAILED: '+json.dumps([r for r in results if not r['ok']]))
print('COMPLETE user-path recording: %d/%d profiles green incl. determinism re-injection (packaged Windows player; real key events; not a physics calibration)'%(len(profiles),len(profiles)),flush=True)
