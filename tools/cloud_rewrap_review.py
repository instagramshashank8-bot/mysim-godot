"""Runner-only authenticated streaming re-encryption; originals and intake key stay intact."""
import os,pathlib,json,subprocess,hashlib,tempfile,re
from cryptography.hazmat.primitives.ciphers import Cipher,algorithms,modes
names=os.environ['REVIEW_ASSETS'].split(',')
source_release=os.environ.get('REVIEW_SOURCE_RELEASE','repair-review-20261029')
if source_release.startswith('windows-candidate-'):
 import runpy
 runpy.run_path(str(pathlib.Path(__file__).with_name('cloud_rewrap_windows.py')),run_name='__main__')
 raise SystemExit(0)
if not re.fullmatch(r'repair-review-[0-9]+(?:-[0-9]+)?',source_release):raise ValueError('Invalid bounded source review release')
if not 1<=len(names)<=8 or len(set(names))!=len(names):raise ValueError('Select1to8distinct review assets')
if any(not re.fullmatch(r'(game-review-[0-9-]+|sheets-[a-zA-Z0-9_-]+)\.enc',n) for n in names):raise ValueError('Only named encrypted review evidence is allowed')
old=bytes.fromhex(os.environ['MYSIM_INTAKE_KEY']);new=bytes.fromhex(os.environ['MYSIM_REVIEW_KEY']);assert len(old)==len(new)==32 and old!=new
rows=[]
import datetime
release='repair-review-'+datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%d')+'-'+str(int(os.environ['GITHUB_RUN_ID'])//1000000)
repo=os.environ['GITHUB_REPOSITORY']
if subprocess.run(['gh','release','view',release,'--repo',repo],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL).returncode:
 subprocess.run(['gh','release','create',release,'--repo',repo,'--prerelease','--title','Encrypted MySim review evidence','--notes','Authenticated encrypted review evidence; originals preserved.'],check=True)
output=pathlib.Path('outputs/review-rewrap');output.mkdir(parents=True,exist_ok=True)
with tempfile.TemporaryDirectory() as temp:
 root=pathlib.Path(temp)
 for name in names:
  subprocess.run(['gh','release','download',source_release,'--repo',os.environ['GITHUB_REPOSITORY'],'--pattern',name,'--dir',temp],check=True)
  source=root/name;target=output/('reviewkey-'+name);digest=hashlib.sha256()
  authenticated=False
  try:
   with source.open('rb') as reader,target.open('wb') as writer:
    if reader.read(9)!=b'MYSIMENC1':raise ValueError('Invalid review envelope')
    nonce=reader.read(12);reader.seek(-16,2);tag=reader.read(16);end=reader.tell()-16;reader.seek(21)
    dec=Cipher(algorithms.AES(old),modes.GCM(nonce,tag)).decryptor();newnonce=os.urandom(12);enc=Cipher(algorithms.AES(new),modes.GCM(newnonce)).encryptor();writer.write(b'MYSIMENC1'+newnonce)
    while reader.tell()<end:
     plain=dec.update(reader.read(min(4*1024*1024,end-reader.tell())));digest.update(plain);writer.write(enc.update(plain))
    tail=dec.finalize();digest.update(tail);writer.write(enc.update(tail));writer.write(enc.finalize());writer.write(enc.tag)
   authenticated=True
   # Only authenticated old plaintext may produce a published new envelope.
   subprocess.run(['gh','release','upload',release,str(target),'--repo',os.environ['GITHUB_REPOSITORY'],'--clobber'],check=True)
   rows.append({'release':release,'original':name,'rewrapped':target.name,'bytes':target.stat().st_size,'plaintextSha256':digest.hexdigest(),'reviewKeyFingerprint':hashlib.sha256(new).hexdigest()[:16],'originalPreserved':True})
  finally:
   source.unlink(missing_ok=True)
   if not authenticated:target.unlink(missing_ok=True) # Failed authentication must never leave a publishable fallback.
pathlib.Path('review-rewrap-results.json').write_text(json.dumps(rows,indent=2));print('Authenticated review envelopes rewrapped:',len(rows))
