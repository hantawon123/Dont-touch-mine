# D205 Backend

Unity 클라이언트의 계정·프로필·친구 데이터를 담당하는 서버입니다.

실시간 동기화는 Photon Fusion이 처리하므로 이 서버는 관여하지 않습니다. 여기서 다루는 것은
세션이 끝나도 남아야 하는 데이터뿐입니다.

## 개발 환경

- Java 21 (LTS)
- Spring Boot 4.1.1
- MySQL 8.4
- Gradle (래퍼 포함, 별도 설치 불필요)

`java -version`이 21인지 확인하세요. Boot 4는 Java 17 미만에서 동작하지 않습니다.

## 실행

DB를 먼저 띄웁니다. Docker Desktop이 실행 중이어야 합니다.

```bash
docker compose -f compose.local.yml up -d
```

애플리케이션을 실행합니다.

```bash
./gradlew bootRun
```

Windows PowerShell에서는 `.\gradlew.bat bootRun`을 사용합니다.

기동을 확인합니다.

```bash
curl http://localhost:8080/actuator/health
```

`{"status":"UP"}`가 나오면 DB 연결까지 정상입니다.

DB를 내릴 때는 다음과 같이 합니다. 데이터까지 지우려면 `-v`를 붙입니다.

```bash
docker compose -f compose.local.yml down
```

## 프로필

| 프로필 | 용도 | DB 접속 정보 |
| --- | --- | --- |
| `local` | 개발자 PC (기본값) | `application-local.yml`에 고정 |
| `prod` | 배포 서버 | 환경변수로만 주입 |

`prod`는 `DB_HOST`, `DB_NAME`, `DB_USERNAME`, `DB_PASSWORD`가 없으면 기동에 실패합니다.
변수가 빠진 채로 엉뚱한 DB에 붙는 것보다 즉시 멈추는 편이 안전하기 때문입니다.

## 스키마

스키마는 Flyway가 관리합니다. Hibernate의 `ddl-auto`는 `validate`로 고정되어 있어
엔티티와 실제 테이블이 어긋나면 기동 시점에 드러납니다.

마이그레이션 파일은 `src/main/resources/db/migration/`에 `V{번호}__{설명}.sql` 형식으로 추가합니다.
**이미 적용된 파일은 수정하지 않습니다.** 변경이 필요하면 새 번호로 파일을 추가합니다.

## 패키지 구조

`global`과 `domain`으로 나누고, 각 도메인 안에서 계층으로 한 번 더 나눕니다.

```
com.ssafy.d205
├─ global/
│  ├─ common/      Timestamps 같은 공통 유틸
│  └─ exception/   전역 예외 처리, 공통 응답, 여러 도메인이 쓰는 예외
└─ domain/
   ├─ user/        계정 발급·조회, 닉네임, 유저 검색
   └─ friend/      친구 요청과 친구 관계
```

**폴더 경계는 데이터와 규칙을 따릅니다. API 표면을 따르지 않습니다.**
`/api/v1/accounts`와 `/api/v1/users`는 URL이 다르지만 같은 `users` 테이블을 보는 두
창이므로 `user` 도메인 하나입니다. 계정 발급을 따로 두려다 합쳤습니다 — 자기 엔티티도
리포지토리도 없이 `user`의 것을 열세 번 가져다 쓰고 있었고, 그건 도메인이 아니라
`user` 위에 얹힌 유스케이스 묶음이었습니다.

각 도메인은 같은 계층 폴더를 씁니다. 없는 계층은 만들지 않습니다.

```
domain/friend/
├─ controller/   HTTP 요청을 받고 응답을 돌려줍니다
├─ service/      비즈니스 로직과 트랜잭션
├─ repository/   DB 접근. 네이티브 쿼리의 투영 인터페이스도 여기 둡니다
├─ entity/       JPA 엔티티와 그 도메인의 enum, 규칙
└─ dto/          요청·응답 객체
```

의존 방향은 `controller → service → repository → entity` 한쪽입니다.
`entity`는 다른 계층을 참조하지 않습니다.

**예외는 도메인 안에 두지 않고 전부 `global/exception`에 모읍니다.** 도메인마다
`exception/`을 두면 `GlobalExceptionHandler`가 모든 도메인을 import해야 하고,
그러면 `global`이 `domain`을 의존하게 되어 방향이 뒤집힙니다.

한곳에 모으는 실질적인 이점도 있습니다. **오류 코드는 Unity 클라이언트와의
약속**이라, 핸들러 한 파일에서 전체 목록을 볼 수 있으면 코드가 중복되거나 서로
어긋나는 일이 생기지 않습니다. 새 예외를 추가할 때 기존 코드와 겹치는지 바로
확인할 수 있습니다.

`global/config/`는 넣을 것이 생길 때 만듭니다. 지금은 설정 클래스가 없습니다.
Photon Custom Authentication처럼 아직 만들지 않은 것은 그때 `domain/` 아래에
도메인으로 추가합니다.

## 협업 규칙

루트의 `CONTRIBUTING.md`를 따릅니다. 백엔드 작업은 다음을 사용합니다.

- 브랜치: `feature/backend/{작업내용}`
- 커밋 태그: `[BE]` (예: `S15P21D205-000 [BE] feat: 친구 목록 조회 구현`)

`[SV]`와 `feature/server/*`는 Unity의 `Game.Server` 어셈블리 작업에 이미 쓰이고 있으므로
백엔드에는 사용하지 않습니다.
