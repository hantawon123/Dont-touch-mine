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

```
com.ssafy.d205
├─ global/   설정, 예외 처리, 공통 응답
├─ auth/     회원가입·로그인·토큰 발급
├─ user/     닉네임, 프로필
├─ friend/   친구 관계
└─ photon/   Photon Custom Authentication 엔드포인트
```

## 협업 규칙

루트의 `CONTRIBUTING.md`를 따릅니다. 백엔드 작업은 다음을 사용합니다.

- 브랜치: `feature/backend/{작업내용}`
- 커밋 태그: `[BE]` (예: `S15P21D205-000 [BE] feat: 친구 목록 조회 구현`)

`[SV]`와 `feature/server/*`는 Unity의 `Game.Server` 어셈블리 작업에 이미 쓰이고 있으므로
백엔드에는 사용하지 않습니다.
