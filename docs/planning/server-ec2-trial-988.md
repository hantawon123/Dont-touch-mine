# 988 — 기존 EC2 게임 서버 검증 결과

기존 EC2에서 Linux Unity 서버를 실행하고 실제 게임 흐름을 확인했다. **경기 서버 연산은 EC2에서 수행했고, 로컬 PC는 클라이언트만 실행했다.** 시험 종료 후 서버는 정상 종료한 상태이며 MR은 아직 생성하지 않았다.

## 변경과 확인

Linux Dedicated Server/Mono 빌드와 Server 타깃의 Fusion 컴파일 기호를 추가했다. DI 등록 생략만으로는 씬의 UI Awake/OnEnable 실행을 막지 못해 제거된 폰트 초기화 오류가 발생했다. 서버 빌드의 씬 복사본에서 Canvas와 명시적인 UI 생성 컴포넌트·UI 전용 Scope를 제외하도록 보완했다. 게임용 Scope와 물리 구성을 보존하며 원본 씬은 수정하지 않는다.

수정 후 Linux 빌드 오류 0, 모든 빌드 씬의 전처리 통과. 실제 EC2 서버 1개와 기존 Windows 클라이언트 6개로 다음을 확인했다.

- 전원 Client 접속, 일반 참가자의 시작 RPC 거절.
- 6인 경기의 숨기기·탐색·비공개 아이템 배정.
- 일반 참가자 1명 퇴장 후 남은 5인 진행.
- 결과 수신·결과 씬 로드, 5인 재경기와 새 배정.
- 재경기 중 방장 정상 퇴장 후 전체 세션과 EC2 서버 종료.
- 검증기 PASS, 서버·네이티브 클라이언트 로그에 런타임 예외 없음. 남은 실험 클라이언트 창 하나는 종료했다.

실제 WebGL도 EC2 서버의 `988EC2` 방을 확보해 `Client / IsServer=False`로 접속했다. 로비의 1/6 표시, 설정에서 나가기, 홈 복귀와 EC2 서버 종료를 확인했다.

## 자원 관측

서버에는 CPU 1.5 vCPU, 메모리 3 GiB의 상한을 설정했다. 실행 최대 수명은 20분이며 자동 재시작·부팅 시 자동 실행은 설정하지 않았다.

| 항목 | 이번 6인 흐름 시험 관측 |
| --- | --- |
| 서버 메모리 최고 관측값 | 464.51 MiB, cgroup MemoryPeak |
| 평균 CPU 사용 | 약 1.127 vCPU, CPUUsageNSec의 시간 차이 기준 |
| 관측 구간 최고 평균 CPU | 약 1.172 vCPU, 순간 최고값 아님 |
| EC2 가용 메모리 최저 관측값 | 10,245 MiB |
| 기존 백엔드 health | 18/18 HTTP 200 |
| health 응답 시간 | 최소 41.47ms / 중앙값 45.75ms / 최대 64.98ms |
| Linux 서버 배치 크기 | 약 268 MiB, 압축 전송 약 140 MB |

한 방의 자동 흐름 시험이며 동시 방 수·최대 수용량·실제 플레이 입력 부하를 측정한 것은 아니다. health 응답만으로 모든 백엔드 API의 성능을 보장하지 않는다.

## 남은 항목

- 서로 다른 PC에서 WebGL 6인 경기·퇴장·재경기와 실제 이동·물건·충돌·파쇄기·음성 확인은 사용자가 직접 진행한다. 기존 자동 시험은 입력 없이 실제 경기 타이머를 진행했다.
- WebGL에서 Escape로 설정을 열 때 기존 Chromium UnknownError가 재발했다. 이후 설정 UI 조작·정상 퇴장·홈 복귀는 완료했으나 원인은 미확정이다. 일회성으로 해소됐다고 판단하지 않는다.
- 기존 친구·접속 상태 API의 401 문제도 남아 있으며 presence 401을 이번 WebGL 실행에서도 관측했다.
- 120 FPS나 운영용 다중 방 수용량을 보장하지 않는다. 서버 구매·공개 WebGL 재개·Jenkins 활성화·방화벽 변경은 하지 않았다.

## 재실행과 증거

- [빌드·EC2 실행 도구](../../Tools/network/server-flow/README.md), [6인 수동 확인](../../Tools/network/server-flow/manual-six-pc.md)
- 기존 WebGL ZIP과 실행 도구는 `검증패키지/988/`에 있다. 호환 버전은 `988-local-v1`.
- EC2의 검증된 바이너리: `/home/ubuntu/d205-game-server-988/linux-v2/`
- 이전 `linux-v1`은 폰트 오류가 있었던 버전이므로 사용하지 않는다.
- 저장소 상세 기록: `docs/planning/server-ec2-trial-988.md`
- 허용 필드만 추출한 6인 증거: `Tools/network/server-flow/ec2-evidence-2026-09-14.txt`
- WebGL·자원 요약: `Tools/network/server-flow/ec2-summary-2026-09-14.txt`
- 로컬 원본 로그: `.build/server-988-ec2-run1/` (전체 인증 로그는 외부에 공유하지 않는다.)

검증한 Linux 압축 파일 SHA-256:

```text
161d479dcaf1dda4ed70b8a1690ae0c52bf184b5f07f4b80107223edbf2205b1
```
