#!/usr/bin/env python3
"""
인트로용 오디오 합성기 — 흙/자갈 발소리 + 밤 앰비언스.

전부 코드로 만든다(녹음 샘플 없음).
  발소리   : 저역 임팩트 + 자갈 알갱이 그레인
  앰비언스 : FFT 로 스펙트럼을 씌운 노이즈(= 주기가 정확히 L 이라 루프 이음매가 없음) + 풀벌레

출력
  Audio/footstep_dirt_01..06.wav   모노 one-shot
  Audio/ambience_night.wav         스테레오 10초 심리스 루프
  Audio/intro_audio_mix_10s.wav    스테레오 10초 (영상 2바퀴분) 미리듣기 겸 간단 적용용
"""
import os
import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt

SR = 44100
OUT = os.environ.get("INTRO_AUDIO_DIR", "../Audio")
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(20260914)

# ── 인트로 타이밍 (INTRO_SPEC.md 와 동일) ──────────────────
LOOP = 5.0          # 영상 루프 길이(초)
FPS = 19.2          # 러닝 스프라이트 재생 속도
CYCLES = 12         # 5초당 러닝 사이클 수
MIX_LEN = 10.0      # 믹스/앰비언스 길이 = 영상 2바퀴

# (이름, 프레임 위상, 상대 볼륨, 피치 배율)  볼륨/피치는 체격·거리감
# 볼륨 차이가 곧 거리감. 선두만 또렷하게 들리고 나머지는 깔리는 정도로.
CAST = [("C", 3, 1.00, 0.92), ("A", 6, 0.38, 1.00), ("B", 1, 0.22, 1.12)]


def lp(x, fc, order=4):
    return sosfilt(butter(order, min(fc, SR / 2 - 100), "lp", fs=SR, output="sos"), x)


def hp(x, fc, order=2):
    return sosfilt(butter(order, fc, "hp", fs=SR, output="sos"), x)


def bp(x, lo, hi, order=4):
    hi = min(hi, SR / 2 - 100)
    return sosfilt(butter(order, [lo, hi], "bp", fs=SR, output="sos"), x)


def env(n, attack, decay, curve=3.0):
    """짧은 어택 + 지수 감쇠 엔벨로프."""
    a = max(1, int(attack * SR))
    e = np.ones(n)
    e[:a] = np.linspace(0, 1, a) ** 0.6
    t = np.arange(n) / SR
    return e * np.exp(-t / max(1e-4, decay)) ** 1.0 * (1 - t / (t[-1] + 1e-9)) ** 0.15 if curve else e


# ══════════════════════════════════════════════════════════
# 1. 흙/자갈 발소리
# ══════════════════════════════════════════════════════════
def footstep(seed, dur=0.20):
    r = np.random.default_rng(seed)
    n = int(dur * SR)
    t = np.arange(n) / SR
    out = np.zeros(n)

    # (a) 임팩트 : 발이 땅을 누르는 저역 "툭"
    f0 = r.uniform(78, 135)
    thump = np.sin(2 * np.pi * f0 * t * np.exp(-t * 7)) * np.exp(-t / r.uniform(0.028, 0.048))
    out += thump * r.uniform(0.45, 0.62)

    # (b) 흙 몸통 : 중역 노이즈, 아주 빠른 감쇠
    body = lp(r.standard_normal(n), r.uniform(900, 1500)) * np.exp(-t / r.uniform(0.030, 0.055))
    out += body * r.uniform(0.48, 0.66)

    # (c) 자갈 알갱이 : 흩어지는 작은 그레인들
    for _ in range(r.integers(7, 14)):
        s = int(r.uniform(0.004, 0.115) * SR)
        g = int(r.uniform(0.004, 0.012) * SR)
        if s + g >= n:
            continue
        grain = bp(r.standard_normal(g), r.uniform(1100, 2100), r.uniform(3200, 6000))
        grain *= np.exp(-np.arange(g) / (g * 0.30))
        out[s:s + g] += grain * r.uniform(0.08, 0.22)

    # (d) 전체 엔벨로프로 꼬리 정리 + DC 제거 + 클릭 방지
    out *= np.exp(-t / 0.070)   # 꼬리를 짧게 -> 초당 14걸음이 겹쳐도 안 뭉개짐
    out = hp(out, 45)                      # DC/초저역 제거 (헤드룸 확보, 클릭 방지)
    fade = min(256, n // 8)
    out[:4] *= np.linspace(0, 1, 4)
    out[-fade:] *= np.linspace(1, 0, fade)

    peak = np.max(np.abs(out))
    return out / peak * 0.85 if peak > 0 else out


# ══════════════════════════════════════════════════════════
# 2. 밤 앰비언스 (심리스 루프)
# ══════════════════════════════════════════════════════════
def spectral_noise(n, shape_fn, seed):
    """FFT 로 스펙트럼을 씌운 노이즈. 결과의 주기가 정확히 n 이라 루프 이음매가 없다."""
    r = np.random.default_rng(seed)
    spec = np.fft.rfft(r.standard_normal(n))
    f = np.fft.rfftfreq(n, 1 / SR)
    spec *= shape_fn(f)
    x = np.fft.irfft(spec, n)
    p = np.max(np.abs(x))
    return x / p if p > 0 else x


def cricket(dur=0.26, base=4500, seed=0):
    """풀벌레 한 마리의 짧은 트릴."""
    r = np.random.default_rng(seed)
    n = int(dur * SR)
    out = np.zeros(n)
    pulse_n = int(0.009 * SR)
    step = int(0.017 * SR)
    for k in range(r.integers(4, 7)):
        s = k * step
        if s + pulse_n >= n:
            break
        tt = np.arange(pulse_n) / SR
        f = base * r.uniform(0.97, 1.03)
        p = np.sin(2 * np.pi * f * tt) + 0.5 * np.sin(2 * np.pi * f * 2 * tt)
        p *= np.hanning(pulse_n)
        out[s:s + pulse_n] += p
    return out * 0.8


def ambience(length=MIX_LEN, seed=7):
    n = int(length * SR)
    t = np.arange(n) / SR

    # (a) 바람 : 저역 중심. 250Hz 위로 급격히 떨어지게
    wind = spectral_noise(n, lambda f: 1.0 / (1.0 + (f / 210.0) ** 2.6), seed)
    # 숨쉬는 세기 변화 — 정수 주기 사인만 써서 루프가 유지되게
    lfo = 1.0
    for cyc, amp in ((1, 0.30), (2, 0.16), (3, 0.09)):
        lfo = lfo + amp * np.sin(2 * np.pi * cyc * t / length + seed * cyc)
    wind *= np.clip(lfo, 0.25, 2.0) * 0.5

    # (b) 공기감 : 아주 작은 고역 쉬익
    air = spectral_noise(n, lambda f: np.exp(-((np.log10(f + 20) - 3.1) ** 2) / 0.12), seed + 1) * 0.045

    # (c) 풀벌레 : 루프를 넘어가면 앞쪽으로 되감아 붙여 이음매를 없앤다
    L = np.zeros(n)
    R = np.zeros(n)
    r = np.random.default_rng(seed + 2)
    for i in range(26):
        c = cricket(base=r.uniform(3900, 5200), seed=seed + 10 + i)
        s = int(r.uniform(0, n))
        pan = r.uniform(-0.85, 0.85)
        gain = r.uniform(0.04, 0.12)
        idx = (np.arange(len(c)) + s) % n          # 되감기
        L[idx] += c * gain * (0.5 - pan * 0.5 + 0.5)
        R[idx] += c * gain * (0.5 + pan * 0.5 + 0.5)

    # (d) 스테레오로 벌리기 (바람은 좌우 살짝 다른 시드)
    wind_r = spectral_noise(n, lambda f: 1.0 / (1.0 + (f / 210.0) ** 2.6), seed + 3)
    wind_r *= np.clip(lfo, 0.25, 2.0) * 0.5

    left = wind + air + L * 0.5
    right = wind_r + air + R * 0.5
    left, right = hp(left, 42), hp(right, 42)   # 안 들리는 초저역이 헤드룸만 먹는 걸 방지
    st = np.stack([left, right], 1)
    st /= np.max(np.abs(st)) / 0.42
    return st


# ══════════════════════════════════════════════════════════
# 3. 발 접지 타이밍
# ══════════════════════════════════════════════════════════
def step_times(phase, length):
    """프레임 번호 (n + phase) % 4 == 0 인 순간이 발이 닿는 시점."""
    out = []
    total = int(round(length * FPS))
    for n in range(total):
        if (n + phase) % 4 == 0:
            out.append(n / FPS)
    return out


# ══════════════════════════════════════════════════════════
if __name__ == "__main__":
    # --- 발소리 6종 ---
    steps = []
    for i in range(6):
        s = footstep(1000 + i)
        steps.append(s)
        wavfile.write(f"{OUT}/footstep_dirt_{i + 1:02d}.wav", SR,
                      (s * 32767).astype(np.int16))
    print(f"발소리 6종 : 각 {len(steps[0]) / SR:.3f}초")

    # --- 앰비언스 ---
    amb = ambience()
    wavfile.write(f"{OUT}/ambience_night.wav", SR, (amb * 32767).astype(np.int16))
    print(f"앰비언스   : {len(amb) / SR:.1f}초 스테레오")

    # --- 믹스 (영상 2바퀴 = 10초) ---
    n = int(MIX_LEN * SR)
    mix = amb * 0.45          # 앰비언스는 발소리보다 확실히 아래로 깔기
    placed = 0
    r = np.random.default_rng(99)
    for name, phase, vol, pitch in CAST:
        for st in step_times(phase, MIX_LEN):
            src = steps[r.integers(0, 6)]
            # 피치 = 리샘플. 체격이 작을수록 높게.
            m = max(8, int(len(src) / pitch))
            s = np.interp(np.linspace(0, len(src) - 1, m), np.arange(len(src)), src)
            s = s * vol * r.uniform(0.82, 1.0) * 0.55

            # 화면상 x 위치로 좌우 팬 (오른쪽 -> 왼쪽으로 지나감)
            tt = st % LOOP
            x = 13.4 - (( {"C": 5.45, "A": 2.85, "B": 0.0}[name] + 5.44 * tt) % 27.2)
            pan = float(np.clip(x / 9.6, -1, 1))
            gl, gr = np.sqrt((1 - pan) / 2), np.sqrt((1 + pan) / 2)

            i0 = int(st * SR)
            idx = (np.arange(m) + i0) % n          # 루프를 넘어가면 되감기
            mix[idx, 0] += s * gl
            mix[idx, 1] += s * gr
            placed += 1

    peak = np.max(np.abs(mix))
    mix = mix / peak * 0.89
    wavfile.write(f"{OUT}/intro_audio_mix_10s.wav", SR, (mix * 32767).astype(np.int16))
    print(f"믹스       : {MIX_LEN:.0f}초, 발소리 {placed}개 배치")
    for name, phase, vol, pitch in CAST:
        ts = step_times(phase, LOOP)
        print(f"  {name}: 5초당 {len(ts)}걸음, 첫 발 {ts[0]:.4f}초, 간격 {ts[1] - ts[0]:.4f}초")
