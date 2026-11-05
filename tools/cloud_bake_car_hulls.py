"""Bake selected recovered collision resources on the runner and enforce all existing error/containment gates."""
import os,pathlib,json,subprocess
out=pathlib.Path('outputs/cloud-game');out.mkdir(parents=True,exist_ok=True)
shard=int(os.environ['HULL_REVIEW_SHARD']);depth=os.environ['HULL_REVIEW_DEPTH'];cars=[] if depth=='native_geometry_recovery' else [int(x) for x in os.environ.get('NATIVE_CARS','0,16').split(',')] if depth=='native_mounted_laps' else [shard*2,shard*2+1] if depth=='native_hull_matrix' else [[0,16][shard]]
if depth=='native_geometry_recovery':cars=sorted({x['car'] for x in json.loads(pathlib.Path(os.environ.get('GEOMETRY_CASE_MANIFEST','data/native_geometry_recovery.json')).read_text())['cases'] if x['track']%12==shard})
if depth=='driving_validation':cars=sorted({x['car'] for x in json.loads(pathlib.Path(os.environ.get('DRIVING_VALIDATION_MANIFEST','data/native_driving_validation_core.json')).read_text())['cases'] if x['track']%12==shard})
if depth=='native_hull_recovery':cars=sorted(set(x['car'] for x in json.loads(pathlib.Path('data/native_hull_recovery.json').read_text())['cases']))
if depth in ('native_traffic_lap','native_traffic_diagnostic','native_ccd_diagnostic'):cars=[[0,1],[16,17]][shard]
if depth=='native_ai_diagnostic':cars=[16]
if depth=='chiron_wing_rebuild':cars=[20]
assert cars and len(set(cars))==len(cars) and all(0<=car<24 for car in cars),'Invalid hull car scope'
env=dict(os.environ);env['MYSIM_SOURCE_AXLE_FIT']=os.environ.get('SOURCE_AXLE_FIT','0');env['MYSIM_NATIVE_HULL_BAKE']=','.join(map(str,cars));log=out/'hull-bake.log'
if os.environ.get('NATIVE_PIT_SERVICE')=='1' or depth in ('driving_validation','native_hull_recovery','native_hull_matrix','native_geometry_recovery','native_gt_pit_diagnostic','native_traffic_diagnostic','native_ccd_diagnostic','native_traffic_lap','native_mounted_gameplay','native_mounted_laps','native_retirement','native_mounted_visual','native_ai_diagnostic'):env.update(MYSIM_COMPONENT_MOUNTS='1')
with log.open('w') as stream:result=subprocess.run([os.environ['GODOT_BIN'],'--headless','--path','.'],env=env,stdout=stream,stderr=subprocess.STDOUT,timeout=1200)
text=log.read_text(errors='replace')
if result.returncode or 'ERROR:' in text:
 print('Native hull bake failed; preserved hull-bake.log',flush=True)
 for line in text.splitlines():
  if 'ERROR:' in line:print(line,flush=True)
 raise SystemExit(1)
rows=json.loads((out/'native-hull-summary.json').read_text());assert {x['car'] for x in rows}==set(cars) and all(x['sourceContainmentVerified'] and x['parts']>0 for x in rows)
for row in rows:print('SOURCE_HULL_BAKE car',row['car'],'parts',row['parts'],'paddedParts',len(row['conservativePaddedParts']),flush=True)

# Bounds-only evidence for intact steering-link assemblies; raw hull vertices remain encrypted.
for row in rows:
 for part in row['partBounds']:
  if 'STEERING_ARM' in part['part'].upper():
   print('STEERING_LINK_BOUNDS',json.dumps({'car':row['car'],'part':part,'wheelCentres':row['wheelCentres'],'wheelRadii':row['wheelRadii']}),flush=True)
