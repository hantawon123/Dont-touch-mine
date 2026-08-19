# SOAP

ScriptableObject Architecture Pattern의 공통 타입을 둡니다.

- `Definitions`: 아이템, 맵 등 정적 데이터 정의
- `Config`: 게임 시간, 최대 인원 등 설정
- `Events`: UI, 사운드 등 로컬 시스템에 전달할 이벤트 채널

현재 점수, 플레이어 위치, 방 번호, 아이템 발견 상태 같은 네트워크 런타임 상태는 ScriptableObject에 저장하지 않습니다. 실제 타입이 필요할 때 하위 폴더를 추가합니다.
