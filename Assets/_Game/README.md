# _Game

프로젝트에서 직접 작성하거나 관리하는 코드와 콘텐츠만 둡니다.

```text
Bootstrap -> Client, Server, Network, Backend
Client    -> Core, SOAP
Server    -> Core, SOAP
Network   -> Core, Server
Backend   -> Core
SOAP      -> Core
Content   : 씬, 프리팹, 오디오와 ScriptableObject 에셋
Tests     : EditMode와 PlayMode 테스트
```

`Client`와 `Server`는 서로 직접 참조하지 않습니다. `Network`는 Photon Fusion과 맞닿는
유일한 계층이며, `Server`가 확정한 결과를 복제하는 한 방향으로만 의존합니다.
`Backend`는 같은 규칙을 REST 서버에 적용한 것으로, `UnityWebRequest`를 만지는 유일한
계층입니다. 계정·친구·접속 상태는 `Core/Ports`의 포트로만 노출되므로 화면은 HTTP를
알지 못합니다. 경기 규칙을 판정하는 `Server`와는 다른 것입니다 — 저장소의 `backend/`
(Spring) 와 짝이고, 커밋 태그도 `[SV]`가 아니라 `[BE]` 쪽입니다.

외부 패키지와 구매 에셋은 별도 위치에 보존하고 원본을 직접 수정하지 않습니다.
