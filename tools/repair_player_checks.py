import os,pathlib,subprocess,shutil,json,sys
r=pathlib.Path.cwd();out=pathlib.Path('D:/MySim-builds/repair-20261029');binary=out/'MySim.exe'
si=subprocess.STARTUPINFO();si.dwFlags|=subprocess.STARTF_USESHOWWINDOW;si.wShowWindow=0
flags=subprocess.CREATE_NO_WINDOW|subprocess.BELOW_NORMAL_PRIORITY_CLASS
env=dict(os.environ,MYSIM_TRACK='24',MYSIM_CAR='16',MYSIM_LAPS='1',MYSIM_RIVALS='5',MYSIM_AI='1',MYSIM_AUTODRIVE='1',MYSIM_NOPIT='1',MYSIM_SECONDS='8',MYSIM_FPS='1',MYSIM_SHOT=str(r/'outputs/repair-20261029/player.png'))
with open('outputs/repair-20261029/player.log','w') as f:
 p=subprocess.run([str(binary),'--resolution','960x540','--no-window','--fixed-fps','30','--audio-driver','Dummy','--write-movie',str(out/'drive.avi')],env=env,stdout=f,stderr=subprocess.STDOUT,timeout=180,startupinfo=si,creationflags=flags)
print('player exit',p.returncode)
if p.returncode:sys.exit(p.returncode)
ff=shutil.which('ffmpeg');mp4=r/'outputs/repair-20261029/drive.mp4'
p=subprocess.run([ff,'-y','-i',str(out/'drive.avi'),'-c:v','libx264','-preset','fast','-crf','22','-pix_fmt','yuv420p','-an',str(mp4)],stdout=subprocess.DEVNULL,stderr=subprocess.PIPE,startupinfo=si,creationflags=flags)
if p.returncode==0 and mp4.stat().st_size>10000:(out/'drive.avi').unlink();print('recording',mp4)
else:print(p.stderr.decode(errors='replace')[-1000:]);sys.exit(1)
