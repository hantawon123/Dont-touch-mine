# 로비 정적 배칭

## 관측

공개 빌드 `944d8e47`의 로컬 복사본, 동일 PC의 Intel Arc WebGL 컨텍스트에서 혼자 로비를 실행했다. 각 비교는 동일 카메라에서 약 30초간 Unity 프레임을 기록했다.

| 비교 | 정상 | 진단 조건 |
| --- | --- | --- |
| 렌더 해상도 | 831×519: 41.71 FPS | 416×260: 47.76 FPS |
| WebGL draw 명령 | 44.85 FPS | draw 명령만 생략: 56.94 FPS |

두 번째 비교의 정상 상태에서 WebGL draw 호출은 프레임당 약 1,960회였다. 모든 패스를 포함한 브라우저 API 호출 수이며 Unity Batches와 동일한 지표는 아니다. draw 생략은 원인 분리용 실험이며 제품에는 포함하지 않는다. CPU/GPU 각각의 시간이나 개별 렌더 패스 비용을 확정한 결과도 아니다. 기존 Chrome이 GPU 엔진을 많이 사용한 환경이므로 절대 FPS를 다른 PC에 일반화하지 않는다.

## 변경

- WebGL Static Batching 활성화. 플랫폼 설정이므로 다른 씬의 기존 정적 배칭 대상에도 적용된다.
- 로비 배경의 고정·불투명 MeshRenderer 428개에 배칭 플래그 유지/설정, 223개 제외.
- CarryableItem, Rigidbody, Animator, Animation 또는 사용자 스크립트가 있는 상위 계층 제외.
- 투명 재질과 DisableBatching=True 재질 제외.
- 프리팹 변경은 정적 플래그에 한정. 배치, 재질, 조명, 그림자, SSAO, 해상도 품질 변경 없음.
- `Game/Lobby/Rendering/Apply Static Background Batching` 메뉴로 배경 수정 후 재적용 가능.

## 검증과 남은 확인

Unity 6000.3.22f1에서 적용 및 컴파일 확인. 직렬화된 프리팹에서 정적 플래그를 제외한 내용이 기준 버전과 동일함을 비교했다. EditMode의 `LobbyBatchingSafetyTests`는 배칭 대상에 움직이는 계층이나 투명 재질이 섞이지 않는지 검사한다.

새 WebGL 빌드의 성능 및 화면 비교는 아직 진행하지 않았다. 정적 배칭은 빌드에서 적용되므로 에디터 컴파일 통과만으로 개선 폭을 판단할 수 없다. 같은 PC·카메라·해상도에서 30초씩 프레임과 draw 호출, 메모리를 비교하고 집기/던지기, 경기 전환, 로비 복귀를 확인해야 한다. 정적 배칭은 결합 메시 메모리를 추가로 사용하며 재질·라이트맵 경계에 따라 효과가 달라진다.

## 복구

긴급 비활성화는 Player Settings의 WebGL Static Batching을 끄고 재빌드한다. 완전 복구는 이 변경의 ProjectSettings 및 로비 프리팹을 함께 되돌린다. 기존 공개 배포는 변경하지 않았다.
