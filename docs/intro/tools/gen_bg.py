#!/usr/bin/env python3
"""달 / 글로우 / 울퉁불퉁 바닥 타일 생성기."""
import math, os, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "../../../Assets/_Game/Content/UI/Intro"))
os.makedirs(OUT, exist_ok=True)
random.seed(7)


# ---------- 1. 달 원반 ----------
def moon_disc(size=1024):
    S = 4
    img = Image.new("L", (size * S, size * S), 0)
    d = ImageDraw.Draw(img)
    m = size * S * 0.06
    d.ellipse([m, m, size * S - m, size * S - m], fill=255)
    a = img.resize((size, size), Image.LANCZOS).filter(ImageFilter.GaussianBlur(size * 0.012))
    out = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    out.putalpha(a)
    out.save(f"{OUT}/moon_disc.png")


# ---------- 2. 달무리(글로우) : 방사형 폴오프 ----------
def moon_glow(size=1024, power=2.6):
    y, x = np.mgrid[0:size, 0:size]
    c = (size - 1) / 2
    r = np.sqrt((x - c) ** 2 + (y - c) ** 2) / c
    v = np.clip(1.0 - r, 0, 1) ** power          # 부드럽게 0으로 수렴
    v = v + 0.30 * np.clip(1.0 - r * 2.1, 0, 1) ** 1.4   # 중심부 코어 보강
    a = (np.clip(v, 0, 1) * 255).astype(np.uint8)
    rgba = np.zeros((size, size, 4), np.uint8)
    rgba[..., 0], rgba[..., 1], rgba[..., 2] = 255, 253, 240   # 살짝 따뜻한 흰색
    rgba[..., 3] = a
    Image.fromarray(rgba, "RGBA").save(f"{OUT}/moon_glow.png")


# ---------- 3. 울퉁불퉁한 바닥 타일 (가로 심리스) ----------
def ground(name, W=1024, H=320, amp=1.0, pebbles=26, seed=1):
    rnd = random.Random(seed)
    S = 2
    img = Image.new("L", (W * S, H * S), 0)
    d = ImageDraw.Draw(img)

    base = H * S * 0.42
    waves = [(1, 26 * amp), (2, 15 * amp), (3, 9 * amp),
             (5, 6 * amp), (8, 3.5 * amp), (13, 2.0 * amp)]   # 정수 주파수 = 심리스
    ph = [rnd.uniform(0, 2 * math.pi) for _ in waves]

    pts = []
    for px in range(W * S + 1):
        t = px / (W * S)
        h = base + sum(a * S * math.sin(2 * math.pi * f * t + ph[k]) for k, (f, a) in enumerate(waves))
        pts.append((px, h))
    d.polygon(pts + [(W * S, H * S), (0, H * S)], fill=255)

    # 돌멩이 / 흙덩이 : 경계를 넘으면 반대편에도 복제해서 심리스 유지
    for _ in range(pebbles):
        px = rnd.uniform(0, W * S)
        top = base + sum(a * S * math.sin(2 * math.pi * f * (px / (W * S)) + ph[k])
                         for k, (f, a) in enumerate(waves))
        rx = rnd.uniform(7, 26) * S * amp
        ry = rx * rnd.uniform(0.38, 0.72)
        py = top + rnd.uniform(-ry * 0.35, ry * 0.5)
        for off in (0, W * S, -W * S):
            d.ellipse([px - rx + off, py - ry, px + rx + off, py + ry], fill=255)

    a_ch = img.resize((W, H), Image.LANCZOS)
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    out.putalpha(a_ch)
    out.save(f"{OUT}/{name}.png")


# ---------- 4. 비네트 ----------
def vignette(W=1920, H=1080, strength=0.85):
    y, x = np.mgrid[0:H, 0:W]
    nx, ny = (x / W - .5) * 2, (y / H - .5) * 2
    r = np.sqrt(nx ** 2 * 0.85 + ny ** 2)
    a = (np.clip((r - 0.45) / 0.85, 0, 1) ** 1.6 * strength * 255).astype(np.uint8)
    rgba = np.zeros((H, W, 4), np.uint8)
    rgba[..., 3] = a
    Image.fromarray(rgba, "RGBA").save(f"{OUT}/vignette.png")


moon_disc()
moon_glow()
ground("ground_front", amp=1.0, pebbles=26, seed=1)
ground("ground_back", H=260, amp=0.62, pebbles=16, seed=5)
vignette()

# Linear 색공간 프로젝트용 달무리 (Unity 는 이쪽을 쓴다)
import bake_linear
bake_linear.bake(OUT)

print("done:", sorted(os.listdir(OUT)))
