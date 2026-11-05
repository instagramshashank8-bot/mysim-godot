import subprocess,os,pathlib,sys,json
root=pathlib.Path.cwd();exe=root/'build-cloud/MySim.exe';reports=[]
for label,values,marker in [('contact',{'MYSIM_RUNTIME_CHECKS':'1','MYSIM_CONTACT_ONLY':'1'},'PASS contact checks'),('player',{'MYSIM_TRACK':'24','MYSIM_CAR':'16','MYSIM_RIVALS':'5','MYSIM_AI':'1','MYSIM_AUTODRIVE':'1','MYSIM_NOPIT':'1','MYSIM_SECONDS':'8'},'onSurface=True')]:
 env={k:v for k,v in os.environ.items() if not k.startswith('MYSIM_')};env.update(values)  # scrub inherited MYSIM_* so the smoke pass cannot re-run another step's scenario (Windows failures-scenario double-count fix)
 stdout=root/f'windows-{label}-stdout.log';engine=root/f'windows-{label}-engine.log'
 with stdout.open('w') as f:
  p=subprocess.run([str(exe),'--headless','--fixed-fps','60','--log-file',str(engine)],env=env,stdout=f,stderr=subprocess.STDOUT,timeout=600,creationflags=subprocess.CREATE_NO_WINDOW)
 text=stdout.read_text(errors='replace')+'\n'+(engine.read_text(errors='replace') if engine.exists() else '')
 (root/f'windows-{label}.log').write_text(text);ok=p.returncode==0 and marker in text and 'ERROR:' not in text
 reports.append({'test':label,'exit':p.returncode,'ok':ok,'marker':marker});print(json.dumps(reports[-1]),flush=True)
 if not ok:sys.exit(1)
(root/'windows-smoke-results.json').write_text(json.dumps(reports,indent=2))
