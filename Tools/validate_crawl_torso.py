"""Prepare an isolated Unity project for the actual imported skinning check."""
import json
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Temp/crawl-torso-stable'
QA = OUT / 'unity-qa'
for folder in ('Assets/New', 'Assets/Old', 'Assets/Editor', 'Packages', 'ProjectSettings'):
    (QA / folder).mkdir(parents=True, exist_ok=True)
for file in (OUT / 'fbx').glob('*.fbx'):
    shutil.copy2(file, QA / 'Assets/New' / file.name)
shutil.copy2(OUT / 'backup/FirstPlayerCapsule_Idle.fbx', QA / 'Assets/Old/FirstPlayerCapsule_Idle.fbx')
shutil.copy2(ROOT / 'Tools/CrawlTorsoUnityValidation.cs', QA / 'Assets/Editor/CrawlTorsoUnityValidation.cs')
shutil.copy2(ROOT / 'ProjectSettings/ProjectVersion.txt', QA / 'ProjectSettings/ProjectVersion.txt')
(QA / 'Packages/manifest.json').write_text(json.dumps({'dependencies': {}}))
print(QA)
