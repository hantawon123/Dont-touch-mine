# WebGL 자동 빌드와 배포

GitLab develop → EC2 Jenkins → Unity Docker → `https://j15d205.p.ssafy.io/play/`.
EC2의 `unity-webgl` 전용 에이전트를 사용한다. 기존 백엔드 작업은 이 전용 노드에 배정되지 않는다.
같은 EC2이므로 CPU·디스크는 공유한다. 실행 슬롯 분리만으로 빌드 속도가 빨라진다고 보장하지 않는다.
새 접속에는 **최신 성공 빌드**를 제공한다. 커밋 반영에는 SCM 확인(최대 약 5분)과 빌드 시간이 필요하다.

## 현재 적용 범위 (987~990)

서버 전환 이후에는 WebGL만 게시하지 않는다. 같은 커밋 SHA로 `Builds/WebGL`과 `Builds/Server`를 순차 빌드하고 `publish_release.py`로 함께 게시한다. 게임 서버가 인증·Photon 방 준비를 끝낸 후에만 `/play/`의 신규 접속을 새 버전으로 보낸다. 기존 탭과 경기는 이전 버전을 유지한다.

현재 환경 세팅을 마쳤으며 **WebGL Jenkins 작업 비활성화와 공개 `/play/` 유지보수는 유지**한다. 실제 최종 빌드·공개 재개·외부 PC 6인 검증은 사용자 요청으로 S15P21D205-1007에 분리했다. 아래 자동 배포 설명은 작업 활성화 이후의 동작이다.

- 배포 호스트: `d205-game-release.service`, `/var/www/d205-game`, loopback `4292`, `/play/` HTTPS 프록시.
- 기존 임시 시험 호스트의 loopback `4291`과 별도다. 새 배포 호스트는 최초 정상 서버/WebGL 쌍을 아직 활성화하지 않아 내부 루트도 503을 반환한다.
- 운영·이전·복구 명령은 [서버 릴리스 호스트](../network/server-flow/release-host.md)의 ‘CI 배포 환경’ 절을 따른다. 기존 `publish.py`와 정적 사이트 복구 기록은 이전 방식의 이력이며 새 서버 전환 배포에 사용하지 않는다.
- 팀의 일상 테스트에는 [에디터 개발 서버](../network/server-flow/development-server.md)를 사용한다. CI 빌드가 필요 없다.

## Jenkins

- 작업: `d205-unity-webgl`, Pipeline script from SCM, `Tools/webgl/Jenkinsfile`.
- 저장소: 기존 GitLab 저장소와 `gitlab-deploy-token` 읽기 자격 증명 사용.
- 운영 Branch Specifier: `*/develop`. MR 병합 전 검증에는 대상 기능 브랜치를 명시적으로 선택한다.
- feature 빌드는 검증·산출물 보관까지만 실행한다. `origin/develop`만 Publish 단계를 실행한다.
- 실행 동시성 1, 제한 120분. NuGet 복원 1 CPU/1GB, Unity 빌드 3 CPU/8GB.
- Unity 컨테이너 CPU shares는 1024(기본 가중치)로 두어 CPU 경쟁 시 기존 256의 낮은 우선순위를 해제한다. CPU 3개는 상한이며 예약량이 아니다. 이미 상한까지 사용하는 경우 속도 개선은 제한적이다. 백엔드 지연이 증가하면 256으로 복구한다.
- 노드 `d205-unity-webgl`: 라벨 `unity-webgl`, 실행 슬롯 1, 라벨 일치 작업만 허용.
- 전용 에이전트 루트 `/var/lib/jenkins/agents/unity-webgl`, 서비스 `d205-unity-agent.service`.
  서비스 정의는 `unity-agent.service`에 보관한다. `agent.args`와 `agent-secret`은 서버에서 0600으로 관리하고 Git에 넣지 않는다.
- `FORCE_BUILD`: 같은 SHA도 재빌드. `FAST_BUILD`: IL2CPP OptimizeSize 비교용이며 자동 배포하지 않는다.
- `TEST_ONLY`: 계약 테스트만 실행해 전용 에이전트 연결을 검증한다. 플레이어 빌드·산출물 게시·배포 기준 갱신을 생략한다.
- 최초 실행이나 비교 기준 누락 시 빌드한다. 이후 마지막 정상 설정의 성공 SHA와 비교하여 backend/docs/source만 바뀌면 생략한다.
  빠른 설정의 비교 빌드와 무관한 변경으로 생략한 실행은 정상 빌드 기준 SHA를 갱신하지 않는다.
- Unity는 명시적 `git lfs pull`로 에셋을 복원한다. Jenkins 전역 Git 설정을 변경하지 않는다.
- Git LFS로 모델·텍스처를 복원하고, NuGetForUnity CLI 4.5.0으로 R3 등을 먼저 복원한다.
- Unity 이미지: WebGL `ubuntu-6000.3.22f1-webgl-3`와 Linux Dedicated Server 지원이 포함된 `ubuntu-6000.3.22f1-linux-il2cpp-3.2.2`를 사용한다. 정확한 이미지 참조는 build.sh를 따른다.
- Unity `Library`는 Jenkins workspace에 남아 다음 빌드에서 재사용된다. Unity 버전 변경 시 캐시 재생성이 필요하다.
- `Library/WebGLCiCache`에 머신 Bee 캐시와 NuGet 패키지·도구를 보관하고 컨테이너에 마운트한다.
  동시에 실행하는 다른 프로젝트에서 동일 Library를 공유하지 않는다.
- 네트워크 계약 EditMode 테스트 통과 후 빌드한다. 테스트·빌드 로그는 콘솔에 실시간 출력하고 실패 시에도 보관한다.
- 성공 산출물은 최근 3회, 빌드 기록은 최근 15회 보관한다.
- 실패한 빌드는 Publish까지 진행하지 않는다. 시작할 때 이전 산출물 폴더를 비워 오래된 파일의 재배포를 막는다.
- 이전 실행 로그는 자격 증명 검사 전부터 제거한다. `Logs/webgl-timings.tsv`는 복원·테스트·빌드·전체 시간과 종료 코드를 기록한다.
  `Logs/webgl-build-report.json`은 Unity 버전·SHA·IL2CPP 설정·단계별 시간을 기록한다. 중첩 단계 시간을 합산하지 않는다.

## 반복 빌드 측정

### 화면 보존을 위한 셰이더 설정

- PC URP의 LOD Cross Fade와 Light Cookies만 비활성화한다. 현재 직렬화된 LODGroup 34개는 Fade Mode None이며 Light 70개는 Cookie 참조가 없다(2026-09-08 develop 통합 후 재검사). 관련 프리팹 오버라이드와 런타임 할당도 소스 검색에서 발견되지 않았다.
- 조명, 그림자, SSAO, 반사, 안개, 후처리, 텍스처 품질은 유지한다. 앞으로 LOD 전환 효과나 Light Cookie를 추가하면 PC URP에서 해당 지원을 다시 활성화해야 한다.
- 정적 참조 검사만으로 모든 플레이 화면의 동일성을 보증하지 않는다. 병합 전 기존 빌드와 같은 카메라·씬·품질로 화면을 비교한다. 복구는 `PC_RPAsset.asset`의 `m_EnableLODCrossFade`, `m_SupportsLightCookies`를 각각 1로 되돌린다.

목표는 PC 브라우저용 게임의 일반적인 변경 빌드를 약 5분 수준으로 줄이는 것이다. 5분을 절대 제한으로 사용하지 않는다.
WebGL에서는 PC 품질을 기본으로 사용하고 Mobile 품질을 빌드 대상에서 제외한다.
PC 렌더링 효과를 유지한 상태에서 빌드 시간과 셰이더 변형 수를 비교한다.

같은 장비·Unity 버전·프로필로 최초 실행과 반복 실행을 구분한다. 동일 코드, 작은 C# 변경, UI 변경을 각각 측정한다.
캐시 없는 최초 실행을 평균에서 숨기지 않고 각 실행 시간과 최대값을 기록한다. 5분 타임아웃으로 실패시키는 것은 목표 달성이 아니다.
`FAST_BUILD`의 속도 이득과 인게임 성능은 별도로 검증한 후 기본 적용 여부를 결정한다.
`FAST_BUILD`는 IL2CPP `OptimizeSize`와 WebAssembly `BuildTimes`를 함께 적용하고 종료 시 기존 설정으로 복원한다.
프로필을 바꾸는 첫 실행은 컴파일 캐시를 다시 준비할 수 있으므로 동일 프로필 반복과 구분한다.
`WEBGL_TEST_ONLY=1 bash Tools/webgl/build.sh`로 빌드 없이 계약 테스트만 실행할 수 있다.
로컬 테스트 환경의 설정 경로는 `WEBGL_CONFIG_DIR`, `WEBGL_UNITY_HOME`으로 지정한다.

```sh
python3 Tools/webgl/test_changes.py
bash Tools/webgl/test_build.sh
python3 Tools/webgl/test_publish.py

# 업데이트 알림은 사용자 확인 전 페이지를 이동시키지 않는지 검사 (Node.js)
node Tools/webgl/test_release_info.cjs
```

## 재개 이력

- 2026-09-07 최신 develop 통합 후 네트워크 계약 테스트 100/100 통과.
- 이전 6개 실패는 홈 복귀 정책과 테스트 spy의 방 찾기 전용 기대값 불일치였다. 홈/강퇴 복귀 경로와 콜백 이후 처리 조건을 검증한다.
- 로컬 최초 BuildPlayer 1734.34초, 동일 코드 캐시 재사용 168.82초·167.75초. 전체 CI의 5분 달성을 의미하지 않는다. 측정 조건은 [측정 기록](measurements.md)을 참고한다.
- EC2 전용 노드의 Jenkins #5 계약 테스트 100/100 통과. 전체 빌드 #6 측정 중이며 자동 트리거는 비활성 상태다.
- 이전 Jenkins 작업 백업: `/var/lib/jenkins/webgl-backups/d205-unity-webgl-20260907-paused.tar.gz`.
  복원 시 빌드 번호를 마지막 게시 순서보다 크게 유지한다.

## 서버 사전 구성

Jenkins 실행 계정에 Docker 실행 권한과 `git-lfs`, `python3`가 필요하다.
라이선스 초기 활성화에는 공식 Unity CLI, `gnome-keyring`, `libsecret-1-0`, `unzip`을 사용했다.
인증은 Unity 공식 OAuth 화면에서 계정 소유자가 진행하고, Personal 약관 동의 후 무료 활성화를 실행한다.

다음 파일은 서버에서만 관리하고 Git이나 빌드 아티팩트에 포함하지 않는다.

| 경로 | 용도 |
| --- | --- |
| `/var/lib/jenkins/.config/unity-webgl/PhotonAppSettings.asset` | 승인된 게임 Photon 연결 설정, Jenkins 전용 읽기 권한 |
| `/var/lib/jenkins/.config/unity-webgl/keyring-password` | 서버에서 생성한 키링 암호, 소유자만 읽기 |
| `/var/lib/jenkins/.local/share/keyrings/` | Unity OAuth 인증의 암호화 저장소 |
| `/var/lib/jenkins/.config/unity3d/Unity/licenses/` | 이 EC2에서 정식 활성화한 Unity 라이선스 |

Docker는 **동일 EC2의 실제 `/etc/machine-id`**와 활성화된 Unity 설정 디렉터리를 사용한다.
다른 PC의 식별자나 라이선스를 복사하거나 XML을 수정하지 않는다. 호스트 교체 시 새 호스트에서 다시 활성화한다.
계정 재인증이 필요하면 키링을 연 세션에서 `unity auth login`을 실행한다.
무료 Personal 활성화의 `--accept-eula`는 계정 소유자의 약관 동의 후에만 사용한다.

## HTTPS 경로 설치

`nginx.conf`를 `/etc/nginx/snippets/d205-webgl.conf`로 설치한다.
기존 HTTPS server 블록에는 `include /etc/nginx/snippets/d205-webgl*.conf;`를 추가한다.
추가 전 원본을 백업하고 `nginx -t` 성공 후 reload한다. API와 `/jenkins/` 프록시는 유지한다.
`/var/www/d205-webgl`은 Jenkins가 쓰고 nginx가 읽을 수 있어야 한다(디렉터리 755, 공개 파일 644).
인증 파일은 이 공개 디렉터리에 두지 않는다.

WebGL은 gzip으로 빌드하며 nginx가 `.wasm.gz`, `.js.gz`, `.data.gz`에 올바른 MIME과
`Content-Encoding: gzip`을 설정한다. 동일 HTTPS origin의 기존 API를 사용하므로 배포용 CORS 완화가 필요 없다.

## 버전과 롤백

- 빌드마다 전체 Git SHA를 `Application.version`과 `version.txt`에 넣는다.
- `/play/releases/<sha>/`는 덮어쓰지 않는다. 최신 포인터 `current.json`만 원자적으로 교체한다.
- 고정 주소와 포인터는 캐시를 재검증/차단하고 버전별 파일은 immutable 캐시를 사용한다.
- 실행 중인 게임은 자신의 버전 URL에 남는다. 새 버전 알림을 선택하고 확인한 경우에만 게임을 나간다.
  브라우저 전체 화면에서는 HTML 알림이 가려질 수 있으며, 전체 화면을 나오면 확인할 수 있다.
- WebGL Photon AppVersion은 `web-<sha>`다. 서로 다른 웹 빌드와 기존 네이티브 빌드의 방은 분리된다.
  Windows/전용 서버와 웹의 교차 플레이를 도입하려면 양쪽의 호환 버전 정책을 함께 맞춰야 한다.
- 배포 잠금과 Jenkins 빌드 번호로 오래된 작업의 덮어쓰기를 차단한다. 같은 작업의 빌드 번호를 초기화하지 않는다.

```sh
# 서버에서 성공한 빌드 배포
python3 Tools/webgl/publish.py /var/www/d205-webgl FULL_COMMIT_SHA \
  --source Builds/WebGL --sequence JENKINS_BUILD_NUMBER

# 이미 배포했던 버전으로 신규 접속만 롤백
python3 Tools/webgl/publish.py /var/www/d205-webgl PREVIOUS_FULL_COMMIT_SHA

# Linux에서 배포 순서, 잘못된 입력, 실패 보존, 롤백 검증
python3 Tools/webgl/test_publish.py
```

이전 release는 자동 삭제하지 않는다. 플레이 중인 버전과 롤백 후보를 확인한 뒤 별도로 정리한다.
롤백 시 이미 열린 경기는 유지하고 신규 접속만 이전 버전으로 보낸다.

## 검증 범위와 제한

- 로컬 Unity 6000.3.22f1 WebGL 빌드 성공, 브라우저 홈 표시·비공개 방 생성·코드 입장 확인.
- 동일 PC의 브라우저에서 4명 연결 및 호스트 시작 카운트다운 확인. 6인 전체 경기·음성 검증은 미완료.
- EC2 계정 OAuth 및 무료 Personal 활성화 성공. 동일 EC2 Docker에서 Unity 빈 프로젝트 생성·정상 종료 확인.
- EC2 Jenkins #11 develop 빌드·HTTPS 게시 성공. 공개 이전 버전 롤백과 현재 버전 복원 검증 통과.
- 롤백 및 기존 버전 재선택 시에도 version.txt와 필수 빌드 파일의 존재·비어 있지 않음을 검사한다.
- 최신 완료 증빙과 미완료 범위는 [마무리 검증 기록](acceptance-2026-09-08.md)을 참고한다.
- 브라우저 Host/Client는 Photon 권장 토폴로지가 아니다. Host 탭이 백그라운드로 가면 경기 지연/연결 끊김이 발생할 수 있다.
- 여러 내장 브라우저를 동시에 제어하는 검증 중 Chromium PointerLock UnknownError가 관측됐다. 6인 안정성 검증 성공으로 간주하지 않는다.
- WebRTC 음성 DSP는 WebGL에서 지원되지 않는다는 Photon 경고가 있다. 실제 마이크 송수신은 별도 검증 대상이다.

참고: [Photon 토폴로지](https://doc.photonengine.com/fusion/v2/fusion-choose),
[Unity Web 배포](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-deploying.html),
[Unity Personal 자격 조건](https://unity.com/products/unity-personal),
[Unity 라이선스](https://unity.com/pages/license-compliance).

Jira: S15P21D205-875~878, Story 874 / Epic 413. 전체 하나의 MR로 제출한다.


## 복구용 기준 버전

`webgl-build-baseline-20260908` 태그와 Jenkins #8·#10 산출물을 보존한다. 소스·산출물·Jenkins 설정 복구 범위는 [복구 절차](recovery.md)를 참고한다.
