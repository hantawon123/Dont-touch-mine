#!/usr/bin/env python3
"""Linear 색공간 프로젝트용 달무리 텍스처 생성.

프리뷰 영상(render_preview.py)은 sRGB(감마) 공간에서 알파 합성한다.
Unity 프로젝트가 Linear 색공간이면 같은 알파도 리니어 공간에서 섞여
중심 밝기가 109 -> 173 (약 1.6배), 중간부는 3배 가까이 밝아진다.

배경이 거의 검정(#030813)이므로  결과 = 알파  로 근사할 수 있고,
알파에 sRGB->Linear 변환을 미리 먹여 두면 리니어 합성 결과가 감마 합성과 같아진다.
IntroSceneBuilder 가 스프라이트 색으로 곱하는 0.42 는 그대로 유지되도록 나눠 둔다.

    a' = srgb_to_linear(a * 0.42) / 0.42

numpy 없이 PIL 만 쓴다 (gen_bg.py 끝에서도 자동 호출됨).
"""
import os
from PIL import Image

OUT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                    "../../../Assets/_Game/Content/UI/Intro"))
GLOW_ALPHA = 0.42   # IntroSceneBuilder 의 MoonGlow 스프라이트 색 알파와 같아야 한다


def srgb_to_linear(x):
    return x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4


def bake(out_dir=OUT, color_alpha=GLOW_ALPHA):
    src = Image.open(f"{out_dir}/moon_glow.png").convert("RGBA")
    lut = [round(srgb_to_linear(i / 255 * color_alpha) / color_alpha * 255) for i in range(256)]
    r, g, b, a = src.split()
    dst = Image.merge("RGBA", (r, g, b, a.point(lut)))
    path = f"{out_dir}/moon_glow_linear.png"
    dst.save(path)
    print("baked:", path)
    return path


if __name__ == "__main__":
    bake()
