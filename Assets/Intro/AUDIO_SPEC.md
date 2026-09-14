# 인트로 오디오 — 구현 지시서

달빛 도둑 인트로(5초 루프)에 발소리와 밤 앰비언스를 붙이는 작업 명세입니다.
**오디오 파일은 이미 다 만들어져 있습니다.** 이 문서는 그걸 Unity 씬에 연결하는 방법만 다룹니다.

이 문서를 그대로 구현 담당(사람이든 코딩 에이전트든)에게 넘기면 됩니다.
씬 구성 자체는 `INTRO_SPEC.md` 를 보세요.

---

## 0. 두 가지 방법 — 먼저 고를 것

| | A. 한 파일 재생 | B. 발마다 트리거 |
|---|---|---|
| 작업량 | AudioSource 1개, 코드 없음 | 스크립트 1개 작성 |
| 파일 | `intro_audio_mix_10s.wav` | `footstep_dirt_01~06` + `ambience_night` |
| 결과 | 지금 프리뷰 영상 그대로 | 같은 소리 + 캐릭터별 볼륨·팬이 실시간으로 변함 |
| 한계 | 인트로 길이·속도를 바꾸면 소리가 어긋남 | 없음 |

**A 로 충분한 경우가 많습니다.** 인트로는 5초짜리 고정 연출이라 실시간 계산이 꼭 필요하진 않아요.
§1 만 읽으면 끝납니다. 나중에 속도나 캐릭터 수를 바꿀 계획이면 §2 로 가세요.

---

## 1. 방법 A — 믹스 한 파일

1. `Assets/Intro/Audio/intro_audio_mix_10s.wav` 를 프로젝트에 둡니다 (이미 있음).
2. `IntroScene` 아래에 빈 오브젝트 `IntroAudio` 를 만들고 `AudioSource` 를 붙입니다.

| 항목 | 값 |
|---|---|
| AudioClip | `intro_audio_mix_10s` |
| Play On Awake | ✔ |
| Loop | ✔ |
| Spatial Blend | `0` (2D) |
| Volume | `0.8` |
| Priority | `128` |

3. 임포트 설정: Load Type `Decompress On Load`, Compression Format `Vorbis`, Quality `70`

**주의** — 이 파일은 **10초**입니다. 영상 루프가 5초이므로 영상 2바퀴 = 오디오 1바퀴입니다.
둘 다 씬 시작 시점에 같이 출발해야 발이 닿는 순간과 소리가 맞습니다.
`IntroLoopController` 가 `Awake` 에서 시계를 0으로 잡으므로, AudioSource 의 `Play On Awake` 를 켜두면 자동으로 맞습니다.
중간에 오디오만 멈췄다 켜면 어긋납니다.

---

## 2. 방법 B — 발마다 트리거

### 2-1. 파일과 임포트 설정

```
Assets/Intro/Audio/
├── footstep_dirt_01.wav ~ footstep_dirt_06.wav   모노, 0.20초, one-shot
└── ambience_night.wav                            스테레오, 10초, 심리스 루프
```

| | 발소리 6개 | 앰비언스 |
|---|---|---|
| Force To Mono | ✔ | ✘ |
| Load Type | Decompress On Load | Compressed In Memory |
| Compression Format | PCM | Vorbis (Quality 70) |
| Preload Audio Data | ✔ | ✔ |

발소리는 짧고 자주 쓰이므로 압축하지 않는 편이 CPU 에 유리합니다.

### 2-2. 앰비언스

`IntroScene` 아래 `Ambience` 오브젝트 + `AudioSource`:
Clip `ambience_night` / Play On Awake ✔ / Loop ✔ / Spatial Blend `0` / **Volume `0.45`**

볼륨 0.45 는 임의값이 아니라 프리뷰 믹스에서 쓴 비율입니다. 이보다 올리면 발소리가 묻힙니다.

### 2-3. 발 접지 타이밍 — 이 문서의 핵심

러닝 스프라이트는 8프레임이고 `19.2 fps` 로 재생됩니다 (5초에 12사이클).
한 사이클에 발이 **두 번** 닿고, 그 순간은 **프레임 0번과 4번**입니다.

기존 `SpriteSheetAnimator` 가 쓰는 프레임 번호 계산과 똑같이 갑니다.

```
t = IntroLoopController.Now()          // 인트로 공통 시계(초)
n = floor(t * 19.2)                    // 스프라이트 프레임 카운터
표시 프레임 = (n + phaseOffset) % 8

발이 닿는 조건 :  (n + phaseOffset) % 4 == 0
```

`n` 이 바뀌는 순간에만 검사하면 됩니다. 결과적으로 **캐릭터마다 0.2083초(= 1/4.8초)에 한 번**씩 울립니다.

각 캐릭터의 첫 발과 5초당 걸음 수 (검산용):

| 캐릭터 | phaseOffset | 첫 발 | 간격 | 5초당 |
|---|---|---|---|---|
| thief_C (선두) | 3 | 0.0521초 | 0.2083초 | 24걸음 |
| thief_A (중간) | 6 | 0.1042초 | 0.2083초 | 24걸음 |
| thief_B (후미) | 1 | 0.1563초 | 0.2083초 | 24걸음 |

셋이 조금씩 어긋나 있어서 전체적으로는 초당 약 14.4걸음이 됩니다. 이게 무리가 달려가는 소리입니다.

### 2-4. 작성할 스크립트 : `ThiefFootsteps.cs`

`ThiefRunner` 가 붙은 도둑 오브젝트에 같이 붙입니다. 요구사항:

**인스펙터 필드**

| 필드 | 타입 | 기본값 | 설명 |
|---|---|---|---|
| `clips` | `AudioClip[]` | 발소리 6개 | 매번 이 중에서 하나를 고름 |
| `phaseOffset` | `int` | — | **같은 오브젝트의 `SpriteSheetAnimator.phaseOffset` 과 반드시 같은 값** |
| `fps` | `float` | `19.2` | 마찬가지로 `SpriteSheetAnimator.fps` 와 같게 |
| `volume` | `float` | 캐릭터별 (아래 표) | 거리감 |
| `pitch` | `float` | 캐릭터별 (아래 표) | 체격 |
| `pitchJitter` | `float` | `0.06` | 매 걸음 ± 랜덤 |
| `volumeJitter` | `float` | `0.18` | 매 걸음 ± 랜덤 |
| `panScale` | `float` | `9.6` | 화면 반폭(유닛). x 를 이걸로 나눠 팬 값을 만듦 |

| 캐릭터 | volume | pitch |
|---|---|---|
| thief_C | `1.00` | `0.92` |
| thief_A | `0.38` | `1.00` |
| thief_B | `0.22` | `1.12` |

**동작**

1. `Awake` 에서 `AudioSource` 를 하나 가져오거나 추가한다.
   설정: `playOnAwake = false`, `loop = false`, `spatialBlend = 0f`
2. `Update` 에서
   - `int n = Mathf.FloorToInt(IntroLoopController.Now() * fps);`
   - `n` 이 직전 값과 같으면 아무것도 안 함
   - `n` 이 바뀌었고 `(n + phaseOffset) % 4 == 0` 이면 발소리 재생
   - `n` 을 저장
3. 재생할 때
   - `clips` 중 랜덤 선택. **직전에 쓴 클립은 제외** (같은 소리가 연속되면 기계처럼 들림)
   - `src.pitch = pitch * Random.Range(1f - pitchJitter, 1f + pitchJitter)`
   - `src.panStereo = Mathf.Clamp(transform.position.x / panScale, -1f, 1f)`
   - `src.PlayOneShot(clip, volume * Random.Range(1f - volumeJitter, 1f))`

**구현 시 주의**

- **AudioSource 는 도둑마다 하나씩.** 하나를 공유하면 `panStereo` 와 `pitch` 가 재생 중인 소리에까지 영향을 줍니다.
  발소리가 0.2초라 한 캐릭터 안에서는 겹침이 거의 없어 문제가 없습니다.
- `n` 이 2 이상 건너뛰어도 **한 번만** 재생하세요. 프레임 드랍 때 발소리가 몰아서 터지면 더 이상합니다.
- `IntroLoopController.Now()` 를 쓰세요. `Time.time` 을 쓰면 씬 재진입 시 위상이 틀어집니다.
- 팬은 진행 방향(오른쪽 → 왼쪽)을 그대로 따라갑니다. `ThiefRunner.direction` 을 뒤집어도 x 좌표 기반이라 자동으로 맞습니다.

### 2-5. AudioListener 확인 (3D 프로젝트라 특히)

씬에 `AudioListener` 가 **정확히 하나** 있어야 합니다.
이 프로젝트는 3D 게임이라 기존 카메라에 이미 붙어 있을 수 있습니다.
인트로 카메라에 무심코 하나 더 붙이면 `There are 2 audio listeners in the scene` 경고가 뜨고 소리가 이상해집니다.
인트로 카메라에는 **없을 때만** 추가하세요.

### 2-6. AudioMixer (선택)

게임 전체 볼륨 설정과 엮으려면 `Intro` 그룹을 만들고 발소리·앰비언스 AudioSource 의 Output 을 거기로 보냅니다.
`Master > SFX > Intro` 정도 구조면 충분합니다.

---

## 3. 검수 체크리스트

- [ ] Play 했을 때 발이 땅에 닿는 순간과 소리가 맞는가 (특히 제일 큰 선두 캐릭터)
- [ ] 5초마다(방법 A 는 10초마다) 뚝 끊기거나 튀는 지점이 없는가
- [ ] 도둑이 오른쪽에 있을 때 오른쪽에서, 왼쪽으로 가면 왼쪽에서 들리는가
- [ ] 같은 발소리가 연속으로 두 번 나오지 않는가
- [ ] 콘솔에 AudioListener 경고가 없는가
- [ ] 발소리가 앰비언스에 묻히지 않는가 (묻히면 앰비언스 볼륨을 내릴 것)

---

## 4. 소리를 다시 뽑고 싶다면

`Assets/Intro/_Tools~/gen_audio.py` 로 전부 재생성됩니다 (Unity 밖에서 실행).

```bash
pip install numpy scipy
python gen_audio.py
```

녹음 샘플이 아니라 **코드로 합성**한 소리입니다. 고칠 만한 지점:

- `footstep()` 의 `(a) 임팩트` `(b) 흙 몸통` `(c) 자갈 알갱이` 세 덩어리가 발소리를 이룹니다.
  자갈 느낌을 줄이려면 `(c)` 의 개수(`r.integers(7, 14)`)나 게인을 낮추세요.
  더 둔탁하게 하려면 `(b)` 의 로우패스 주파수(`900~1500`)를 내립니다.
- `ambience()` 의 `(a) 바람` 은 `210.0` 이 기준 주파수입니다. 올리면 바람이 밝아집니다.
  풀벌레는 `(c)` 의 개수 `26` 과 게인으로 조절합니다.
- 앰비언스가 이음매 없이 도는 이유는 **FFT 로 스펙트럼을 씌운 노이즈를 쓰기 때문**입니다.
  결과 신호의 주기가 정확히 파일 길이와 같아서 끝과 처음이 저절로 이어집니다.
  일반 노이즈를 잘라 쓰면 이음매에서 "틱" 소리가 납니다.
- `CAST` 의 볼륨·피치를 바꾸면 믹스 파일에도 바로 반영됩니다.
  **단 §2-4 표의 값도 같이 고쳐야** 방법 A 와 방법 B 의 소리가 같아집니다.

### 인트로 길이나 속도를 바꿨다면

`gen_audio.py` 상단의 `LOOP`, `FPS`, `CYCLES` 를 `INTRO_SPEC.md` 와 똑같이 맞추고 다시 실행하세요.
§2-3 의 접지 조건 `(n + phase) % 4 == 0` 은 8프레임 사이클을 전제로 합니다.
프레임 수를 바꾸면 이 `4` 도 `프레임수 / 2` 로 같이 바뀝니다.
