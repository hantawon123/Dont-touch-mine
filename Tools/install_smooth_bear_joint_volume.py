"""Install the validated joint-volume model and clips without changing GUIDs."""
import json,shutil,re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'artifacts/smooth-bear-unity/joint-volume'
QA=ROOT/'Temp/smooth-bear-unity-qa'
relative=Path('Assets/_Game/Content/Characters/SmoothBear')
report=json.loads((QA/'package-report.json').read_text())
assert report['clips']==119 and report['sampledFrames']==4077 and report['volumeBones']==8 and not report['errors'],report
clips=sorted((QA/relative/'Animations').glob('*.anim'))
assert len(clips)==119
def guid(path):return re.search(r'guid: (\w+)',Path(str(path)+'.meta').read_text()).group(1)
for clip in clips:
    target=ROOT/relative/'Animations'/clip.name
    assert guid(clip)==guid(target),(clip,target)
for clip in clips:
    target=ROOT/relative/'Animations'/clip.name
    backup=OUT/'before-animations'/clip.name
    backup.parent.mkdir(exist_ok=True)
    if not backup.exists():shutil.copy2(target,backup)
    shutil.copy2(clip,target)
shutil.copy2(OUT/'SmoothBear.fbx',ROOT/relative/'SmoothBear.fbx')
shutil.copy2(OUT/'SmoothBear.blend',ROOT/'source/blender/characters/SmoothBear/SmoothBear.blend')
shutil.copy2(QA/'package-report.json',OUT/'package-report.json')
shutil.copy2(QA/'package-report.json',OUT.parent/'validated-package-report.json')
(OUT.parent/'run-project-validation.request').write_text('validate')
print('INSTALLED_JOINT_VOLUME',len(clips),report['volumeBones'])
