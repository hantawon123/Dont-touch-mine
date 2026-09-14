# 관리 API 가이드

관리 화면을 만드는 사람을 위한 문서입니다. 게임 클라이언트 담당자는
[client-guide.md](client-guide.md) 를 보세요.

관리 화면은 이 서버가 `/admin` 에서 직접 서빙합니다
(`src/main/resources/static/admin/index.html`, `global/web/AdminPageConfig`). 화면을 고치는
사람은 그 파일을 수정하고, 화면이 부르는 API 는 아래를 따릅니다.

---

## 1. 이 API 는 다른 API 와 규칙이 다릅니다

| | 게임 API (`/api/v1/...`) | 관리 API (`/api/v1/admin/...`) |
| --- | --- | --- |
| 신원 | `X-User-Id` + `X-Account-Token` 헤더 (계정 토큰 서명) | 로그인 세션 쿠키 |
| CSRF | 없음 | **필요합니다** |
| 상태 | 무상태 | 세션 |

**게임 API 는 아무것도 바뀌지 않았습니다.** 유니티 클라이언트는 지금까지처럼 헤더만
보냅니다.

---

## 2. 로그인

```
POST /api/v1/admin/session
Content-Type: application/x-www-form-urlencoded

username=admin&password=...
```

**`application/json` 이 아니라 폼 형식입니다.** Spring Security 의 필터가 직접 받기
때문입니다. 로그인 처리를 직접 짜면 세션에 인증 정보를 저장하는 단계를 빠뜨리기 쉽고,
그러면 로그인은 성공인데 다음 요청이 401 이 됩니다. 그 함정을 피하려고 필터에 맡겼고,
대신 요청 형식이 폼으로 고정됩니다.

| 응답 | 뜻 |
| --- | --- |
| `204` | 성공. `JSESSIONID` 와 `XSRF-TOKEN` 쿠키가 함께 옵니다 |
| `401 BAD_CREDENTIALS` | 아이디나 비밀번호가 틀림 |

**아이디가 틀린 것과 비밀번호가 틀린 것을 구분해 주지 않습니다.** 구분하면 아이디를
하나씩 찔러 볼 수 있습니다.

### 로그인이 항상 401 이라면

서버에 계정이 설정되지 않았을 수 있습니다. 계정은 DB 가 아니라 서버 환경변수
(`ADMIN_USERNAME`, `ADMIN_PASSWORD`) 에 있고, **없으면 아무 계정도 등록되지 않습니다.**
그 경우 서버 로그 첫머리에 경고가 남습니다.

---

## 3. 이후 요청

`JSESSIONID` 쿠키는 브라우저가 알아서 붙입니다. 따로 할 일이 없습니다.

**상태를 바꾸는 요청(POST, PATCH, DELETE)에는 CSRF 토큰이 하나 더 필요합니다.**

```js
// XSRF-TOKEN 쿠키를 읽어 X-XSRF-TOKEN 헤더에 그대로 넣습니다.
fetch('/api/v1/admin/...', {
  method: 'PATCH',
  headers: { 'X-XSRF-TOKEN': readCookie('XSRF-TOKEN') },
})
```

쿠키와 헤더 **둘 다** 가야 합니다. 서버는 그 둘이 같은지 봅니다. 다른 사이트에서 온
요청은 쿠키는 실려도 값을 읽을 수 없어 헤더를 채우지 못하고, 그래서 걸러집니다.

빠뜨리면 `403` 입니다. 로그인이 풀린 것이 아니니 다시 로그인시키지 마세요 —
`401` 과 `403` 을 구분해서 다뤄야 합니다.

---

## 4. 로그인 상태 확인

```
GET /api/v1/admin/session
```

```json
{ "username": "admin" }
```

살아 있지 않으면 `401 UNAUTHORIZED` 입니다. 화면을 새로 그릴 때 이것으로 확인하세요.

**리다이렉트가 오지 않습니다.** 로그인 페이지로 넘기는 대신 `401` 로 답합니다.
리다이렉트는 요청하는 쪽에서 성공처럼 보여서, 화면이 로그인 폼 HTML 을 데이터로
받게 됩니다.

---

## 5. 로그아웃

```
POST /api/v1/admin/logout
X-XSRF-TOKEN: ...
```

`204` 입니다. 세션이 서버에서 즉시 끊깁니다.

---

## 6. 신고 검토

로그인한 세션으로 부릅니다. 세션이 없으면 `401 UNAUTHORIZED` 이고, 상태를 바꾸는 PATCH 에는
3절의 CSRF 헤더가 필요합니다(없으면 `403`).

신고를 접수하는 `POST /api/v1/reports` 는 게임 클라이언트가 인증 없이 부르는 반대 성격의
API 이고, 여기서 다루지 않습니다.

### 신고당한 사람 목록

```
GET /api/v1/admin/reports?status=PENDING
```

`status` 는 생략하면 `PENDING` 입니다. `ACTIONED` 나 `DISMISSED` 를 주면 이미 검토한 것을
봅니다. 지난 판단을 확인할 방법이 없으면 "그때 왜 기각했지"를 DB 를 열어야 알게 됩니다.

신고 한 건씩이 아니라 **사람 단위로 묶어서** 옵니다. 한 건씩 나열하면 같은 사람에 대한
다섯 건이 흩어져 나와, 그 사람이 문제인지 신고한 사람이 문제인지 구분되지 않습니다.

```json
{
  "users": [
    {
      "userId": "...",
      "nickname": "...",
      "reportCount": 7,
      "reporterCount": 5,
      "fromDeletedAccounts": 0,
      "reasons": { "ABUSE": 5, "SPAM": 2 },
      "lastReportedAt": "20260909120000"
    }
  ]
}
```

| 필드 | 뜻 |
| --- | --- |
| `userId` | 신고당한 사람의 공개 식별자. 상세 조회와 마무리에 씁니다 |
| `nickname` | 지금 닉네임. 바뀔 수 있으니 표시에만 씁니다 |
| `reportCount` / `reporterCount` | 건수와 신고한 사람 수. **함께 봐야 합니다.** "3건 1명"은 신고한 쪽이, "7건 5명"은 신고당한 쪽이 의심스럽습니다 |
| `fromDeletedAccounts` | 신고자가 탈퇴해 누구인지 알 수 없는 건수. `reporterCount` 에 섞지 않습니다 |
| `reasons` | 사유별 건수. 키는 `ABUSE`, `CHEATING`, `SPAM`, `INAPPROPRIATE_NAME`, `OTHER` |
| `lastReportedAt` | 가장 최근 신고 시각. `yyyyMMddHHmmss`, UTC |

없으면 `users` 가 빈 배열입니다.

### 한 사람의 신고 상세

```
GET /api/v1/admin/reports/{userId}
GET /api/v1/admin/reports/{userId}?status=PENDING
```

`status` 를 주면 그 상태만, 비우면 전부 봅니다. 목록의 사유 분포로 부족할 때 펼쳐 봅니다.
최근 순입니다.

```json
{
  "reports": [
    { "id": 41, "reason": "ABUSE", "memo": "...", "createdAt": "20260909120000", "status": "PENDING" }
  ]
}
```

`memo` 는 신고자가 적은 한 줄이고 없으면 `null` 입니다. 신고가 없는 사람은 빈 배열이고,
없는 계정은 `404 TARGET_NOT_FOUND` 입니다. 구분하지 않으면 오타로 부른 것과 정상 조회가
같아 보입니다.

`id` 는 신고 한 건의 번호입니다. 건별로 치울 때 이 값을 씁니다(8절).

### 마무리

```
PATCH /api/v1/admin/reports/{userId}
Content-Type: application/json
X-XSRF-TOKEN: ...

{ "status": "ACTIONED" }
```

그 사람의 **미검토 신고를 한 번에** 마무리합니다. 경로가 신고 번호가 아니라 사용자인
이유입니다 — 다섯 건 쌓인 사람을 다섯 번 누르게 할 이유가 없습니다.

`status` 는 `ACTIONED`(실제 문제였음) 또는 `DISMISSED`(조치할 것 없음) 입니다. 이미 검토한
신고는 그대로 두고, 검토 뒤에 새로 들어온 것만 미검토로 남아 다음 차례에 다시 올라옵니다.
누가 마무리했는지는 로그인 아이디로 남습니다.

```json
{ "reviewed": 3 }
```

`reviewed` 는 이번 요청이 마무리한 건수입니다. 처리할 것이 없어도 `200` 이고 `reviewed` 가
`0` 입니다. 두 사람이 같은 화면을 보다가 둘 다 눌렀을 때 뒤에 누른 쪽에 오류를 주면 무엇이
잘못됐는지 알 수 없는데, 원하는 결과는 이미 이루어져 있습니다.

| 응답 | 뜻 |
| --- | --- |
| `400 INVALID_REQUEST` | `status` 가 없거나, `PENDING` 이거나, 알 수 없는 값. `PENDING` 으로 되돌리는 것은 검토 취소라 막습니다 |
| `401 UNAUTHORIZED` | 로그인 세션이 없음 |
| `403` | CSRF 헤더가 없거나 쿠키와 다름 |
| `404 TARGET_NOT_FOUND` | 그 `userId` 의 계정이 없음 |

---

## 7. 피드백 읽기

```
GET /api/v1/admin/feedback
GET /api/v1/admin/feedback?limit=100
```

플레이어가 설정 화면에서 보낸 글입니다. `/admin` 의 **피드백 탭**이 이 API 를 그립니다 —
API 를 직접 부르는 것은 화면에 없는 것을 볼 때만 필요합니다.

신고와 달리 **한 건씩 그대로** 옵니다. 신고는
"이 사람이 몇 번 신고당했나"가 판단 단위지만 피드백은 한 건 한 건이 읽을 내용이라,
묶으면 정작 본문이 사라집니다. 최근 순입니다.

```json
{
  "feedback": [
    {
      "id": 17,
      "userId": "...",
      "nickname": "...",
      "message": "숨는 시간이 너무 짧아요",
      "buildVer": "1.4.2",
      "platform": "WebGL",
      "createdAt": "20260909120000"
    }
  ]
}
```

| 필드 | 뜻 |
| --- | --- |
| `userId`·`nickname` | 쓴 사람. **탈퇴했으면 둘 다 `null`** 입니다. 내용은 남기고 작성자만 비웁니다 |
| `message` | 본문. 500자까지입니다 |
| `buildVer`·`platform` | 어느 빌드·환경에서 왔나. 클라이언트가 안 보냈으면 `null` |
| `createdAt` | 보낸 시각. `yyyyMMddHHmmss`, UTC |

`limit` 은 생략하면 50, 최대 200 입니다. **200 을 넘겨도 400 이 아니라 200 으로 깎입니다.**
큰 값을 넣는 상황은 "다 보고 싶다" 이고 거기에 오류를 주면 답이 되지 않습니다. 상한 자체를
두는 이유는 피드백이 지워지지 않고 쌓이기만 해서, 상한이 없으면 응답이 시간에 비례해
자라기 때문입니다.

### 읽음 표시가 없습니다

신고에는 검토 상태가 있지만 피드백에는 없습니다. 판단할 것이 없고 읽을 것만 있어서입니다.
읽음 표시를 두면 "읽음"이 "처리했음"처럼 읽히고, 실제로는 아무것도 하지 않은 채 목록만
깨끗해집니다.

**답장할 방법도 없습니다.** 보낸 사람에게 메일이나 알림을 보낼 경로가 없고, 클라이언트도
"답변을 드립니다" 라고 말하지 않습니다. 개선으로 답하는 것이 유일한 답입니다.

### 같은 말이 여러 건 보이면

같은 사람이 반복해 보낸 것과 여러 사람이 같은 지적을 한 것은 전혀 다른 신호입니다.
`userId` 로 구분하세요. 다만 **탈퇴한 사람들의 피드백은 전부 `null`** 이라 그 구분이
되지 않습니다. 신고 목록의 `fromDeletedAccounts` 와 같은 한계입니다.

---

## 8. 치우기 — 숨김과 완전 삭제

신고와 피드백을 목록에서 없앱니다. 두 가지가 있고 **차이는 되돌릴 수 있는가 하나**입니다.

| | 하는 일 | 되돌리기 |
| --- | --- | --- |
| **숨김** (`PATCH .../hidden`) | `deleted_at` 을 채워 조회에서 뺍니다. 행은 남습니다 | DB 를 열면 가능합니다. **화면에서는 불가능합니다** |
| **완전 삭제** (`DELETE`) | 행을 지웁니다 | 불가능합니다 |

### 신고

```
PATCH  /api/v1/admin/reports/{userId}/hidden?status=PENDING   그 사람 신고 숨김
DELETE /api/v1/admin/reports/{userId}?status=PENDING          그 사람 신고 완전 삭제
PATCH  /api/v1/admin/reports/entries/{id}/hidden              신고 한 건 숨김
DELETE /api/v1/admin/reports/entries/{id}                     신고 한 건 완전 삭제
```

`{id}` 는 상세 조회(6절)의 `reports[].id` 입니다. 경로에 `entries` 가 있는 이유는 사람은
UUID 로, 신고는 순번으로 가리키기 때문입니다. 같은 자리에 두면 무엇을 받는 경로인지가
값의 모양에 달리게 됩니다.

**사람 단위 요청에는 `status` 를 목록과 같은 값으로 주세요.** 목록은 `status` 로 걸러
보여주므로 치우는 범위도 거기 맞춰야 합니다. 빼면 **상태를 가리지 않고 전부** 처리하는데,
그러면 `ACTIONED` 화면에서 "1건"을 보고 누른 한 번에 한 번도 보지 못한 `PENDING` 신고까지
사라집니다. 완전 삭제는 되돌릴 수 없어 그 차이가 그대로 손실이 됩니다.

목록과 달리 기본값을 두지 않았습니다. 여기서 `PENDING` 을 기본으로 삼으면 이번에는 반대로
범위가 조용히 좁혀집니다. **화면은 늘 채워 보내고**, 전부 치우려는 요청만 일부러 뺍니다.

**숨김은 검토 상태를 건드리지 않습니다.** 숨기는 것과 판단하는 것은 다른 일입니다. 여기서
`DISMISSED` 를 찍으면 운영자가 내리지 않은 판단이 기록에 남고, 무고성 신고를 세는 집계가
조용히 틀어집니다.

**완전 삭제는 범위 안의 숨긴 것까지 지웁니다.** 지우기로 한 범위에 숨긴 것만 남으면 나중에
그 행들의 출처를 아무도 설명하지 못합니다.

### 피드백

```
PATCH  /api/v1/admin/feedback/{id}/hidden   한 건 숨김
DELETE /api/v1/admin/feedback/{id}          한 건 완전 삭제
```

`{id}` 는 목록(7절)의 `feedback[].id` 입니다. **사람 단위 동작이 없습니다** — 피드백 목록은
사람으로 묶여 있지 않고 한 건 한 건이 읽을 내용이라, 같은 사람의 것을 한 번에 치울 이유가
없습니다.

숨김은 읽음 표시가 아닙니다. 읽음을 두지 않은 이유는 7절 그대로이고, 이것은 **더는 목록에
띄우지 말라**는 뜻입니다. 스팸 한 줄이 계속 위쪽을 차지하는 것을 막습니다.

### 응답

```json
{ "affected": 3 }
```

이번 요청이 치우거나 지운 건수입니다. **치울 것이 없어도 `200` 이고 `affected` 가 `0`**
입니다. 없는 `{id}` 를 줘도 `404` 가 아닙니다. 두 사람이 같은 화면을 보다가 둘 다 눌렀을 때
뒤에 누른 쪽에 오류를 주면 무엇이 잘못됐는지 알 수 없는데, 원하는 결과는 이미 이루어져
있습니다. 검토 마무리(6절)와 같은 규칙입니다.

`{userId}` 쪽만 없는 계정에 `404 TARGET_NOT_FOUND` 입니다. 사람을 가리키는 경로라 오타로
부른 것과 정상 요청을 구분해야 합니다. `status` 에 모르는 값을 주면 `400` 입니다.

### 조심할 것

**확인 창이 없습니다.** 화면에서 누르면 그 자리에서 실행됩니다. 완전 삭제는 되돌릴 수
없고 숨김도 화면에서는 되돌릴 수 없으므로, 잘못 눌렀을 때 화면에서 할 수 있는 일이
없습니다. 삭제 버튼만 빨갛게 칠해 두었고 그것이 유일한 경고입니다.

**신고를 지우면 그 사람이 신고당한 이력이 사라집니다.** 무고성 신고를 세는 근거도 함께
사라집니다. 눈앞에서 치우는 것이 목적이라면 숨김이 맞습니다.

**숨긴 것을 보는 조회가 없습니다.** 운영자에게 숨김과 완전 삭제는 똑같아 보입니다. 되살릴
일이 실제로 생기면 `deleted_at` 이 `NULL` 이 아닌 행을 DB 에서 찾으세요.

---

## 9. 사용자 조회

신고가 없는 사람도 찾아서 정지하거나 들여다보는 자리입니다. 그 전에는 정지 버튼이 신고
목록 안에만 있었습니다.

### 검색

```
GET /api/v1/admin/users?q=검색어&limit=50
```

`q` 는 **닉네임 부분 일치**(대소문자 구분) 또는 **userId 정확 일치**입니다. 게임 클라이언트의
유저 검색과 규칙이 다릅니다. 그쪽은 정확 일치이고 검색을 꺼 둔 사람을 빼지만, 운영자는
그 사람도 찾아야 합니다. 그래서 이 조회는 반드시 관리자 세션 뒤에 있습니다.

`q` 를 비우면 최근 가입순입니다. `limit` 은 1~50 이고 벗어나면 50 입니다. `%` 와 `_` 는
글자 그대로 찾습니다.

```json
{
  "users": [
    {
      "userId": "...",
      "nickname": "...",
      "createdAt": "20260914120000",
      "presence": "IN_LOBBY",
      "lastSeenAt": "20260914123000",
      "reportCount": 2,
      "friendCount": 5,
      "suspended": false,
      "suspendedAt": null,
      "suspendedReason": null
    }
  ]
}
```

| 필드 | 뜻 |
| --- | --- |
| `presence` | `OFFLINE` `ONLINE` `IN_LOBBY` `IN_GAME`. 한 번도 붙은 적이 없으면 `OFFLINE` 이고 `lastSeenAt` 이 `null` |
| `reportCount` | 받은 신고 중 숨기지 않은 것의 수 |
| `friendCount` | 수락된 친구 수. 보낸·받은 요청은 세지 않습니다 |
| `suspendedReason` | 운영자만 보는 화면이라 그대로 옵니다. 게임 API 응답에는 절대 담기지 않습니다 |

기기 식별자는 어떤 응답에도 없습니다. 그 값은 그 계정의 비밀번호입니다.

### 한 사람 상세

```
GET /api/v1/admin/users/{userId}
```

```json
{
  "user": { "...위와 같은 한 줄..." },
  "receivedReports": [
    { "id": 12, "reason": "ABUSE", "memo": "욕설", "createdAt": "...", "status": "PENDING",
      "counterpartUserId": "...", "counterpartNickname": "신고한 사람" }
  ],
  "madeReports": [
    { "id": 13, "reason": "CHEATING", "memo": null, "createdAt": "...", "status": "DISMISSED",
      "counterpartUserId": "...", "counterpartNickname": "신고당한 사람" }
  ],
  "feedback": [
    { "id": 3, "message": "...", "buildVer": "0.9.1", "platform": "WINDOWS", "createdAt": "..." }
  ]
}
```

`receivedReports` 의 상대편은 **신고자**, `madeReports` 의 상대편은 **신고당한 사람**입니다.
6절의 신고 상세는 신고자를 일부러 빼지만, 사용자 한 명을 열어 보는 이 자리에서는 무고성
신고를 판단해야 하므로 보여줍니다. 상대편이 탈퇴했으면 둘 다 `null` 이고 화면은 "탈퇴한
계정"으로 표시합니다.

`madeReports` 에서 `DISMISSED` 가 많으면 남을 반복해서 무고하는 사람입니다.

숨긴 신고와 피드백은 어디에도 나오지 않습니다. 각각 최근 순 최대 200건, 100건입니다.
없는 계정은 `404 TARGET_NOT_FOUND` 입니다.

---

## 10. 개요

관리 화면의 첫 탭입니다. 지금 숫자와 최근 추이를 한 화면에 둡니다.

```
GET /api/v1/admin/overview?range=24h
```

`range` 는 `24h`(기본) 또는 `7d`. 그 외 값은 `24h` 로 읽습니다.

```json
{
  "now": {
    "online": 3, "inLobby": 4, "inGame": 6, "socketConnections": 13,
    "totalUsers": 120, "signupsToday": 5, "deletionsToday": 0,
    "pendingReports": 2, "feedbackToday": 1, "suspendedUsers": 1,
    "cpuPct": 12.5, "heapUsedMb": 310, "heapMaxMb": 1024, "dbPoolActive": 2, "dbPoolMax": 13
  },
  "range": "24h",
  "series": [
    { "at": "20260914120000", "online": 3, "inLobby": 4, "inGame": 6, "socketConnections": 13,
      "signups": 0, "deletions": 0, "cpuPct": 12.5, "heapUsedMb": 310, "dbPoolActive": 2 }
  ],
  "matches": { "matchesToday": 12, "inProgress": 1, "avgDurationSec": 412.5, "dropoutRate": 0.125 }
}
```

| 필드 | 뜻 |
| --- | --- |
| `now.*` | 요청 순간에 다시 잰 값입니다. 샘플이 아닙니다 |
| `signupsToday` `feedbackToday` | "오늘"은 **Asia/Seoul** 기준입니다. 저장은 UTC 지만 운영자의 하루는 한국 시간입니다 |
| `deletionsToday` | 탈퇴는 행이 지워져 셀 수 없어 이벤트로 셉니다. 서버가 재시작한 사이의 것은 빠질 수 있습니다 |
| `cpuPct` | JVM 이 보는 시스템 CPU, 0~100. 컨테이너 안에서는 cgroup 한도 기준 |
| `dbPool*` | 게임 DB 와 분석 DB 커넥션 풀을 합친 값 |
| `series` | 1분마다 남기는 샘플. `24h` 는 그대로(최대 1,440점), `7d` 는 15분 버킷(최대 672점)으로 인원·자원은 평균, 가입·탈퇴는 합 |
| `matches` | 경기 통계. 수집 서비스의 내부 API 에서 받아옵니다. `matchesToday`(오늘 시작한 경기), `inProgress`(시작 후 30분 안이고 끝 이벤트 없음), `avgDurationSec`, `dropoutRate`(시작 인원 대비 결과 없이 사라진 비율). 수집 서비스가 죽어 있으면 `null` 이고 나머지 카드는 그대로입니다 |

샘플은 서버가 1분마다 스스로 남기고 30일 지난 것은 지웁니다(`ops.*` 설정). 서버가 꺼져 있던
구간은 점이 없습니다. 그래프의 빈 구간이 바로 다운타임입니다.

---

## 11. 알고 있어야 할 것

**계정이 하나이고 팀이 공유합니다.** 누가 무엇을 했는지 구분할 수 없습니다. 사람마다
계정을 나눌 일이 생기면 그때 계정 테이블을 만들어야 합니다.

**로그인 시도 횟수를 제한하지 않습니다.** 실패는 서버 로그에 남지만 막지는 않습니다.
비밀번호를 길게 쓰세요.

**관리 화면은 같은 도메인 아래 두는 것을 전제로 합니다.** 지금은 이 서버가 `/admin` 으로
직접 서빙하므로 이미 그렇습니다. 다른 곳에 배포하면 쿠키가 교차 출처가 되어 CORS 와
`SameSite` 설정이 따로 필요합니다.
