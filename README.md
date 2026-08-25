# Unity 프로젝트 컨벤션

이 문서는 프로젝트 코드와 Unity 에셋을 같은 기준으로 작성하기 위한 아키텍처 규칙만 다룹니다.

## 개발 환경

- Unity `6000.3.22f1`
- Universal Render Pipeline(URP)
- Photon Cloud
- VContainer `1.19.0`
- R3 `1.3.1`
- UniTask `2.5.11`

Unity 버전과 렌더 파이프라인을 맞추지 않으면 씬, 프리팹, 머티리얼과 `ProjectSettings`에 불필요한 변경이 생길 수 있습니다.

R3의 코어 DLL은 NuGetForUnity가 `Assets/Packages`에 복원합니다. DLL이 없다면 Unity 메뉴에서 `NuGet > Restore Packages`를 실행합니다. `Assets/Packages`는 생성 결과이므로 직접 수정하지 않습니다.

## 기본 구조

```text
Assets/_Game/
├─ Bootstrap/  앱 전체 조립과 루트 LifetimeScope
├─ Core/       공통 게임 규칙과 순수 타입
├─ Client/     입력, UI, 카메라, 애니메이션과 로컬 표현
├─ Server/     Photon 연결, 권한, 상태 동기화와 경기 진행
├─ SOAP/       정적 Definition과 Config
├─ Content/    씬, 프리팹, 오디오와 ScriptableObject 에셋
└─ Tests/      EditMode와 PlayMode 테스트
```

asmdef 의존 방향은 다음과 같습니다.

```text
Bootstrap -> Client, Server
Client    -> Core, SOAP
Server    -> Core, SOAP
SOAP      -> Core
Core      -> 외부 게임 계층에 의존하지 않음 (허용: UniTask)
```

`Core`가 참조할 수 있는 것은 `UniTask` 하나뿐입니다. `Core`의 포트 인터페이스가 서버 요청을 표현하려면 비동기 반환 타입이 필요하고, 아래 비동기 규칙이 `UniTask` 반환을 요구하기 때문입니다. R3, Fusion, VContainer, UI 계열과 `Client`·`Server`·`SOAP`는 참조하지 않습니다.

`Client`와 `Server`는 서로 직접 참조하지 않습니다. 두 영역이 함께 사용하는 규칙과 타입은 `Core`로 이동합니다. 이 프로젝트에서 `Server`는 별도 Spring 서버가 아니라 Unity 안의 Photon 네트워크 영역을 뜻합니다.

## VContainer 의존성 주입

- 앱 전체에서 하나만 존재하는 서비스는 `ProjectLifetimeScope`에 Singleton으로 등록합니다.
- 경기나 씬에서만 필요한 객체는 해당 씬의 자식 LifetimeScope에 Scoped로 등록합니다.
- 일반 C# 클래스는 생성자 주입을 사용합니다.
- `MonoBehaviour`는 VContainer의 메서드 주입을 사용합니다.
- 코드 곳곳에서 컨테이너의 `Resolve`를 직접 호출하는 Service Locator 방식은 사용하지 않습니다.
- View, Presenter, 네트워크 서비스의 생성과 연결은 LifetimeScope의 `Configure`에서만 조립합니다.

`VContainerSettings.asset`이 `ProjectLifetimeScope.prefab`을 Preloaded Asset으로 로드하므로 어떤 씬부터 실행해도 앱 공통 의존성을 사용할 수 있어야 합니다.

## R3 상태와 이벤트

- 런타임 상태와 로컬 이벤트 흐름은 R3로 표현합니다.
- 변경 가능한 `ReactiveProperty<T>`는 소유 클래스의 `private` 필드로 둡니다.
- 외부에는 `ReadOnlyReactiveProperty<T>` 또는 `Observable<T>`만 공개합니다.
- 구독은 소유 객체의 수명에 맞춰 `CompositeDisposable`, `AddTo` 또는 명시적 `Dispose`로 반드시 해제합니다.
- 여러 시스템이 아무 제한 없이 값을 변경할 수 있는 public setter를 만들지 않습니다.
- 한 번 호출하고 끝나는 비동기 작업은 R3가 아니라 UniTask로 작성합니다.

## UI와 로직 분리

UI는 MVP 구조를 기본으로 사용합니다.

- View(`MonoBehaviour`)는 화면 출력과 사용자 입력 전달만 담당합니다.
- Presenter(일반 C# 클래스)는 상태를 구독하고 View 갱신을 지시합니다.
- View에서 점수 계산, 경기 판정, Photon 호출과 데이터 저장을 하지 않습니다.
- Presenter는 View의 구체 구현보다 필요한 인터페이스에 의존합니다.
- UI 상태의 원본은 View 컴포넌트가 아니라 Model 또는 런타임 상태 객체입니다.

## UniTask 비동기 규칙

- 비동기 메서드는 `UniTask` 또는 `UniTask<T>`를 반환합니다.
- `async void`는 사용하지 않습니다. Unity 이벤트에서 기다릴 수 없을 때만 예외 처리를 갖춘 `Forget()` 진입점을 사용합니다.
- `CancellationToken`은 마지막 매개변수에 두고 MonoBehaviour나 LifetimeScope의 수명과 연결합니다.
- 씬 로드, 리소스 로드, 팝업 연출과 서버 요청을 `async/await`로 통일합니다.
- 경기 제한 시간의 원본은 클라이언트의 `Delay`나 Coroutine이 아니라 Photon의 서버 시간 기준으로 계산합니다.

## Photon 상태 규칙

- 방 참가자 모두에게 영향을 주는 결과는 권한을 가진 네트워크 영역에서 확정합니다.
- 클라이언트는 입력 의도를 보내고, 확정된 상태를 받아 화면에 표현합니다.
- 경기 페이즈, 남은 시간, 점수, 아이템 소유와 발견 여부를 ScriptableObject에 저장하지 않습니다.
- 네트워크로 확정된 상태를 로컬 R3 상태에 반영한 뒤 UI, 사운드와 이펙트가 이를 구독하게 합니다.

```text
사용자 입력
  -> Photon 요청 및 권한 검증
  -> 네트워크 상태 확정/동기화
  -> 로컬 R3 상태 갱신
  -> Presenter
  -> View
```

## ScriptableObject 사용 범위

SOAP 폴더에는 런타임 변수 에셋과 이벤트 채널을 만들지 않습니다.

- `Definitions`: 아이템, 맵 등 변경되지 않는 데이터 정의
- `Config`: 경기 시간, 인원 제한 등 조정 가능한 정적 설정

플레이 중 변하는 상태는 일반 C# 객체와 R3가 소유합니다. 방 정보와 네트워크 상태는 Photon 영역이 원본을 소유합니다. ScriptableObject 에셋은 런타임 종료 후 값이 남지 않도록 상태 저장소로 사용하지 않습니다.

## 씬 독립 실행

- 작업 중인 씬을 Bootstrap 또는 Title 씬을 거치지 않고 직접 Play해도 오류 없이 실행되어야 합니다.
- 앱 공통 서비스는 `ProjectLifetimeScope`가 제공합니다.
- 씬 전용 의존성이 생기면 씬 안에 자식 LifetimeScope를 추가합니다.
- 씬에 필요한 참조가 없을 때 조용히 실패하지 말고 초기화 단계에서 명확한 오류를 냅니다.

## Unity 에셋 규칙

- 외부 패키지와 구매 에셋의 원본 파일은 직접 수정하지 않습니다.
- 프로젝트용 수정은 `Content` 아래 Prefab Variant나 별도 에셋으로 만듭니다.
- 모든 Unity 에셋은 대응하는 `.meta` 파일과 함께 관리합니다.
- 메인 씬과 같은 프리팹을 여러 명이 동시에 수정하지 않습니다.
- Unity가 생성하는 `Library`, `Temp`, `Logs`, `UserSettings`, IDE 프로젝트 파일은 프로젝트 소스로 취급하지 않습니다.
- Scene, Prefab과 ScriptableObject는 Force Text 직렬화를 유지합니다.

## 테스트 기준

- 상태 전환, 점수 계산과 권한 판정 같은 순수 로직은 EditMode 테스트를 작성합니다.
- 씬 조립, View 연결과 네트워크 객체 수명은 PlayMode 테스트 또는 실제 멀티플레이 실행으로 검증합니다.
- 새 asmdef를 추가할 때 의존 방향이 역전되거나 `Client`와 `Server`가 직접 연결되지 않았는지 확인합니다.
- 기능 완료 전 Unity Console의 컴파일 오류가 없어야 하며, 관련 테스트를 실행합니다.
