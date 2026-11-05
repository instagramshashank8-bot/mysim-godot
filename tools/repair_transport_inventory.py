import json,pathlib,collections
rows=json.load(open('outputs/repair-20261029/mesh-archive-intake.json'));missing=[];total=0;largest=0
for r in rows:
 p=pathlib.Path(r['zip_path'])
 try:n=p.stat().st_size;total+=n;largest=max(largest,n)
 except OSError:missing.append(str(p))
print(json.dumps({'archives':len(rows),'bytes':total,'GiB':round(total/2**30,2),'largestMiB':round(largest/2**20,2),'missing':len(missing)}));pathlib.Path('outputs/repair-20261029/mesh-transport-inventory.json').write_text(json.dumps({'archives':len(rows),'bytes':total,'missing':missing},indent=2))
