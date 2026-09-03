# 기획 (Planning)

게임 규칙과 시스템 기획 문서를 둡니다.

## 권장 구성

- `mvp.md` — MVP 기획서 (전체 규칙 요약)
- `기획_내용_정리_0901.md` — 페이지별 기획 요약
- `wireframes/` — Jira `[와이어]` 태스크별 화면 설명·체크리스트
- `systems/` — 시스템별 상세 기획
  - 예: `combat.md`(전투·기절), `items.md`(물건·배치·파쇄기), `match-flow.md`(페이즈·턴·승리 조건)
- `balancing.md` — 조정 가능한 수치 목록 (이동 속도, 제한 시간, 사용 횟수 등)

## 참고

- 규칙이 바뀌면 문서를 함께 수정합니다. 커밋 이력이 곧 기획 변경 이력이 됩니다.
- 수치의 실제 원본은 Unity의 Config 에셋(`Assets/_Game/Content/Config/`)이며,
  `balancing.md`에는 의도와 근거를 기록합니다.
