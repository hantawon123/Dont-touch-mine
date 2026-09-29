"""Install verified punch assets and register their preview/game states."""
import json, shutil, sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
OUT=Path(sys.argv[1]) if len(sys.argv)>1 else Path('C:/Users/SSAFY/.codex/visualizations/2026/09/07/01a0792f-184a-72c2-8704-dfc1b0ae814d/party_punch_footwork')
items=json.loads((OUT/'manifest.json').read_text())
backup=OUT/'before_install';backup.mkdir(exist_ok=True)
for x in items:
 filename=f'FirstPlayerCapsule_{x["name"]}.fbx';dest=ROOT/'Assets/Scenes/CharacterTest/First'/filename
 if dest.exists() and not (backup/filename).exists():shutil.copy2(dest,backup/filename)
 shutil.copy2(OUT/filename,dest)
new=[x for x in items if x['name'].startswith(('Punch_Left','Punch_Combo'))]
for rel in ('Assets/_Game/Editor/CharacterTestPreviewSetup.cs','Assets/_Game/Editor/FirstInGameSetup.cs','Assets/_Game/Client/CharacterTestPreviewDriver.cs'):
 p=ROOT/rel
 with p.open(encoding='utf-8-sig',newline='') as f:old=f.read()
 if '"Punch_Left"' in old:continue
 shutil.copy2(p,backup/p.name)
 nl='\r\n' if '\r\n' in old else '\n';lines=old.splitlines(keepends=True)
 idx=next(i for i,l in enumerate(lines) if '"Punch_Crouch_Walk"' in l)+1
 insert=[]
 for x in new:
  n=x['name'];last=x['last'];loop=str(x['loop']).lower()
  if 'Driver' in rel:insert.append(f'            "{n}",{nl}')
  else:
   extra=', true' if 'PreviewSetup' in rel else ''
   insert.append(f'            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_{n}.fbx", "{n}", {last}, {loop}{extra}),{nl}')
 lines[idx:idx]=insert;updated=''.join(lines)
 if 'FirstInGameSetup' in rel:
  updated=updated.replace('states.Any(state => state.name == "Punch") &&','states.Any(state => state.name == "Punch") &&'+nl+'                   states.Any(state => state.name == "Punch_Left") &&'+nl+'                   states.Any(state => state.name == "Punch_Combo_Crouch_Walk") &&')
 with p.open('w',encoding='utf-8',newline='') as f:f.write(updated)
print('Installed',len(items),'clips and registered',len(new),'new states')
