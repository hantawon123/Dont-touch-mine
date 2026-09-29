# WebGL 빌드 측정 — 2026-09-07

## 로컬 반복 실행

Core Ultra 9 185H(16코어, 22논리 CPU), RAM 약64GB, Windows, Unity 6000.3.22f1.
WebGL, IL2CPP OptimizeSize, gzip. Web Code Optimization은 기존 설정을 사용했다.
버전 문자열은 세 번 모두 `ecfcc4d25bb3ad760dc154e60fe3419a9f7ae590`으로 고정했다.
최신 develop 통합 후 작업 중인 소스에서 측정했으며, 이후 커밋한 CI 변경이 포함된다.
따라서 이 SHA만 체크아웃한 배포 산출물의 증명으로 사용하면 안 된다.
기존 로컬 그래픽 설정도 보존한 상태이며 EC2와 동일 조건이 아니다.

| 실행 | 조건 | BuildPlayer | 결과 |
| --- | --- | ---: | --- |
| 1 | 기존 Library에서 최신 develop 및 프로필 반영 후 첫 실행 | 1734.34초 (28분54초) | 성공 |
| 2 | 동일 게임 코드·설정, 캐시 재사용 | 168.82초 (2분49초) | 성공 |
| 3 | 동일 게임 코드·설정, 캐시 재사용 | 167.75초 (2분48초) | 성공 |

위 시간은 Unity BuildReport.summary.totalTime이다. 프로세스 시작·초기 import·체크아웃·테스트·전송·큐 대기를 포함하지 않는다.
실행1은 캐시를 삭제한 완전한 clean build가 아니다. 두 번의 warm 실행만으로 모든 변경의 5분 완료를 보장하지 않는다.
같은 조건에서 런타임 C# 로그 한 줄 추가 시 380.78초(6분21초)에 성공했다.
측정용 코드는 원본으로 복원했으며 MR 및 공개 배포에 포함하지 않는다.
이후 FAST_BUILD에 WebAssembly BuildTimes를 명시한 첫 실행도 387.79초(6분28초)에 성공했다.
해당 실행은 임시 로그 원복과 프로필 설정 반영을 포함하므로 설정 하나만 바꾼 엄밀한 대조군은 아니다.
실제 보고서의 설정은 OptimizeSize/BuildTimes이며, 5분 달성이나 옵션 추가에 따른 단축은 확인되지 않았다.
기존/명시 후 wasm-opt 명령 모두 `-O2`가 관측됐다. 옵션의 명시 자체를 새로운 속도 개선으로 계산하지 않는다.
빌드 데이터 로그는 압축450.2MB/비압축1.44GB였다. 이는 전체 웹 배포 파일 크기와 별도 지표다.
UI 변경 및 프로필 간 런타임 성능 비교는 아직 측정하지 않았다.

첫 실행의 URP/Lit ForwardLit 셰이더 단계는 1219.52초였고 7584개 변형을 컴파일했다(로컬 캐시 적중96개).
현재 근거로는 Library/ShaderCache 보존이 우선이며, 사용하는 셰이더를 확인하지 않고 변형을 제거하면 안 된다.
BuildReport.steps는 중첩되어 있으므로 단계 시간을 모두 합산하지 않는다.

## EC2 Jenkins

- `unity-webgl` EXCLUSIVE 노드 1슬롯, 기존 내장 노드 2슬롯 유지.
- 기존 EC2를 공유한다. Unity Docker CPU 상한3, CPU shares256, 메모리 상한8GB, swap 없음.
- #4는 Git LFS 인증 연결 누락으로 Unity 실행 전에 실패했다. 기존 Jenkins Git 자격증명 바인딩으로 수정했다.
- #5는 계약 테스트100/100 및 배포 스크립트 검사 성공. TEST_ONLY이므로 플레이어 빌드·공개 배포는 수행하지 않았다.
- #6은 기본 배포 프로필의 전체 빌드 측정 중이다. 자동 트리거는 꺼 두었다.
- #6 초반 복원1초, Unity 테스트 프로세스61초. 같은 시점 Metabase가 CPU 약195%를 사용했다. 슬롯 분리와 CPU 자원 분리는 다르다.
- #6 진행 중 백엔드 develop #83(13:43:56 UTC,5.6초), MR-221 #10(13:46:11 UTC,82.9초) SUCCESS. Unity가 기존 실행 슬롯을 점유하지 않는 것을 확인했다.
- GitLab 웹훅 API는 현재 사용자 권한에서403을 반환한다. Maintainer 권한 또는 현재 폴링 유지 여부를 확인 중이다. Git notify 전용 토큰은 Jenkins 전용 파일에만 저장했다.

## 남은 확인

EC2 전체/반복 빌드, 실제 변경 유형별 시간, 백엔드 동시 실행 지연, 웹훅과 MR 상태 연결,
새 산출물 HTTPS 플레이와 실제 버전 롤백이 남아 있다.
현재 `/play/`의 성공 응답은 이전 게시 버전의 접근 복구이며 이번 변경의 배포 검증이 아니다.
MR은 검증이 끝날 때까지 Draft로 유지한다.
# 미사용 렌더링 기능 검증 (2026-09-08)

- PC URP의 LOD Cross Fade와 Light Cookies 지원만 비활성화. 소스·씬·프리팹·애니메이션 3,136개 검사에서 LOD Fade Mode 34개는 모두 None, Light Cookie 72개는 모두 null이며 관련 오버라이드·런타임 할당 검색 결과 없음.
- Unity 6000.3.22f1, 로컬 Windows, OptimizeSize/BuildTimes, 캐시 유지. 별도 `Builds/WebGLVisualCheck` 출력으로 기존 산출물 보존.
- BuildReport: Succeeded, 307.82초(5분 8초). Unity 빌드 단계 시간이며 EC2 CI 전체 시간이나 cold build 성능을 의미하지 않는다. 이전 실행과 출력 경로·품질 설정 등이 달라 단일 변수 속도 비교로 해석하지 않는다.
- URP/Lit ForwardLit: 스트리핑 후 1,280개, 모두 로컬 캐시 적중, 컴파일 0개, 해당 패스 처리 1.31초. 서버 #7의 5,120개와는 실행 환경이 다르므로 전체 시간 단축률로 환산하지 않는다.
- 기존/변경 WebGL 홈 화면을 같은 브라우저 크기에서 육안 비교하여 차이 없음. 전체 인게임 씬·조명·이펙트의 동일성 및 Windows 네이티브 빌드와의 동일성은 아직 검증하지 않았다. 병합 전 확인 필요.


# EC2 동일 버전 WebGL 재빌드 측정

커밋: f6d67c9cc85e854519f2ddb7017dd7abc1831079
Unity 6000.3.22f1, IL2CPP OptimizeSpeed / WebAssembly BuildTimes.
FAST_BUILD=false, FORCE_BUILD=true, TEST_ONLY=false. 동일 EC2, CPU 상한 3, shares 1024, 메모리 8GB. 캐시 유지.

| 구간 | #8 | #10 |
|---|---:|---:|
| Jenkins 전체 | 3866.263초 | 434.919초 |
| 스크립트 전체 | 3830초 | 394초 |
| 복원 | 1초 | 2초 |
| 계약 테스트 | 43초 | 41초 |
| Unity 프로세스 포함 빌드 | 3786초 | 350초 |
| Unity BuildReport | 3751.778164초 | 316.72384초 |
| 에셋 기록 | 1574.263186초 | 24.205805초 |
| 압축 패키지 생성 | 224.98708초 | 219.357109초 |
| 플레이어 후처리 | 1925.626853초 | 49.490904초 |

두 빌드 모두 SUCCESS. Jenkins 전체 시간 약 88.75% 감소, 약 8.89배 빨라짐.
#10 Lit ForwardLit: 1280개 모두 로컬 캐시 적중, 컴파일 0개, 패스 처리 0.37초.
압축 패키지 생성이 #10 Unity 빌드 시간의 약 69.3%이며 거의 그대로 남음. 이 단계 전체를 HTTP gzip 시간으로 단정하지 않는다.

#9는 SHA를 브랜치로 해석한 경량 체크아웃 오류로 Unity 시작 전에 실패했으며 비교에서 제외.
#10은 정확한 SHA 체크아웃을 위해 경량 체크아웃을 일시 해제했다. 실제 작업 checkout 확인 후 원래 브랜치/경량 체크아웃 설정 복구, 자동 트리거 비활성 유지.
따라서 Jenkins 준비 단계 조건에는 차이가 있으며 Unity BuildReport와 스크립트 시간을 별도로 제시한다.

동일 코드/설정의 캐시 재사용 1회 결과이며 코드·에셋 변경, clean build, 모든 반복 실행의 시간을 보장하지 않는다.
서버 측 증빙: /var/lib/jenkins/webgl-measurements/8 및 /var/lib/jenkins/webgl-measurements/10.

- https://j15d205.p.ssafy.io/jenkins/job/d205-unity-webgl/8/
- https://j15d205.p.ssafy.io/jenkins/job/d205-unity-webgl/10/


## 최신 develop 통합 검증 — 2026-09-08

원격 develop `ed8330b1`을 `60d10c5f`에서 병합했다. 로비 조명/라이트맵 재베이크, Carryable 소품, 미사용 에셋 정리 및 Metabase 연결 수정을 포함한다. 새 그림자 아틀라스 4096과 기존 WebGL 최적화 두 옵션을 함께 유지한다.

통합 상태에서 Unity NetworkContractTests 100/100, 변경 범위 판정 3개 검사, 빌드 스크립트 캐시·실패 처리 검사를 통과했다. LOD Fade None 34개, Light Cookie null 70개 및 관련 오버라이드 없음 재확인.

#8·#10 시간은 통합 전 `f6d67c9c`의 기록이다. 새 조명/에셋이 포함된 통합 버전의 WebGL 전체 빌드와 인게임 화면 검증은 아직 실행하지 않았다. 복구 태그는 통합 전 기준을 그대로 보존한다.
