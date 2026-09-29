# Host / Server 비교와 복귀 (870)

기존 MR !215의 868·869에 이어 진행한다. 기본 방 생성 SessionRequest.Create는 Host이며 기존 UI에서 Server로 자동 전환하지 않는다. 명시적인 CreateServer 호출만 Server 모드의 비공개 방을 생성한다.

## 범위와 원복

- 런타임 GameMode.Server 비교용 진입점 추가. 6명 입장 후 측정 하네스가 서버 권한으로 경기를 시작한다.
- 기존 Host 경기/방장 동작은 유지한다. Server 운영용 방장 위임, 방 생성 서버 배정, 배포/재시작 서비스는 도입하지 않았다.
- 동일 실행 파일과 같은 설정에서 run-local.ps1의 `-Mode Host`로 실행하면 Host로 돌아간다. Mode 생략 시에도 Host다. 별도 데이터/설정 마이그레이션은 없다.
- Server 모드에서 VoiceNetworkObject.Spawned가 VoiceConnection을 전제하여 예외가 발생했다. 서버에 비활성 VoiceConnection 등록 객체만 붙여 SDK 수명주기를 지원한다. 서버는 Voice Cloud에 연결하거나 마이크를 사용하지 않는다. Host/Client 음성 경로는 그대로다. SDK/프리팹/네트워크 상태 레이아웃을 변경하지 않았다.
- 최초 예외 발생 run `server-mode-observed`는 DISCARDED로 표시하고 최종 수치에서 제외한다.

## 조건

동일 PC, Unity 6000.3.22f1, Fusion 2.1.2.2279, 64Hz, 60fps 상한, Development Windows Mono. CPU/Deep Profile OFF, Grafana ON. Windows Dedicated Server 빌드 타깃이 아닌 같은 실행 파일의 -batchmode -nographics 및 GameMode 선택 비교다.

| 항목 | Host | Server |
|---|---|---|
| 게임 참가자 | 6명 | 6명 |
| 로컬 프로세스 | Host 1 + Client 5 = 6 | Server 1 + Client 6 = 7 |
| 화면 출력 | Client peer1 하나, 1280×720 | Client peer1 하나, 1280×720 |
| 시뮬레이션 권한 | 플레이어도 겸하는 Host | 플레이어가 없는 Server |
| 입력/경기 | 같은 자동 이동·달리기·점프 | 같은 자동 이동·달리기·점프 |
| 길이 | 290초 | 290초 |

따라서 실제 플레이어 수는 같지만 같은 PC에서 Server 쪽은 프로세스 하나가 더 자원을 사용한다. 원격 회선, 서버 전용 머신, 서버 빌드의 자동 제거 최적화 효과는 미검증이다. 단일 시행으로 우열을 일반화하지 않는다. 실제 음성 발화/아이템 상호작용 전체 회귀는 별도다.

## 재실행

`run-local.ps1`은 LocalPhotonBaseline 측정 하네스를 포함한 Development 빌드 전용이다. 일반 게임 빌드에 자동 입력/입장 기능을 추가하는 스크립트가 아니다. 현재 PC의 하네스 소스는 `.build/photon-baseline/project/Assets/_Game/Bootstrap/LocalPhotonBaseline.cs`, 빌드는 `.build/photon-baseline/player/Baseline.exe`에 있다. 측정용 복사 프로젝트에만 BackendSignIn 우회와 NetworkPlayerMotor 자동 입력, ProjectLifetimeScope 하네스 등록을 적용했다. 제품 프로젝트에는 적용하지 않았다. Grafana 계측 코드는 MR !213의 코드가 포함돼 있다.

```powershell
./Tools/performance/ingame/run-local.ps1 `
  -PlayerPath <측정루트>/player/Baseline.exe `
  -RuntimeRoot <관제런타임루트> `
  -OutputRoot <측정루트> `
  -Run my-host-check -Mode Host
```

Server 비교는 Run에 새로운 이름, Mode에 Server를 지정한다. 기존 결과 폴더 덮어쓰기는 거부한다. Grafana 토큰은 RuntimeRoot/credentials.json에서 읽고 자식 프로세스 환경으로 전달한다. 종료 시 기존 환경 변수를 복원하며 자신이 시작한 게임 프로세스만 정리한다. 6인 탐색까지 측정하려면 기본 290초 이상을 사용한다.

```powershell
python Tools/performance/ingame/compare.py <측정루트>/host-final-observed <측정루트>/server-final-observed --allow-topology-change
```

일반 전후 비교에서는 Mode가 다르면 도구가 거부한다. 위 명시적 옵션을 사용한 토폴로지 비교에서만 프로세스 수 차이를 결과에 표시하면서 허용한다. Searching 시작 10초를 제외한 공통 길이 구간을 사용한다.

## 결과와 판단

2026-09-07 KST Server 15:19–15:24, Host 15:27–15:32 순서로 실행했다. 두 실행에서 Game.Network.dll SHA256은 `DDEB68F129E019A299D14F6651E4D3940658AF93135FED3EB14A1E677E296F30`으로 같다. 공통 Searching 구간은 **64.002초**다. 전체 13개 프로세스 결과는 `server-comparison.json`에 보존한다.

| 지표 | Host | Server |
|---|---:|---:|
| 권한 프로세스 Fusion update p95 | 7.1771ms | 8.4829ms |
| 권한 프로세스 Fusion update p99 | 10.3717ms | 12.7387ms |
| 권한 프로세스 GC0 | 12회 | 13회 |
| 화면 클라이언트 frame p95 | 16.7249ms | 16.6944ms |
| 화면 클라이언트 frame p99 | 17.2274ms | 16.8444ms |
| 화면 클라이언트 33.333ms 초과 | 0% | 0% |
| 화면 클라이언트 Fusion update p95 | 4.0987ms | 2.5337ms |
| 화면 클라이언트 GC0 | 11회 | 11회 |
| 화면 클라이언트 RTT p95 | 33.3216ms | 33.3053ms |

**현재 기본값은 Host 유지.** Server에서 화면 클라이언트 Fusion 처리 시간은 낮았지만 프레임 차이는 작고, 권한 프로세스 처리 시간은 오히려 증가했다. 모든 측정 프로세스에서 해당 구간의 33.333ms 초과 프레임은 0%였다. 이번 결과만으로 서버 전환 비용을 감수할 명확한 끊김 개선을 입증하지 못했다. 60fps 제한이 걸려 있으므로 최대 처리량 비교도 아니다.

868·869에서는 중복 조회/ID 문자열 할당 제거 후 호스트 GC0가 26→12회로 감소했다. 이는 별도 변경 전후 실험이며 여기의 Host/Server 수치와 같은 실험으로 합산하지 않는다. 해당 최적화는 유지하고, 실제 플레이에서 끊김이 재현되면 남아 있는 KCC 충돌/물리 처리 비용을 우선 프로파일링한다. Server 재평가는 별도 머신 또는 Unity Dedicated Server 빌드로 같은 플레이 시나리오를 반복할 때 의미가 있다. 현재는 배포 서버나 유료 자원을 만들지 않았다.

## 검증과 한계

- SessionRequest 모드 계약 + 스냅샷 회귀 EditMode 7/7 통과. 최종 Windows Development 빌드 오류 0건. 음성 등록 수정은 최종 Server 6인 스폰/경기 및 Host 재실행으로 검증했다.
- Server 1 + Client 6, Host 1 + Client 5의 실제 Grafana 전송과 Searching 진입 확인. 게임 종료 후 실시간 참가자 0은 정상이며 아래 고정 시간 범위로 이력을 볼 수 있다.
- 최종 Server에서 최초 파일럿의 VoiceNetworkObject.Spawned 예외 재발 없음. CSV 저장 뒤 기존 HUD Dispose NullReferenceException은 Server 및 Host 종료 로그에 남아 있다. 본 변경으로 해결한 문제로 표시하지 않는다.
- run-local.ps1 PowerShell 파서 통과, compare.py 실제 두 런 13개 CSV 계산 통과. 측정 순서·PC 자원 경쟁·자동 입력 경로 차이는 반복 실험으로 통제하지 않았다.
- `-Mode Host`로 같은 바이너리의 6인 경기를 다시 실행하여 복귀 경로 확인. 제품 UI의 Host 기본값은 처음부터 유지한다. 소스 수준 원복은 870 커밋만 revert하고 868 최적화 커밋은 유지할 수 있다.

[측정 시간대 Grafana](http://127.0.0.1:3300/d/game-performance?from=1788761880000&to=1788762780000&var-build=$__all&var-phase=$__all&var-players=6) — 측정 PC에서만 접근 가능.
