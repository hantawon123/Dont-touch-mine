# Core

클라이언트와 서버가 함께 사용하는 게임 규칙, 공통 타입, 상수만 둡니다.

- Photon, Steam, UI에 직접 의존하지 않습니다.
- 공통이라는 이유만으로 임시 코드를 넣지 않습니다.

## 참조 규칙

`Game.Core.asmdef`가 참조할 수 있는 것은 `UniTask`와 `R3` 둘뿐입니다.

- **허용**: `UniTask` — 포트 인터페이스가 서버 요청을 `UniTask<T>`로 표현하기 위해서입니다.
- **허용**: `R3.Unity` — `IMatchState`가 경기 상태를 `ReadOnlyReactiveProperty<T>`로
  노출하기 때문입니다. 읽기 전용 타입만 쓰고, `ReactiveProperty`를 만들어 값을 쓰는 쪽은
  `Server`와 `Client`입니다.
- **금지**: Fusion, VContainer, UI 계열, 그리고 `Client`·`Server`·`SOAP`·`Network`.

Fusion을 넣지 않는 이유는 네트워크 타입이 `Network` 계층 밖으로 나가지 않아야 하기
때문입니다. 세션과 플레이어는 `Core`의 중립 타입으로 전달됩니다.

새 참조를 추가하려면 이 목록을 먼저 고치고 팀에 알립니다.
