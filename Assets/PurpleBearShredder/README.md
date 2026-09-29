# PurpleBear Shredder V2 — Unity 6 / URP

## 바로 사용하기

1. Unity에서 `Assets > Import Package > Custom Package`로 `PurpleBear_Shredder_v2_URP.unitypackage`를 가져옵니다.
2. `Assets/PurpleBearShredder/Prefabs/PurpleBear_Shredder.prefab`을 씬에 드래그합니다.
3. Play를 누르면 두 칼날 축이 서로 반대 방향으로 4초마다 한 바퀴씩 반복 회전합니다.

미리보기는 `Assets/PurpleBearShredder/Demo/Shredder_Demo.unity`를 여세요.
FBX에 URP 재질을 연결해 두었으며, 프리팹에는 Animator와 충돌 영역까지 설정했습니다.
게임에는 `Prefabs` 폴더의 프리팹을 배치하면 됩니다.

## 구성

- 총 9,292 삼각형, 메시 3개, 모델 재질 8개.
- 4초 반복 회전 클립과 기본 상태 `Spin`이 연결된 Animator Controller.
- 본체용 BoxCollider 5개. 상단 투입구는 열려 있습니다.
- 칼날 영역의 `Collision/ShredZone`은 Trigger입니다.
- 원본 크기는 약 가로 2.96m, 깊이 2.29m, 높이 1.68m이며 씬에서 루트 Scale로 조절할 수 있습니다.
- URP Lit 재질을 사용하며 외부 이미지 텍스처가 필요하지 않습니다.

## 게임 로직 연결

Animator의 `Speed`가 1이면 기본 속도, 0이면 정지합니다. 코드에서도 `animator.speed`로 제어할 수 있습니다.
주황색 버튼과 민트색 표시등은 외관 요소입니다. 버튼 상호작용은 게임의 입력 시스템에서 연결합니다.
`ShredZone` 진입에 따른 피해·물체 삭제·이펙트·사운드는 게임 규칙에 맞춰 연결하세요.
물리 Trigger 이벤트를 쓰는 경우 상호작용하는 물체 쪽에 Collider와 Rigidbody를 설정합니다.
칼날의 회전 자체가 물체를 물리적으로 잘게 나누지는 않습니다.

## 호환 환경

Unity 6000.3.22f1 / URP 17.3 기준입니다. Built-in/HDRP에서는 재질 변환이 필요합니다.
패키지는 프로젝트의 렌더 파이프라인이나 기존 씬 설정을 변경하지 않습니다.
