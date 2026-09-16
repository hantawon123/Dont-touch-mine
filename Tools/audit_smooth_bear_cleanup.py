"""Find incoming references before removing obsolete character-only assets."""
from pathlib import Path
import re,json,subprocess,sys
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'artifacts/smooth-bear-unity'
old=ROOT/'Assets/Scenes/CharacterTest'
candidates={p.relative_to(ROOT).as_posix() for p in old.rglob('*') if p.is_file() and p.suffix!='.meta'}
candidates.add('Assets/_Game/Content/Models/PlayerCapsule.fbx')
extra='--extra' in sys.argv
if extra:
    candidates={p.relative_to(ROOT).as_posix() for p in (ROOT/'Assets/_Game/Content/Animations/Mixamo').rglob('*') if p.is_file() and p.suffix!='.meta'}
    candidates.update(('Assets/_Game/Content/Animations/TestMove.controller','Assets/_Game/Content/Animations/TestWalk.controller',
                       'Assets/_Game/Client/CharacterTestMotionBank.cs'))
guids={}
for name in candidates:
    meta=ROOT/(name+'.meta')
    if meta.exists():
        match=re.search(r'^guid: (\w+)',meta.read_text(encoding='utf-8-sig'),re.M)
        if match:guids[match[1]]=name
incoming={name:[] for name in candidates}
files=subprocess.check_output(['rg','--files','Assets','-g','*.unity','-g','*.prefab','-g','*.asset','-g','*.controller','-g','*.overrideController','-g','*.mat','-g','*.meta'],cwd=ROOT).decode('utf-8').splitlines()
for file in files:
    name=Path(file).as_posix()
    if name in candidates or name.removesuffix('.meta') in candidates:continue
    data=(ROOT/file).read_text(encoding='utf-8-sig',errors='replace')
    for guid in set(re.findall(r'guid: ([a-f0-9]{32})',data)):
        if guid in guids:incoming[guids[guid]].append(name)
report={'candidate_count':len(candidates),'externally_referenced':{k:v for k,v in incoming.items() if v},
        'unreferenced':sorted(k for k,v in incoming.items() if not v)}
(OUT/('extra-cleanup-audit.json' if extra else 'cleanup-audit.json')).write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps({'candidate_count':len(candidates),'externally_referenced':report['externally_referenced']},indent=2))
