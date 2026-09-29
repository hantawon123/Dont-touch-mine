> 서버 전환 이후의 복구는 [서버 릴리스 호스트](../network/server-flow/release-host.md)의 같은 서버/WebGL 쌍을 선택하는 절차를 사용한다. 아래는 이전 WebGL 단독 배포 기준의 보존 기록이다.

# WebGL 복구 기준 — 2026-09-08

## 보존 버전

- Git 태그: `webgl-build-baseline-20260908`
- 소스: `f6d67c9cc85e854519f2ddb7017dd7abc1831079`
- Jenkins `d205-unity-webgl` #8, #10: SUCCESS. 두 빌드는 Keep forever로 보존하며 `Builds/WebGL` 산출물과 로그를 포함한다.
- 설정: Unity 6000.3.22f1, OptimizeSpeed / BuildTimes, FAST_BUILD=false, CPU 상한 3, shares 1024, 메모리 8GB.
- #10 동일 버전 재빌드: Jenkins 434.919초, Unity BuildReport 316.72384초. 전체 인게임 화면 동일성 검증을 완료한 릴리스라는 의미는 아니다.

## 소스 복구

기존 체크아웃의 미커밋 변경을 건드리지 않고 별도 작업 디렉터리에서 기준 버전을 연다.

```sh
git fetch origin tag webgl-build-baseline-20260908
git worktree add --detach ../webgl-build-baseline webgl-build-baseline-20260908
```

해당 worktree에는 별도 Library가 만들어지므로 최초 빌드에서 기존 캐시 속도를 기대하지 않는다. 라이선스와 Photon 설정은 기존 비밀 설정 경로에서 제공하며 Git 태그에 포함하지 않는다.

## 산출물 복구

[Jenkins #10](https://j15d205.p.ssafy.io/jenkins/job/d205-unity-webgl/10/)의 Artifacts에서 `Builds/WebGL` 전체를 다운로드하고, `version.txt`가 위 소스 SHA와 일치하는지 확인한다. 개별 wasm 파일만 교체하지 않는다.

이 버전은 기능 브랜치 검증 산출물이며 현재 공개 사이트의 배포 버전과 다르다. 보존 작업은 공개 포인터를 바꾸지 않는다. 실제 배포 시 기존 `publish.py`의 버전 검증·원자적 게시 절차를 사용한다. 아직 게시하지 않은 버전에 `--source` 없는 롤백 명령을 사용할 수는 없다.

## 재빌드 설정

기준 SHA를 Jenkins Branch Specifier에 직접 지정한다면 경량 체크아웃(Lightweight checkout)을 해제해야 한다. 이 환경에서는 경량 체크아웃이 SHA를 브랜치명으로 해석하여 #9가 Unity 실행 전에 실패했다. FORCE_BUILD=true, TEST_ONLY=false, FAST_BUILD=false로 실행한 뒤 원래 브랜치와 경량 체크아웃 설정을 복구한다.

현재 작업 설정은 `*/feature/server/webgl-delivery`, 경량 체크아웃 활성, 자동 실행 비활성이다. Jenkins 작업 설정 자체는 Git 태그 복구만으로 변경되지 않는다.

서버 측 추가 측정 증빙은 `/var/lib/jenkins/webgl-measurements/8`, `/var/lib/jenkins/webgl-measurements/10`에 보존한다.
