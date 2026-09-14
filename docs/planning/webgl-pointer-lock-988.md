# 988 WebGL 마우스·Escape 입력 수정 검증

## 현재 판정

v3 실제 Chrome 게임에서 이동·시점·마우스 입력 복구를 사용자가 확인했다. 전체화면 + Keyboard Lock 실험 페이지에서도 Escape로 메뉴를 열고 닫은 뒤 추가 클릭 없이 실제 잠금과 마우스 이동이 복구됨을 확인했다. 이를 실제 게임에 연결한 v5는 빌드에 성공했으며(505.56초), 게임 설정창에서 같은 동작을 재검증해야 한다. 아직 최종 완료 판정이 아니다.

## 원인과 수정

기존 WebGL은 C# Cursor.lockState 요청과 JavaScript pointerdown 요청이 섞여 있었다. Unity/Emscripten은 DOM 이벤트 밖의 C# 요청을 예약했고, 후속 Escape에서 실행된 요청의 Promise 거절이 전역 오류 팝업까지 전파됐다. 실제 추적에서 Object.runDeferredCalls → requestPointerLock, activation=false, WrongDocumentError를 확인했다. 사용자가 보낸 UnknownError 역시 Chromium 포인터 잠금 오류 경로의 문구다.

C#의 WebGL 잠금 요청을 제거한 v2는 팝업을 없앴지만, 실제 잠금과 Unity의 요청 상태가 달라 마우스 입력 차단 조건이 남았다. 따라서 v2는 완료 빌드가 아니다. 공통 WebPointerInput.IsLocked가 WebGL에서는 document.pointerLockElement를 읽도록 바꾸고 카메라·공격·상호작용·경기 UI가 같은 상태를 보도록 했다. 해제도 document.exitPointerLock을 호출한다. 네이티브의 기존 Cursor 경로는 유지한다.

Chrome이 첫 Escape를 소비해 Unity에 키 입력을 전달하지 않는 경우를 위해 브라우저의 자발적 잠금 해제를 설정창 열기로 연결했다. 코드에 의한 해제·중복 해제·포커스 손실과 구분하고 로비/경기 UI에는 한 프레임의 동일한 이벤트를 제공한다.

전체화면에서는 기존 게임 전체화면 버튼을 사용하고 Keyboard Lock으로 Escape만 요청한다. 설정·채팅이 잠금을 해제한 경우에만 게임 입력 재활성화 시 한 번 재잠금을 시도한다. 홈 복귀·연결 종료·포커스 손실은 이 복귀 요청을 취소한다. 권한 거절은 처리하며 반복 잠금 요청이나 전역 오류 무시는 하지 않는다.

## 검증 범위

| 확인 | 결과 |
| --- | --- |
| v3 WebGL 빌드 | 성공, 532.20초 |
| v3 실제 Chrome 게임 | 사용자: 이동·시점·마우스 입력 동작 |
| 기존 에디터 입력 테스트 | 1 passed / 1 skipped / 0 failed; 화면 없는 에디터의 실제 잠금 테스트 제외 |
| 브라우저 연동 계약 테스트 | 실제 상태 조회, Escape, 중복/코드 해제, 포커스 손실, 이벤트 소비 통과 |
| 전체화면 연동 계약 테스트 | 한 번 복귀, 종료 시 취소, 포커스 손실, 전체화면 종료, 권한 거절 통과 |
| 별도 전체화면 실험 페이지 | 사용자: Esc 메뉴 닫기 → locked=true → 잠금 상태 마우스 이동 확인 |
| v5 WebGL 빌드 | 성공, 505.56초; 최종 framework의 잠금 연동 함수 포함 확인 |
| v5 실제 게임 설정창 | 재검증 대기 |
| 서로 다른 PC의 WebGL 6인 경기 | 별도 수동 검증 대기 |

Node 테스트는 격리된 DOM 모형의 계약 검증이다. 실제 브라우저 권한이나 게임 입력 성공의 대체 증거로 사용하지 않는다. 인앱 브라우저는 Unity 없는 기본 HTML에서도 잠금을 거절했으므로 실제 잠금 검증은 일반 Chrome에서 한다.

## 실제 게임 재검증 절차

1. 새 빌드를 Ctrl+Shift+R로 불러온다. 빈 시험 서버가 준비됐으면 방 만들기, 이미 할당된 방이면 게임 찾기로 입장한다.
2. 일반 창에서 화면 클릭 → 시점·공격 → Escape 한 번으로 설정창이 열리는지 확인한다. 일반 창의 재잠금에는 화면 클릭이 필요할 수 있다.
3. 게임 하단의 전체화면 버튼을 클릭한다. F11 대신 게임 버튼을 사용하고 브라우저의 키보드/마우스 권한 요청을 허용한다.
4. 화면 클릭으로 게임 입력을 시작한다. 짧은 Escape로 설정을 열고 5초 이상 기다린 뒤 Escape로 닫는다. 추가 클릭 없이 커서가 사라지고 시점이 움직여야 한다. 여러 번 반복한다.
5. 채팅·다른 UI를 열 때 게임 조작이 차단되는지 확인한다. 홈 복귀·연결 종료 화면에서는 커서가 풀려 있어야 한다.
6. Escape를 약 2초 이상 눌러 전체화면을 종료한다. 권한 거절이나 전체화면 종료 후에는 화면 클릭으로 돌아갈 수 있어야 한다.

Tools/network/server-flow/pointer-lock-check.js를 Unity loader보다 먼저 삽입하면 요청 시점과 실제 잠금 이벤트를 확인할 수 있다. 전체화면의 활성 사용자 동작에 따른 복귀 요청은 정상이다. 제품 패키지에는 진단 스크립트를 포함하지 않는다.

## 접속·presence의 별도 문제

프리뷰가 제한된 네트워크 권한으로 실행되면 계정 API 중계가 502로 실패하고 Photon Authentication type None 오류가 뒤따랐다. 기존 승인 범위에서 중계를 정상 네트워크 권한으로 다시 실행한 뒤 실험 계정 발급 201·Photon 토큰 반환과 방 접속을 확인했다. 페이지 HTTP 200만으로 준비 완료라 하지 않는다. 계정·토큰 값은 기록하지 않는다.

presence는 동일한 실험 계정으로 HTTPS와 내부 8080 모두 401 UNAUTHORIZED였다. 따라서 초기의 외부 라우팅 원인 추정은 철회한다. 추가 기기 ID 헤더와 Photon 토큰의 Bearer 전달도 해결하지 못했다. 승인된 배포 SecurityConfig / PresenceController / SuspensionInterceptor 비교에서는 로컬과 같은 기본 계약을 확인했고 제한된 최근 로그에는 예외 종류가 나오지 않았다. 추가 배포 클래스 탐색은 자동 승인 검토가 비공개 아티팩트 접근 범위를 이유로 차단해 중단했다. presence 해결이나 운영 인증 변경은 하지 않았다.

## 공식 근거

- [Chromium 포인터 잠금 오류 처리](https://chromium.googlesource.com/chromium/src/+/0a8c7ee1034e43691c810ab4dcc9b72eef8dc951%5E%21/)
- [Pointer Lock 요청 조건](https://developer.mozilla.org/en-US/docs/Web/API/Element/requestPointerLock)
- [사용자 활성화 입력 종류](https://developer.mozilla.org/en-US/docs/Web/Security/Defenses/User_activation)
- [Chrome Keyboard Lock: 전체화면·권한·Escape 종료](https://developer.chrome.com/docs/capabilities/web-apis/keyboard-lock)
