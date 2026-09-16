"""Package rendered comparison frames and reproducible verification reports."""
import json
import shutil
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'Temp/crawl-torso-stable'
DEST = Path('C:/Users/SSAFY/.codex/visualizations/2026/09/15/01a0a3bb-bf50-70a3-b969-beb1f5f21188/crawl-stable')
DEST.mkdir(parents=True, exist_ok=True)
font = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 25)
frames = []
for frame in range(1, 37, 2):
    canvas = Image.new('RGB', (1280, 610), '#17191e')
    draw = ImageDraw.Draw(canvas)
    for col, (name, title) in enumerate([('before', '수정 전'), ('after', '수정 후 · 몸통 형태 유지')]):
        image = Image.open(SOURCE / f'cycle-{name}-{frame:02d}.png').convert('RGBA')
        canvas.paste(image, (640 * col, 50), image)
        draw.text((640 * col + 24, 10), title, font=font, fill='white')
    frames.append(canvas)
frames[0].save(DEST / 'crawl-comparison.gif', save_all=True, append_images=frames[1:], duration=67, loop=0)
frames[4].save(DEST / 'crawl-comparison.png')
for name in ('Crawl_Torso_Stable.blend', 'report.json'):
    shutil.copy2(SOURCE / name, DEST / name)
if (SOURCE / 'unity-qa/unity-report.json').exists():
    shutil.copy2(SOURCE / 'unity-qa/unity-report.json', DEST / 'unity-report.json')
shutil.copytree(SOURCE / 'backup', DEST / 'backup', dirs_exist_ok=True)
print(DEST)
