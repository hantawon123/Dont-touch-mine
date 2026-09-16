import json
import shutil
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = Path(r'C:\Users\SSAFY\.codex\visualizations\2026\09\14\01a09e00-d780-7f01-a5d2-f47dc854742f\current-bear-all-actions')
SOURCE = OUT/'crawl-volume-fixed'
BACKUP = OUT/'before-crawl-volume-fix'
BACKUP.mkdir(exist_ok=True)
DEST = ROOT/'Assets/Scenes/CharacterTest/First'
entries = json.loads((SOURCE/'manifest.json').read_text())
for entry in entries:
    target = DEST/entry['file']
    backup = BACKUP/entry['file']
    if not backup.exists():
        shutil.copy2(target, backup)
    shutil.copy2(SOURCE/entry['file'], target)
print('INSTALLED', len(entries), 'crawl-volume FBXs')
