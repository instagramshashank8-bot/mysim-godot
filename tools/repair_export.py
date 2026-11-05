import pathlib,subprocess,os,json,time
r=pathlib.Path.cwd();out=pathlib.Path('D:/MySim-builds/repair-20261029');out.mkdir(parents=True,exist_ok=True)
log=r/'outputs/repair-20261029/export.log';state=r/'outputs/repair-20261029/export-state.json'
state.write_text(json.dumps({'status':'running','output':str(out),'started':time.time()}))
si=subprocess.STARTUPINFO();si.dwFlags|=subprocess.STARTF_USESHOWWINDOW;si.wShowWindow=0
with log.open('w') as f:
 p=subprocess.run([(r/'.godotpath').read_text().strip(),'--headless','--path',str(r),'--export-release','Windows Desktop',str(out/'MySim.exe')],stdout=f,stderr=subprocess.STDOUT,timeout=2400,startupinfo=si,creationflags=subprocess.CREATE_NO_WINDOW|subprocess.BELOW_NORMAL_PRIORITY_CLASS)
state.write_text(json.dumps({'status':'exported' if p.returncode==0 else 'failed','exit':p.returncode,'output':str(out),'finished':time.time()}));print('export exit',p.returncode)
