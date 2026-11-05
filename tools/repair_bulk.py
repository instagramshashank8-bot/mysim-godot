import pathlib,os,subprocess,json,time
from PIL import Image,ImageDraw
r=pathlib.Path.cwd();dest=r/'outputs/repair-20261029/tracks';dest.mkdir(exist_ok=True);g=(r/'.godotpath').read_text().strip();rows=[]
si=subprocess.STARTUPINFO();si.dwFlags|=subprocess.STARTF_USESHOWWINDOW;si.wShowWindow=0
names=[x['name'] for x in json.loads((r/'assets/RealRacing/TrackList.json').read_text(encoding='utf-8-sig'))['tracks']]
for t in range(60):
 env=dict(os.environ,MYSIM_TRACK=str(t),MYSIM_CAR='16',MYSIM_LAPS='1',MYSIM_RIVALS='0',MYSIM_AI='1',MYSIM_AUTODRIVE='1',MYSIM_NOPIT='1',MYSIM_SECONDS='1',MYSIM_SHOT=str(dest/f'{t:02}.png'))
 ok=False
 for attempt in (1,2):
  log=dest/f'{t:02}-attempt{attempt}.log'
  try:
   with log.open('w') as f:p=subprocess.run([g,'--path',str(r),'--resolution','640x360','--no-window','--fixed-fps','60','--audio-driver','Dummy'],env=env,stdout=f,stderr=subprocess.STDOUT,timeout=100,startupinfo=si,creationflags=subprocess.CREATE_NO_WINDOW|subprocess.BELOW_NORMAL_PRIORITY_CLASS)
   text=log.read_text(errors='replace');ok=p.returncode==0 and 'DRIVETEST' in text and 'onSurface=True' in text and 'ERROR:' not in text and (dest/f'{t:02}.png').exists()
  except subprocess.TimeoutExpired:ok=False
  if ok:break
 rows.append({'track':t,'name':names[t],'ok':ok,'attempts':attempt});(dest/'state.json').write_text(json.dumps({'status':'running','completed':len(rows),'rows':rows},indent=2));print('track',t,'OK' if ok else 'FAIL',flush=True)
canvas=Image.new('RGB',(320*6,200*10));d=ImageDraw.Draw(canvas)
for row in rows:
 t=row['track'];p=dest/f'{t:02}.png';x=t%6*320;y=t//6*200
 if p.exists():canvas.paste(Image.open(p).convert('RGB').resize((320,180)),(x,y))
 d.text((x+3,y+183),f"{t} {row['name'][:32]} {'OK' if row['ok'] else 'FAIL'}",fill='white' if row['ok'] else 'red')
canvas.save(dest/'mosaic.jpg');(dest/'state.json').write_text(json.dumps({'status':'complete','completed':60,'pass':sum(x['ok'] for x in rows),'rows':rows},indent=2));print('DONE',sum(x['ok'] for x in rows),'/60',flush=True)
