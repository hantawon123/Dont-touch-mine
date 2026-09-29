# 리플레이 직렬화 사전 측정 (S15P21D205-574)

실제 저장소의 직렬화·프레임 타입 소스를 링크해 합성 데이터를 측정한다. 벤치마크 전용 직렬화 구현이나 외부 패키지는 추가하지 않는다. 게임 코드의 동작은 변경하지 않는다.

## 실행

필요 환경: .NET SDK 10, Unity 6000.3.22f1, Unity가 컴파일한 `Library/ScriptAssemblies/Game.SOAP.dll` (후보 생성의 상수 참조).

저장소 루트에서 PowerShell로 실행한다.

```powershell
dotnet build Tools/performance/replay/ReplayBaseline.csproj -c Release
$env:DOTNET_TieredCompilation = '0'
dotnet Tools/performance/replay/bin/Release/net10.0/ReplayBaseline.dll > replay-result.json
Remove-Item Env:DOTNET_TieredCompilation
```

Unity 설치 위치가 다르면 build에 `-p:UnityEditor="설치경로/Editor"`를 전달한다. 소스 변경 후에는 반드시 다시 build한다. 런타임·운영체제·반복 수는 출력에 포함된다. 비교 결과에는 커밋과 링크한 소스·Game.SOAP.dll의 SHA256도 함께 보관한다.

## 측정 조건

- 고정 시드574, 플레이어 데이터6개, 클립3개, 클립당10초/101프레임(현재 기록 주기0.1초 기준).
- 프레임별 물건6/32/64개. 모든 물건 위치가 변하는 합성 입력이며 실맵 물건 수·실제 움직임 분포를 뜻하지 않는다. 압축률도 이 입력에 한정한다.
- 조건별20회 워밍업 후 압축·복원 각각100회씩3세트.
- nearest-rank p50/p95, 현재 스레드 managed 할당량/호출, 압축 전후 크기 출력. 데이터 생성은 측정 밖에서 수행한다.
- 각 측정 묶음 직전에 명시적 GC를 수행한다. 측정 중 발생한 GC 지연은 포함된다.
- 실제 압축→복원→재직렬화 byte 일치와 잘못된 압축 입력 거절을 확인하며 실패하면 종료 코드가0이 아니다.

## 해석 제한

이 도구의 .NET 10 시간·할당량은 Unity Mono/IL2CPP, Fusion Tick, 네트워크 송수신, GPU·FPS 측정값이 아니다. GZip의 native 할당도 managed 할당 수치에 포함되지 않는다. 실제6인 검증은 별도로 수행한다. 자동 성능 임계값으로 CI를 실패시키지 않는다.

2026-09-07 첫 실행으로 빌드·왕복 검사를 확인한 뒤 tiered compilation을 끈 별도 실행을 기준 파일로 기록했다. Unity 에디터가 열린 일반 개발 PC에서 측정했으므로 백그라운드 부하·GC에 따른 편차가 있다. 동일 환경·입력·런타임으로 재측정해야 한다.

결과와 남은 검증은 [사전 측정 기록](../../../docs/performance/baseline-2026-09-07.md)을 참고한다.
