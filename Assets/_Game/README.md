# _Game

프로젝트에서 직접 작성하거나 관리하는 코드와 콘텐츠만 둡니다.

```text
Bootstrap -> Client, Server, Network
Client    -> Core, SOAP
Server    -> Core, SOAP
Network   -> Core, Server
SOAP      -> Core
Content   : 씬, 프리팹, 오디오와 ScriptableObject 에셋
Tests     : EditMode와 PlayMode 테스트
```

`Client`와 `Server`는 서로 직접 참조하지 않습니다. `Network`는 Photon Fusion과 맞닿는
유일한 계층이며, `Server`가 확정한 결과를 복제하는 한 방향으로만 의존합니다. 외부 패키지와 구매 에셋은 별도 위치에 보존하고 원본을 직접 수정하지 않습니다.
