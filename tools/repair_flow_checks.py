import pathlib,subprocess,os
r=pathlib.Path.cwd();env=dict(os.environ,MYSIM_TRACK='24',MYSIM_CAR='16',MYSIM_LAPS='1',MYSIM_RIVALS='5',MYSIM_AI='1',MYSIM_AUTODRIVE='1',MYSIM_NOPIT='1',MYSIM_SECONDS='1000',MYSIM_FLOWCHECKS='1',MYSIM_FPS='1')
si=subprocess.STARTUPINFO();si.dwFlags|=subprocess.STARTF_USESHOWWINDOW;si.wShowWindow=0
with open('outputs/repair-20261029/flow.log','w') as f:
 p=subprocess.run([(r/'.godotpath').read_text().strip(),'--path',str(r),'--resolution','960x540','--no-window','--fixed-fps','60','--audio-driver','Dummy'],env=env,stdout=f,stderr=subprocess.STDOUT,timeout=240,startupinfo=si,creationflags=subprocess.CREATE_NO_WINDOW|subprocess.BELOW_NORMAL_PRIORITY_CLASS)
print('flow exit',p.returncode)
