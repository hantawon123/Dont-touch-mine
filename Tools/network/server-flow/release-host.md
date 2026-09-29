# 한 장비의 다중 방 풀과 릴리스 교체

Linux x86_64 게임 서버를 방마다 별도 프로세스로 실행한다. 현재 버전의 동시 방 상한은
`room_capacity`, 항상 준비할 빈 방 수는 `warm_rooms`, 이전 버전·배포 후보를 포함한 전체 프로세스
상한은 `max_processes`다.
Unity Multi-Peer나 여러 장비의 자동 증설은 사용하지 않는다. Python 3.11 이상이 필요하다.

## 설정

`host.example.json`을 복사해 경로·지역·주소를 맞춘다. 예제는 **동시 방 20개, 최대 120명**을 위한
슬롯 설정이고 성능 보장값은 아니다. 실제 입장 인원은 Photon 한도와 게임 자원에도 제한된다.

```json
{
  "root": "/srv/d205-game/runtime",
  "bind": "127.0.0.1",
  "port": 4292,
  "origin": "https://your-game.example",
  "api_origin": "https://your-api.example",
  "region": "kr",
  "path_prefix": "/download",
  "startup_timeout": 120,
  "room_capacity": 20,
  "warm_rooms": 2,
  "max_processes": 40,
  "internal_api_origin": "http://127.0.0.1:8080",
  "chat_internal_key": ""
}
```

- 새 필드가 없는 기존 설정은 `rooms_per_release`를 용량과 대기 방 수로 함께 사용한다.
- 방 용량은 1~64 정수, 대기 방 수는 1~용량, 전체 상한은 용량보다 큰 정수이며 최대 128이다.
  이 한도는 성능 보증이 아니다.
- `chat_internal_key`는 게임 서버가 채팅 금칙어 목록을 받고 채팅 기록을 보낼 때 내미는 공유 키다\
  (S15P21D205-1027). 계정 서비스의 `CHAT_INTERNAL_KEY`와 같은 값이어야 한다. **이 파일에 실제 값을 적고\
  저장소에 넣지 않는다.** 서버의 설정 파일에만 둔다.
- 키는 명령줄이 아니라 `D205_CHAT_KEY` 환경변수로 게임 서버에 전달한다. 명령줄 인자는 같은 장비의\n  누구나 `ps` 로 읽는다.
- 비워 두면 주소도 넘기지 않는다. 채팅은 그대로 동작하고 금칙어 필터와 기록만 빠진다.\
  값이 틀리면 그 경로가 404로 답하므로 게임 서버 로그에 목록을 받지 못했다는 경고가 남는다.
- `internal_api_origin`은 nginx를 거치지 않는 루프백 주소다. 계정 서비스가 127.0.0.1:8080에만 바인딩돼\
  있으므로 같은 장비의 게임 서버만 닿는다. nginx에 `/internal`을 열면 안 된다 - 목록이 새고 누구나\
  가짜 채팅 기록을 심을 수 있다.
- 여유 슬롯은 버전 교체용이다. `max_processes=rooms_per_release*2`면 이전 버전 전체와 새 버전 전체를 함께 담을 수 있다.
- 설정 변경은 호스트 재시작이 필요하고 **진행 중인 방이 종료된다.** 참가자가 없을 때만 변경한다.
- 기본 서비스 파일의 CPU 2개·메모리 5GiB 제한은 시험용이다. 슬롯을 늘린다고 자원이 늘어나지 않는다.
  20방 배포 전부터 현재 서비스 제한과 여유 메모리를 확인해야 한다. 상한 부족으로 호스트 전체가 OOM 종료되지 않도록 한다.
  100명 운영 전에 실제 6인 게임 부하로 CPU·메모리·tick 지연을 측정하고 장비/서비스 상한을 조정한다.

## 방 생성·종료

1. 호스트가 `warm_rooms`개의 빈 서버만 차례로 시작한다. 앞선 방이 Ready 상태가 된 뒤 다음 방을 시작해 초기화 폭주를 줄인다.
2. 각 프로세스는 다른 6자리 Photon 방 코드와 다른 `state/slot-N` 홈을 사용한다. 서버 기기 ID는 슬롯별로 유지된다.
3. 기존 클라이언트의 방 만들기가 Photon에서 `available=true`인 서버를 찾아 참가·선점을 요청한다.
   서버 권한으로 한 사람만 첫 방장이 되고, 선점된 방은 빈 서버 목록에서 제외된다. 나머지 사람은 게임 찾기로 참가한다.
4. 빈 방이 선점되면 용량 안에서 다음 빈 방을 준비한다. 한 방이 종료되거나 프로세스가 실패하면 필요한 대기 수만 보충한다.
5. 모든 방이 사용 중이면 클라이언트는 빈 서버를 최대 30초 기다린다. 예약 대기열은 없으며 시간 초과하면 실패 안내 후 재시도한다.
   동시에 같은 빈 서버를 선점하려는 경우 기존 접속 거절/CodeUnavailable 처리로 돌아갈 수 있다. 전역 FIFO 예약은 제공하지 않는다.

아무도 접속하지 않은 정상 대기 방은 시간 제한 없이 유지하며, 접속자가 없을 때는 `warm_rooms`개만 준비한다. 최초 접속 후 방 선점이 완료되지 않으면 120초 제한을 적용하고, 사용한 방의 빈 상태 종료 규칙도 유지한다. 새 버전 활성화 후 이전 버전의 미사용 대기 방은 종료하지만, 접속 이력이 있는 방은 정상 종료까지 유지한다. 종료 요청에 10초간 응답하지 않는 프로세스는 강제 종료한다.

이 정책은 `DedicatedServerStartup`이 포함된 새 서버 빌드와 갱신된 `release_host.py`를 함께 배포해야 적용된다. 기존 호스트 서비스 재시작은 자식 게임 서버를 종료하므로 플레이 중인 방이 없는 시점에 수행한다.
장시간 시작하지 못하는 프로세스는 `startup_timeout` 후 종료하고 재시도한다.
`ready=true`는 초기 인증·방 준비 완료이며 현재 비어 있다는 뜻이 아니다. 빈 방 여부의 원본은 Photon의 서버 속성이다.

## 배포와 롤백

Windows/Linux 빌드는 [데스크톱 배포 안내](../../client/README.md)를 따른다.
`web` 디렉터리는 현재 Windows 다운로드 사이트이며 WebGL 플레이어를 의미하지 않는다.

```bash
umask 077
python3 tools/releases.py /srv/d205-game/runtime stage <release-id> \
  --web incoming/<release-id>/web --server incoming/<release-id>/server
python3 tools/release_host.py host.json
# 다른 터미널에서 실행
python3 tools/releases.py /srv/d205-game/runtime activate <release-id>
python3 tools/releases.py /srv/d205-game/runtime status
```

새 버전의 첫 서버가 Ready가 된 뒤 다운로드 대상을 바꾸고 나머지 새 방을 순차 준비한다.
이전 버전의 미사용 방은 종료하고, 접속 이력이 있는 방은 기존 종료 규칙까지 유지한다. 종료된 이전 버전 방은 다시 만들지 않는다.
전체 상한이 꽉 차면 새 방 준비/배포를 기다리며, 제한 시간 내 준비되지 않으면 이전 공개 버전을 유지한다.
진행 중인 게임을 다른 프로세스로 옮기거나 강제 종료해서 배포 공간을 확보하지 않는다.
이전 버전으로 `activate`하면 살아 있는 해당 버전 방을 재사용하고 빈 슬롯을 보충한다.
이전 다운로드 ZIP을 가진 플레이어는 버전이 다르므로 새 버전 방에 합류하지 못한다.

**배포에는 호스트 서비스 재시작을 쓰지 않는다.** activate만 사용한다.
서버 실행 파일·version.txt·다운로드 파일의 일치와 SHA-256 검증은 기존 release 도구가 수행한다.
실행 중인 릴리스, 상태, 로그는 삭제하지 않는다. 원시 Unity 로그에는 인증 정보가 있을 수 있어 공개하지 않는다.

`status.json`의 각 항목은 `release`, `room_index`, 물리 `slot`, PID, 방 코드, 준비 상태, 이전 버전 여부를 제공한다.
기존 첫 방 키는 릴리스 ID 그대로이며 추가 방 키는 `<release>#1`, `<release>#2` 형태다.
관리 파일과 서버 실행 파일은 HTTP로 노출하지 않는다. Nginx 공개 경로는 [다운로드 설정](../../client/nginx-download.conf)을 사용한다.

## 에디터 연결

빌드와 호환되는 같은 소스를 받은 뒤 다음 명령으로 현재 서버 버전을 맞추고 Play를 다시 시작한다.

```powershell
python Tools/network/server-flow/sync_editor.py . --origin https://your-game.example/download
```

Git에서 제외된 `UserSettings/ServerFlowVersion.txt`에 버전만 기록한다. 지역은 서버와 같은 한국(`kr`)으로 설정한다.
임의의 버전 번호로 호환성을 우회하지 않는다. 로컬 개발 서버 Play 방식과 이 배포 서버 풀은 별도 실행 경로다.

## 자동 검증

```bash
python3 -m unittest discover -s Tools/network/server-flow -p 'test_*.py'
```

다중 슬롯 신원 분리, 개별 방 재기동, 이전 버전 방 유지, 상한 도달 시 기존 방 보존,
남은 이전 방을 통한 롤백, 초기화 정지 프로세스 회수와 기존 다운로드/보안 경로를 검사한다.
이 테스트만으로 100명 실제 경기 성능이 검증되지는 않는다.

실제 EC2 격리 시험은 서버 2개 동시 Ready와 개별 프로세스 교체까지 통과했다. 20개 슬롯 보충·상한은 자동 테스트로 검증하며, 20방/120인 실경기 부하는 아직 검증하지 않았다. 운영 설정의 20방 적용은 배포 시 수행한다.
