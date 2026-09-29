import hashlib,json,sys
from pathlib import Path
root=Path(__file__).resolve().parents[1];out=Path(sys.argv[1])
assets=json.loads((out/'manifest.json').read_text());checks=[]
for a in assets:
 target=root/'Assets/Scenes/CharacterTest/First'/a['file']
 assert target.read_bytes()==(out/'fbx'/a['file']).read_bytes(),target
 assert target.with_suffix('.fbx.meta').read_bytes()==(out/'originals'/(a['file']+'.meta')).read_bytes(),target
 checks.append({'file':a['file'],'sha256':hashlib.sha256(target.read_bytes()).hexdigest()})
result={'installed_files':len(checks),'controller_states':sum(len(a['states']) for a in assets),'meta_guids_unchanged':True,'files':checks}
(out/'installation-verification.json').write_text(json.dumps(result,indent=2))
print('Verified installation:',len(checks),'files, all original Unity meta files unchanged')
