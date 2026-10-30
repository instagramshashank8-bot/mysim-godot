"""Checks source-route parity, NOT surveyed track accuracy or FBX handedness."""
import json,re,pathlib
root=pathlib.Path(__file__).resolve().parents[1]
source=(root/'src/Logic/SourceRoutes.cs').read_text();arrays=re.findall(r'new Vec\[\]\{(.*?)\}',source,re.S);tracks=json.loads((root/'assets/RealRacing/TrackList.json').read_text())['tracks'];assert len(arrays)==len(tracks)
rows=[]
for index,(array,track) in enumerate(zip(arrays,tracks)):
 points=[(float(x.rstrip('f')),float(z.rstrip('f'))) for x,z in re.findall(r'new Vec\(([^,]+),([^\)]+)\)',array)];original=[(p['x'],p['z']) for p in track['centrePoints']]
 exact=len(points)==len(original) and all(abs(a[0]-b[0])<.0001 and abs(a[1]-b[1])<.0001 for a,b in zip(points,original))
 rows.append({'track':index,'name':track['name'],'source_order_and_xz_preserved':exact,'points':len(points),'source_points':len(original)})
result={'scope':'Runtime route arrays versus stored source metadata only; does not establish real-world direction, geographic north or mesh reflection','tracks':rows};print(json.dumps(result,indent=2));assert all(r['source_order_and_xz_preserved'] for r in rows),'Source route parity mismatch; investigate without flipping assets'
