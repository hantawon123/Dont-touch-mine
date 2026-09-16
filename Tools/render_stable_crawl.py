"""Render original and corrected FBX cycles for a side by side preview."""
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from stabilize_crawl_torso import OUT, hold, render

for folder, label in [('backup', 'cycle-before'), ('fbx', 'cycle-after')]:
    rig, body, _ = hold.import_fbx(OUT / folder / 'FirstPlayerCapsule_Crawl_Forward.fbx')
    render(rig, body, label, range(1, 37, 2))
