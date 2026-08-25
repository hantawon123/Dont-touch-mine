# 협업 규칙

## 1. 브랜치 전략 (Gitflow)

- `main`: 배포 브랜치. 배포 시점에만 `release`를 병합합니다. 직접 push하지 않습니다.
- `release`: 배포 준비 브랜치. `develop`에서 완성된 내용을 모아 검증한 뒤 `main`으로 보냅니다.
- `develop`: 통합 브랜치. 모든 작업이 모이는 기본 브랜치이며, Merge Request로만 병합합니다.
- `feature/*`: 실제 작업 브랜치. `develop`에서 분기해서 작업하고 `develop`으로 Merge Request를 보냅니다.
  - **이름 규칙**: `feature/파트/작업내용` (파트는 `client` 또는 `server` 소문자)
  - 예시: `feature/client/login`, `feature/client/room-lobby`, `feature/server/match-sync`
- `hotfix/*`: 배포 후 긴급 수정 브랜치. `main`에서 분기하고 `main`과 `develop` 양쪽에 병합합니다.

### 작업 순서

```bash
# 1. develop 최신화 후 feature 브랜치 생성
git checkout develop
git pull
git checkout -b feature/client/login

# 2. 작업 후 커밋 컨벤션에 맞춰 커밋
git add .
git commit -m "S15P21D205-91 [CL] feat: 로그인 기능 구현"

# 3. push 후 GitLab에서 develop 대상 Merge Request 생성
git push origin feature/client/login
```

- 리뷰 승인 후 병합하고, 병합된 feature 브랜치는 삭제합니다.
- 로컬은 주기적으로 `git fetch --prune`으로 삭제된 원격 브랜치 참조를 정리합니다.

## 2. 커밋 컨벤션

- **포맷**: `이슈번호 [파트] 태그: 제목` (예: `S15P21D205-91 [SV] feat: 로그인 기능 구현`)
- **이슈 번호**: Jira 이슈 키 필수 작성 (예: `S15P21D205-91`)
- **파트 구분**: `[SV]`, `[CL]` (대문자)
- **태그**: 소문자 작성 (`feat`, `fix`, `docs`, `refactor` 등)
- **제목**: 한글 명령조, 50자 이내 작성

### 태그 종류

| 태그 | 사용 시점 |
| --- | --- |
| `feat` | 새로운 기능 추가 |
| `fix` | 버그 수정 |
| `docs` | 문서 수정 |
| `style` | 코드 포맷팅, 세미콜론 등 동작 변경 없는 수정 |
| `refactor` | 동작 변경 없는 코드 구조 개선 |
| `test` | 테스트 코드 추가 및 수정 |
| `chore` | 빌드 설정, 패키지, 에셋 정리 등 그 외 작업 |

### 예시

```text
S15P21D205-91 [SV] feat: 로그인 기능 구현
S15P21D205-104 [CL] fix: 카메라 회전 시 캐릭터 떨림 수정
S15P21D205-112 [CL] docs: 협업 규칙 문서 추가
```

### 📌 Jira 이슈 자동 완료 (Merge Request 시)

- **GitLab 기본 방식**: MR 설명란 또는 커밋 본문에 `Closes 이슈번호` 작성
  - 예시: `Closes S15P21D205-91`

## 3. Merge Request

- 작업은 `feature/*` 브랜치에서 하고 `develop`으로 Merge Request를 보냅니다.
- 제목은 커밋 컨벤션과 동일한 포맷으로 작성합니다.
- 설명란에 작업 내용과 `Closes 이슈번호`를 함께 작성합니다.
- 최소 1명의 리뷰 승인 후 병합합니다.
- 병합 후 feature 브랜치는 삭제합니다.

## 4. Unity 작업

- 씬과 프리팹은 동시에 수정하지 않도록 담당자를 정합니다.
- `.meta` 파일은 에셋과 함께 커밋합니다.
- `Library`, `Temp`, `Logs`, `UserSettings`는 커밋하지 않습니다.
- Photon의 네트워크 상태는 `NetworkBehaviour`와 `[Networked]`를 원본으로 사용합니다.
- ScriptableObject는 정적 설정, 아이템 정의, 로컬 이벤트 전달에 사용합니다.

## 5. 병합 흐름

```text
feature/* -> develop -> release -> main
hotfix/*  -> main (develop에도 함께 병합)
```
