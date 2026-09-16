"""Stage old motion sources separately; build a clean model + native clip package."""
from pathlib import Path
import json, re, shutil, uuid
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'artifacts/smooth-bear-unity'
QA=ROOT/'Temp/smooth-bear-unity-qa'
PACKAGE='Assets/_Game/Content/Characters/SmoothBear'
for d in ('Assets/Source','Assets/Editor',PACKAGE+'/Animations','Packages','ProjectSettings'):
    (QA/d).mkdir(parents=True,exist_ok=True)
manifest=[]
for p in sorted((ROOT/'Assets/Scenes/CharacterTest/First').glob('*.fbx')):
    meta=p.with_suffix('.fbx.meta').read_text(encoding='utf-8-sig')
    guid=re.search(r'^guid: (\w+)',meta,re.M)[1]
    name=re.search(r'      name: (.+)',meta)[1]
    entry={'file':p.name,'name':name,'guid':guid,
           'end':float(re.search(r'      lastFrame: ([\d.]+)',meta)[1]),
           'loop':re.search(r'      loopTime: (\d)',meta)[1]=='1'}
    manifest.append(entry)
    shutil.copy2(p,QA/'Assets/Source'/p.name)
    if name=='Idle':
        idle_meta=meta
        meta=meta.replace('guid: '+guid,'guid: '+uuid.uuid5(uuid.NAMESPACE_URL,'smooth-bear-source-idle').hex,1)
    (QA/'Assets/Source'/(p.name+'.meta')).write_text(meta,encoding='utf-8')
shutil.copy2(OUT/'SmoothBear.fbx',QA/PACKAGE/'SmoothBear.fbx')
modelmeta=idle_meta.replace('importAnimation: 1','importAnimation: 0')
(QA/PACKAGE/'SmoothBear.fbx.meta').write_text(modelmeta,encoding='utf-8')
for src,dst in [('Assets/Scenes/CharacterTest/First/FirstCharacterPreview.controller','SmoothBearPreview.controller'),
                ('Assets/_Game/Content/Animations/PlayerAnimator.controller','PlayerAnimator.controller')]:
    shutil.copy2(ROOT/src,QA/(dst+'.source'))
    shutil.copy2(ROOT/(src+'.meta'),QA/(dst+'.meta.source'))
(QA/'motion-manifest.json').write_text(json.dumps({'motions':manifest},indent=2))
(OUT/'motion-manifest.json').write_text(json.dumps({'motions':manifest},indent=2))
shutil.copy2(ROOT/'Tools/SmoothBearPackage.cs',QA/'Assets/Editor/SmoothBearPackage.cs')
shutil.copy2(ROOT/'ProjectSettings/ProjectVersion.txt',QA/'ProjectSettings/ProjectVersion.txt')
(QA/'Packages/manifest.json').write_text(json.dumps({'dependencies':{}}))
print('STAGED',len(manifest),'motions',QA)
