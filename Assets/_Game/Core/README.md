# Core

클라이언트와 서버가 함께 사용하는 게임 규칙, 공통 타입, 상수만 둡니다.

- Photon, Steam, UI에 직접 의존하지 않습니다.
- 공통이라는 이유만으로 임시 코드를 넣지 않습니다.

## 참조 규칙

`Game.Core.asmdef`가 참조할 수 있는 것은 `UniTask` 하나뿐입니다.

- **허용**: `UniTask` — 포트 인터페이스가 서버 요청을 `UniTask<T>`로 표현하기 위해서입니다.
- **금지**: R3, Fusion, VContainer, UI 계열, 그리고 `Client`·`Server`·`SOAP`.

R3를 넣지 않는 이유는 런타임 상태의 소유가 `Client`이기 때문입니다. 서버가 확정한
값은 `Core`의 포트를 통해 전달되고, `ReactiveProperty`로 바꾸는 것은 `Client`가 합니다.

새 참조를 추가하려면 이 목록을 먼저 고치고 팀에 알립니다.
