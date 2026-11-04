"""Cloud-only authenticated Windows package rewrap; no persistent plaintext."""
import os, pathlib, json, re, subprocess, hashlib
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
release = os.environ['REVIEW_SOURCE_RELEASE']
name = os.environ['REVIEW_ASSETS']
assert re.fullmatch(r'windows-candidate-[0-9a-f]{40}', release)
assert re.fullmatch(r'windows-build-[0-9]+-[0-9]+-manifest\.json', name)
root = pathlib.Path(os.environ['RUNNER_TEMP']) / 'windows-rewrap'; root.mkdir()
out = pathlib.Path('outputs/review-rewrap'); out.mkdir(parents=True, exist_ok=True)
repo = os.environ['GITHUB_REPOSITORY']
def download(asset):
    subprocess.run(['gh', 'release', 'download', release, '--repo', repo, '--pattern', asset, '--dir', str(root)], check=True)
download(name)
manifest = json.loads((root/name).read_text())
assert manifest['source_commit'] == release.removeprefix('windows-candidate-')
parts = manifest['parts']; assert 1 <= len(parts) <= 20
old = root/'original.enc'
with old.open('wb') as target:
    for i, part in enumerate(parts):
        assert re.fullmatch(r'windows-build-[0-9]+-[0-9]+-[0-9]{5}\.enc\.part', part['name'])
        assert int(part['name'].split('-')[-1].split('.')[0]) == i
        download(part['name']); p = root/part['name']; digest = hashlib.sha256()
        assert p.stat().st_size == part['bytes']
        with p.open('rb') as source:
            for chunk in iter(lambda: source.read(4*1024*1024), b''):
                digest.update(chunk); target.write(chunk)
        assert digest.hexdigest() == part['sha256']; p.unlink()
new = root/'reviewkey.enc'; authenticated = False
try:
    with old.open('rb') as source, new.open('wb') as target:
        assert source.read(9) == b'MYSIMENC1'
        nonce = source.read(12); source.seek(-16, 2); tag = source.read(16); end = source.tell()-16; source.seek(21)
        dec = Cipher(algorithms.AES(bytes.fromhex(os.environ['MYSIM_INTAKE_KEY'])), modes.GCM(nonce, tag)).decryptor()
        new_nonce = os.urandom(12)
        enc = Cipher(algorithms.AES(bytes.fromhex(os.environ['MYSIM_REVIEW_KEY'])), modes.GCM(new_nonce)).encryptor()
        target.write(b'MYSIMENC1'+new_nonce)
        while source.tell() < end:
            target.write(enc.update(dec.update(source.read(min(4*1024*1024, end-source.tell())))))
        target.write(enc.update(dec.finalize())); target.write(enc.finalize()); target.write(enc.tag)
    authenticated = True
finally:
    if not authenticated: new.unlink(missing_ok=True)
old.unlink()
rows=[]
with new.open('rb') as source:
    for i in range(20):
        chunk=source.read(512*1024*1024)
        if not chunk: break
        p=out/f'windows-reviewkey-{i:05}.enc'; p.write_bytes(chunk)
        rows.append({'name':p.name,'bytes':len(chunk),'sha256':hashlib.sha256(chunk).hexdigest()})
new.unlink()
pathlib.Path('review-rewrap-results.json').write_text(json.dumps({'source_commit':manifest['source_commit'], 'original_release':release, 'original_manifest':name, 'parts':rows, 'originalPreserved':True, 'authenticated':True, 'format':'concatenate parts then AES-GCM decrypt using review key; same original Windows build'}, indent=2))
print('Authenticated Windows package rewrap complete; source build unchanged; originals preserved.')
