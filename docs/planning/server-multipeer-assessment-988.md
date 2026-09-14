# Multi-Peer 적용 검토 — 988 후속

현재 프로젝트에서도 구현할 수 있다. 다만 설정 하나를 바꾸는 작업이 아니라 **방마다 상태·씬·물리·수명을 분리하는 구조 변경**이다. 이번 요청에서는 가능성과 비용을 검토했으며 제품의 PeerMode를 변경하지 않았다.

## 무엇이 달라지는가

지금은 EC2에서 Unity 실행 파일 하나가 방 하나를 담당한다. 같은 EC2에서 실행 파일을 두 개 실행하면 방 두 개를 운영할 수 있다. Multi-Peer는 그 두 방을 실행 파일 한 개 안의 NetworkRunner 두 개로 운영하는 방식이다. 어느 방식이든 게임 연산은 EC2가 담당한다. Photon이 게임 실행용 컴퓨터를 대신 제공하는 기능은 아니다.

Photon 공식 문서에는 하나의 Unity 인스턴스로 여러 게임 세션을 서비스하는 전용 서버 용도가 명시돼 있다. 각 Runner에는 독립된 시뮬레이션·물리 공간·연결·씬이 필요하다. [Photon Multi-Peer](https://doc.photonengine.com/fusion/v2/manual/testing-and-tooling/multipeer)

## 현재 코드에서 바꿔야 하는 부분

| 현재 코드 | 그대로 여러 방을 만들면 생기는 문제 | 필요한 변경 |
| --- | --- | --- |
| ProjectLifetimeScope의 NetworkRunnerService, PlayerRegistry, PlayerSpawner, AppFlowSystem Singleton | 모든 방이 한 서비스·플레이어 목록·화면 전환 상태를 공유 | 프로세스 공통 설정과 방별 DI Scope 분리 |
| NetworkRunnerService의 단일 `_runner`, CaptureCurrentScene, GetSceneByBuildIndex, 로비 사전 로드 | A방의 씬 변경/퇴장이 B방의 씬 조회·해제와 충돌 | 각 Runner의 SceneManager에 씬 소유권 부여 |
| PhysicsPlacementValidator의 Physics.Raycast/OverlapBoxNonAlloc | Runner 전용 물리 공간 대신 기본 물리 공간 검사 | 해당 Runner의 PhysicsScene을 주입해 검사 |
| MatchSceneSpawnPoints 등의 FindAnyObjectByType, NetworkInteractionSceneBridge 전역 탐색 | 다른 방의 스폰 지점·물건·아바타를 잘못 선택 | 방 소유 씬/객체 목록으로 탐색 범위 제한 |
| DedicatedServerStartup의 방 종료 후 Application.Quit | A방 종료가 프로세스와 B방까지 종료 | 방 Runner·Scope만 정리하고 프로세스는 유지 |

Core/Server의 규칙과 권한 코드가 나뉘어 있고, 일부 맵 구성은 이미 씬 단위로 수집하므로 재사용할 부분은 충분하다. Singleton이라는 등록 자체가 금지되는 것은 아니다. 방별 컨테이너가 독립되어 있으면 그 안의 Singleton은 방 하나에만 속한다. 정적 불변 설정도 무조건 제거할 필요가 없다.

Fusion 기본 SceneManager는 Runner별 PhysicsScene을 지원한다. 그러나 우리 코드의 Unity 전역 씬/물리 조회까지 자동으로 바꾸지는 않는다. [Photon 씬 관리](https://doc.photonengine.com/fusion/v2/manual/scene-loading)

## 자원이 얼마나 절약되는가

**현재 측정으로는 절감률을 확정할 수 없다. 한 방만 운영하면 Multi-Peer로 합칠 중복 프로세스가 없으므로 이 방식의 절감 이점도 없다.**

최근 EC2 자동 6인 흐름에서 메모리 최고 관측값은 464.51MiB, 평균 CPU는 1.127vCPU였다. 이것은 입력이 없는 기능 검증값이며, 공통 엔진 비용과 방별 비용을 분리한 측정이 아니다. 이 값을 방 수에 곱해 최대 수용량을 계산하면 안 된다.

여러 방을 한 프로세스로 묶으면 엔진·일부 공통 자산 메모리를 공유할 여지가 있다. 방별 맵 오브젝트·물리·네트워크 상태와 경기 계산은 여전히 필요하다. 프로세스가 나뉘어 있어도 OS가 공유하는 페이지가 있으므로 단순 RSS 합계만 비교하는 것도 부정확하다.

예를 들어, *가정상* 방 하나의 465MiB 중 200MiB가 한 프로세스 안에서 공유 가능하고 추가 비용이 없다면, 두 방은 930MiB 대신 730MiB로 약 22% 줄어든다. **200MiB는 측정값이 아니고 22%도 예상치가 아닌 계산 설명용 예시다.** 실제 비교는 같은 두 방·같은 맵·같은 입력·같은 시간 구간에서 해야 한다.

CPU 계산은 방마다 계속 필요하다. Runner를 여러 개 생성한다고 Unity 게임 코드가 방별 CPU 코어로 자동 분산된다고 전제할 수 없다. 공유 프로세스의 프레임 정지·GC·크래시는 묶인 모든 방에 영향을 준다. 따라서 메모리가 줄어도 CPU 병목 때문에 운영 가능한 방 수가 늘지 않을 수 있다.

특히 현재 전용 서버 시작 경로는 일반 그래픽 설정 적용을 건너뛰고 서버용 targetFrameRate를 명시하지 않는다. Multi-Peer 전에 서버 프레임 상한과 실제 Fusion tick 처리 시간을 비교하는 편이 변경 범위가 작다. Photon도 전용 서버의 프레임 상한 설정을 권장한다. 아직 이 변경의 절감량을 측정하거나 제품에 반영하지 않았다. [Photon 최적화 지침](https://doc.photonengine.com/fusion/v2/concepts-and-patterns/optimizations)

## 보내주신 자료에서 그대로 받아들이면 안 되는 내용

- “호스팅 비용 60~80% 절감”: 우리 게임에 대한 근거가 없다. 메모리 절감률과 EC2 청구액 절감률은 별개다. 제공된 EC2를 그대로 사용하면 메모리가 남는다고 청구액이 자동 감소하지 않는다.
- “멀티스레드를 잘 활용하니 20~30개 방”: 현재 게임과 EC2의 수용량을 뒷받침하지 않는다. 코어별 부하와 시뮬레이션 지연을 먼저 측정해야 한다.
- “Kubernetes/Agones가 필요”: Multi-Peer의 필수 조건이 아니다. 현재 Photon의 대기 방 공개/확보 구조를 활용하는 작은 서버 슬롯 관리로 시작할 수 있다.
- 예제는 제품용 완성 코드가 아니다. 마지막 예제는 SceneManager를 컴포넌트에 추가하지만 StartGameArgs에 전달하지 않는다. 공식 지침은 명시적인 SceneManager 전달을 요구한다. 인증·취소·시작 실패·진행 중 종료·방별 정리·종료된 Runner 재생성도 완성해야 한다.
- 프로세스가 종료되면 누수가 회수될 수는 있지만, 단일 방에서도 긴 경기 중 누수는 문제다. 풀링만으로 이벤트 구독이나 정적 참조 누수가 해결되는 것도 아니다.

## 현실적인 진행 순서

1. 현재 한 방 서버에서 프레임 상한을 설정했을 때 CPU·tick 지연을 비교한다. 실제 이동·물건 조작을 포함한다.
2. 여러 방이 필요한 경우 우선 같은 EC2의 프로세스 두 개로 기준값을 측정한다. 기존 백엔드 자원 여유도 함께 확인한다.
3. Multi-Peer 시험은 두 방으로 제한한다. DI, 씬, 물리, 종료 수명을 방 단위로 분리한다. A방 재경기/퇴장 중 B방 상태가 유지되는지 검증한다.
4. 두 프로세스와 한 프로세스 두 Runner를 동일 조건으로 비교한다. cgroup 메모리/PSS, CPU, tick 지연, 씬 로드 정지, 반복 방 생성·종료 후 잔류 메모리를 기록한다.
5. 절감 효과와 안정성이 확인되면 도입한다. 현재 단계에서 운영 전환이나 큰 프레임워크 도입을 먼저 할 이유는 없다.

판정: **기술적으로 가능, 현재 코드는 바로 활성화 불가, 절감률은 미측정. 먼저 단일 서버 CPU 기준을 정리하고 필요 시 두 방 실험으로 판단하는 것을 권장한다.**
