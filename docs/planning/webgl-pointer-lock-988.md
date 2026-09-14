# 988 WebGL 마우스·Escape 입력 수정 검증

## 현재 판정

사용자는 v5 실제 게임에서 전체화면의 Escape 메뉴 복귀가 추가 클릭 없이 정상 동작한다고 확인했다. 창 모드는 재클릭이 필요하다. 다만 W를 누른 채 메뉴를 열고 안에서 놓은 뒤 닫으면 실제 캐릭터 이동이 재개되는 별도 오류가 확인됐다. 해당 입력 잔류를 처리한 v6는 회귀 테스트 및 WebGL 빌드에 성공했으며 실기 확인이 남았다. 전체 작업 완료 판정이 아니다.

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
| v5 실제 게임 설정창 | 사용자: 전체화면 즉시 복귀 정상, 창 모드 재클릭 필요; 이동 입력 잔류 별도 발견 |
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

## 사용자 v5 확인 및 v6 후속 검증

사용자 추가 확인: 전체화면에서는 Escape로 설정을 닫은 뒤 추가 클릭 없는 마우스 복귀가 정상이다. 창 모드에서만 재클릭이 필요하며 이 동작은 일반 창의 브라우저 잠금 제한과 구분한다.

별도 재현: W를 누른 채 설정을 열고 메뉴 안에서 W를 놓은 뒤 닫으면 캐릭터 위치가 실제로 계속 이동한다. 애니메이션만 남는 현상이 아니다. 기존 로비 잠금은 네트워크 이동 출력만 0으로 만들고 복귀 후 InputAction의 남은 상태를 다시 읽는다.

v6 수정 후보: WebGL의 메뉴 입력 소유권 전환과 채팅 진입/복귀에서 InputSystem.ResetDevice로 이전 키·마우스 버튼 상태를 취소한다. 반복 Update마다 초기화하지 않고 전환 때만 적용하며 마우스 위치는 보존한다. 네이티브 입력 동작은 유지한다. 전체화면 잠금 방식은 변경하지 않는다.

회귀 검증은 실제 Input System 장치/액션으로 W+Shift+공격 입력 → 키 해제 이벤트 누락 → 메뉴 전환 → 이동/버튼 취소 → 새 W 입력 정상 수신을 확인한다. 실제 WebGL에서 같은 사용자 조작의 재검증이 필요하다.


v6 격리 Input System 회귀 검사: 2 passed / 0 failed / 1 skipped. 제외 항목은 화면 없는 에디터의 실제 포인터 잠금 검사다. 초기 일반 에디터 실행에서는 가상 입력이 액션에 전달되지 않아 테스트 준비 단계가 실패했으며, 설치된 Unity.InputSystem.TestFramework로 입력 런타임을 격리한 실행에서 통과했다. v6 WebGL 실플레이 확인 전에는 이동 오류를 해결 완료로 처리하지 않는다.


v6 WebGL 빌드: Succeeded, 564.18초. 실제 게임 입력 재검증 대기.

## 창 모드 사용자 로그 및 브라우저 해제 수정 후보

사용자 제공 InputFlow 로그(2026-09-14): W keydown → 잠금/입력 비활성화 → 메뉴 닫기 Escape → 재잠금 사이에 browser/Unity 어느 쪽에도 W keyup이 없다. 이후 W를 다시 누르고 놓았을 때 양쪽 keyup이 나온다. 반복 실기에서 v6의 InputSystem 초기화만으로 해결되지 않았으므로 v6 성공으로 처리하지 않는다.

브라우저 연결부는 canvas에서 눌린 키를 기록하고, 잠금 해제·코드 해제·UI 복귀·포커스 손실에서 대응하는 keyup을 canvas로 전달하도록 수정했다. Escape와 HTML 입력 필드는 제외한다. 해제 후 물리 keyup이 유실됐더라도 OS 반복 keydown이 다시 이동을 켜지 않도록 막고, 새 keydown은 받는다. 키 내용은 외부 전송/저장하지 않는다.

Node 계약 검사: 해제 유실, 반복 억제, 새 키 허용, 텍스트/Escape 제외, 메뉴 복귀, 중복 해제, 포커스 손실 통과. 기존 포인터/전체화면 계약도 통과.

실제 검증을 빠르게 하기 위해 v6 실행 파일의 GamePointerArm/Release를 현재 저장소 jslib 함수로 교체한 v7 브라우저 코드 검증본을 별도 폴더에 준비했다. .build/server-988-webgl-input-v7-preview이며 전체 Unity 재빌드를 거친 최종 패키지는 아니다. 기존 wasm/C#은 유지하고 전체 framework 문법 검사 통과. 기존 v6 출력과 ZIP은 새 후보로 대체하지 않았다. 정식 빌드/실제 Chrome 확인은 남아 있다.
