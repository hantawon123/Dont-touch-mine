import json
import shutil
from pathlib import Path

ROOT = Path(r"C:\Users\SSAFY\barleymilk\S15P21D205")
ART = Path(r"C:\Users\SSAFY\.codex\visualizations\2026\09\14\01a09e00-d780-7f01-a5d2-f47dc854742f\current-bear-all-actions")
SRC = ART / "crawl-cylinder"
DEST = ROOT / "Assets" / "Scenes" / "CharacterTest" / "First"
BACKUP = ART / "before-crawl-cylinder-fix"
BACKUP.mkdir(parents=True, exist_ok=True)

manifest = json.loads((ART / "manifest.json").read_text(encoding="utf-8"))
changed = set(json.loads((SRC / "corrective-report.json").read_text(encoding="utf-8"))["clips"])
entries = [entry for entry in manifest if entry["clip"] in changed]

for entry in entries:
    name = entry["file"]
    target = DEST / name
    source = SRC / name
    if not source.exists():
        raise FileNotFoundError(source)
    if not target.exists():
        raise FileNotFoundError(target)
    backup = BACKUP / name
    if not backup.exists():
        shutil.copy2(target, backup)
    shutil.copy2(source, target)

print(f"INSTALLED {len(entries)}")
print(f"BACKUP {BACKUP}")
