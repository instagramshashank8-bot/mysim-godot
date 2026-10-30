"""Limit only isolated diagnostics' importer scope; full-game/Windows campaigns stay complete."""
import os,json,pathlib,re
root=pathlib.Path.cwd();depth=os.environ.get('IMPORT_REVIEW_DEPTH','');out=root/'outputs/cloud-game';out.mkdir(parents=True,exist_ok=True)
scopes={'wheel_compounds':([1,6,16],[]),'car_panel_uv':([6],[]),'car_uv_roles':([1,6,16],[]),'car_uv':([1,6,16],[]),'precise_ai':([16],[43,15]),'contact_trace':([16],[43,15]),'geometry':(list(range(24)),[])}
if depth=='assembly_review':scopes[depth]=(list(range(int(os.environ.get('SHARD','0')),24,6)),[])
if depth=='native_focus':
 caseSet=os.environ.get('IMPORT_CASE_SET','shutdown');assert caseSet in ('shutdown','bridge','monaco_ai3','ai4_regression')
 manifest={'bridge':'data/native_bridge_regression.json','shutdown':'data/native_shutdown_recovery.json','monaco_ai3':'data/native_monaco_ai3.json','ai4_regression':'data/native_ai4_regression.json'}[caseSet]
 cases=json.loads((root/manifest).read_text())['cases'];scopes[depth]=(sorted(set(x['car'] for x in cases)),sorted(set(x['track'] for x in cases)))
if depth not in scopes:
 print('Full source import retained for',depth);raise SystemExit(0)
cars,tracks=scopes[depth]
catalogSource=(root/'src/Logic/CarCatalog.cs').read_text()
catalog=re.findall(r'"([^"]+)"',re.search(r'Folders\s*=\s*\{(.*?)\};',catalogSource,re.S)[1])
keep={'common',*(catalog[i].casefold() for i in cars)};trackList=json.loads((root/'assets/RealRacing/TrackList.json').read_text())['tracks']
for i in tracks:
 track=trackList[i];keep.add(track['map'].casefold())
 for path in track['variationPaths']:
  marker='Assets/RealRacing/Source/'
  if marker in path:keep.add(path.split(marker,1)[1].split('/')[0].casefold())
source=root/'assets/RealRacing/Source';available={p.name.casefold() for p in source.iterdir() if p.is_dir()};missing=keep-available
if missing:raise ValueError('Diagnostic source folders missing: '+str(sorted(missing)))
skipped=[]
for folder in source.iterdir():
 if folder.is_dir() and folder.name.casefold() not in keep:
  marker=folder/'.gdignore'
  if marker.exists():raise ValueError('Unexpected pre-existing import exclusion: '+str(folder))
  marker.write_text('Temporary isolated-diagnostic import scope; original assets remain intact.\n');skipped.append(folder.name)
(out/'import-scope.json').write_text(json.dumps({'depth':depth,'cars':cars,'tracks':tracks,'retained_source_folders':sorted(keep),'excluded_unrelated_source_folders':sorted(skipped),'scope':'isolated diagnostic only; no all-assets acceptance'},indent=2))
print('Isolated import scope',depth,'retains',sorted(keep),'excludes unrelated folders',len(skipped))
