import os
#!/usr/bin/env python3
"""Unity 씬과 동일한 규격으로 5초 루프 프리뷰를 렌더 (검증용).
도둑은 화면 오른쪽 -> 왼쪽으로 이동한다."""
import math, os
from PIL import Image

A = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "../../../Assets/_Game/Content/UI/Intro"))
SHEETS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "reference_sheets")
TMP = "./preview_frames"
os.makedirs(TMP, exist_ok=True)

W, H = 1920, 1080
BG = (3, 8, 19)                 # #030813
GROUND_Y = 900                  # 바닥 윗면(캐릭터가 딛는 선)
MOON_C = (960, 700)
MOON_D = 520
GLOW_D = 1650

T, FPS = 5.0, 30                # 5초 루프
LOOP_PX = 2720                  # 가로 랩 주기
SPEED = LOOP_PX / T             # px/s
CYCLES = 12                     # 5초 동안 러닝 사이클 12회(정수 -> 심리스)
RIGHT_EDGE = 2300               # 등장하는 쪽(오른쪽) 끝
FOOT_RATIO = 241 / 256          # 프레임 위에서 발바닥이 있는 위치

moon = Image.open(f"{A}/moon_disc.png").resize((MOON_D, MOON_D), Image.LANCZOS)
glow = Image.open(f"{A}/moon_glow.png").resize((GLOW_D, GLOW_D), Image.LANCZOS)
vig = Image.open(f"{A}/vignette.png").resize((W, H), Image.LANCZOS)
gf = Image.open(f"{A}/ground_front.png")
gb = Image.open(f"{A}/ground_back.png")

sheets = {n: Image.open(f"{SHEETS}/thief_{n}_run.png") for n in "ABC"}
FRAMES, FS = 8, 256

# (변형, 화면상 키 px, PNG 안 캐릭터 픽셀높이, 무리 안 오프셋 px, 러닝 위상)
# 오프셋이 클수록 더 왼쪽 = 무리의 앞쪽
CAST = [
    ("C", 300, 220, 545, 3),     # 선두 (제일 왼쪽, 제일 큼)
    ("A", 272, 205, 285, 6),
    ("B", 240, 183, 0,   1),     # 후미 (제일 오른쪽)
]

SURFACE_RATIO = 0.42


def tile_ground(img, surface_y, scale, color):
    tw, th = int(img.width * scale), int(img.height * scale)
    t = img.resize((tw, th), Image.LANCZOS)
    strip = Image.new("RGBA", (W + tw * 2, th + 400), (0, 0, 0, 0))
    for x in range(0, W + tw * 2, tw):
        strip.alpha_composite(t, (x, 0))
    strip.paste((255, 255, 255, 255), (0, th, strip.width, th + 400))
    solid = Image.new("RGBA", strip.size, color)
    solid.putalpha(strip.getchannel("A"))
    return solid, int(surface_y - SURFACE_RATIO * th)


def render(fi):
    t = fi / FPS
    canvas = Image.new("RGBA", (W, H), BG + (255,))

    # 달무리 (미세한 호흡)
    pulse = 1.0 + 0.015 * math.sin(2 * math.pi * t / T)
    gd = int(GLOW_D * pulse)
    g = glow.resize((gd, gd), Image.LANCZOS)
    g.putalpha(g.getchannel("A").point(lambda v: int(v * 0.42)))
    canvas.alpha_composite(g, (MOON_C[0] - gd // 2, MOON_C[1] - gd // 2))

    # 달 원반
    canvas.alpha_composite(moon, (MOON_C[0] - MOON_D // 2, MOON_C[1] - MOON_D // 2))

    # 원경 바닥
    sb, yb = tile_ground(gb, GROUND_Y - 34, 0.85, (9, 15, 28, 255))
    canvas.alpha_composite(sb, (-260, yb))

    # 도둑들 : 오른쪽 -> 왼쪽
    for name, hgt, char_px, off, pofs in CAST:
        fs = int(hgt * FS / char_px)
        x = RIGHT_EDGE - ((off + SPEED * t) % LOOP_PX)
        fr = (int(t / T * CYCLES * FRAMES) + pofs) % FRAMES
        sp = sheets[name].crop((FS * fr, 0, FS * (fr + 1), FS)).resize((fs, fs), Image.LANCZOS)
        black = Image.new("RGBA", sp.size, (0, 0, 0, 255))
        black.putalpha(sp.getchannel("A"))
        canvas.alpha_composite(black, (int(x - fs / 2), int(GROUND_Y - FOOT_RATIO * fs)))

    # 근경 바닥
    sf, yf = tile_ground(gf, GROUND_Y, 1.0, (0, 0, 0, 255))
    canvas.alpha_composite(sf, (-90, yf))

    canvas.alpha_composite(vig, (0, 0))
    return canvas.convert("RGB")


if __name__ == "__main__":
    n = int(T * FPS)
    for i in range(n):
        render(i).save(f"{TMP}/f{i:04d}.png")
    print("frames:", n)
