import os,pathlib,subprocess,json,sys,time
r=pathlib.Path.cwd();out=r/'outputs/cloud-game';out.mkdir(parents=True,exist_ok=True);g=os.environ['GODOT_BIN'];shard=int(os.environ.get('SHARD','0'));rows=[]
def run(label,values,args=None,timeout=300):
 env={k:v for k,v in os.environ.items() if not k.startswith('MYSIM_')};env.update(values);env['MYSIM_BINDING_AUDIT']=str(out/label);log=out/(label+'.log')
 for attempt in (1,2):
  env['MYSIM_PROBE_ATTEMPT']=str(attempt)
  shot=pathlib.Path(values['MYSIM_SHOT']).resolve() if values.get('MYSIM_SHOT') else None
  if shot is not None:
   if not shot.is_relative_to(out):raise ValueError('Screenshot must stay in this run output directory')
   if shot.exists():shot.rename(shot.with_name(shot.stem+f'-previous-attempt{attempt}'+shot.suffix))
  started=time.monotonic();timed_out=False
  with (out/(label+f'-{attempt}.log')).open('w') as f:
   try:p=subprocess.run([g,'--path',str(r),'--resolution','960x540','--no-window','--fixed-fps','60','--audio-driver','Dummy']+(args or []),env=env,stdout=f,stderr=subprocess.STDOUT,timeout=timeout);code=p.returncode
   except subprocess.TimeoutExpired:code=-1;timed_out=True
  text=(out/(label+f'-{attempt}.log')).read_text(errors='replace')
  (out/(label+f'-{attempt}-process.json')).write_text(json.dumps({'returncode':code,'timed_out':timed_out,'elapsed_seconds':time.monotonic()-started,'timeout_seconds':timeout,'screenshot_exists':shot.exists() if shot else None,'error_lines':[line for line in text.splitlines() if 'ERROR:' in line]},indent=2))
  if code==0 and 'ERROR:' not in text:return text
 raise RuntimeError(label+' failed; see saved logs')
if os.environ.get('GROUND_REVIEW')=='1':
 track=(0,5)[shard]
 for authored in (0,1):
  label=f'ground-track-{track:02}-authored-{authored}';shot=out/(label+'.png')
  run(label,{'MYSIM_TRACK':str(track),'MYSIM_CAR':'16','MYSIM_TOD':'0','MYSIM_WET':'0','MYSIM_AI':'1','MYSIM_AUTODRIVE':'1','MYSIM_NOPIT':'1','MYSIM_RIVALS':'0','MYSIM_SECONDS':'3','MYSIM_AUTHORED_GROUND':str(authored),'MYSIM_SHOT':str(shot)})
  assert shot.exists()
 print('COMPLETE matched authored/current surface comparison; no production acceptance',flush=True);sys.exit(0)
if os.environ.get('RAIN_FOCUS')=='1':
 for car in (6,16):
  for tod,wet in ((0,2),(3,0),(3,3)):
   label=f'rain-focus-track-00-car-{car:02}-tod-{tod}-wet-{wet}';shot=out/(label+'.png')
   text=run(label,{'MYSIM_CAMERA_CYCLE_CHECK':'1','MYSIM_CAM':'0','MYSIM_TRACK':'0','MYSIM_CAR':str(car),'MYSIM_TOD':str(tod),'MYSIM_WET':str(wet),'MYSIM_AI':'1','MYSIM_AUTODRIVE':'1','MYSIM_NOPIT':'1','MYSIM_RIVALS':'0','MYSIM_SECONDS':'8','MYSIM_SHOT':str(shot)})
   assert 'PASS full camera cycle restores all four wheels' in text
   assert shot.exists()
 print('COMPLETE first-track rain/light comparison; no full weather or track acceptance',flush=True);sys.exit(0)
if os.environ.get('REAR_PANEL_UV')=='1':
 for view,camera in enumerate(('4,1,0','0,1.15,-6')):
  for node in ('baseline','LOD_A_BODY_mm_ext','LOD_A_BODY_mm_misc','LOD_A_REARBUMPER_mm_ext','LOD_A_REARBUMPER_mm_misc'):
   label=f'rear-panel-{view}-{node}';shot=out/(label+'.png');values={'MYSIM_CARVIEW':'6','MYSIM_FITVIEW':'1','MYSIM_CAMPOS':camera,'MYSIM_SHOT':str(shot)}
   if node!='baseline':values.update(MYSIM_CAR_UV='v',MYSIM_CAR_UV_NODE=node)
   text=run(label,values);assert shot.exists() and 'PASS complete visible car framing' in text
 print('COMPLETE isolated rear panel material comparison; no production repair accepted',flush=True);sys.exit(0)
if os.environ.get('LIVERY_UV')=='1':
 for car in (0,6,12,18):
  for view,camera in enumerate(('4,1,0','0,1.15,-6')):
   for mode in ('baseline','none','u','v'):
    label=f'livery-car-{car:02}-view-{view}-{mode}';shot=out/(label+'.png');values={'MYSIM_CARVIEW':str(car),'MYSIM_FITVIEW':'1','MYSIM_CAMPOS':camera,'MYSIM_SHOT':str(shot)}
    if mode!='baseline':values.update(MYSIM_CAR_UV=mode,MYSIM_CAR_UV_SCOPE='livery')
    text=run(label,values);assert shot.exists() and 'PASS complete visible car framing' in text
 print('COMPLETE isolated livery UV comparison; production unchanged',flush=True);sys.exit(0)
if os.environ.get('GUI_REVIEW')=='1':
 for resolution in ('960x540','1280x720','1600x900'):
  label='touch-'+resolution;shot=out/(label+'.png')
  text=run(label,{'MYSIM_TOUCHCHECKS':'1','MYSIM_TOUCH':'1','MYSIM_SHOT':str(shot)},['--resolution',resolution]);assert 'PASS independent touch fingers and focus clearing' in text
 for resolution in ('960x540','1280x720','1600x900'):
  label='settings-'+resolution;shot=out/(label+'.png')
  text=run(label,{'MYSIM_SETTINGSCHECKS':'1','MYSIM_SHOT':str(shot)},['--resolution',resolution]);assert 'PASS race settings real controls' in text and shot.exists()
 for resolution in ('960x540','1280x720','1600x900'):
  label='navigation-'+resolution;shot=out/(label+'.png')
  text=run(label,{'MYSIM_NAVCHECKS':'1','MYSIM_SHOT':str(shot)},['--resolution',resolution]);assert 'PASS mode-first navigation routing' in text
 print('COMPLETE actual settings signals and resolution captures; manual layout review separate',flush=True);sys.exit(0)
if os.environ.get('WEATHER_REVIEW')=='1':
 for track in range(shard,60,6):
  for tod,wet in ((0,0),(3,0),(0,2),(3,3),(0,4)):
   label=f'weather-track-{track:02}-tod-{tod}-wet-{wet}';shot=out/(label+'.png')
   run(label,{'MYSIM_TRACK':str(track),'MYSIM_CAR':'16','MYSIM_TOD':str(tod),'MYSIM_WET':str(wet),'MYSIM_AI':'1','MYSIM_AUTODRIVE':'1','MYSIM_NOPIT':'1','MYSIM_RIVALS':'0','MYSIM_SECONDS':'3','MYSIM_SHOT':str(shot)})
   assert shot.exists(),label+' missing render evidence'
 print('COMPLETE track lighting/weather captures; manual acceptance separate',flush=True);sys.exit(0)
if os.environ.get('ASSEMBLY_REVIEW')=='1':
 for car in range(shard,24,6):
  for view,camera in enumerate(('0,1.15,6','4,1,0','0,1.15,-6')):
   label=f'assembled-car-{car:02}-view-{view}';shot=out/(label+'.png')
   text=run(label,{'MYSIM_CARVIEW':str(car),'MYSIM_FITVIEW':'1','MYSIM_CAMPOS':camera,'MYSIM_SHOT':str(shot)});assert shot.exists() and 'CARMAP car=' in text
 print('COMPLETE assembled front/side/rear evidence; manual approval separate',flush=True);sys.exit(0)
if os.environ.get('WHEEL_COMPOUNDS')=='1':
 for car in (1,6,16):
  for compound in range(4):
   label=f'wheel-compound-car-{car:02}-{compound}';shot=out/(label+'.png')
   run(label,{'MYSIM_CARVIEW':str(car),'MYSIM_FITVIEW':'1','MYSIM_COMPOUND':str(compound),'MYSIM_CAMPOS':'4,1,0','MYSIM_SHOT':str(shot)});assert shot.exists()
 print('COMPLETE assembled wheel/compound captures; visual acceptance pending',flush=True);sys.exit(0)
if os.environ.get('CAR_PANEL_UV')=='1':
 for view,camera in enumerate(('5.2,1.8,5.6','5.2,1.5,-5.6')):
  for mode in ('baseline','hood'):
   label=f'panel-car-06-{view}-{mode}';shot=out/(label+'.png');values={'MYSIM_CARVIEW':'6','MYSIM_CAMPOS':camera,'MYSIM_SHOT':str(shot),'MYSIM_DISABLE_SURFACE_UV_REPAIRS':'1'}
   if mode=='hood':values.update(MYSIM_CAR_UV='v',MYSIM_CAR_UV_NODE='LOD_A_HOOD_mm_misc')
   run(label,values);assert shot.exists()
 print('COMPLETE single-panel UV comparison; production unchanged',flush=True);sys.exit(0)
if os.environ.get('CAR_UV_ROLES')=='1':
 for car in (1,6,16):
  for scope in ('baseline','misc','nonlivery'):
   label=f'uv-roles-car-{car:02}-{scope}';shot=out/(label+'.png');values={'MYSIM_CARVIEW':str(car),'MYSIM_CAMPOS':'5.2,1.8,5.6','MYSIM_SHOT':str(shot)}
   if scope!='baseline':values.update(MYSIM_CAR_UV='v',MYSIM_CAR_UV_SCOPE=scope)
   run(label,values);assert shot.exists()
 print('COMPLETE material-role UV comparison; production UV remains unchanged',flush=True);sys.exit(0)
if os.environ.get('CONTACT_TRACE')=='1' or os.environ.get('PRECISE_AI')=='1':
 import re
 for track in (43,15):
  label=f'trace-track-{track:02}'
  try:
   text=run(label,{'MYSIM_TRACK':str(track),'MYSIM_CAR':'16','MYSIM_LAPS':'1','MYSIM_AI':'1','MYSIM_AUTODRIVE':'1','MYSIM_NOPIT':'1','MYSIM_RIVALS':'0','MYSIM_SECONDS':'1000','MYSIM_CONTACT_TRACE':str(out/(label+'-contacts.jsonl')),'MYSIM_PRECISE_AI':os.environ.get('AI_MODE','1') if os.environ.get('PRECISE_AI')=='1' else '0'},['--headless'],timeout=1800)
   finish=re.search(r'FINISHED.*best=([0-9.]+).*wallHits=([0-9]+).*damage=([0-9.]+)',text);rows.append({'track':track,'ok':bool(finish) and float(finish[1])>0 and int(finish[2])==0 and float(finish[3])==0,'finish':finish[0] if finish else None})
  except RuntimeError as error:rows.append({'track':track,'ok':False,'error':str(error)})
  (out/'contact-trace-status.json').write_text(json.dumps(rows,indent=2))
 assert all(row['ok'] for row in rows)
 print('PASS targeted native scene regression',flush=True);sys.exit(0)
if os.environ.get('WHEEL_ORIENTATION')=='1':
 for car in (1,6,16):
  for mode in ('legacy','source'):
   label=f'wheel-facing-car-{car:02}-{mode}';shot=out/(label+'.png');run(label,{'MYSIM_CARVIEW':str(car),'MYSIM_CAMPOS':'6,1.2,0','MYSIM_WHEEL_MIRROR':mode,'MYSIM_SHOT':str(shot)});assert shot.exists()
 print('COMPLETE source/legacy wheel-facing comparison; production choice pending',flush=True);sys.exit(0)
if os.environ.get('NATIVE_RACES')=='1' or os.environ.get('NATIVE_FOCUS')=='1':
 import re
 cars=[int(x) for x in os.environ.get('NATIVE_CARS','16').split(',')];assert len(set(cars))==len(cars) and all(0<=car<24 for car in cars)
 caseSet=os.environ.get('NATIVE_CASE_SET','shutdown');assert caseSet in ('shutdown','bridge','monaco_ai3','ai4_regression','braking_monaco')
 focusPath={'bridge':'data/native_bridge_regression.json','shutdown':'data/native_shutdown_recovery.json','monaco_ai3':'data/native_monaco_ai3.json','ai4_regression':'data/native_ai4_regression.json','braking_monaco':'data/native_braking_monaco.json'}[caseSet]
 focus=json.loads(pathlib.Path(focusPath).read_text())['cases'] if os.environ.get('NATIVE_FOCUS')=='1' else None
 for track in (sorted(set(x['track'] for x in focus)) if focus else range(shard,60,12)):
  for car in ([x['car'] for x in focus if x['track']==track] if focus else cars):
   label=f'native-car-{car:02}-track-{track:02}'
   try:
    text=run(label,{'MYSIM_TRACK':str(track),'MYSIM_CAR':str(car),'MYSIM_LAPS':'1','MYSIM_AI':'1','MYSIM_AUTODRIVE':'1','MYSIM_NOPIT':'1','MYSIM_RIVALS':'0','MYSIM_SECONDS':'1000','MYSIM_PRECISE_AI':'4' if focus and caseSet=='ai4_regression' else '3' if focus and caseSet=='monaco_ai3' else os.environ.get('NATIVE_AI_MODE','0'),'MYSIM_CONTACT_TRACE':str(out/(label+'-contacts.jsonl')) if focus else ''},['--headless'],timeout=1800)
    finished=re.search(r'FINISHED.*best=([0-9.]+).*wallHits=([0-9]+).*damage=([0-9.]+)',text)
    ok=bool(finished) and float(finished[1])>0 and int(finished[2])==0 and float(finished[3])==0
    row={'track':track,'car':car,'ok':ok,'finish':finished[0] if finished else None}
   except RuntimeError as e:row={'track':track,'car':car,'ok':False,'error':str(e)}
   row['scope']='native full-scene physics lap; no visual performance acceptance';rows.append(row);(out/'native-race-status.json').write_text(json.dumps(rows,indent=2))
 assert all(x['ok'] for x in rows)
 print('PASS native full-scene race shard (visual gates separate)',flush=True);sys.exit(0)
if os.environ.get('CAR_UV_REVIEW')=='1':
 for car in (1,6,16):
  for mode in ('both','none','u','v'):
   label=f'uv-car-{car:02}-{mode}';shot=out/(label+'.png');run(label,{'MYSIM_CARVIEW':str(car),'MYSIM_CAMPOS':'5.2,1.8,5.6','MYSIM_CAR_UV':mode,'MYSIM_SHOT':str(shot)});assert shot.exists()
 print('COMPLETE controlled car UV comparisons; production UV unchanged',flush=True);sys.exit(0)
if os.environ.get('INPUT_PHYSICS')=='1':
 text=run('input-physics',{'MYSIM_TRACK':'24','MYSIM_CAR':'16','MYSIM_RIVALS':'0','MYSIM_INPUTCHECKS':'1','MYSIM_TOUCH':'1'},['--headless'],timeout=300)
 assert 'PASS playable input' in text and 'SCREENSHOT_SKIPPED headless input logic only' in text
 print('PASS native game input/physics (headless; visual timeout remains unresolved)',flush=True);sys.exit(0)
if os.environ.get('CAR_MATERIAL_REVIEW')=='1':
 for car in (1,6,16):
  for view,camera in enumerate(('5.2,1.8,5.6','6,1.2,0','5.2,1.5,-5.6')):
   label=f'wheel-material-car-{car:02}-view-{view}';shot=out/(label+'.png');run(label,{'MYSIM_CARVIEW':str(car),'MYSIM_FITVIEW':'1','MYSIM_CAMPOS':camera,'MYSIM_WHEELDBG':'1','MYSIM_SHOT':str(shot)});assert shot.exists()
 print('COMPLETE authored wheel material capture; visual acceptance pending',flush=True);sys.exit(0)
if os.environ.get('COLLISION_PROBE')=='1':
 # Reproduce a heavily flagged layout with fresh source collision, not the obsolete campaign bins.
 track=48;track_list=json.loads((r/'assets/RealRacing/TrackList.json').read_text())['tracks'];map_name=track_list[track]['map']
 folder=out/'fresh-collision';folder.mkdir(exist_ok=True);baked=folder/('col_'+map_name+'.bin')
 text=run('collision-bake',{'MYSIM_BAKECOL':str(track),'MYSIM_BAKEOUT':str(baked)},['--headless'],timeout=600);assert 'BAKECOL map=' in text and baked.exists()
 import hashlib,struct,collections
 data=baked.read_bytes();triangles=struct.unpack('<i',data[:4])[0];assert triangles>0 and len(data)==4+triangles*36
 report={'track':track,'triangles':triangles,'collision_sha256':hashlib.sha256(data).hexdigest(),'scope':'fresh live/baked diagnostic comparison, not game acceptance','results':{}}
 for mode in ('live','baked'):
  result=out/('crash-'+mode+'.jsonl');values={'MYSIM_CRASHMAP':str(track),'MYSIM_CARS':'16','MYSIM_CRASHPTS':'8','MYSIM_ONLYFLAGGED':'1','MYSIM_CRASHOUT':str(result)}
  if mode=='baked':values['MYSIM_COLDIR']=str(folder)
  text=run('collision-'+mode,values,['--headless'],timeout=1800);assert 'CRASHBATCH track=' in text and result.exists()
  records=[json.loads(line) for line in result.read_text().splitlines()];summaries=[x for x in records if x.get('summary')];assert len(summaries)==1 and summaries[0]['crashes']>0
  flags=collections.Counter(flag for row in records if not row.get('summary') for flag in row['flags']);report['results'][mode]={'summary':summaries[0],'flags':dict(flags)}
  (out/'collision-comparison.json').write_text(json.dumps(report,indent=2))
 # Flags remain visible and require diagnosis; running a diagnostic is never quality acceptance.
 assert report['results']['live']['summary']['crashes']==report['results']['baked']['summary']['crashes']
 print('COMPLETE collision diagnostics; inspect flags and live/baked parity',flush=True);sys.exit(0)
if os.environ.get('SURFACE_REVIEW')=='1':
 for track in (5,23,59):
  for authored,shadows in ((False,True),(True,True),(False,False),(True,False)):
   label=f'surface-{track:02}-authored-{int(authored)}-shadows-{int(shadows)}';shot=out/(label+'.png')
   values={'MYSIM_TRACK':str(track),'MYSIM_CAR':'16','MYSIM_AI':'1','MYSIM_AUTODRIVE':'1','MYSIM_NOPIT':'1','MYSIM_RIVALS':'0','MYSIM_SECONDS':'2','MYSIM_AUTHORED_GROUND':str(int(authored)),'MYSIM_SHOT':str(shot)}
   if not shadows:values['MYSIM_NOSHADOW']='1'
   text=run(label,values,timeout=300);assert shot.exists() and 'onSurface=True' in text
 print('PASS surface comparison capture (visual acceptance pending)',flush=True);sys.exit(0)
if shard==0:
 text=run('geometry',{'MYSIM_RUNTIME_CHECKS':'1','MYSIM_GEOMETRYOUT':str(out/'geometry.json')},['--headless']);assert 'PASS runtime' in text
if os.environ.get('GEOMETRY_ONLY')=='1':
 print('PASS isolated full-car geometry validation',flush=True);sys.exit(0)
for t in range(shard,60,6):
 label=f'track-{t:02}';shot=out/(label+'.png');full=os.environ.get('FULL_RACES')=='1'
 text=run(label,{'MYSIM_TRACK':str(t),'MYSIM_CAR':'16','MYSIM_LAPS':'1','MYSIM_AI':'1','MYSIM_AUTODRIVE':'1','MYSIM_NOPIT':'1','MYSIM_RIVALS':'0','MYSIM_SECONDS':'1000' if full else '2','MYSIM_SHOT':str(shot)},timeout=1800 if full else 120)
 import re
 finished=re.search(r'FINISHED.*best=([0-9.]+).*wallHits=([0-9]+).*damage=([0-9.]+)',text)
 ok=shot.exists() and (bool(finished) and float(finished[1])>0 and int(finished[2])==0 and float(finished[3])==0 if full else 'onSurface=True' in text)
 rows.append({'track':t,'scope':'full live-collision lap' if full else 'startup render probe','ok':ok,'finish':finished[0] if finished else None});(out/'track-status.json').write_text(json.dumps(rows,indent=2))

if shard<3 and os.environ.get('FULL_RACES')!='1':
 camera=['5.2,1.8,5.6','6,1.2,0','5.2,1.5,-5.6'][shard]
 for car in range(24):
  label=f'car-{car:02}-view-{shard}';shot=out/(label+'.png');run(label,{'MYSIM_CARVIEW':str(car),'MYSIM_FITVIEW':'1','MYSIM_CAMPOS':camera,'MYSIM_SHOT':str(shot)});assert shot.exists()
if shard==0 and os.environ.get('FULL_RACES')!='1':
 text=run('controls-dialog',{'MYSIM_CONTROLCHECKS':'1','MYSIM_SHOT':str(out/'controls-dialog.png')});assert 'PASS controls dialog' in text
 for resolution in ('960x540','1600x900'):
  label='menu-'+resolution
  text=run(label,{'MYSIM_MENUSHOT':str(out/(label+'.png'))},['--resolution',resolution]);assert (out/(label+'.png')).exists()
 text=run('input-controls',{'MYSIM_TRACK':'24','MYSIM_CAR':'16','MYSIM_RIVALS':'0','MYSIM_INPUTCHECKS':'1','MYSIM_TOUCH':'1','MYSIM_SHOT':str(out/'input-controls.png')});assert 'PASS playable input' in text
 for track in (0,4,24):
  for mode in ('0','1'):
   label=f'ground-{track:02}-authored-{mode}';run(label,{'MYSIM_TRACK':str(track),'MYSIM_CAR':'16','MYSIM_AI':'1','MYSIM_AUTODRIVE':'1','MYSIM_NOPIT':'1','MYSIM_RIVALS':'0','MYSIM_SECONDS':'8','MYSIM_AUTHORED_GROUND':mode,'MYSIM_SHOT':str(out/(label+'.png'))})
 for bias in ('0.2','1.0'):
  label='shadow-normal-bias-'+bias;run(label,{'MYSIM_TRACK':'4','MYSIM_CAR':'16','MYSIM_AI':'1','MYSIM_AUTODRIVE':'1','MYSIM_NOPIT':'1','MYSIM_RIVALS':'0','MYSIM_SECONDS':'2','MYSIM_SHADOW_NORMAL_BIAS':bias,'MYSIM_SHOT':str(out/(label+'.png'))})
 text=run('race-flow',{'MYSIM_TRACK':'24','MYSIM_CAR':'16','MYSIM_LAPS':'1','MYSIM_RIVALS':'5','MYSIM_AI':'1','MYSIM_AUTODRIVE':'1','MYSIM_NOPIT':'1','MYSIM_SECONDS':'1000','MYSIM_FLOWCHECKS':'1'},timeout=1200);assert 'PASS restart to main menu' in text
 import shutil
 result=r/'outputs/repair-20261029/results.png'
 if result.exists():shutil.copyfile(result,out/'results.png')
from PIL import Image,ImageDraw
files=sorted(out.glob('*.png'));sheet=Image.new('RGB',(320*5,200*((len(files)+4)//5)));d=ImageDraw.Draw(sheet)
for i,p in enumerate(files):
 im=Image.open(p).convert('RGB');im.thumbnail((320,180));x=i%5*320;y=i//5*200;sheet.paste(im,(x,y));d.text((x+3,y+182),p.stem,fill='white')
sheet.save(out/'mosaic.jpg',quality=90)
if not all(x['ok'] for x in rows):sys.exit(1)
