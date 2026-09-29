# 네트워크 QA 작업 기록 (2026-09-10)

브랜치: `feature/client/qa-network-contract`

## 범위

QA 목록의 체크되지 않은 항목만 확인. 사용자 승인에 따라 Client UI·캐릭터·애니메이션 코드는 읽기만 하고 Bootstrap/Network 연결 및 테스트 수정.

## 커밋별 변경

- `cb14aeaf`: Jenkins #56 네트워크 계약 테스트의 카탈로그 초기화 격리. WebGL 대상 로컬 EditMode 106/106 통과. Jenkins 전체 파이프라인 재실행 결과는 아직 없음.
- `c51f8aac`: 로컬 하이라이트 스킵 뒤 로비가 보이는 동안 이전 경기 맵의 렌더러·콜라이더 숨김 유지. 관련 135/135 통과.
- `8b740e13`: 숨기기/찾기 안내 UI가 실제로 표시되는 동안 로컬 이동·상호작용 입력과 물건 HUD 차단. 관련 203/203 통과.
- `20513bdd`: 결과 씬이 표시되는 동안 네트워크 들기 애니메이션 값 전달 억제. 물건 소유 상태와 리플레이 데이터는 유지.
- `f2d4cee6`: 앉기/엎드리기 상태에서 점프로 서기 전환 제거, 서 있고 접지한 경우에만 점프. 회귀 케이스 포함 관련 223/223 통과.
- 인게임 ESC: 기존 SettingsView/SettingsPresenter를 Bootstrap에서 조립. 열린 동안 로컬 입력과 채팅 차단, 뒤로가기 시 복원, 나가기 확인은 기존 RoomUiCommands 경로 사용. 설정 화면 내부 UI는 변경하지 않음.

## 하이라이트 맵 잔존 원인과 확인 한계

하이라이트 카메라의 가림 복구가 Renderer.forceRenderingOff를 false로 되돌리는 반면, 기존 로비 코드는 보이기 상태가 바뀔 때 한 번만 이전 맵을 숨겼다. 따라서 스킵 후 정리/재생 데이터 처리가 렌더러를 복원하면 공통 하이라이트 종료까지 맵이 다시 보일 수 있다. 로비 표시 중 숨김을 계속 유지하도록 보완했다.

이는 코드상 가능한 재노출 경로를 차단한 것이다. 실제 팀원 한 명에게 발생한 원인과 완전히 동일한지는 아직 확정하지 못했다. 실제 다중 플레이로 재현/재검증해야 한다.

## 남은 확인 및 담당자 연결

- 3명 각각 다른 PC에서 호스트 먼저 스킵, 참가자 먼저 스킵, 거의 동시에 스킵, 늦은 참가자 스킵 확인. 로비 진입부터 공통 하이라이트 종료까지 맵 재노출 여부 확인.
- 배정/찾기 안내 중 클릭·이동 차단과 안내 종료 후 정상 복구 확인.
- 결과 화면의 승자 들기 자세, 로비 복귀 시 정상 상호작용 확인.
- 앉기/엎드리기 상태 점프 금지와 서기 전환 후 점프 확인. 변경 대상은 네트워크 플레이.
- 인게임 ESC 설정 열기, 화면의 뒤로가기, 변경사항 취소/적용, 나가기 확인(호스트/참가자 각각) 실플레이 확인. 다른 참가자의 경기 시간을 정지하지 않음.
- 배정 물건/카테고리 상시 표시: Client/Match/NetworkMatchHudView.cs의 SetAssignedItem()이 비어 있음. UI 담당자 구현 필요.
- 홈 모달·닉네임/친구 상태 표시·음소거 표시 등 UI 담당 영역은 직접 수정하지 않음.
- WebGL 프레임은 이번 코드 테스트로 검증할 수 없음. 새 카탈로그 포함 배포 빌드에서 별도 측정 필요.

## 테스트 범위 주의

넓은 필터 테스트에서 333개 중 332개 통과, PlaySettingsPresenterTests.RealView_GameStartMatchesLeaveGamePlate 1개 실패: 테스트가 GameStartButton을 찾지 못함. 해당 Client UI는 이번에 수정하지 않았으며 실패를 기록한다. 이 결과를 전체 통과로 취급하지 않는다.

Unity 테스트 로그와 XML은 작업용 문서 저장소의 `.build/lobby-profiler/qa-*-tests.*`에 보관. 이번 작업은 아직 MR/배포하지 않음.

최종 수정 범위의 정규화된 테스트 필터 실행: 242/242 통과 (`qa-scoped-tests.xml`). 실제 ESC 화면과 다중 플레이 검증은 위 목록대로 남아 있음.

## 후속 플레이 오류 보완

- `34334767`: WebGL 대상 Editor에서도 Host 모드 허용. 기존 조건의 UNITY_EDITOR 제외 제거.
- `8a109430`: Playground의 NetworkInteractionSceneBridge에 AsSelf 등록 추가. 실제 씬 조립에서 발생한 VContainer Resolve 실패 보완.
- `70d31917`: 소지 좌우 오프셋을 코드 기본값과 PlayerCharacter 프리팹에서 0으로 변경. 이 위치 설정에 한해 사용자에게 Client 수정 예외 승인받음. 결과 화면에서 CarriedItem 참조를 해제하고 이전 경기 스냅샷의 재부착 차단.
- 위 후속 수정은 자동 테스트를 재실행하지 않았으며, 이전 242개 통과 결과에 포함되지 않음. 열린 사용자 Unity의 재컴파일과 실플레이 확인 필요.
- Fusion RejoinSession NullReferenceException 직전에 Game does not exist 오류 확인. 재입장 시도 원인은 아직 미확정이며 해결 완료로 처리하지 않음.
- 사용자 요청에 따라 QA 브랜치를 origin에 push하며 MR은 생성하지 않음.

## 설정 모달 및 Space/Space/Tab 스킵 후속

- Bootstrap 설정 조립에서 기존 1920x1080 UI를 중앙 정렬한 80% 크기로 축소. CanvasScaler Expand로 화면비가 달라도 전체가 들어오도록 처리하고 게임 HUD 위로 표시.
- 열린 설정은 ESC로 기존 RequestBack 경로 호출. 확인/피드백 창에서 소비한 ESC가 같은 프레임에 설정까지 닫지 않도록 처리.
- 이전 경기 SceneRoots 배열에 남은 재사용 PlayerCameraController 루트를 로비 전환의 숨김 대상에서 제외. 프레임 후반에도 이전 맵 숨김 유지. 실제 재현 해결 여부는 플레이 검증 필요.
- 사용자 오류 두 개는 동일한 RPC_RequestThrow 거절 응답의 Player:None 대상 오류. 던지기/놓기/떨어뜨리기 RPC에 SourceIsHostPlayer를 지정해 호스트 발신자를 명시.
- 카메라 루트가 이전 씬 배열에 남은 경우의 회귀 테스트 추가. 사용자 Unity 세션을 종료하지 않았으므로 이번 자동 테스트는 미실행.

## ESC 메뉴 재현 수정 및 재검증

- 메뉴가 검은 배경에 가려진 원인: Bootstrap에서 UI 자식들을 역순으로 옮겨 배경이 맨 위로 이동. 원래 형제 순서를 유지하도록 수정하고 회귀 테스트 추가.
- ESC 닫기 후 카메라가 동일 ESC로 커서를 다시 해제하지 않도록 경기 설정에서 EscapeReleasesCursor 관리. 크기는 100% 유지.
- 스킵 후 이전 맵 숨김은 Renderer.enabled까지 비활성화하며 원래 활성 상태를 보관/복원. 다른 참가자가 재생 중일 때 Playground 씬 자체의 존재는 유지. 실제 다중 플레이의 맵 재노출 해결 여부는 미확정.
- 사용자가 Editor를 닫은 뒤 WebGL 대상 EditMode 검증 실행: NetworkContractTests, ResultPresentationTests, LobbyHighlightHandoffTests, NetworkMatchHudPresenterTests 총 154/154 통과. 로그: qa-menu-regression.xml / qa-menu-regression.log.

## 재발 진단 로그

기존 맵 숨김 및 커서 수정 이후에도 사용자가 같은 증상을 재현. 해결 완료로 처리하지 않음.

- QA-Transition: 스킵 현재/전체 전후, 로비 표시 후 1초 간격 3회, 설정창 열기/닫기에 씬 목록, 캐시된 경기 루트, 렌더러 drawable 개수, 카메라/리그 및 아바타 위치 기록.
- QA-Cursor: ESC 닫기 다음 프레임의 캡처 복원 결과 기록. 같은 프레임 이후 커서가 풀리는 상황을 위해 한 번 지연 복원. 채팅/포커스 이탈/결과 전환 시에는 적용하지 않음.
- Unity Console Collapse 해제 후 ESC 열기/닫기 및 Space/Space/Tab 재현. Editor.log를 종료 전에 확보해 분석. 현재 사용자의 Editor 실행 중으로 자동 테스트 미실행.

## 실제 재현 로그 기반 추가 수정

- 보관 로그 qa-user-repro-latest.log: 스킵 후 기존 House/Floor 렌더러 drawable=0, 플레이어는 로비 위치 (6.42, 0.01, -6.00). 반면 새 로비 출력 카메라는 리플레이 위치 (-2.54, 9.83, -16.73)에 고정. 기존의 단순 맵 숨김 진단만으로 설명되지 않음.
- 로비 진입 시 PlayerCameraController만 옮기던 처리에 기존 Camera.main도 함께 이관. 리플레이가 사용하던 출력/Brain과 리그 연결을 유지하고 별도 로비 카메라와의 교체 방지. 이관된 출력은 이전 씬 캐시의 숨김 대상에서 제외.
- ESC 닫기 순간과 다음 프레임은 실제 로그에서 Locked. 이후 게임 입력 소유 중 잠금/커서 표시가 바뀌면 다시 캡처하도록 보완. 포커스 이탈/채팅/설정/결과/대기 중 제외. 원래 잠금이 풀리는 정확한 외부 시점은 아직 미확정.
- WebGL 대상 Unity EditMode 165/165 통과 (qa-camera-handoff.xml). 실제 두 증상의 재현 해소 확인은 별도이며 통과로 간주하지 않음.


## Space → Tab 재발 원인 및 수정

- 최신 재현 qa-repro-after-camera-transfer.log에서 출력 카메라 이관은 실행됐으나, 로비에서 출력이 첫 리플레이 위치에 계속 고정. 로비 플레이어/리그 위치는 정상이며 Playground House/Floor drawable=0.
- 원인: 다음 하이라이트로 전환할 때 기존 HighlightCameraDirector의 가림 처리만 해제하고 객체를 덮어써서 priority 100의 HighlightReplayCameraRig가 남음. Tab은 마지막 director만 정리하므로 이전 카메라가 계속 시점을 점유.
- 전환 시 이전 director.Dispose() 후 참조를 비우도록 수정. Client 소스 수정 없음.
- 실제 Cinemachine 리그를 생성하고 Space 이후 다음 Tick 및 Tab 경로를 검증하는 회귀 테스트 추가. 동일 테스트가 수정 전 실패(qa-replay-before.xml), 수정 후 통과. 관련 Unity WebGL 대상 EditMode 총 166/166 통과(qa-replay-dispose-v2.xml).
- ESC: 로그에는 Locked이며 이후 잠금 해제를 감지한 기록도 없어 네이티브 커서 문제의 정확한 원인은 미확정. ESC 키를 놓은 이후 None → Locked로 새 캡처하도록 보완. 배치 테스트는 실제 하드웨어 커서와 시점 회전의 재현 해소를 검증하지 않음. 사용자 Editor 플레이 재확인 필요.
- MR 생성 없이 현재 QA 브랜치 커밋/푸시. 결과 화면 공격 입력은 기존 합의대로 보류.


## Editor ESC 커서 잠금 권한 보정

- 사용자 확인: 하이라이트 1개에서는 Space와 Tab 모두 복귀 정상. 여러 하이라이트의 연속 스킵은 아직 사용자 확인 전.
- 로비/경기 ESC 닫기 뒤 커서 잔존은 Unity Editor GameView가 Escape KeyDown에서 AllowCursorLockAndHide(false)를 호출하는 동작과 일치. 게임 Cursor.lockState=Locked 값과 별개로 네이티브 잠금/숨김이 차단되고 GameView MouseDown에서만 재허용됨.
- 근거: https://github.com/Unity-Technologies/UnityCsReference/blob/6000.3/Editor/Mono/GameView/GameView.cs (AllowCursorLockAndHide 및 OnGUI).
- Bootstrap에 UNITY_EDITOR 전용 EditorGameViewCursor 추가. GameView에서 ESC 입력을 받은 경우만 대기하며, 키를 놓고 기존 메뉴 처리기가 Locked/visible=false를 요청하면 에디터의 잠금/숨김 권한을 한 번 복원. 포커스 이탈/일시정지/플레이 종료 시 취소. Client 수정 없음, 플레이어 빌드에는 포함되지 않음.
- 비공개 Editor API 사용에 따른 버전 변경 위험은 설치 버전 API 호환성 테스트로 검사. 지원 API가 없으면 경고 후 보정 비활성화.
- Unity WebGL 대상 EditMode 167/167 통과 (qa-editor-cursor.xml). 실제 하드웨어 커서 숨김/잠금 및 마우스 시점 회전은 배치 테스트로 검증할 수 없어 로비/경기 각각 재확인 필요.
- 적용 시 Console: [QA-Cursor] Game view native lock/hide restored after Escape.


## 개인 스킵 후 로비 환경설정 허용

- 사용자 확인: ESC 커서 및 하이라이트 복귀 문제 해결. 추가 발견: 먼저 스킵한 사용자가 로비에 도착해도 전체 하이라이트 시간이 끝날 때까지 환경설정이 열리지 않음.
- 원인: LobbySettingsOverlay.Open과 Tick이 IsWaitingForMatch만 허용해 서버의 공유 Highlight 페이즈 동안 열기를 거절하거나 열린 창을 닫음.
- Bootstrap의 두 조건을 통일: 기존 로비 상태 또는 Highlight 진행 중 개인 IsLocalHighlightComplete인 상태에서 허용. 실제 경기 시작 시 기존 자동 닫기 유지. Client 수정 없음.
- 기존 로비 메뉴·커서·하이라이트·네트워크 관련 Unity EditMode 192/192 통과 (qa-skipped-lobby-settings.xml). 다중 참가자 중 먼저 스킵한 사용자의 실제 설정 열기/닫기는 플레이 재확인 필요.


## 캐릭터의 물건 밀기 차단과 발판/낙하 유지

- 사용자 요구 확정: 캐릭터가 걷거나 점프해서 접촉한다고 물건이 밀리면 안 됨. 쌓인 상자의 받침을 집으면 위 상자들은 중력으로 떨어져야 함.
- 기존 캐릭터 KCC CollisionLayerMask는 Default만 포함하고 Carryable을 제외했지만 PhysX 접촉은 살아 있었음. 캐릭터 무게 변경이 아니라 이동 질의와 강체 접촉의 역할 분리가 필요.
- NetworkPlayerMotor 초기화에서 Carryable을 KCC 충돌 질의에 포함하고, 캐릭터 Rigidbody.excludeLayers에는 Carryable을 추가. 캐릭터가 물건을 벽/발판으로 감지하되 운동량을 전달하지 않음. 물건의 Rigidbody, 질량, 중력, 물건끼리 충돌 및 받침 제거 각성 로직 유지. Client/프리팹/Photon SDK 변경 없음.
- 실제 NetworkedPlayer 프리팹과 Fusion Single 입력을 사용하는 PlayMode 검증: 상자 위 착지/접지, 옆에서 보행 시 차단, 두 경우 상자 위치 변동 0.04m 미만, 동적 강체 유지. 기존 받침 제거 낙하 및 물리 재동기화 등 총 6/6 통과(qa-carryable-contact-v3.xml).
- 초기 테스트의 렌더 단계 KCC 입력은 다음 fixed update에 사라져 이동 검증 실패. 실제 NetworkEvents.OnInput 전달 및 이벤트 초기화로 테스트 구성을 수정한 후 통과. 생산 코드 추가 변경 없음.
- 기존 EditMode 192/192 통과(qa-carryable-contract.xml). 별도 PC 다중 접속 및 WebGL 빌드 재배포는 미실행.
- 참고: https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Rigidbody-excludeLayers.html 및 저장소 KCC.Physics.cs의 레이어 기반 질의/ComputePenetration 경로 확인.


## 로비 → 경기 씬 전환 시 제거된 상호작용 대상 참조 정리

- 사용자 재현: 로비에서 PLAYGROUND 이동 중 PlayerInteractor.TryGetPrompt의 aimedTarget.transform 접근에서 MissingReferenceException. 플레이 중 물건 파괴 행동 없이도 씬 정리로 기존 CarryableItem이 제거될 수 있음.
- 이번 건에 한해 Client 수정 허용. PlayerInteractor의 공통 HUD 갱신 진입점에서 Unity null 검사로 제거된 조준·강조·소지 대상 참조를 실제 null로 정리. 비활성화 시 조준 참조를 해제하고 제거된 강조 대상 접근을 차단. 살아 있는 물건의 상호작용 및 네트워크 판정 변경 없음.
- 제거 직후 HUD 표시/숨김 및 비활성화 회귀 테스트 3개: 수정 전 3개 실패, 수정 후 통과. 테스트는 실제 오브젝트 제거로 씬 정리 후의 참조 상태를 재현하며 실제 다중 접속 씬 전환 전체를 자동 재현한 것은 아님.
- 함께 실행한 기존 소지자 복구 테스트는 수정 전에도 EditMode의 Rigidbody 보간 후 Transform 동기화를 즉시 기대해 실패. 실제 권한 적용 위치인 Rigidbody.position을 검사하도록 수정. 생산 물리 코드는 변경하지 않음.
- 최종 Unity EditMode 소지 상태 복구·상호작용 안내·네트워크 계약 테스트 121/121 통과 (qa-destroyed-target-final.xml). WebGL 빌드/배포 및 MR 생성 없음.


## EditMode 테스트 현행화

- EditMode 137개 C# 파일의 테스트 목록과 실패 11건을 현재 구현에 대조. 폐기된 사운드 빈 탭 전제의 `SettingsPresenterTests.Reset_OnATabWithNoRows_DoesNothing` 1개 제거. 실제 사운드 초기화/저장 테스트 유지. 구버전 토큰·방 설정 호환성, 호스트 복구 및 그림자 고정 회귀 검증은 현재 경로가 있으므로 유지.
- `Bubble_SitsAboveNameplate`는 닉네임 TMP 머티리얼 생성과 프레임 갱신을 사용하는 런타임 테스트이므로 EditMode에서 PlayMode의 InterfaceRuntimeSmokeTests로 이동. 실제 프레임 후 말풍선 하단과 이름 영역 상단 사이의 간격 검증. 오류 로그 무시나 성공 처리로 우회하지 않음.
- SessionPropertyMapperTests의 잘못 붙은 TestCase 4개를 원래의 InvalidPackedRules 테스트로 이동. 버전 1/2 시간 단위 호환성 검사 및 잘못된 입력 검사 모두 유지.
- Settings/CharacterCloset UI 테스트의 Awake 초기화 명시, Loading 테스트 캔버스 해상도 설정, PlaySettings 테스트 부모 RectTransform 보완. 실제 UI 레이아웃/입력/게임 코드는 변경하지 않음.
- 기존 입력 테스트의 조건 미충족 Assert.Pass를 명시적 Ignore로 변경. 실제 커서 잠금에 실패하는 무화면 실행도 이유와 함께 건너뜀. 검증하지 않은 항목을 통과로 집계하지 않음.
- 최종 전체 EditMode: 1,324개 중 1,322 통과 / 0 실패 / 2 건너뜀(무화면 커서 잠금, 기존 화면 캡처 테스트). 결과: qa-editmode-cleanup-final.xml.
- 이동한 PlayMode 말풍선 검증: 1/1 통과(qa-chat-bubble-playmode.xml). MR 및 배포 없음.
