# Server

Photon 연결, 방과 로비, 네트워크 권한, 상태 동기화, 경기 진행을 둡니다.

이 폴더의 Server는 별도 Spring 서버가 아니라 Unity 안의 Photon 네트워크 영역을 의미합니다. 네트워크 상태의 원본은 `NetworkBehaviour`와 `[Networked]` 프로퍼티로 관리합니다.
