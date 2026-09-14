# 988 WebGL 포인터 잠금 오류 수정

## 원인

로비에서 Escape로 설정을 열 때 보였던 Chromium UnknownError는 포인터 잠금 요청 경로의 오류다. Chromium 소스에 같은 오류 문구가 PointerLockResult의 예외로 정의돼 있다. 이번 재현에서는 같은 경로에서 WrongDocumentError도 발생했다. [Chromium 포인터 잠금 오류 처리](https://chromium.googlesource.com/chromium/src/+/0a8c7ee1034e43691c810ab4dcc9b72eef8dc951%5E%21/)

기존 코드는 두 경로에서 잠금을 요청했다. 카메라의 C# `Cursor.lockState = Locked`와 브라우저 `pointerdown`의 `requestPointerLock`이다. C# 경로는 씬 활성화/Update에서도 호출됐고, navigator.userActivation.isActive가 참이어도 Unity/Emscripten의 현재 DOM 이벤트 처리 중이라는 뜻은 아니었다. 이때 잠금 요청이 예약되었다가 나중의 Escape 이벤트에서 실행됐다. Emscripten 경로는 반환된 Promise의 거절을 처리하지 않아 Unity 전역 오류 창까지 전파했다.

2026-09-14 11:46:16 UTC, 기존 빌드의 실제 브라우저 호출 추적:

```text
[PointerQA] Escape
[PointerQA] request activation=false locked=false
Element.requestPointerLock
  requestPointerLock
  Object.runDeferredCalls
  HTMLCanvasElement.jsEventHandler
[PointerQA] rejected WrongDocumentError: The root document of this element is not valid for pointer lock.
```

## 수정

PlayerCameraController의 WebGL 경로에서 C#의 잠금 요청을 없앴다. 이미 존재하던 WebPointerInput의 게임 화면 pointerdown 처리만 잠금을 요청한다. 설정·채팅·씬 종료로 잠금을 해제할 때는 클릭 처리도 즉시 비활성화한다. 브라우저가 잠금을 승인하면 Unity가 기존 pointerlockchange 콜백으로 상태를 받는다. 사용하지 않게 된 WebCursor.jslib와 meta를 함께 제거했다.

Windows/Linux/에디터 경로는 기존 Cursor 처리를 유지한다. 오류 전역 무시나 브라우저 보안 설정 변경은 하지 않았다. WebGL에서 씬 입장이나 Escape로 설정을 닫은 직후에는 게임 화면을 클릭해 마우스를 다시 잡는다.

## 반복 확인 방법

같은 EC2 linux-v2 서버와 호환 버전 988-local-v1 WebGL을 사용한다. 새로 빌드한 페이지에만 Tools/network/server-flow/pointer-lock-check.js를 Unity loader 이전에 삽입하면 요청 시점 assertion과 실제 잠금/해제 이벤트를 확인할 수 있다. 이 파일은 제품 Assets 밖의 검증 도구이며 배포 패키지에는 넣지 않는다.

1. 홈 설정에 들어갔다가 나온다.
2. 방 생성 후 화면 클릭 없이 Escape로 설정을 열고 닫는다. Escape에서 잠금을 요청하거나 Unity 오류 창이 나오면 실패다.
3. 게임 화면 클릭으로 잠금을 잡고 이동/시점을 확인한 뒤 Escape로 해제한다. 반복한다.
4. 채팅 입력 중에는 잠금이 풀리고, 입력 종료 후 게임 화면 클릭으로 다시 잡혀야 한다.
5. 로비 단축키의 캐릭터/플레이어 화면을 열고 닫는다. UI 클릭이 잠금을 잡으면 실패다.
6. 게임 나가기 확인 후 홈으로 복귀하고 EC2 서버가 종료되는지 확인한다.

pointer-lock-check.js의 `FAIL` 또는 처리되지 않은 Promise 오류가 없어야 한다. 오류 없음만으로 통과시키지 않고 실제 locked=true/false와 게임 입력 복구까지 확인한다.

## 이번 실행 결과와 남은 확인

- 새 WebGL 빌드 성공: Unity 6000.3.22f1, 988-local-v1, 빌드 단계 495.34초. 출력은 `.build/server-988-webgl-cursor-v2`.
- 12:02 UTC EC2 linux-v2 방 접속. Escape로 설정 열기·닫기에서 예약된 requestPointerLock 호출과 Unity 오류 창이 재현되지 않았다. 화면 클릭 요청은 pointerdown / userActivation=true로만 기록됐다.
- 인앱 브라우저는 Unity 없는 기본 HTML에서도 focused=true, connected=true, userActivation=true인데 WrongDocumentError로 잠금을 거절했다. 실제 마우스 잠금·이동·시점 복구는 일반 Chrome 수동 확인이 남아 있다. 이것을 게임 실행 검증 전체 통과로 처리하지 않는다.
- 사용자 Chrome 접속에서 Authentication type None not supported 발생. 프리뷰 인증 중계가 제한된 실행 권한으로 시작돼 외부 HTTPS 요청이 차단되고 계정 발급이 502로 실패한 것을 확인했다. 기존 승인 범위에서 정상 네트워크 권한으로 중계를 재실행했다. 새 실험용 UUID의 계정 발급 HTTP 201 및 비어 있지 않은 Photon 토큰 반환 확인. 기기 ID/토큰은 기록하지 않는다.
- 시험 서버는 1개이며 이미 생성된 988EC2 방에 배정돼 있다. 추가 빈 서버가 없으므로 새 방 만들기 실패는 별도 용량 조건이다. 수동 확인자는 새로고침 후 게임 찾기로 기존 방에 참가한다.

인증 중계 실행 시 로컬 페이지 200만으로 준비 완료라 하지 않는다. 중계 프로세스가 기존 HTTPS 백엔드에 접근할 수 있고 계정 발급이 성공하는지 확인한 뒤 사용자 접속을 안내한다.
