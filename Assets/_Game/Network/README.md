# Network

Photon Fusion과 맞닿는 코드만 둡니다. 러너 생성, 세션 시작과 종료, 방 목록 수신,
스폰과 상태 복제가 여기에 있습니다.

- `Server`가 확정한 규칙 판정을 참가자에게 복제하는 것이 이 계층의 역할입니다.
  반대 방향은 없습니다. `Server`는 Photon을 모릅니다.
- Fusion 타입(`NetworkRunner`, `PlayerRef`, `NetworkObject`)은 이 계층 밖으로
  나가지 않습니다. 바깥에는 `Core`의 중립 타입으로 전달합니다.
- `Client`를 직접 참조하지 않습니다.

## 왜 `Server`와 분리했나

`Server`는 경기 규칙을 순수 C#으로 판정하고 EditMode 테스트로 검증합니다.
Fusion을 같은 어셈블리에 두면 그 테스트에 Photon이 딸려 들어옵니다.
전송 계층만 떼어내면 양쪽 모두 원래 목적대로 남습니다.
