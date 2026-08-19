# 협업 규칙

## 브랜치

- `main`: 배포 가능한 통합 브랜치. 직접 push하지 않고 Pull Request로만 병합합니다.
- `develop/client`: 캐릭터, 입력, 카메라, UI와 클라이언트 표현 작업을 통합합니다.
- `develop/server`: Photon 방, 권한, 동기화와 경기 상태 작업을 통합합니다.
- 실제 작업은 `feature/client-*` 또는 `feature/server-*` 브랜치에서 시작하고 해당 develop 브랜치로 Pull Request를 보냅니다.

## Unity 작업

- 씬과 프리팹은 동시에 수정하지 않도록 담당자를 정합니다.
- `.meta` 파일은 에셋과 함께 커밋합니다.
- `Library`, `Temp`, `Logs`, `UserSettings`는 커밋하지 않습니다.
- Photon의 네트워크 상태는 `NetworkBehaviour`와 `[Networked]`를 원본으로 사용합니다.
- ScriptableObject는 정적 설정, 아이템 정의, 로컬 이벤트 전달에 사용합니다.

## 병합 흐름

```text
feature/client-* -> develop/client --\
                                     -> main
feature/server-* -> develop/server --/
```
