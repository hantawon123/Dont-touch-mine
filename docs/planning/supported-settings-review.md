# 기존 지원 설정 확인 — 2026-09-09

## 확정 범위

사용자 지시: 이미 실제 반영되는 기능만 확인하고, 미연동 기능을 새로 구현하거나 클라이언트 화면 코드를 변경하지 않는다.

- 742: 마스터 볼륨만 확인. 배경음·환경음·효과음, 마이크 장치·모드·볼륨·테스트의 신규 연동은 후속.
- 741: 디스플레이 모드, 해상도, FPS 제한/수직동기화 해제, 텍스처 품질만 확인. AA/HBAO/그림자/피사계 심도/볼류메트릭 신규 연동은 후속.
- 기존 develop에 병합된 인터페이스·컨트롤 구현은 유지한다. 전달된 초기 설정 현황표를 기준으로 이미 병합된 작업을 되돌리지 않는다.

기준: `origin/develop` ad85f080. 설정 작업 자체는 테스트와 이 문서만 포함한다. 아래 별도 사용자 요청에 따른 WebGL 입력 버그 수정은 예외로 포함한다.

## 실제 경로와 한계

| 항목 | 현재 적용 경로 | 범위/한계 |
| --- | --- | --- |
| 디스플레이 모드·해상도 | UnityGraphicsSettingsApplier → Screen.SetResolution | Windows Player에서 적용. Editor와 WebGL은 기존 코드에서 생략하며 브라우저가 캔버스 크기를 관리한다. |
| FPS 제한 | QualitySettings.vSyncCount = 0, Application.targetFrameRate | 120/60/40/30 설정값 전달. 목표 FPS이며 실제 성능이나 브라우저 프레임 스케줄링을 보장하지 않는다. |
| 텍스처 품질 | QualitySettings.globalTextureMipmapLimit | 높음/중간/낮음 = 0/1/2. 밉맵 없는 텍스처와 예외 그룹은 영향을 받지 않을 수 있다. |
| 마스터 볼륨 | AudioListener.volume | 0~100을 0~1로 변환. 하위 채널이나 마이크 입력 볼륨 연동을 의미하지 않는다. |

시작 시 ProjectLifetimeScope의 GraphicsSettingsStartup / SoundSettingsStartup이 저장된 설정을 적용한다. 설정 화면은 초안을 유지하고 적용 요청 시에만 공용 설정 시스템에 반영한다.

해상도 선택 목록은 현재 고정 목록이다. Screen.resolutions 기반 필터링은 741 전체 완료 조건에 남아 있지만 이번 요청에 따라 신규 구현하지 않는다.

## 검증

최종 Unity 6000.3.22f1 EditMode 결과: **125개 통과, 실패 0개**. `supported-settings-editmode.xml` / `.log`에 기록.

- SupportedSettingsRuntimeTests: 실제 Unity의 FPS/수직동기화·텍스처·AudioListener 값 및 저장값 시작 적용 확인. 전역 설정은 테스트 후 원복.
- 기존 SoundSettingsTests / GraphicsSettingsTests / SettingsPresenterTests: 저장·초안·적용·취소 흐름 회귀 확인.
- Editor 테스트에서 창 크기를 바꾸지 않는다. Windows 디스플레이 전환이나 Chrome/Edge 실제 FPS 측정 결과로 간주하지 않는다.
- 별도 추가 구현을 시험하던 로그(`audio-graphics-*`)는 제외된 코드의 결과이므로 최종 근거로 사용하지 않는다. 최종 로그 이름은 `supported-settings-*`이다.

## 575 / 875 완료 조건

설정 회귀 테스트만으로 두 Task를 완료 처리할 수 없다.

- 575: 같은 최종 Windows 빌드의 2/6인 전체 경기·결과·로비 복귀·재경기, 동시 상호작용 및 이탈/호스트 종료 시나리오 기록 필요.
- 875: 같은 최종 WebGL 빌드의 Chrome/Edge 다중 접속, 실제 음성 송수신·권한, 탭 전환, 물건·공격 동작 기록 필요.
- 외부 PC가 없으므로 현재 PC의 다중 클라이언트로 검증 가능한 범위와 별도 PC 검증을 구분한다. 과거 성능 측정이나 단일 플레이어 테스트를 최신 6인 수용 결과로 바꾸어 기록하지 않는다.
- 이번 범위 축소로 제외된 741/742 조건도 완료로 표시하지 않는다.

## 추가 요청: WebGL 한글 조합 중 안내 문구 겹침

- 증상: 방 이름에서 첫 한글을 조합하는 동안 `방 이름 입력` 문구가 뒤에 겹침.
- 원인: 브라우저는 조합 중인 글자를 표시하지만 TMP에는 아직 확정값을 보내지 않는다. TMP_InputField.UpdateLabel이 빈 값에 대해 placeholder.enabled를 다시 켜므로 Open에서 한 번 끈 Graphic이 재표시된다.
- 수정: 브라우저 입력 중에는 Unity placeholder 오브젝트를 비활성화해 안내 문구의 표시를 HTML 입력창에 맡긴다. 입력 종료 시 기존 활성 상태를 복원하고 확정된 입력값으로 TMP 라벨을 갱신한다.
- 공용 WebTextInput 경로이므로 같은 연동을 사용하는 닉네임·채팅 등에도 적용한다. 조합 중 확정값 전송이나 Enter 제출 정책은 변경하지 않는다.
- 검증: 기존 Node 브라우저 입력 브리지 테스트와 실제 TMP의 라벨 갱신을 사용하는 WebTextInputPlaceholderTests. 공개 사이트 반영에는 후속 WebGL 빌드·배포가 필요하다.
