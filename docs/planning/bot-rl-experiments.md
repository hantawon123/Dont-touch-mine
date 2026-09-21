# 봇 강화학습 실험 기록

## 2026-09-21 — Reach 3개 학습 시드 및 Windows 실행 검증

상태: P2의 에디터·Windows·WebGL 목표 도달 실행 검증 완료. 장시간 안정성·메모리 회귀 및 실제 경기 검증과는 구분한다.

### 과제와 조건

- 목적: 우리 캐릭터의 단순 목표 도달 학습부터 ONNX 실행 파일 추론까지 연결 확인.
- 씬: `Assets/_Game/Content/Training/Training_Reach.unity`
- 관찰: 목표의 상대 X/Z 위치를 5로 나눈 숫자 2개. 계획의 초기 예시 4개와 달리 자기 속도는 포함하지 않는다.
- 행동: X/Z 이동 연속값 2개. `TrainingMovement`와 CharacterController 사용. 제품의 Fusion KCC 이동과 다르다.
- 설정: `Tools/Training/configs/reach_ppo.yaml`, PPO, max_steps 100000.
- 학습 실행 ID: `reach-seed101-v1`, `reach-seed202-v1`, `reach-seed303-v1`. 각 configuration.yaml에서 학습 시드 확인.
- 평가: 고정 시드 20260915, 100회, Inference Only, Deterministic Inference.
- 평균 시간은 ReachAgent의 Time.time으로 계산한 성공 에피소드의 게임 시간이다.

### 결과

| 학습 시드 | 에디터 성공 횟수 | 평균 성공 시간 |
|---|---:|---:|
| 101 | 100/100 | 1.46초 |
| 202 | 100/100 | 1.54초 |
| 303 | 100/100 | 1.47초 |

에디터 결과는 사용자 모델 교체 순서와 Editor.log에 근거한다. 평가 로그 자체에는 모델 ID가 없어 과거 실행의 모델 해시까지 추적할 수는 없다.

Windows: 2026-09-21 16:17:37 빌드 성공. Player.log에서 공통 초기화 생략과 100/100 성공, 평균 1.47초 확인. 현재 저장된 씬 모델 GUID는 Reach_Seed303_v1.onnx와 일치한다.

### 빌드 문제와 처리

1. Google.Protobuf DLL 참조 실패: ML-Agents DLL 재가져오기 및 에디터 재시작 후 다음 빌드에서 해당 컴파일 오류가 사라짐.
2. 자동차 셰이더의 ColorMaskInput DOTS 선언 오류: subGraph_ColorMasking의 EmissionIntensity, ColorMaskInput, Color 입력에서 강제 HybridPerInstance 선언을 해제. 이후 빌드 성공. 자동차 외형 회귀 확인은 별도로 남음.
3. 게임 공통 초기화: 학습 프로필의 GAME_TRAINING 정의와 ProjectLifetimeScope.Configure 조건부 컴파일로 서비스 등록을 생략. 이것만으로 모든 공통 리소스가 빌드에서 제외되는 것은 아님.

### WebGL 평가와 패키지 수정

- 사용자 제공 브라우저 콘솔 화면: Episodes 100/100, Success 100, 성공률 100%, 평균 성공 시간 1.47초. 수정 후 끝까지 평가된 것을 확인했다. 화면에 이전 오류는 보이지 않으나 전체 콘솔·장시간 메모리 측정까지 확인한 것은 아니다.
- 수정 전 오류: `memory access out of bounds`, `TensorProxy_Finalize` 호출 경로에서 중단.
- ML-Agents 4.1.0을 `Packages/com.unity.ml-agents`에 임베드하고 `Runtime/Inference/TensorProxy.cs`의 `Dispose()`에 `data`와 `data.dataOnBackend` null 검사를 추가했다. 기존 CPU 백엔드 처리 조건은 유지했다.
- 현재 로컬 소스에서 수정 적용 확인. 패키지 업데이트 시 이 변경을 비교·재검증해야 한다. 단일 평가 성공만으로 모든 메모리 누수가 해결됐다고 판단하지 않는다.
- 다음 단계는 계획 P3의 실제 입력·KCC 연결이다. 기존 Reach 씬과 모델은 회귀 확인용으로 보존한다.

### 남은 작업

- WebGL 반복 실행·장시간 메모리 회귀 확인. 최종 평가에는 별도 미사용 배치 사용.
- 자동차 셰이더 변경 후 실제 게임 외형 확인.
- 학습 관련 변경과 그래픽·클라우드 설정 변경을 구분해 커밋 준비. 전체 파일 일괄 커밋하지 않음.
- 이후 P3: 실제 입력 → NetworkPlayerMotor → Fusion KCC 흐름을 읽고, 호스트가 제어하는 최소 봇 1개의 이동을 연결.

한계: 평면 목표 도달만 검증했다. 시야 제한, 장애물 탐색, 물건 상호작용, 실제 네트워크 경기 성능은 미검증이다. 같은 평가 배치를 반복 사용했으므로 최종 출시 시험에는 별도의 미사용 배치를 준비한다.
