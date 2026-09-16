"""Install verified staged FBXs, preserving Unity GUIDs and the original backup."""
import json,shutil,sys,hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
OUT=Path(sys.argv[1]);assets=json.loads((OUT/'manifest.json').read_text())
checked=json.loads((OUT/'roundtrip-report.json').read_text())
assert {a['clip'] for a in assets}=={a['clip'] for a in checked}
dest=ROOT/'Assets/Scenes/CharacterTest/First'
for name in ('CharacterTest.unity','CharacterTest.unity.meta'):
 p=ROOT/'Assets/Scenes'/name
 if not (OUT/'originals'/name).exists():shutil.copy2(p,OUT/'originals'/name)
for a in assets:
 original=OUT/'originals'/a['file'];target=dest/a['file']
 assert hashlib.sha256(original.read_bytes()).digest()==hashlib.sha256(target.read_bytes()).digest(),f'Input changed since backup: {target}'
for a in assets:
 shutil.copy2(OUT/'fbx'/a['file'],dest/a['file'])
print('INSTALLED',len(assets),'FBXs; original meta GUIDs preserved')
