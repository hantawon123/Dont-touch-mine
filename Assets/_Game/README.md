# _Game

프로젝트에서 직접 작성하거나 관리하는 코드와 콘텐츠만 둡니다.

```text
Bootstrap -> Client, Server
Client    -> Core, SOAP
Server    -> Core, SOAP
SOAP      -> Core
Content   : 씬, 프리팹, 오디오와 ScriptableObject 에셋
Tests     : EditMode와 PlayMode 테스트
```

`Client`와 `Server`는 서로 직접 참조하지 않습니다. 외부 패키지와 구매 에셋은 별도 위치에 보존하고 원본을 직접 수정하지 않습니다.
