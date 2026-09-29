# 6인 런타임 조회 최적화 (868 / 869)

## 근거

기준: origin/develop 9bc830eb, Unity 6000.3.22f1 / Fusion 2.1.2.2279, Host 64Hz.
같은 PC에서 호스트와 클라이언트 5개를 실행한다. 클라이언트 1개만 1280×720 / 60fps로 렌더링한다.

일반 CPU 원본 1,141 프레임에서 KCC.FixedUpdate 평균 3.91ms, Physics.OverlapCapsuleNonAlloc 평균 0.54ms.
KCC 내부를 구분하기 위해 별도 Development + Deep Profiling 빌드로 탐색 20초를 수집했다.
Deep Profiling은 36프레임 / 1,286 simulation ticks이며 1.53GB raw를 생성했다. 일반 빌드 성능 수치와 비교하지 않는다.

| 상세 호출 | 호출 수 | 해석 |
|---|---:|---|
| MatchRuntimeController.Tick | 1,286 | 권한 서버 틱마다 실행 |
| NetworkMatchRuntimeContext.CapturePlayers | 3,858 | 행동/자세/위치 접근에서 틱당 3회 전체 재수집 |
| PlayerRoster.TryGetAvatar | 46,296 | 반복 수집의 플레이어 검색 |
| PlayerRegistry.IdOf | 162,876 | 검색 중 문자열 반복 생성 포함 |
| KCC.DepenetrateMultiple | 7,895 | KCC 충돌 해소 반복 비용 |
| Physics.ComputePenetration | 473,090 | 접지/충돌 정확도와 연결된 SDK 내부 비용 |

## 변경

- NetworkMatchRuntimeContext는 같은 ServerTime의 행동·위치·자세를 한 번만 수집한다. 다른 시간이면 과거 방향의 시간 보정도 다시 수집한다. 최초 수집 실패는 완료로 캐시하지 않아 같은 시간에도 재시도한다.
- PlayerAvatar의 로컬 PlayerId 문자열은 실제 Owner가 바뀔 때 갱신한다. 네트워크 객체가 유효하지 않으면 null을 반환한다. PlayerRoster는 해당 ID를 재사용한다. 캐시는 아바타 수명에 한정하며 전역 캐시를 만들지 않는다.
- Fusion 네트워크 필드, 64Hz, KCC CCD/접지/계단/관통 해소 설정은 동일하다.

KCC의 ForceSingleOverlapQuery는 Photon 문서에서 이동 오류 가능성이 있는 NPC용 옵션이다. 플레이어에는 적용하지 않았다.
https://doc.photonengine.com/fusion/v2/addons/advanced-kcc/data-layer

## 검증 조건

계측용 코드가 동일한 일반 Development 빌드로 변경 전후 각각 6인, 290초 실행한다. CPU/Deep Profiling을 끄고 Grafana 전송을 양쪽 모두 켠 상태에서 동일한 자동 이동·회전·달리기·점프 입력을 사용한다. 성능 표는 Searching 진입 초기 10초를 제외하고 peers=6인 자료만 사용한다. 게임 시작 지연에 따라 표본 구간 길이가 다를 수 있어 시간도 함께 기록한다.

단일 PC는 클라이언트와 호스트가 CPU/메모리를 공유한다. 실제 6대 PC/원격 회선 성능 또는 Photon Cloud 서버 한계를 입증하지 않는다. 자동 이동은 실제 아이템·공격·음성 플레이의 전체 회귀 검증을 대체하지 않는다. RTT와 프레임 시간은 환경 잡음이 있으며 단일 전후 비교를 보장된 개선율로 일반화하지 않는다.

원본은 로컬 작업 공간 `.build/photon-baseline/`의 six-cpu, six-deep, control-observed, optimized-observed 디렉터리에 보존한다. 대용량 바이너리와 자격 증명은 저장소에 포함하지 않는다.

Grafana 전송 환경 변수 없이 실행한 six-control은 최종 동등 조건 비교에서 제외한다. 최초 six-optimized는 관제 누락을 확인하고 중단했으며 DISCARDED.txt로 표시했다. 이후 측정 런처는 로컬 credentials.json에서 토큰을 읽어 GAME_METRICS_URL/TOKEN/BUILD를 자식 프로세스에 전달하도록 수정했다. 토큰은 로그·코드·커밋에 포함하지 않는다. 실제 Prometheus game_reporting_peers 및 Grafana 화면에서 호스트 1 / 클라이언트 5를 확인했다.

대시보드는 게임 프로세스가 전송하는 지표만 표시한다. 전송 설정 없이 실행한 일반 에디터/빌드는 자동 수집되지 않는다. 게임 종료 후 최근 보고 참가자가 0이 되는 것은 정상이며, 그래프를 보려면 해당 측정 시각을 포함하는 시간 범위를 선택한다.

네트워크 DLL SHA256:
- control-observed: FE4D501298B7C2CB3DECD05C5EE5713B0C958C10383EB48177E493DAD85990D1
- optimized-observed: DBE8C6933E7C69601F77E3073EA273C6FFC204C7646D4E771EFD32BB65981094

EditMode NetworkMatchRuntimeContextTests: 6/6 통과. 원본 및 최적화 Windows Development 빌드 오류 0건.

## 최종 전후 결과

Searching 진입 10초 후부터 **63.508초**의 동일 길이 구간. 일반 Development, 관제 전송 ON, 6명. 실행 순서는 optimized-observed → control-observed이며 시간/열/다른 PC 작업의 영향을 통제한 반복 실험은 아니다.

| 지표 | 변경 전 | 변경 후 |
|---|---:|---:|
| 호스트 표본 수 | 3,786 | 3,805 |
| 호스트 Fusion update p95 | 7.9868ms | 7.9366ms |
| 호스트 Fusion update p99 | 13.2408ms | 11.5353ms |
| 호스트 frame p99 | 18.9518ms | 17.0550ms |
| 호스트 GC0 횟수 | 26 | 12 |
| 화면 클라이언트 표본 수 | 3,799 | 3,808 |
| 화면 클라이언트 frame p95 | 16.7199ms | 16.7339ms |
| 화면 클라이언트 frame p99 | 17.9044ms | 16.8945ms |
| 화면 클라이언트 33.333ms 초과 비율 | 0% | 0% |
| 화면 클라이언트 GC0 횟수 | 12 | 11 |
| 화면 클라이언트 RTT p95 | 33.1882ms | 48.5942ms |

호스트 GC 횟수는 약 54% 감소했으나 Fusion update p95는 사실상 같고, 화면 클라이언트 frame p95도 거의 같다. p99 개선은 이번 표본의 관찰 결과이며 반복/다른 PC 검증 없이 일반화하지 않는다. RTT는 증가했으므로 네트워크 지연 개선 또는 Photon 서버 성능 향상으로 주장하지 않는다. 실질적인 변경은 같은 틱 내 중복 조회와 반복 문자열 할당 제거다. KCC 물리 처리 비용은 남아 있다.

프레임별 전체 비교 수치는 `comparison.json`, 재계산 도구는 `compare.py`에 있다.

```powershell
python Tools/performance/ingame/compare.py <측정루트>/control-observed <측정루트>/optimized-observed
```

양쪽 종료 로그에 기존 NetworkMatchHudPresenter.Dispose → NetworkMatchHudView.EnsureDestroyedItemsHud의 NullReferenceException이 남는다. CSV 저장 후 종료 과정의 기존 문제이며 이번 MR의 해결 범위에는 포함하지 않았다. 추가 Jira는 생성하지 않았다.
