#!/usr/bin/env python3
"""
도둑 캐릭터 러닝 사이클 실루엣 생성기.

캐릭터 비율은 원화(뜨개 두건 + 둥근 귀 2개, 짧고 통통한 몸통, 큰 둥근 신발, 등에 멘 보따리)를 기준으로 한다.
내부적으로는 "오른쪽을 보고" 그린 뒤 마지막에 좌우 반전하여 **왼쪽으로 달리는** 스프라이트를 출력한다.
(인트로에서 도둑은 화면 오른쪽 -> 왼쪽으로 이동한다.)

전신 높이 200 을 기준으로 한 설계값 (원화 실측 비율):
    귀 끝 ~ 발바닥   200
    머리(두건) 지름   96   <- 전체의 48%. 이 큰 머리가 캐릭터의 정체성.
    귀 지름           34
    몸통 높이         44
    다리 길이         48   <- 전체의 24%. 아주 짧고 통통하다.
    신발              40 x 27  (다리 두께 21 보다 확실히 크다)
    보따리 지름       72
"""
import math, os
from PIL import Image, ImageDraw

SS = 4              # 슈퍼샘플링 배수
FW = FH = 256       # 최종 프레임 크기
FRAMES = 8
GROUND_LINE = 236   # 프레임 안에서 발바닥이 닿는 y (피벗 계산의 기준)
BODY_H = 206        # 귀 끝 ~ 발바닥 높이 (기준 스케일 1.0 일 때)
OUT = os.environ.get("INTRO_SPRITE_DIR", os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "../../../Assets/_Game/Content/UI/Intro")))
SHEETS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "reference_sheets")
os.makedirs(SHEETS, exist_ok=True)
os.makedirs(OUT, exist_ok=True)

# ---- 8키 러닝 사이클(도) : 아래방향 기준, 앞(오른쪽)이 + ----
# 다리가 짧고 통통해서 원본보다 진폭을 줄였다. 과하면 다리가 몸에서 떨어져 보인다.
THIGH = [ 30,  10, -12, -32,  -6,  22,  34,  36]
KNEE  = [ 10,  38,  16,   6,  86,  96,  62,  26]
# 자유로운 쪽 팔 (다리와 반대 위상)
SHLDR = [-40, -14,  16,  40,  34,   8, -22, -42]
ELBOW = [ 70,  84,  92,  80,  70,  84,  92,  80]


def seg(d, x, y, ang, length, width):
    """아래방향 기준 ang(앞+)으로 뻗는 캡슐 마디. 끝점과 각도를 반환."""
    a = math.radians(ang)
    ex, ey = x + math.sin(a) * length, y + math.cos(a) * length
    d.line([(x, y), (ex, ey)], fill=255, width=max(1, int(width)))
    r = width / 2
    for px, py in ((x, y), (ex, ey)):
        d.ellipse([px - r, py - r, px + r, py + r], fill=255)
    return ex, ey


def blob(d, cx, cy, rx, ry, ang=0.0):
    """회전한 타원 (신발처럼 방향이 있는 덩어리용)."""
    pts = []
    a = math.radians(ang)
    ca, sa = math.cos(a), math.sin(a)
    for i in range(48):
        t = 2 * math.pi * i / 48
        px, py = rx * math.cos(t), ry * math.sin(t)
        pts.append((cx + px * ca - py * sa, cy + px * sa + py * ca))
    d.polygon(pts, fill=255)


def draw_thief(i, cfg):
    W, H = FW * SS, FH * SS
    img = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(img)
    u = SS * cfg["scale"]                 # 1 설계단위 = u 픽셀
    gl = GROUND_LINE * SS                 # 발바닥 선

    p = i / FRAMES
    bob = (-2.0 + 3.4 * math.cos(4 * math.pi * p)) * u   # 사이클당 2회 상하 바운스

    cx = W * 0.50

    # ── 설계 치수 (원화 실측을 전신 200 으로 환산) ──────────
    head_r = 42 * u * cfg["head"]         # 머리(두건) 지름 84
    ear_r  = 14.5 * u * cfg["ear"]        # 귀 지름 29
    hipY   = gl - 56 * u + bob            # 다리 길이 56 (전체의 27%)
    shY    = hipY - 37 * u                # 몸통 높이 37
    tL, sL = 23 * u, 22 * u               # 허벅지 / 종아리
    uA, fA = 25 * u, 22 * u               # 위팔 / 아래팔
    hipR, shR = 24 * u, 26 * u            # 몸통 폭 ~50
    legW, armW = 22 * u, 17 * u
    shoe_rx, shoe_ry = 21.5 * u, 12 * u   # 신발 43 x 24 (다리 두께 20 보다 확실히 큼)
    sack_r = 31 * u * cfg["sack"]         # 보따리 지름 60 (원화 72 -> 실루엣용으로 축소)

    lean = cfg["lean"]
    a = math.radians(lean)
    shX = cx + math.sin(a) * (44 * u)     # 몸통이 기운 만큼 어깨가 앞으로

    j0 = i % FRAMES                       # 가까운 다리
    j1 = (i + FRAMES // 2) % FRAMES       # 먼 다리

    def leg(k, w, shoe_scale=1.0):
        shin_ang = THIGH[k] - KNEE[k]
        kn = seg(d, cx, hipY, THIGH[k], tL, w)
        ak = seg(d, kn[0], kn[1], shin_ang, sL, w * 0.82)
        # 큰 둥근 신발 : 발목에서 앞쪽으로 튀어나온 덩어리
        fa = shin_ang * 0.3 + 8
        far = math.radians(fa)
        fcx = ak[0] + math.sin(far) * (shoe_rx * 0.42)
        fcy = ak[1] + math.cos(far) * (shoe_ry * 0.55)
        blob(d, fcx, fcy, shoe_rx * shoe_scale, shoe_ry * shoe_scale, fa)

    def free_arm(k, w):
        el = seg(d, shX, shY, SHLDR[k], uA, w)
        seg(d, el[0], el[1], SHLDR[k] + ELBOW[k], fA, w * 0.88)

    # 1) 먼 쪽 다리 + 먼 쪽 팔
    leg(j1, legW * 0.86, 0.9)
    free_arm(j1, armW * 0.86)

    # 2) 보따리 : 어깨보다 아래, 등 뒤로 46 만큼 물러난 위치
    #    (머리와 같은 높이에 두면 실루엣이 한 덩어리로 뭉개진다)
    bx = shX - 44 * u
    by = shY + 15 * u + bob * 0.25
    d.ellipse([bx - sack_r, by - sack_r * 0.96, bx + sack_r, by + sack_r * 1.04], fill=255)
    # 보따리를 묶어 어깨로 넘긴 천
    d.polygon([(bx - sack_r * 0.26, by - sack_r * 0.90),
               (bx + sack_r * 0.26, by - sack_r * 0.90),
               (shX + 1 * u,        shY - shR * 0.45),
               (shX - 7 * u,        shY - shR * 0.05)], fill=255)

    # 3) 몸통 : 어깨가 골반보다 약간 넓은 통통한 덩어리
    nx, ny = math.cos(a), math.sin(a)
    d.polygon([(cx + nx * hipR, hipY + ny * hipR), (cx - nx * hipR, hipY - ny * hipR),
               (shX - nx * shR, shY - ny * shR), (shX + nx * shR, shY + ny * shR)], fill=255)
    d.ellipse([cx - hipR, hipY - hipR, cx + hipR, hipY + hipR], fill=255)
    d.ellipse([shX - shR, shY - shR, shX + shR, shY + shR], fill=255)

    # 4) 머리 : 목 없이 어깨 위에 바로 얹힌 커다란 두건
    hx = shX + 6 * u + math.sin(a) * (head_r * 0.35)
    hy = shY - 48 * u
    d.ellipse([hx - head_r, hy - head_r * 1.02, hx + head_r, hy + head_r * 0.98], fill=255)

    # 5) 둥근 귀 2개 : 머리 옆·위로 확실히 튀어나오게 (캐릭터 식별 포인트)
    for ang_deg in (-38, 36):
        ea = math.radians(ang_deg)
        ex_ = hx + math.sin(ea) * head_r * 1.10
        ey_ = hy - math.cos(ea) * head_r * 1.10
        d.ellipse([ex_ - ear_r, ey_ - ear_r, ex_ + ear_r, ey_ + ear_r], fill=255)

    # 6) 두건이 목덜미를 덮은 부분 (머리와 어깨를 한 덩어리로 이어 붙임)
    d.polygon([(hx - head_r * 0.68, hy + head_r * 0.45),
               (hx + head_r * 0.58, hy + head_r * 0.52),
               (shX + shR * 0.80,   shY - shR * 0.25),
               (shX - shR * 0.86,   shY - shR * 0.25)], fill=255)

    # 7) 가까운 쪽 다리
    leg(j0, legW, 1.0)

    # 8) 가까운 쪽 팔 (제일 위에 그려서 몸통과 구분되게)
    free_arm(j0, armW)

    alpha = img.resize((FW, FH), Image.LANCZOS)
    out = Image.new("RGBA", (FW, FH), (0, 0, 0, 0))
    out.putalpha(alpha)
    # 왼쪽으로 달리도록 좌우 반전 (보따리가 진행 반대쪽으로 따라오게 된다)
    return out.transpose(Image.FLIP_LEFT_RIGHT)


# scale : 전체 크기 / head·ear·sack : 부위별 미세 차이 / lean : 앞으로 숙인 각도
VARIANTS = {
    "thief_A": dict(scale=1.00, lean=7,  head=1.00, ear=1.00, sack=1.00),
    "thief_B": dict(scale=0.88, lean=10, head=1.05, ear=1.10, sack=0.86),
    "thief_C": dict(scale=1.09, lean=5,  head=0.96, ear=0.94, sack=1.12),
}

if __name__ == "__main__":
    import numpy as np
    os.makedirs(f"{OUT}/Frames", exist_ok=True)
    for name, cfg in VARIANTS.items():
        sheet = Image.new("RGBA", (FW * FRAMES, FH), (0, 0, 0, 0))
        top, bot = 10 ** 9, 0
        for i in range(FRAMES):
            fr = draw_thief(i, cfg)
            sheet.paste(fr, (FW * i, 0))
            # Unity 쪽에서 슬라이스할 필요가 없도록 프레임을 개별 파일로도 저장
            fr.save(f"{OUT}/Frames/{name}_run_{i}.png")
            ys = np.nonzero(np.asarray(fr.getchannel("A")) > 8)[0]
            top, bot = min(top, ys.min()), max(bot, ys.max())
        sheet.save(f"{SHEETS}/{name}_run.png")  # 참고용 시트 (Unity 에서는 안 씀)
        print(f"{name}: 캐릭터 세로 {bot - top + 1}px, 발바닥 y={bot}, 피벗y={(256 - bot) / 256:.4f}")
