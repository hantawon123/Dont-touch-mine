"""Install the checked package and remove only audited obsolete character assets."""
from pathlib import Path
import hashlib,json,re,shutil
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'artifacts/smooth-bear-unity'
QA=ROOT/'Temp/smooth-bear-unity-qa'
BACKUP=Path('C:/Users/SSAFY/.codex/visualizations/2026/09/15/01a0a3bb-bf50-70a3-b969-beb1f5f21188/smooth-bear-unity/previous-assets')
PACKAGE='Assets/_Game/Content/Characters/SmoothBear'
report=json.loads((QA/'package-report.json').read_text())
assert report['clips']==119 and report['sampledFrames']>=4000 and not report['errors'],report
audit=json.loads((OUT/'cleanup-audit.json').read_text())
candidates=sorted(set(audit['unreferenced'])|set(audit['externally_referenced']))
for name,refs in audit['externally_referenced'].items():
    if '/First/' not in name:raise AssertionError((name,refs))
    if name.endswith('FirstPlayerCapsule_Idle.fbx'):continue # Model GUID moves to the new mesh.
    if name.endswith('FirstCharacterPreview.controller'):continue # Controller GUID moves with it.
    assert refs==['Assets/_Game/Content/Animations/PlayerAnimator.controller'],(name,refs)
extra=['Assets/_Game/Editor/FirstCrawlCorrectivePostprocessor.cs']
changed=['Assets/_Game/Editor/CharacterTestPreviewSetup.cs','Assets/_Game/Editor/FirstInGameSetup.cs',
    'Assets/_Game/Content/Animations/PlayerAnimator.controller','Assets/Scenes/CharacterTest.unity',
    'Assets/_Game/Content/Prefabs/PlayerCharacter.prefab',
    'Assets/_Game/Editor/CurrentBearMotionValidation.cs','Assets/_Game/Tests/PlayMode/CurrentBearMotionPlaybackTests.cs',
    'Assets/_Game/Tests/EditMode/PlayerAnimationDriverTests.cs']
backup=[]
for name in candidates+extra+changed:
    for filename in (name,name+'.meta'):
        path=(ROOT/filename).resolve()
        path.relative_to((ROOT/'Assets').resolve())
        if not path.is_file():continue
        target=BACKUP/filename
        target.parent.mkdir(parents=True,exist_ok=True)
        assert not target.exists(),f'Backup already exists: {target}'
        shutil.copy2(path,target)
        digest=hashlib.sha256(path.read_bytes()).hexdigest()
        assert hashlib.sha256(target.read_bytes()).hexdigest()==digest
        backup.append({'path':filename,'sha256':digest})
BACKUP.mkdir(parents=True,exist_ok=True)
(BACKUP/'manifest.json').write_text(json.dumps(backup,indent=2))
# All paths have been audited and backed up; file-by-file removal cannot
# traverse outside the named asset scope.
for name in candidates+extra:
    for filename in (name,name+'.meta'):
        path=(ROOT/filename).resolve()
        path.relative_to((ROOT/'Assets').resolve())
        if path.is_file():path.unlink()
old=ROOT/'Assets/Scenes/CharacterTest'
for folder in sorted([p for p in old.rglob('*') if p.is_dir()]+[old],key=lambda p:len(p.parts),reverse=True):
    folder.resolve().relative_to((ROOT/'Assets/Scenes').resolve())
    if not any(folder.iterdir()):
        folder.rmdir()
        meta=folder.with_name(folder.name+'.meta')
        if meta.exists():
            target=BACKUP/meta.relative_to(ROOT)
            target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(meta,target);meta.unlink()
shutil.copytree(QA/PACKAGE,ROOT/PACKAGE,dirs_exist_ok=True)
for suffix in ('','.meta'):
    path='Assets/_Game/Content/Animations/PlayerAnimator.controller'+suffix
    shutil.copy2(QA/path,ROOT/path)
for name in ('CharacterTestPreviewSetup.cs','FirstInGameSetup.cs'):
    shutil.copy2(OUT/'scripts'/name,ROOT/'Assets/_Game/Editor'/name)
for name in ('SmoothBearAssets.cs','SmoothBearProjectValidation.cs'):
    shutil.copy2(ROOT/'Tools'/name,ROOT/'Assets/_Game/Editor'/name)
# Keep existing test coverage on the current paths and active corrective keys.
for file in ('Assets/_Game/Editor/CurrentBearMotionValidation.cs','Assets/_Game/Tests/PlayMode/CurrentBearMotionPlaybackTests.cs',
             'Assets/_Game/Tests/EditMode/PlayerAnimationDriverTests.cs'):
    path=ROOT/file;text=path.read_text(encoding='utf-8-sig')
    text=text.replace('Assets/Scenes/CharacterTest/First/','Assets/_Game/Content/Characters/SmoothBear/')
    text=text.replace('FirstPlayerCapsule_Idle.fbx','SmoothBear.fbx').replace('FirstCharacterPreview.controller','SmoothBearPreview.controller')
    text=text.replace('o.name == "FirstPlayerCapsule"','o.name == "SmoothBear"')
    text=text.replace('"Belly_Breath", "Crouch_Groin_Flat", "Crawl_Waist_Round", "Crawl_Follow_R", "Crawl_Follow_L"','"Belly_Breath", "Crouch_Groin_Flat"')
    text=text.replace('blendShapeCount, Is.EqualTo(5)','blendShapeCount, Is.EqualTo(2)')
    path.write_text(text,encoding='utf-8')
source=ROOT/'source/blender/characters/SmoothBear'
source.mkdir(parents=True,exist_ok=True)
shutil.copy2(OUT/'SmoothBear_Unity.blend',source/'SmoothBear.blend')
shutil.copy2(QA/'package-report.json',OUT/'validated-package-report.json')
(OUT/'installation.json').write_text(json.dumps({'removed_assets':candidates+extra,'backup':str(BACKUP),
    'model':PACKAGE+'/SmoothBear.fbx','animations':119},indent=2))
print('INSTALLED 119 clips; removed',len(candidates+extra),'old assets; backup',BACKUP)
