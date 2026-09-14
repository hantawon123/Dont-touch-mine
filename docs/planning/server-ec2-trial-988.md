# 988 EC2 시험 실행 — 중단 시점 기록

2026-09-14 사용자 요청으로 안전하게 중단했다. **EC2 배치·서버 기동은 확인했으나 경기 검증은 완료하지 않았다.** MR은 생성하지 않는다.

## 확인한 결과

- 기존 EC2: Ubuntu 24.04 x86_64, 4 vCPU, 메모리 약 15 GiB. 시험 전 디스크 약 204 GB와 메모리 약 10 GiB 여유.
- Unity 6000.3.22f1 공식 Linux Mono·Linux Dedicated Server 모듈을 로컬 에디터에 설치했다. 설치 파일 서명이 Unity Technologies이며 유효함을 확인했다.
- 일반 Linux 플레이어 빌드는 불필요한 그래픽 컴파일 비용 때문에 중단했다. Dedicated Server/Mono와 `dedicatedServerOptimizations`로 전환했다.
- Server 타깃의 Fusion 컴파일 기호 누락을 발견해 ProjectSettings에 Standalone/WebGL과 동일한 기호를 추가했다. 이후 Linux 서버 빌드 성공, 오류 0.
- 디버그 심벌을 제외한 전송 압축 파일 약 140 MB, EC2에서 푼 실행 폴더 약 268 MB. SHA-256 대조 성공.
- `/home/ubuntu/d205-game-server-988/linux-v1/`에서 임시 systemd 서비스 `d205-game-988-trial`로 실행했다. CPU 상한 150%(1.5 vCPU), 메모리 상한 3 GiB, 최대 수명 20분, 자동 재시작·부팅 시 자동 실행 없음.
- 기존 계정 인증과 Photon 접속 후 `Session '988EC2' started as Server. IsServer=True`, `[Server] Ready` 확인.
- 서버 기동 시 메모리 cgroup 현재값 약 334 MiB를 한 번 관찰했다. 경기 부하 측정값이 아니며 동시 방 수를 산정할 수 없다.

## 해결 중인 문제

전용 서버 최적화가 제거한 폰트를 HomeMenuView/LoadingView 등의 씬 UI가 Awake/OnEnable에서 초기화하며 `Korean TMP font is missing` 예외가 발생했다. DI 등록을 생략하는 것만으로 씬에 미리 배치된 MonoBehaviour의 초기화를 막지 못했다. 오류가 있는 서버는 중단했다.

`DedicatedServerScenePreparation`을 추가했다. 서버 빌드의 임시 씬 복사본에서 Canvas 오브젝트를 비활성화하고, Canvas 밖에서 UI를 생성하는 HomeMenuView·CharacterClosetView·ResultView·SettingsView·MatchChatBubbleView·EndingStage 컴포넌트를 제거한다. 원본 씬은 저장하지 않으며 같은 GameObject의 LifetimeScope를 유지한다. UI 아래 Collider/Rigidbody/LifetimeScope가 있으면 무작정 비활성화하지 않고 빌드를 실패시킨다.

첫 컴파일에서 `BuildSummary.subtarget`이 지원되지 않는 오류가 났다. 설치된 ShaderGraph 코드의 사용례를 확인하고 `EditorUserBuildSettings.standaloneBuildSubtarget`으로 수정했다. **사용자 중단 요청 이후 새 컴파일·빌드는 실행하지 않았으므로 이 수정과 씬 전처리는 아직 검증 전이다.**

## 현재 안전 상태

- EC2 시험 서버·측정 루프 종료 확인. 임시 서비스는 inactive, 해당 실행 파일 프로세스 없음.
- 기존 백엔드 health HTTP 200, MySQL/Metabase 정상 상태. 운영 서비스·Jenkins·방화벽 설정 변경 없음.
- 로컬 검증 빌드 프로세스 모두 종료. 사용자 Unity 에디터와 사용자 편집 씬은 유지.
- 시험 전용 폴더·실험 인증 상태·빌드·로그는 재개를 위해 보존한다. EC2에 남은 linux-v1은 폰트 오류가 확인된 이전 바이너리이므로 최종 검증본으로 취급하지 않는다.

## 재개 순서

1. 현재 변경과 사용자 씬을 구분하고 [검증 도구 README](../../Tools/network/server-flow/README.md)를 확인한다.
2. 독립 프로젝트의 씬 전처리 코드가 최신인지 확인하고 Linux 전용 서버를 새 출력 폴더로 빌드한다. 아직 검증되지 않은 새 전처리의 컴파일부터 확인한다.
3. `[ServerBuild]` 로그로 씬별 UI 제외를 확인한다. 물리·서버 씬 구성 보존과 UI 초기화 예외가 사라졌는지 확인한다. 예외가 더 있으면 호출 흐름을 확인해 수정한다.
4. 새 빌드 해시를 검증해 EC2의 별도 버전 폴더에 배치하고 같은 자원 상한으로 실행한다. 이전 오류 로그를 덮어쓰지 않는다.
5. 검증된 Windows 클라이언트 6개를 연결해 6인 경기→1명 퇴장→5인 결과·재경기→방장 퇴장→서버 종료를 실행한다. 그동안 기존 백엔드 응답과 서버 CPU·메모리를 측정한다.
6. 실제 WebGL의 방 생성·입장·퇴장을 EC2 서버에 연결해 확인한다. 서로 다른 PC의 WebGL 6인 최종 확인은 별도로 사용자가 진행한다.
7. 실행 결과·한계·재시작 방법을 보고하고 커밋한다. 사용자 결정 전 MR은 만들지 않는다.

기존 클라이언트/서버 호환 버전은 `988-local-v1`, 이번 EC2 시험 방 코드는 `988EC2`다. 계정·토큰·기기 ID와 전체 인증 응답은 커밋하지 않는다. 이번 서버 기동 확인만으로 EC2 경기 검증이나 988 전체 완료를 선언하지 않는다.
