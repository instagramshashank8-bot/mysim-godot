"""Check complete controlled braking coverage and timestep convergence, on cloud."""
import json,pathlib,math
rows=[r for p in pathlib.Path('.').glob('brake-results/**/braking-*.json') for r in json.loads(p.read_text())['results']]
assert len(rows)==1152,f'incomplete braking coverage: {len(rows)}/1152'
pairs={}
for row in rows:
 assert row['stopped'] and row['finite'],f'failed braking case: {row}'
 assert all(math.isfinite(row[k]) for k in ('seconds','metres','finalSpeed'))
 key=(row['car'],row['compound'],row['water'],row['initialKmh'])
 pair=pairs.setdefault(key,{})
 assert row['hz'] not in pair,'duplicate braking case'
 pair[row['hz']]=row
worst=0
for key,pair in pairs.items():
 assert set(pair)=={120,240},f'missing timestep: {key}'
 error=abs(pair[120]['metres']-pair[240]['metres'])/max(.01,pair[240]['metres'])
 worst=max(worst,error)
 assert error<.02,f'timestep-dependent braking: {key} error {error}'
pathlib.Path('braking-summary.json').write_text(json.dumps({'cases':len(rows),'failures':0,'worstRelativeDistanceDifference':worst,'scope':'flat uninterrupted road dynamics fixture; no native geometry or real-world validation'},indent=2))
print(f'PASS {len(rows)} controlled stops; maximum timestep distance difference {worst:.6%}')
