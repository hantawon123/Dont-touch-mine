# Windows 클라이언트·Linux 게임 서버 배포

`release` 변경을 Jenkins `d205-unity-release`가 5분 간격으로 감지한다.
`Tools/client/Jenkinsfile`은 Windows x64 Mono 클라이언트와 Linux 전용 서버를 같은 Git SHA로 빌드한다.
기존 `d205-unity-webgl` 작업은 비활성 상태를 유지한다. 실행 노드의 `unity-webgl`은 기존 슬롯 이름일 뿐 WebGL을 빌드하지 않는다.

## 팀 사용 흐름

1. 기능 MR을 develop에 병합하고 검증한 내용을 release로 병합한다.
2. Jenkins의 계약 테스트 → Windows 빌드 → Linux 서버 빌드 → ZIP 생성 → 서버 준비 확인을 기다린다.
3. `https://j15d205.p.ssafy.io/download/`에서 ZIP을 받고 모두 압축 해제한 뒤 `Game.exe`를 실행한다.
4. 기존 파일은 자동 패치되지 않는다. 새 버전 공개 후에는 참가자 모두 최신 ZIP을 사용한다.

각 ZIP과 서버의 `version.txt`는 동일한 전체 커밋 SHA다. 이전 버전은 Photon 매칭 영역이 달라 최신 방에 입장하지 않는다.
`TEST_ONLY`는 네트워크 계약 테스트만 실행하며 빌드·배포하지 않는다.
release 이외의 브랜치를 검증용으로 연결하면 빌드·아티팩트 검증만 하고 공개 전환하지 않는다.

## 배포 전환과 한계

기존 `Tools/network/server-flow/release_host.py`를 그대로 사용한다.
`Builds/Download`의 HTML·ZIP·SHA256SUMS를 기존 release host의 `web` 디렉터리에 저장한다. 이는 다운로드용 정적 파일이며 WebGL 플레이어가 아니다.
빌드 중에는 기존 게임 서버를 유지한다. 새 서버가 준비된 뒤 `active.json`을 원자적으로 교체하므로 다운로드 버전도 함께 전환된다.
실패 시 기존 버전을 유지한다. 기존 경기 서버는 해당 방이 비고 종료될 때까지 유지된다.
현재는 활성 버전 1개 방(최대 6인)과 전환 중 이전 방까지 최대 2개 프로세스다. 이전 방이 남은 상태에서 추가 전환하면 슬롯 대기로 배포가 실패할 수 있으며 기존 방을 강제 종료하지 않는다.
다중 방 운영 규모로 확장하는 자동 할당기는 이 구성에 포함하지 않는다.

## 빌드 머신

- Unity 6000.3.22f1 GameCI Windows Mono / Linux 이미지, Docker, Python 3.11+, Git LFS.
- 기존 Jenkins Unity 실행 슬롯을 재사용하며 두 타깃을 순차 실행한다. Unity 컨테이너당 CPU 3개·메모리 8GB 제한. Jenkins 에이전트 서비스는 Git 체크아웃을 포함하여 MemoryMax=2G를 사용한다(512MB에서는 신규 클론 중 OOM 종료 확인).
- `CLIENT_CONFIG_DIR` 기본 `/var/lib/jenkins/.config/unity-webgl`의 `PhotonAppSettings.asset`를 주입한다.
- `CLIENT_UNITY_HOME` 기본 `/var/lib/jenkins/.config/unity3d/Unity`의 해당 EC2에서 정상 활성화한 Unity 라이선스를 사용한다. 다른 PC의 machine-id/라이선스를 복사하지 않는다.
- GMS 키는 공용 백엔드에만 둔다. ZIP에 소스·백업·환경변수 파일을 포함하지 않는다.
- 캐시는 `Library/ClientCiCache`, 로그는 `Logs/client-*`와 `Logs/server-build.log`다.
- Jenkins 빌드 로그 15개/아티팩트 3개를 보관한다. 서비스 릴리스는 현재·접속 중 버전을 자동 삭제하지 않는다. 디스크가 부족하면 사용하지 않는 과거 버전을 확인 후 정리한다.

## 호스트 이동과 복구

기존 [release host 설치 절차](../network/server-flow/release-host.md)를 따른다. 현재 호스트 루트는 `/var/www/d205-game/runtime`, HTTP는 loopback 4292, path_prefix는 `/download`다.
Nginx [다운로드 설정](nginx-download.conf)을 `/etc/nginx/snippets/d205-client-download.conf`로 설치하고 `nginx -t` 성공 후 reload한다. `backend/deploy/nginx/d205.conf`의 `d205-client*.conf` include가 이를 읽는다. `/download/`만 해당 loopback으로 프록시한다. API와 Jenkins 경로는 기존 설정을 유지한다.
새 서버에서는 유효한 Unity 라이선스를 다시 준비하고 설정 경로·도메인·서비스 계정을 맞춘다.

```bash
python3 Tools/network/server-flow/releases.py /var/www/d205-game/runtime status
python3 Tools/network/server-flow/releases.py /var/www/d205-game/runtime activate <이전_전체_SHA>
```

롤백도 해당 버전 서버 준비 후 전환된다. status의 `outcome=activated`, `active`와 `/download/current.json`의 revision을 확인한다.
실행 중인 방을 보존하려면 배포 때 release host 자체를 재시작하지 않는다.

## 로컬 검증

```bash
python3 -m unittest discover -s Tools/client -p 'test_*.py'
python3 -m unittest discover -s Tools/network/server-flow -p 'test_*.py'
```
