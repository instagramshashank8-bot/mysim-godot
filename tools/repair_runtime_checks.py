import os,subprocess,pathlib
root=pathlib.Path.cwd();g=(root/'.godotpath').read_text().strip();env=dict(os.environ,MYSIM_RUNTIME_CHECKS='1')
si=subprocess.STARTUPINFO();si.dwFlags|=subprocess.STARTF_USESHOWWINDOW;si.wShowWindow=0
with open('outputs/repair-20261029/runtime-checks.log','w') as f:
 p=subprocess.run([g,'--headless','--path',str(root)],env=env,stdout=f,stderr=subprocess.STDOUT,timeout=120,startupinfo=si,creationflags=subprocess.CREATE_NO_WINDOW)
print('runtime checks exit',p.returncode)
