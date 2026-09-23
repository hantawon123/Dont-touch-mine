# 2026-09-23 최적화 MR 검증 범위

대상: `feature/client/performance-validation-20260923` → `develop`. 기존 검증 기본 소스 `2e95f0f1f`에 최신 `develop`의 `792fd52e2`를 통합했다. 기존 !441은 사용자가 직접 닫으며, 운영 배포·자동 병합은 수행하지 않는다.

## 포함한 변경

| 커밋 | 변경 | 의도 |
|---|---|---|
| `c880c6eb1` | 무화면 AvatarFacePortrait/HidingIntroItemPreview 렌더링 가드 | 그래픽 장치가 없는 시험 봇에서만 렌더링 생성 생략. 일반 화면 유지 |
| 위 커밋 | 전용 서버 EmoteWheelController 비활성화 | 서버에서 필요 없는 로컬 UI 입력 처리 제거. 네트워크 감정표현 복제 유지 |
| 위 커밋 | 방 설정 인원 계산 회귀 테스트 | 최신 develop에 같은 문제의 수정이 있어 제품 코드는 최신 `CountActivePlayers(_runner)`를 그대로 유지. 6/6 허용·7/6 거절 테스트만 추가 |
| `56eb605c8` | 음성 처리 구간 ProfilerMarker | 검색·음소거·전송 비용을 분리 측정. 서비스 주기 변경 없음 |
| `f4fd4f487` | PlayerRoster 기반 음성 대상 탐색·목록 재사용 | 전체 씬을 매 프레임 검색하지 않고 현재 아바타 안에서 Speaker 검색. 명단이 없거나 비면 기존 전체 검색 유지 |

## 확인한 증거

- 마지막 음성 변경 관련 Windows EditMode 회귀: **189/189**, 실패0. 결과 파일 `voice-roster-after-tests.xml`.
- 음성 변경의 Development/일반 Windows 빌드 모두 성공. `voice-roster-pair-build.log` 종료코드0.
- `ec2-voice-roster-profile-r1`: **6명 경기 흐름·이동·결과 화면·재경기 통과**, 각2라운드에서6개 Speaker 듣기 off/on 및 Voice 방 연결 확인.
- 위 실행에서 기존 Presence 종료 예외가6건 남았다. 기능 검사 통과와 무오류 판정을 구분하며, 로그 검증기 종료코드2를 숨기지 않는다.
- 실제 사람 간 음성 청취 품질과 모든 HUD 시각 검증을 대체하지 않는다.
- 전용 서버 가드의 이전 관련 회귀142개 통과, 6인 이동·결과·재경기 및 감정표현 복제/애니메이션 상태 검사 기록을 보존했다. 보이는 감정표현 UI의 전수 수동 검증과 구분한다.

| 개발용 계측 | 변경 전 | 변경 후 | 해석 |
|---|---:|---:|---|
| VoiceRig 수신 상태 처리(ms/frame) | 1.208178 | 0.106601 | 1,289/1,276프레임 단회 관측. 약91.2% 감소 |
| 전용 서버 EmoteWheel 아래 GC 할당(B/frame) | 4,464.51 | 해당 경로 제거 | 전용 서버에서 필요 없는 UI 처리 제거 확인 |

음성 후측정 때 다른 Unity 프로젝트가 열려 있었다. 개발 프로파일 구간 개선을 일반 게임 FPS·서버 최대CCU·요금 절감률로 환산하지 않는다. FPS 확정 비교는 사용자 요청으로 보류했다. 원본 게임에 이미 포함된 과거 성과는 [별도 요약](optimization-summary-20260923.md)과 구분한다.

## 이번 MR에서 제외한 것

- PresenceHeartbeat 종료 순서 수정: 관련 자동210개는 통과했지만, 실제 완주 검증은 EC2 CPU 안전 가드로 중단되어 미커밋 상태로 보존.
- HUD 여백 재사용: 사전 테스트만 작성됐고 수정 전 예상 실패1건 상태. 제품 수정 및 테스트 파일 모두 제외.
- 시험용 자동 봇·계측 도구, 원시 로그, 장치 식별값, Unity 자동 생성 설정, Packages 변경.
- 미채택 무음 봇·렌더링 실험과 GPU 품질 변경.

마지막 `ec2-presence-fixed-r1`/flow21은 EC2 전체 CPU95.98%를 감지한 기존90% STOP 가드가 임시 서버를 종료했다. 전체 경기 완료0건으로 성과에서 제외했다. 시험 종료 후 운영active/backendUP/운영 재시작횟수12 유지, 소유 시험 프로세스 정리를 확인했다. 사용자 지시로 추가 시험은 중지했다.

## 최신 develop과의 통합 검증

사용자 요청으로 `origin/develop`의 `792fd52e2`를 새 격리 브랜치에 통합했다. 충돌은 `NetworkRunnerService.cs`의 인원 계산 한 곳이며, 최신 `CountActivePlayers(_runner)`를 유지해 해결했다. 해당 파일 전체가 최신 develop과 동일함을 확인했다. 최신 방 정원 표시, 방 목록, 설정 검증·경고 및 하이라이트 진단 로직도 보존한다. 기존 격리 폴더의 Presence/HUD 실험 및 설정 변경은 옮기지 않았다.

통합 후 검증 결과는 아래에 기록한다. 과거 빌드·6인 경기 결과는 위의 이전 소스 검증 증거이며 최신 통합본의 실게임 재검증으로 간주하지 않는다. 이번 요청에서는 EC2 부하 시험이나 FPS 측정을 재개하지 않는다.

- Unity 6000.3.22f1 / Win64 / EditMode, 필터 `Voice;NetworkContractTests;Emote;SessionPropertyMapper;SessionCloudRecovery`: **226/226 통과, 실패0, 건너뜀0**, 테스트 실행 종료코드0. 새 결과: `mr-integration-tests.xml`, 로그: `mr-integration-tests.log` (로컬 검증 자료에 보존).
- 테스트 과정에서 최신 통합 코드 재컴파일 완료. 새 Windows/Linux 플레이어 빌드 및 실제 멀티플레이 실행은 이번 통합에서 다시 수행하지 않았다.
- `NetworkRunnerService.cs`는 `origin/develop`과 차이 없음. 미해결 병합 항목0 및 MR 차이의 `git diff --check` 확인.
- 임포트 중 일부 ItemCollection `.meta`의 유효 GUID 경고 관측. 표본 `tools_4f0817e5c1.prefab.meta`는 최신 develop과 내용 해시가 동일(`c9e49ded89f3515145165e61435f0e7feea68ff1`)하여 이번 변경에 포함하지 않았다. 자동 테스트 통과가 전체 에셋 정상 판정을 뜻하지 않는다.

전체 최적화 이슈994는 미완료이므로 자동 완료 문구는 넣지 않는다. 게임 기능·HUD 불변 조건을 유지하며, 사람의 음성 청취 및 HUD 확인과 최소 1명의 리뷰 승인 후 병합 여부를 결정한다.

## 보고서 재현 자료

요약 그래프의 PC 렌더링 비교는 일반 Windows Mono/D3D11/RTX4070 Laptop/1920×1080/3인 정지 고정 시야다. 변경 전 FPS는 `78.17,77.68,74.51,76.22`, 변경 후는 `111.97,111.55,109.92,109.35`로 중앙값76.95→110.735다. 원자료 묶음: `native-three-loop-timing`, `native-three-gpu-result`.

서버 빈 방 대조는 같은 EC2 바이너리를 각18초 측정했다. 무제한1.0032core/15403.78loopFPS, 60회제한0.0517core/59.99FPS, 120회제한0.0667core/119.98FPS, 다시무제한1.0050core/13470.79FPS였다. 실제 제품 적용값64회와 기술 대조60회를 구분한다. 원자료: `server-frame-cap-results.json`, `server-capped-three-20260916.txt`. 측정 파일은 별도 작업 기록에 보존하며 저장소에는 민감한 원시 접속 로그를 올리지 않는다.
