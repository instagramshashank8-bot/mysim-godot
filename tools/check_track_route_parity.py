"""Source parity only; no claim of surveyed geometry or FBX handedness.
Recovered route text retains seven significant digits on large coordinates.
The bound below follows that representation, rather than relaxing physics checks.
"""
import json,re,pathlib
from decimal import Decimal
root=pathlib.Path(__file__).resolve().parents[1]
source=(root/'src/Logic/SourceRoutes.cs').read_text();arrays=re.findall(r'new Vec\[\]\{(.*?)\}',source,re.S);tracks=json.loads((root/'assets/RealRacing/TrackList.json').read_text())['tracks'];assert len(arrays)==len(tracks)
def allowed(value):
 value=abs(Decimal(str(value)))
 return Decimal(0) if value==0 else Decimal(5).scaleb(value.adjusted()-7)
rows=[]
for index,(array,track) in enumerate(zip(arrays,tracks)):
 points=[(Decimal(x.rstrip('f')),Decimal(z.rstrip('f'))) for x,z in re.findall(r'new Vec\(([^,]+),([^\)]+)\)',array)];original=[(Decimal(str(p['x'])),Decimal(str(p['z']))) for p in track['centrePoints']]
 count_match=len(points)==len(original);exact=count_match and points==original;parity=count_match and all(abs(a[c]-b[c])<=allowed(b[c]) for a,b in zip(points,original) for c in (0,1));max_error=max((abs(a[c]-b[c]) for a,b in zip(points,original) for c in (0,1)),default=Decimal(0))
 rows.append({'track':index,'name':track['name'],'source_order_and_xz_preserved':parity,'exact_decimal_values':exact,'max_coordinate_text_rounding_error_metres':float(max_error),'points':len(points),'source_points':len(original)})
result={'scope':'Runtime arrays versus source metadata. Index order and coordinates checked within seven-significant-digit text representation. NOT proof of real-world direction, north or mesh reflection. No gameplay validation thresholds changed.','tracks':rows};print(json.dumps(result,indent=2));assert all(r['source_order_and_xz_preserved'] for r in rows),'Source parity mismatch beyond representation bound; investigate without flipping assets'
