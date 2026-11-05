import os, subprocess, pathlib, json, time
root=pathlib.Path(__file__).resolve().parents[1]
os.chdir(root)
out=root/'outputs/repair-20261029'
g=(root/'.godotpath').read_text().strip()
env=dict(os.environ,MYSIM_TRACK='24',MYSIM_CAR='16',MYSIM_LAPS='1',MYSIM_RIVALS='5',MYSIM_AI='1',MYSIM_AUTODRIVE='1',MYSIM_NOPIT='1',MYSIM_SECONDS='18',MYSIM_FPS='1',MYSIM_TRACE='1',MYSIM_SHOT=str(out/'repaired.png'))
si=subprocess.STARTUPINFO();si.dwFlags|=subprocess.STARTF_USESHOWWINDOW;si.wShowWindow=0
with (out/'repaired.log').open('w') as log:
 p=subprocess.run([g,'--path',str(root),'--resolution','1280x720','--no-window','--audio-driver','Dummy'],env=env,stdout=log,stderr=subprocess.STDOUT,timeout=150,startupinfo=si,creationflags=subprocess.CREATE_NO_WINDOW|subprocess.BELOW_NORMAL_PRIORITY_CLASS)
print('baseline exit',p.returncode)
