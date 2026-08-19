# UnityTestRepository 협업 가이드

이 문서는 프로젝트 참여자가 같은 Git 및 Unity 작업 규칙을 따르기 위한 안내서입니다. 게임 기획 내용은 포함하지 않습니다.

## 기술 환경

- Unity `6000.3.22f1`
- Universal 3D / Universal Render Pipeline(URP)
- Photon Cloud 기반 멀티플레이
- Steam 연동 예정
- ScriptableObject Architecture Pattern(SOAP)

Unity 버전과 렌더 파이프라인이 다르면 씬, 프리팹, 머티리얼과 `ProjectSettings`에서 불필요한 변경이 생길 수 있으므로 반드시 같은 환경을 사용합니다.

## 저장소 받기

```bash
git clone https://github.com/hantawon123/UnityTestRepository.git
cd UnityTestRepository
```

Unity Hub에서 clone한 저장소 폴더를 프로젝트로 엽니다. 처음 열 때 생성되는 `Library`, `Temp`, `Logs`, `UserSettings`, `.csproj`, `.slnx` 파일은 Git에 올리지 않습니다.

## 브랜치 구조

```text
main
├─ develop/client
│  └─ 유형/#이슈번호-작업내용
└─ develop/server
   └─ 유형/#이슈번호-작업내용
```

| 브랜치 | 용도 |
|---|---|
| `main` | 검토가 끝난 통합 버전. 직접 push하지 않고 Pull Request로만 병합 |
| `develop/client` | 입력, 캐릭터, 카메라, UI와 클라이언트 표현 작업 통합 |
| `develop/server` | Photon 연결, 방, 권한, 상태 동기화와 경기 진행 작업 통합 |
| `유형/#이슈번호-작업내용` | 이슈 단위의 개별 작업 |

`main`에는 다음 보호 규칙이 적용되어 있습니다.

- Pull Request 필수
- 승인 1명 필수
- 관리자도 직접 push할 수 없음
- 새 커밋이 추가되면 기존 승인 취소
- 대화가 해결되어야 병합 가능
- 강제 push와 브랜치 삭제 금지

## 이슈 및 커밋 유형

이슈 제목, 커밋 메시지와 Pull Request 제목 맨 앞에 작업 유형을 표시합니다.

| 유형 | 설명 |
|---|---|
| `[FEAT]` | 새로운 기능 구현 |
| `[MOD]` | 코드 및 내부 파일 수정 |
| `[ADD]` | 부수적인 코드, 라이브러리 또는 새로운 파일 추가 |
| `[CHORE]` | 버전, 패키지 구조, 타입과 변수명 등의 작은 작업 |
| `[DEL]` | 불필요한 코드나 파일 삭제 |
| `[UI]` | UI 작업 |
| `[FIX]` | 버그 및 오류 해결 |
| `[HOTFIX]` | Issue 또는 QA에서 발견된 긴급 버그 해결 |
| `[MERGE]` | 브랜치 병합 |
| `[MOVE]` | 프로젝트 내 파일 또는 코드 이동 |
| `[RENAME]` | 파일 이름 변경 |
| `[REFACTOR]` | 코드 전면 수정 |
| `[DOCS]` | README 또는 Wiki 등의 문서 개정 |

커밋 메시지는 다음 형식을 사용합니다.

```text
[유형] 작업 내용

[FEAT] 입력 처리 추가
[FIX] 카메라 회전 오류 수정
[DOCS] 협업 가이드 수정
```

한 커밋에는 가능한 한 하나의 목적만 포함합니다. 관계없는 씬, 프리팹, 설정 파일을 함께 커밋하지 않습니다.

## 이슈 컨벤션

기능 구현이나 수정 전에 이슈를 먼저 생성하고 Assignees에 본인을 등록합니다.

이슈 제목:

```text
[유형] 작업 내용

[UI] 로그인 화면 구현
```

이슈 내용:

```markdown
## What is this issue? 🛠
이슈 설명

## Progress 🏃‍♀️
- [ ] 할 일1
- [ ] 할 일2
```

## 브랜치 컨벤션

앞서 만든 이슈 번호를 사용해 담당 develop 브랜치에서 작업 브랜치를 생성합니다. 유형은 소문자로 작성하고 작업 내용은 영문 `snake_case`를 사용합니다.

```text
유형/#이슈번호-작업내용

ui/#1-login_view
feat/#12-network_room
```

클라이언트 작업:

```bash
git switch develop/client
git pull origin develop/client
git switch -c ui/#1-login_view
```

Photon 및 네트워크 작업:

```bash
git switch develop/server
git pull origin develop/server
git switch -c feat/#12-network_room
```

작업 후:

```bash
git status
git add 변경한_파일
git commit -m "[유형] 작업 내용"
git push -u origin 현재_브랜치명
```

## Pull Request 컨벤션

- Assignees에 본인을 등록합니다.
- Reviewers에 리뷰할 팀원을 등록합니다.
- 대상 브랜치가 `develop/client` 또는 `develop/server` 중 올바른 브랜치인지 확인합니다.

PR 제목:

```text
[유형/#이슈번호] 작업 내용

[UI/#1] 1주차 화면 구현
```

PR 내용:

```markdown
## Related issue 🛠
- closed #이슈번호

## Work Description ✏️
- 작업 내용

## Screenshot 📸
<img src="" width="360"/>
or
<video src="" width="360"/>

## Uncompleted Tasks 😅
- [ ] Task1

## To Reviewers 📢
```

병합 흐름:

```text
유형/#이슈번호-작업내용 -> develop/client 또는 develop/server
develop/client          -> main
develop/server          -> main
```

두 develop 브랜치가 같은 파일을 수정했다면 한쪽을 먼저 병합한 후 다른 브랜치에 `main`의 최신 변경을 반영합니다. 장기간 분리된 상태로 두지 않습니다.

## Merge 컨벤션

Pull Request를 병합할 때 Merge 제목을 다음과 같이 수정합니다.

```text
[MERGE] #이슈번호 -> 대상 브랜치

[MERGE] #1 -> develop/client
[MERGE] #12 -> develop/server
```

## 프로젝트 폴더 구조

```text
Assets/
└─ _Game/
   ├─ Core/
   ├─ Client/
   ├─ Server/
   ├─ SOAP/
   └─ Content/
```

### `Core`

클라이언트와 서버가 함께 사용하는 게임 규칙, 공통 타입과 상수를 둡니다. Photon, Steam, UI에 직접 의존하지 않습니다.

### `Client`

입력, 캐릭터 이동, 카메라, UI, 애니메이션과 로컬 표현을 둡니다. 서버가 복제한 상태를 표현하며 중요한 결과를 클라이언트 단독으로 확정하지 않습니다.

### `Server`

Photon 연결, 방과 로비, 네트워크 권한, 상태 동기화와 경기 진행을 둡니다. 여기서 Server는 별도 Spring Boot 서버가 아니라 Unity 안의 Photon 네트워크 영역을 의미합니다.

### `SOAP`

ScriptableObject Architecture Pattern의 공통 타입을 둡니다.

- `Definitions`: 정적 데이터 정의
- `Config`: 조정 가능한 프로젝트 설정
- `Events`: UI, 사운드 등 로컬 시스템에 전달하는 이벤트 채널

ScriptableObject에는 정적 설정과 로컬 이벤트를 둡니다. 현재 플레이어 상태, 방 정보, 점수, 네트워크 오브젝트 상태와 세션 데이터는 저장하지 않습니다.

Photon 네트워크 상태는 `NetworkBehaviour`와 `[Networked]` 프로퍼티를 원본으로 관리하고, 상태가 변경된 뒤 SOAP 이벤트를 통해 UI와 사운드에 알립니다.

```text
입력
  -> Photon RPC / State Authority
  -> [Networked] 상태 변경
  -> 다른 참가자에게 복제
  -> 로컬 SOAP 이벤트
  -> UI / 사운드 / 이펙트 갱신
```

### `Content`

팀이 관리하는 씬, 프리팹, 오디오와 ScriptableObject `.asset` 파일을 둡니다. 구매한 원본 에셋은 별도 폴더에 보존하고, 프로젝트에서 수정할 프리팹 Variant를 `Content`에 둡니다.

## Unity와 Git 규칙

- `Assets`, `Packages`, `ProjectSettings`를 커밋합니다.
- 모든 Unity 에셋은 대응하는 `.meta` 파일과 함께 이동하고 커밋합니다.
- `Library`, `Temp`, `Logs`, `UserSettings`, `.csproj`, `.slnx`는 커밋하지 않습니다.
- Unity의 Version Control Mode는 `Visible Meta Files`를 사용합니다.
- Asset Serialization Mode는 `Force Text`를 사용합니다.
- 외부 패키지와 구매 에셋의 원본 파일은 직접 수정하지 않습니다.
- 대용량 바이너리 에셋은 팀 합의 후 Git LFS로 관리합니다.

## 씬과 프리팹 충돌 방지

Unity 씬과 프리팹은 Git에서 자동 병합하기 어렵습니다.

- 메인 씬은 한 명의 담당자만 수정합니다.
- 다른 작업자는 개별 프리팹이나 Prefab Variant로 작업합니다.
- 같은 프리팹을 동시에 수정하기 전에 팀에 알립니다.
- Unity를 닫거나 저장한 후 `git status`로 의도하지 않은 변경을 확인합니다.
- `ProjectSettings` 변경은 Pull Request 설명에 이유를 기록합니다.

## Pull Request 확인 항목

- [ ] 올바른 develop 브랜치에서 작업을 시작했는가?
- [ ] 대상 브랜치가 올바른가?
- [ ] 관련 없는 파일이 포함되지 않았는가?
- [ ] `.meta` 파일이 빠지지 않았는가?
- [ ] 씬이나 프리팹 충돌 가능성을 확인했는가?
- [ ] Photon 네트워크 상태를 ScriptableObject에 저장하지 않았는가?
- [ ] Unity에서 실행 또는 필요한 멀티플레이 테스트를 완료했는가?

세부 규칙은 [CONTRIBUTING.md](CONTRIBUTING.md)와 `Assets/_Game` 아래 각 폴더의 README도 확인합니다.
