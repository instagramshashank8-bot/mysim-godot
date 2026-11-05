import pathlib,os,subprocess
r=pathlib.Path.cwd();g=(r/'.godotpath').read_text().strip();env=dict(os.environ,MYSIM_TRACK='24',MYSIM_CAR='16',MYSIM_RIVALS='0',MYSIM_INPUTCHECKS='1',MYSIM_FPS='1',MYSIM_SHOT=str(r/'outputs/repair-20261029/input.png'))
si=subprocess.STARTUPINFO();si.dwFlags|=subprocess.STARTF_USESHOWWINDOW;si.wShowWindow=0
with open('outputs/repair-20261029/input.log','w') as f:
 p=subprocess.run([g,'--path',str(r),'--resolution','1280x720','--no-window','--audio-driver','Dummy'],env=env,stdout=f,stderr=subprocess.STDOUT,timeout=120,startupinfo=si,creationflags=subprocess.CREATE_NO_WINDOW)
print('input exit',p.returncode)
