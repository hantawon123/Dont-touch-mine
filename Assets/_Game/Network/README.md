# Network

Photon Fusion과 맞닿는 코드만 둡니다. 러너 생성, 세션 시작과 종료, 방 목록 수신,
스폰과 상태 복제가 여기에 있습니다.

- `Server`가 확정한 규칙 판정을 참가자에게 복제하는 것이 이 계층의 역할입니다.
  반대 방향은 없습니다. `Server`는 Photon을 모릅니다.
- Fusion 타입(`NetworkRunner`, `PlayerRef`, `NetworkObject`)은 이 계층 밖으로
  나가지 않습니다. 바깥에는 `Core`의 중립 타입으로 전달합니다.
- `Client`를 직접 참조하지 않습니다.

## 왜 `Server`와 분리했나

`Server`는 경기 규칙을 순수 C#으로 판정하고 EditMode 테스트로 검증합니다.
Fusion을 같은 어셈블리에 두면 그 테스트에 Photon이 딸려 들어옵니다.
전송 계층만 떼어내면 양쪽 모두 원래 목적대로 남습니다.

## 두 인스턴스로 테스트하기

빌드를 만들지 않고 에디터 안에서 호스트와 클라이언트를 동시에 띄웁니다.
Multiplayer Play Mode 2.0 기준입니다.

### 1. 태그 만들기 (프로젝트당 한 번)

`Edit > Project Settings > Multiplayer > PlayMode > Player Tags`에서 `+`를 눌러
`Host`와 `Client`를 만들고 **Save**를 누릅니다. `ProjectSettings/VirtualProjectsConfig.json`에
기록되므로 팀원은 이 단계를 건너뜁니다.

### 2. 시나리오 만들기 (프로젝트당 한 번)

1. `Window > Play Mode > Scenarios`를 엽니다.
2. **Configure play mode scenarios**를 고릅니다.
3. `+`로 시나리오를 만듭니다.
4. 오른쪽에서 **Editor** 체크가 켜져 있는지 확인하고,
   **Additional Editor Instances**의 `+`로 인스턴스를 하나 추가합니다.

### 3. 태그 배정

같은 창에서 각 인스턴스의 **Tags** 드롭다운을 펼쳐 고릅니다.

| 인스턴스 | 태그 |
| --- | --- |
| Editor | `Host` |
| Additional Editor | `Client` |

한 인스턴스에는 태그를 하나만 붙일 수 있습니다.

### 4. Play

호스트가 방을 열고 클라이언트가 그 방으로 들어갑니다. 추가 인스턴스 창은
`Ctrl+F9`~`Ctrl+F12`로 전환합니다.

### 태그가 하는 일

태그는 `GameSessionLifetimeScope`의 모드만 덮어씁니다. 방 이름·맵·인원·비밀번호는
인스펙터 값을 양쪽이 그대로 공유하므로 한 곳만 고치면 됩니다.

태그를 붙이지 않으면 인스펙터의 `Mode`가 그대로 쓰이고, 빌드에는 가상 플레이어가
없으므로 태그 분기 자체가 동작하지 않습니다.

> Unity 문서는 역할 배정에 태그 대신 Dedicated Server 패키지를 권합니다. 그쪽은
> 전용 서버 빌드를 전제하는데 우리는 플레이어 호스트 방식이라, 전용 서버로 옮기기
> 전까지는 태그가 맞습니다.
