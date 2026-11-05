import csv,collections,json,pathlib
p=pathlib.Path('D:/MySim-processed/final/manifest/selected_candidates.csv'); cats=collections.Counter();quality=collections.Counter();states=collections.Counter();zips={};n=0
for r in csv.DictReader(p.open(encoding='utf-8-sig',newline='')):
 n+=1;cats[r['category']]+=1;quality[r['quality_accepted']]+=1;states[r['zip_state']]+=1;zips[r['zip_path']]={'account':r['account'],'zip':r['zip'],'zip_path':r['zip_path']}
out={'selected':n,'quality_accepted':dict(quality),'zip_states':dict(states),'categories':dict(cats),'unique_zips':len(zips)}
pathlib.Path('outputs/repair-20261029/mesh-intake-summary.json').write_text(json.dumps(out,indent=2));pathlib.Path('outputs/repair-20261029/mesh-archive-intake.json').write_text(json.dumps(list(zips.values()),indent=2));print(json.dumps(out))
