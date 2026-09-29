# Photon Cloud 재접속 예외 수정 (2026-09-14)

## 확인한 원인
현재 Fusion Runtime은2.1.2.2279. 사용자 Editor.log에서 Host 시작 성공(reusedMatchmaking=True), Supermarket 로딩9.810초, Photon32758(Game does not exist), MoveSessionToNewRoom/RejoinSession 예외가 순서대로 나타났다.
설치된 DLL을 읽기 전용으로 확인한 결과, 예외 오프셋0xCC는 RejoinMetadata.AppSettings.FixedRegion 대입이다. AppSettings가 null인 것이 직접 원인이다.
기존77b6203b의 방 목록 연결 재사용 경로는 이미 Master에 접속된 RealtimeClient를 StartGame에 전달했다. SDK는 이미 연결됐으면 일찍 반환하여 복구용 AppSettings 저장을 생략한다. 정상 입장은 가능하지만 이후 새 방 복구에서 빈 설정을 사용한다.
방이 처음 사라진 원인까지 확정한 것은 아니다. 긴 로딩과 시점이 겹쳤다는 것만으로 네트워크 타임아웃의 원인이라고 단정하지 않는다.

## 수정
- 방 목록 연결은 콜백·서비스를 해제하고 끊는다. 게임용 Runner에 재사용하지 않는다.
- Runner는 기존 인증값, 지역/앱 버전 설정으로 자체 연결을 시작해 복구 설정을 초기화한다.
- 방 생성/참가, 비공개 여부, 암호, 인원 제한, 연결 취소 조건은 유지한다.
- Quick Rejoin / 새 방 자동 복구를 끄거나 SDK DLL을 수정하지 않는다.
- CloudConnectionLost 콜백에 이유·복구 여부·호스트 여부·씬 로딩 상태를 기록한다. Runner 해제 시 콜백도 제거하며 인증정보는 로그에 남기지 않는다.
- 최초 입장에는 재사용하던 인증/연결 절차가 추가된다. 입장 속도 개선을 위해 복구 상태를 희생하지 않는다.

## 검증
진단 복사본에서 EditMode117/117통과: 방 시작·연결 토큰·퇴장·호스트 이전 등 기존 관련 검사, 신규 Host/Client/Server 인자 계약3개, SDK 메타데이터 대조1개.
SDK 대조는 Photon에 실제 접속하지 않고 이미 연결된 클라이언트 상태와 취소된 신규 연결을 비교했다. 재사용 경로 savedSettings=False, 신규 경로 savedSettings=True로 초기화 누락을 재현했다. SDK 내부 반영 검사는 진단 폴더에만 보관하며, 저장소에는 서비스의 공개 시작 인자 계약 검사를 남겼다.
실제 네트워크 단절 후 새 방으로 복구하는 종단 테스트와 WebGL 플레이어 빌드는 실행하지 않았다. 정상 게임 진행만으로 복구 성공을 판정하지 않는다.
