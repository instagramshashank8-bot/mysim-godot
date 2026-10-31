"""Bake selected recovered collision resources on the runner and enforce all existing error/containment gates."""
import os,pathlib,json,subprocess
out=pathlib.Path('outputs/cloud-game');out.mkdir(parents=True,exist_ok=True)
shard=int(os.environ['HULL_REVIEW_SHARD']);depth=os.environ['HULL_REVIEW_DEPTH'];cars=[0,16] if depth=='native_mounted_laps' else [shard*2,shard*2+1] if depth=='native_hull_matrix' else [[0,16][shard]]
if depth=='native_hull_recovery':cars=sorted(set(x['car'] for x in json.loads(pathlib.Path('data/native_hull_recovery.json').read_text())['cases']))
if depth=='native_traffic_lap':cars=[[0,1],[16,17]][shard]
env=dict(os.environ);env['MYSIM_NATIVE_HULL_BAKE']=','.join(map(str,cars));log=out/'hull-bake.log'
if depth in ('native_mounted_gameplay','native_mounted_laps'):env.update(MYSIM_COMPONENT_MOUNTS='1')
with log.open('w') as stream:result=subprocess.run([os.environ['GODOT_BIN'],'--headless','--path','.'],env=env,stdout=stream,stderr=subprocess.STDOUT,timeout=1200)
text=log.read_text(errors='replace')
if result.returncode or 'ERROR:' in text:
 print('Native hull bake failed; preserved hull-bake.log',flush=True)
 for line in text.splitlines():
  if 'ERROR:' in line:print(line,flush=True)
 raise SystemExit(1)
rows=json.loads((out/'native-hull-summary.json').read_text());assert {x['car'] for x in rows}==set(cars) and all(x['sourceContainmentVerified'] and x['parts']>0 for x in rows)
for row in rows:print('SOURCE_HULL_BAKE car',row['car'],'parts',row['parts'],'paddedParts',len(row['conservativePaddedParts']),flush=True)
