# 실제 경기 자동 성능 측정

원본이 아닌 **독립된 Unity 프로젝트 복사본 두 개**에서 사용한다. Assets/Library/ProjectSettings를 원본과 링크하거나 공유하지 않는다. Unity 버전, 소스 커밋, 품질 설정을 맞춘다.

1. 두 복사본의 `Assets/Editor`에 `ClientPerformanceAuto.cs`를 복사하고 각 프로젝트 루트에 빈 `.performance-lab` 파일을 만든다. 원본에는 설치하지 않는다.
2. 동일한 `PERF_REVISION`, 6자리 `PERF_TEST_CODE`(기본 PER994), 새 절대 경로 `PERF_OUTPUT`을 두 프로세스의 환경 변수로 전달한다. 기존 결과 디렉터리를 재사용하지 않는다.
3. 서버는 Unity의 `-batchmode -nographics -projectPath <서버 복사본> -executeMethod ClientPerformanceAuto.Server -logFile <서버 로그>`로 시작한다.
4. 클라이언트는 `-projectPath <클라이언트 복사본> -executeMethod ClientPerformanceAuto.Client -logFile <클라이언트 로그>`로 시작한다. 실제 렌더링이 필요하므로 클라이언트에 batchmode/nographics를 넣지 않는다.

실행기는 기존 개발 서버 경로로 방을 만들고 마트에 입장한다. 서버가 출발 위치를 한 번 준비한 후 클라이언트는 일반 Fusion/KCC 입력으로 작은 동선을 반복한다. 10초 준비 후 30초 구간 3회를 기록하고 마지막 30초는 CPU Profiler를 켜 별도로 기록한다. 마지막 구간을 일반 FPS 평균에 합치지 않는다. 테스트 장면 진입/기존 게임의 인증 경로를 사용하므로 네트워크 연결이 필요하다.

`environment.json`, `trial-0.csv`부터 `trial-3.csv`, `cpu.raw`, `game.png`, 완료 시 `client-done.txt`가 나온다. `SummarizePerformance.ps1 -Directory <결과 경로>`로 평균 FPS와 p95/p99를 읽는다. 완료 파일이 없거나 로그에 오류가 있으면 통과로 판정하지 않는다. 두 에디터는 정상 완료 후 종료된다. 시작 중 스크립트 재컴파일로 Play가 종료되면 준비 후 재시도한다.

실행기는 검증 복사본의 제품명/버전, Play 상태와 Game 창을 변경한다. 테스트 종료 후 복사본의 설정을 원본에 복사하지 않는다. 소스 변경 전후 비교에는 같은 해상도·주사율·전원·품질·서버 부하를 사용하고, 측정 중 에디터 조작과 빌드를 피한다. CSV는 프레임 간격이며 GPU 처리 시간이나 화면 출력률 자체가 아니다.

이 자동 이동은 반복 가능한 탐색 기준선이다. 6인 전투, 모든 맵 구역, 물건 상호작용, 하이라이트 스킵, 재경기의 검증을 대신하지 않는다. 해당 시나리오는 별도 검사로 확장하며 단일 PC 결과를 실제 6인 성능으로 보고하지 않는다.