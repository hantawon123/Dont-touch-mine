# Host 대체 방식과 전환 경계 — S15P21D205-987

기준: 2026-09-14, develop `02997d76`, Unity 6000.3.22f1, 설치된 Fusion 2.1.2.2279.

## 결정

최종 운영에서 플레이어 Host를 제외한다. **네이티브 Unity 프로세스의 Fusion `GameMode.Server` + WebGL `GameMode.Client`**를 전환 대상으로 선택한다. 경기 권한과 비공개 배정, 물리 확정, 하이라이트 기록은 서버가 소유한다. 방장은 플레이어 중 로비 관리 권한을 가진 사람으로 분리한다.

이 결정은 전체 게임 전환 완료나 120FPS 보장이 아니다. 987은 대안 비교·최소 접속 실험·전환 경계의 확정이고, 제품 코드의 전환은 988, 배포는 989/990, 하이라이트 개편은 991~993이다. 현재 제품의 방 생성은 아직 Host이며 이를 Server로 한 줄 바꾸지 않는다.

## 대안 비교

| 기준 | 기존 Host | Fusion Server | Fusion Shared |
|---|---|---|---|
| 권한 | 플레이어 PC가 경기 판정 | 서버에 판정 집중, 기존 Server 규칙 재사용 | 객체별 클라이언트 권한을 전제로 설계 재검토 필요 |
| 물리 | 현재 KCC·Rigidbody·물건 판정 경로 | 현재 권한 측 물리 경로를 유지할 여지가 가장 큼 | 동시 집기·충돌·던지기·기절의 권한 이동/경합 설계 변경 |
| 비공개 정보 | Host 플레이어가 전체 배정 보유 | 플레이어에게 전체 배정 원본을 둘 필요 없음 | 한 플레이어에게 모든 판정을 맡기면 기존 신뢰 문제가 남음 |
| WebGL | 브라우저 Host 정지 영향이 방 전체로 전파 | 브라우저는 Client. WebGL 전송·재시뮬레이션 비용은 남음 | Photon이 WebGL에 권장. 클라우드 직접 연결 및 재시뮬레이션 없는 구조가 장점 |
| 기록 책임 | Host 메모리 | 서버 메모리에서 판정과 같은 타임라인으로 기록 | 이벤트 순서·기록자·이탈 시 인수인계 변경 필요 |
| 운영 비용 | Photon CCU/트래픽 | Photon + 별도 Unity 프로세스 CPU/RAM/트래픽/운영 | Photon CCU/트래픽, 별도 권한 서버는 기본 제공되지 않음 |
| 이번 판단 | 사용자 요구로 제외 | 선택 | 브라우저 효율은 장점이지만 현재 판정·비공개 정보·물리 구조의 변경 비용이 큼 |

Photon 공식 문서는 WebGL에 Shared를 권장한다. 이 프로젝트의 Server 선택은 그 권장을 모른 채 내린 결정이 아니라, 권한 집중과 기존 게임 규칙 보존을 우선한 프로젝트별 판단이다. 실제 WebGL 비용이 목표를 방해하면 측정 결과를 바탕으로 재검토한다. 다른 네트워크 SDK로 교체하는 것은 전송·RPC·예측·프리팹 전반을 바꾸는 별도 범위이며 현재 근거로는 필요하지 않다.

근거: [Fusion 모드 비교](https://doc.photonengine.com/fusion/v2/fusion-choose), [네트워크 토폴로지](https://doc.photonengine.com/fusion/v2/manual/network-topologies), [Dedicated Server 구성](https://doc.photonengine.com/fusion/v2/concepts-and-patterns/dedicated-server-overview).

## 현재 코드의 실제 흐름과 전환 항목

| 구간 | 현재 코드·행동 | 988 및 후속 작업 |
|---|---|---|
| 방 생성 | `SessionRequest.Create`는 Host. `CreateServer`는 이미 있으나 비공개 실험용 | 플레이어의 방 생성 요청 → 서버 세션 배정 → 준비 완료 응답 → Client 입장. 브라우저가 세션을 만들지 않음 |
| 인증 | `NetworkRunnerService.StartAsync`가 계정 준비 후 Photon Custom Auth 사용 | 플레이어 인증은 유지. 서버 실행용 신원·자격 공급과 실패 종료를 분리하고 서비스 계정을 브라우저에 포함하지 않음 |
| 접속 버전 | `GetPhotonSettings`는 WebGL에서만 `web-{Application.version}` 지정 | 네이티브 서버와 WebGL에 동일 네트워크 호환 버전 공급. 다른 버전이 같은 방에 들어오지 않게 검증 |
| 방장 표시 | `PlayerSpawner.Spawn/RefreshHost`가 `player == runner.LocalPlayer`로 `IsHost` 결정 | Server에는 LocalPlayer가 없으므로 현재 모든 사람이 비방장. 서버가 별도 방장 ID를 확정·복제 |
| 시작·설정·강퇴 | `NetworkLobbyHostSession` → `RequestMatchStart/TryApplyLobbySettings/TryKickPlayer`; 서버 직접 호출 전제. `MatchStarter.RequestStart`는 Client 거절 | 방장 Client의 요청 RPC → 송신자 신원·현재 방장·페이즈·값 검증 → 서버 확정. 다른 참가자의 요청은 거절 |
| 경기 규칙 | `NetworkMatchRuntimeCoordinator`가 IsServer일 때 `MatchRuntimeFactory`와 `MatchSessionCoordinator` 조립 | Core/Server 규칙 유지. 서버에도 씬의 스폰·카탈로그·배치 검사·파쇄기 연결 필요 |
| 물건 물리 | `NetworkInteractionSceneBridge`가 `CarryableItem`을 수집·갱신하고 서버 Pose를 확정. 연결 코드가 Client 컴포넌트에도 의존 | 서버에 필요한 물리/배치 데이터와 화면·입력/카메라 역할을 분리. Client 폴더나 Renderer를 통째로 제거하면 안 됨 |
| 안내 준비 | `IsLocalPresentationReady`는 카메라 렌더와 로컬 배정까지 요구 | 서버의 맵 준비와 각 참가자의 화면 준비를 구분. 서버가 자신의 없는 화면을 기다리지 않게 함 |
| 배정 | `_publishedItemAssignments`에 수신자별 배정 보관, 송신자 기준 재요청·대상별 Reliable Data 전달 | 그대로 서버 소유. 전체 배정 맵을 공용 Networked 상태나 SessionProperties에 노출하지 않음 |
| 하이라이트 | 서버 규칙에서 기록 후 `TryPublishHighlightReplay`; 로컬 `HighlightReplayReceived`도 호출. Playground가 재생 Controller 등록 | 서버는 기록·선정·전송·준비 확인만 담당. 서버에 재생 카메라/복제 시각 객체를 만들지 않음. 브라우저별 스킵과 정상 복귀는 유지 |
| 이탈·정리 | `OnPlayerLeft`가 보유 물건·턴·명단을 정리. 서버 끊김은 참가자 복귀 처리 | 방장 퇴장과 서버 장애를 별도 사건으로 취급. MVP 기본은 기존 사용자가 선택한 방장 퇴장 시 방 종료 정책을 보존하되 서버가 이를 집행. 자동 방장 승계는 별도 요구 없이는 추가하지 않음 |
| 로비·재경기 | 서버가 로비/맵 씬 수명을 관리하고 참가자별 하이라이트 종료를 처리 | 서버도 동일 흐름을 실행하지만 로컬 시청 완료가 필요하지 않아야 함. 마지막 참가자 종료 후 방/프로세스 정리, 다음 경기 상태 초기화 확인 |

`Game.Server`는 순수 규칙 계층이며 실행 파일 이름이나 Spring 백엔드가 아니다. `Network`만 Fusion을 알고, `Bootstrap`에서 서버에 필요한 구성과 Client 표현 구성을 선택한다. 새 Service Locator나 병렬 게임 규칙 구현을 만들지 않는다.

## 목표 구조와 세션 수명

```text
WebGL 방 생성 의도 → 배정 경로 → 네이티브 Server 세션 준비
WebGL Client → Photon 연결/인증 → 기존 세션 입장
Client 입력/RPC → Network 송신자·권한 검증 → Server 규칙/물리 확정
             → Network 상태 복제 → Client의 R3 상태 → Presenter/View
Server 기록 → 하이라이트 선정/전송 → 각 Client 재생/스킵 → 로비/재경기
방 종료·마지막 참가자 퇴장 → Runner Shutdown → 자원 회수
```

초기 운영 후보는 기존 배포 기반 위의 **방당 네이티브 서버 프로세스 하나**다. 멀티 Runner 풀, Kubernetes, 자동 확장 시스템은 현재 도입하지 않는다. 기존 장비의 실행 가능 OS·메모리·CPU·동시 방 한도를 989에서 확인한 후 배포 방식을 결정한다. 생성 중/접속 가능/경기 중/종료 중 상태를 구분하고 중복 생성·빈 방 잔류에 한도를 둔다. 프로세스 강제 종료 시 경기 중간 복원까지 제공하는 것은 별도 범위다.

서버 tick과 화면 FPS는 별개의 예산이다. 기존 870은 동일 PC·60FPS 상한·자동 입력 비교였으므로 WebGL 120FPS 근거로 재사용하지 않는다. 새 기준은 994에서 환경과 8.33ms 예산을 고정하고 995~997에서 측정한다.

## 비용 판단

2026-09-14 공식 가격에는 개발용 20CCU 무료(60GB/월), 조건에 맞는 한 앱의 출시용 100CCU 무료(0.3TB/월), 500CCU 월 $125 등이 있다. 실제 계정의 현재 플랜·가용량은 여기서 확인하지 않았으므로 무료 운영을 보장하지 않는다. [Photon 가격](https://www.photonengine.com/fusion/pricing)

Fusion Server는 Photon Cloud만으로 Unity 경기 로직을 호스팅하지 않는다. 별도 서버 호스팅과 세션 생성/종료 관리가 필요하다. 기존 서버를 재사용하더라도 CPU/RAM 경쟁·트래픽·운영 시간이 생긴다. 대략적인 산정은 `동시 방 수 × 방당 실측 메모리/CPU`와 송수신량에서 시작한다. 기존 Spring 서버와의 동거 가능성은 아직 측정하지 않았다. 이번 실험은 기존 PC에서 수행하며 유료 자원·구독을 생성하지 않는다.

## 검증 결과

실험 코드와 재현 절차: [topology-probe](../../Tools/network/topology-probe/README.md).

네이티브 Server/Client는 실제 Photon 인증·연결 후 권한 tick 증가, RPC 처리, 서버 높이 0.500 복제, 자기 nonce만 1회 수신, 정상 Shutdown을 확인했다. 다른 AppVersion은 `GameNotFound`로 거절됐다. Win64와 WebGL 빌드는 오류 0으로 성공했다.

실제 WebGL 두 탭은 인증 API 호출에서 중단됐다. localhost 요청에 CORS 허용 헤더가 없어 동일 출처 계정 전달 경로를 마련했으나, 해당 경로 실행은 민감 데이터 전달에 대한 자동 승인 검토에서 차단되어 사용자 승인을 기다린다. **WebGL 최소 접속 완료 조건은 아직 충족하지 않았다.** 브라우저 동시 접속·개별 회신·퇴장 후 유지·정상 종료 검증이 남았다. 원본 게임의 회귀나 120FPS 검증을 통과했다는 의미도 아니다.

## 후속 완료 기준

- **988**: 일반 UI로 생성/입장, 별도 방장 권한, 서버 물리·스폰·안내 준비, 6인 종료·퇴장·재경기. 서버는 로컬 플레이어·마이크·카메라를 요구하지 않음.
- **989/990**: 서버 실행/배정과 공개 WebGL 연결, 동일 호환 버전, 혼합 버전 거절, 서버 장애 안내, 배포와 복구.
- **991~993**: 서버 기록 책임 확정, 대표 물건/기절/파괴 장면 재구성, 각 사용자 스킵·후보 없음·오류 복귀.
- **994~997**: 공개 빌드 기준 6인 성능·긴 프레임·GC·메모리·연결 안정성 측정. 미달 구간은 수치와 원인을 남김.
