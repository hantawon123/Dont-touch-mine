# 경기 흐름 MR 검증 기록

대상: S15P21D205-571 / 736 / 737 / 572. 로비 MR !207의 커밋 be142cf2 위에서 진행.

## 구현

- Y키 완료 요청: 현재 차례·유효 배치·직접 보유 여부를 서버에서 검증하고 기존 SkipCurrentHidingTurn으로 종료 시각 갱신. 커서 해제 및 UI 선택 중 요청 차단.
- 배치 재검증: 자기 물건 콜라이더 제외, 다른 장애물과 지지면 검증 유지. 충돌 버퍼 포화 시 거절.
- 엎드린 공격: 로컬 모션/예약 타격, 네트워크 모션, 서버 타격 거절. 앉기·서기 공격 유지.
- 시작 요청: 서버 시각 기준 10초 종료 시각 복제, 로비 HUD 표시, 중복 요청 무시. 참가 구성 변경 시 취소, 완료 후 참가자 확정과 씬 진입 1회. 대기 중 규칙 수정 거절.
- 결과: 씬 정리 중 입력 잠금 유지, 예약 공격 취소. 기존 개인 결과 5초와 skip/복귀 경로 재사용. 몽타주 본문 최대 10초 제한, 전환 연출 별도.

## 확인한 근거

- Game.Bootstrap / Game.Architecture.Tests C# 프로젝트 빌드 오류 0개. 기존 경고 존재.
- 실제 컴파일된 ResultPresentationTests와 MatchOutcomeSystemTests 17건 standalone .NET에서 통과. Unity Test Runner 결과가 아님.
- 숨기기 조기 완료 2건, 자기 콜라이더 배치 검증 1건 테스트 추가 및 컴파일 확인. Unity 런타임 실행은 미실시. standalone 실행 시 Unity ScriptableObject 수명에 의존하므로 해당 테스트의 통과로 산정하지 않음.
- Playground.unity에 SpawnPoint_1~6, WaitingSpawnPoint_1~6, ShredderSpot 존재 확인. 실제 수집은 PlaygroundMatchScene이 담당하며 MatchSceneConfiguration의 비어 있는 직렬화 필드를 곧바로 미연결로 판정하지 않음. 물리 충돌·카탈로그의 런타임 결과는 별도 확인 필요.
- git diff --check 통과.

## 병합 전 실제 Unity / 동일 버전 2인 검증

- Fusion IL weaving 및 추가된 StartCountdownEndsAt / RPC 스키마 반영 후 빌드.
- 생성→10초 표시→1회 씬 진입, 중복 시작·게스트 퇴장/교체·방장 단절 시 취소.
- 양쪽 차례별 미배치/보유/유효 배치/다시 집기/벽 충돌/반복 Y/타인 요청/채팅·Esc 모달 입력.
- 자동 배치, 턴별 맵 복원, 마지막 완료 즉시 탐색, 파쇄기 일반 물건 배출.
- 서기·달리기·앉기 공격 허용, 엎드리기 공격 및 타격 대기 중 자세 전환 거절.
- 자기 물건 직접 보유/바닥/타인 보유/파괴 결과, 개인 결과 5초, 후보 0~3개, 개별·전체 skip, 한 사람만 skip, 호스트 이탈, 로비 복귀·재경기.
- 최종 맵 지정·에셋 교체가 있다면 현재 Playground 기준 검증을 해당 맵에서 재실행.

Jira 완료 상태는 사용자가 요청한 MR 생성 기준으로 처리하며 위 미실행 항목의 통과를 뜻하지 않는다.
