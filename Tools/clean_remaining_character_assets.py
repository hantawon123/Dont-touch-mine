"""Remove the audited, unreferenced placeholder motion bank and Mixamo tests."""
from pathlib import Path
import json,shutil,hashlib
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'artifacts/smooth-bear-unity'
BACKUP=Path(json.loads((OUT/'installation.json').read_text())['backup'])
audit=json.loads((OUT/'extra-cleanup-audit.json').read_text())
assert not audit['externally_referenced']
records=[]
for name in audit['unreferenced']:
    for filename in (name,name+'.meta'):
        path=(ROOT/filename).resolve();path.relative_to((ROOT/'Assets/_Game').resolve())
        if not path.exists():continue
        dest=BACKUP/filename;dest.parent.mkdir(parents=True,exist_ok=True)
        assert not dest.exists();shutil.copy2(path,dest)
        digest=hashlib.sha256(path.read_bytes()).hexdigest()
        assert hashlib.sha256(dest.read_bytes()).hexdigest()==digest
        records.append({'path':filename,'sha256':digest});path.unlink()
for name in ('Assets/_Game/Content/Animations/Mixamo','Assets/_Game/Content/Models'):
    folder=(ROOT/name).resolve();folder.relative_to((ROOT/'Assets/_Game').resolve())
    if folder.exists() and not any(folder.iterdir()):
        folder.rmdir();meta=Path(str(folder)+'.meta')
        if meta.exists():
            dest=BACKUP/(name+'.meta');dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(meta,dest);meta.unlink()
(BACKUP/'extra-manifest.json').write_text(json.dumps(records,indent=2))
result=json.loads((OUT/'installation.json').read_text())
result['removed_assets']+=audit['unreferenced']
(OUT/'installation.json').write_text(json.dumps(result,indent=2))
print('REMOVED additional unreferenced assets',len(audit['unreferenced']))
