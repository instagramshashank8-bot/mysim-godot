import pathlib,struct,json
r=pathlib.Path.cwd(); rows=[]
for p in (r/'assets/RealRacing/Source').rglob('*variation.fbx'):
 d=p.read_bytes();version=struct.unpack_from('<I',d,23)[0];wide=version>=7500;fmt='<QQQB' if wide else '<IIIB';hs=struct.calcsize(fmt);nodes=[]
 def scan(pos,end):
  while pos+hs<=end:
   stop,count,propbytes,nlen=struct.unpack_from(fmt,d,pos)
   if stop==0:return
   if stop>len(d) or stop<=pos:raise ValueError(str(p))
   name=d[pos+hs:pos+hs+nlen].decode(errors='replace');nodes.append(name)
   child=pos+hs+nlen+propbytes
   if child<stop:scan(child,stop)
   pos=stop
 scan(27,len(d));rows.append({'path':p.relative_to(r).as_posix(),'bytes':len(d),'models':nodes.count('Model'),'geometry':nodes.count('Geometry')})
empty=[x for x in rows if x['models']==0 and x['geometry']==0]
pathlib.Path('outputs/repair-20261029/variation-audit.json').write_text(json.dumps({'all':rows,'empty':empty},indent=2))
print('variation files',len(rows),'empty',len(empty));print('\n'.join(x['path'] for x in empty))
