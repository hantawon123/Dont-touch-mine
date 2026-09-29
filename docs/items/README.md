# 아이템 분류 및 크기 검증

캐릭터 실제 메시 크기(X/Y/Z): **0.427 × 1.085 × 0.393 m**.
아이템 상한(X/Y/Z): **0.427 × 1.085 × 0.393 m**.

각 축 상한을 모두 만족하는 하나의 배율을 적용했다. 원본의 종횡비·형태를 유지하며 작은 아이템은 키우지 않았다. 원본 에셋 대신 분류 폴더의 게임용 프리팹을 사용한다.

전시 항목: 318개 (색상·형태·성장 변형 및 보류품 포함). 축소: 129개.

**총기·폭발물은 현재 16개로 최소 20개 조건에 4개 부족하다.** 부품을 총기로 세지 않았다. 공구는 기존 창고 팩의 전동드릴·수평계·바이스·십자렌치·나사·사다리·가스통·작업용 콘으로 범위를 보충했다.

## 사용 방법

- `Assets/_Game/Content/Scenes/ItemGallery.unity`를 연다. Play 없이 Scene 뷰에서도 카테고리별로 확인 가능하다.
- Play: 상단 카테고리 버튼 또는 숫자 1~6. 우클릭을 누른 채 WASD 이동, Q/E 상승·하강, Shift 가속, Home 시점 복귀. F 개별 물건 근접 보기, 좌우 화살표 이전·다음 물건. Game 뷰를 클릭해 포커스를 준 뒤 조작한다.
- 씬마다 현재 캐릭터 1배 크기 참조가 있으며 모든 전시 물건은 조정된 게임용 프리팹이다.
- `docs/items/index.html`: 카테고리 필터, 검색, 이미지, 크기, 배율, 프리팹 경로. CSV는 Excel에서 열 수 있다.
- 재생성: Unity 메뉴 `Tools > Game > Items > Build Categorized Item Gallery`. 이후 프로젝트 루트에서 `python Tools/items/build_item_report.py`.

## 범위

게임 배정은 Resources/Items/ItemCatalog.asset의 활성 카테고리·아이템에 연결되어 있다. 새 프리팹에는 CarryableItem, Rigidbody, BoxCollider가 포함되어 있다. 추가·수정 방법은 [게임 카탈로그 관리](게임_카탈로그_관리.md)를 참고한다.

패키지의 중복 GUID 충돌을 피하기 위해 ItemSources 아래 패키지별로 GUID를 분리했다. 필요한 메시·프리팹·재질·텍스처 의존성만 가져왔고 데모 실행 스크립트는 가져오지 않았다. 캐릭터 크기는 PlayerCharacter.prefab의 실제 메시 경계로 측정했다.

## 카테고리별 항목 수

| 카테고리 | 전시 항목 수 |
|---|---:|
| 음식·음료 | 59 |
| 생활용품 | 122 |
| 공구·작업용품 | 31 |
| 총기·폭발물 | 16 |
| 판타지 소품 | 56 |
| 보류·부품·모형 | 34 |

전시 항목 수는 고유 종류 수가 아니다. 색상·성장 변형은 아래 비고와 이미지로 구분한다.

## 음식·음료

| 아이템 | 조정 크기 XYZ(m) | 배율 | 비고 |
|---|---|---:|---|
| 아보카도 001 | 0.098 × 0.161 × 0.098 | 1.0000 |  |
| 아보카도 003 | 0.097 × 0.071 × 0.140 | 1.0000 |  |
| 바 001 | 0.107 × 0.018 × 0.041 | 1.0000 |  |
| 햄버거 001 | 0.140 × 0.089 × 0.140 | 1.0000 |  |
| 캔 009 | 0.078 × 0.116 × 0.078 | 1.0000 |  |
| 치즈케이크 001 | 0.088 × 0.120 × 0.144 | 1.0000 |  |
| 고추 002 | 0.079 × 0.034 × 0.222 | 1.0000 |  |
| 감자칩 008 | 0.159 × 0.232 × 0.086 | 1.0000 |  |
| 커피 005 | 0.116 × 0.150 × 0.116 | 1.0000 |  |
| 쿠키 006 | 0.054 × 0.030 × 0.054 | 1.0000 |  |
| 쿠키 011 | 0.054 × 0.030 × 0.054 | 1.0000 |  |
| 쿠키 012 | 0.054 × 0.030 × 0.054 | 1.0000 |  |
| 크루아상 001 | 0.149 × 0.051 × 0.078 | 1.0000 |  |
| 도넛 002 | 0.107 × 0.036 × 0.106 | 1.0000 |  |
| 달걀 001 | 0.063 × 0.046 × 0.063 | 1.0000 |  |
| 달걀 002 | 0.063 × 0.084 × 0.063 | 1.0000 |  |
| 달걀 003 | 0.063 × 0.044 × 0.085 | 1.0000 |  |
| 달걀 004 | 0.161 × 0.021 × 0.154 | 1.0000 |  |
| 생선 004 | 0.075 × 0.038 × 0.145 | 1.0000 |  |
| 아이스크림 001 | 0.072 × 0.162 × 0.072 | 1.0000 |  |
| 아이스크림 003 | 0.066 × 0.184 × 0.025 | 1.0000 |  |
| 버섯 002 | 0.063 × 0.068 × 0.063 | 1.0000 |  |
| 버섯 004 | 0.063 × 0.068 × 0.032 | 1.0000 |  |
| 버섯 006 | 0.064 × 0.007 × 0.071 | 1.0000 |  |
| 양파 001 | 0.078 × 0.151 × 0.078 | 1.0000 |  |
| 양파 003 | 0.078 × 0.046 × 0.149 | 1.0000 |  |
| 페이스트리 002 | 0.042 × 0.069 × 0.094 | 1.0000 |  |
| 피망 003 | 0.091 × 0.122 × 0.091 | 1.0000 |  |
| 샌드위치 001 | 0.129 × 0.046 × 0.142 | 1.0000 |  |
| 샌드위치 002 | 0.142 × 0.023 × 0.148 | 1.0000 |  |
| 샌드위치 003 | 0.130 × 0.017 × 0.142 | 1.0000 |  |
| 소시지 003 | 0.062 × 0.065 × 0.190 | 1.0000 |  |
| 새우 001 | 0.031 × 0.069 × 0.134 | 1.0000 |  |
| 새우 002 | 0.069 × 0.027 × 0.047 | 1.0000 |  |
| 탄산음료 002 | 0.130 × 0.268 × 0.106 | 1.0000 |  |
| 차 003 | 0.128 × 0.077 × 0.127 | 1.0000 |  |
| 수박 001 | 0.200 × 0.207 × 0.200 | 1.0000 |  |
| 수박 002 | 0.200 × 0.107 × 0.200 | 1.0000 |  |
| 수박 003 | 0.110 × 0.098 × 0.030 | 1.0000 |  |
| 요구르트 002 | 0.054 × 0.150 × 0.054 | 1.0000 |  |
| 커피컵 Green | 0.117 × 0.187 × 0.117 | 1.0000 | 색상 변형 (종류 수 중복 제외) |
| 커피컵 Red | 0.155 × 0.247 × 0.155 | 1.0000 | 색상 변형 (종류 수 중복 제외) |
| 음료컵 Blue | 0.155 × 0.334 × 0.155 | 1.0000 | 색상 변형 (종류 수 중복 제외) |
| 음료컵 Green | 0.155 × 0.334 × 0.155 | 1.0000 | 색상 변형 (종류 수 중복 제외) |
| 음료컵 Red | 0.155 × 0.334 × 0.155 | 1.0000 | 색상 변형 (종류 수 중복 제외) |
| 우유팩 Blue | 0.167 × 0.351 × 0.167 | 1.0000 | 색상 변형 (종류 수 중복 제외) |
| 우유팩 Red | 0.167 × 0.351 × 0.167 | 1.0000 | 색상 변형 (종류 수 중복 제외) |
| 사과 | 0.183 × 0.153 × 0.183 | 1.0000 |  |
| 빵 1 | 0.240 × 0.114 × 0.240 | 1.0000 |  |
| 빵 2 | 0.238 × 0.112 × 0.135 | 1.0000 |  |
| 치즈 1 | 0.249 × 0.076 × 0.252 | 1.0000 |  |
| 치즈 2 | 0.101 × 0.076 × 0.119 | 1.0000 |  |
| 고기 1 | 0.277 × 0.052 × 0.286 | 1.0000 |  |
| 고기 2 | 0.194 × 0.058 × 0.243 | 1.0000 |  |
| 오렌지 | 0.155 × 0.140 × 0.155 | 1.0000 |  |
| 호박 | 0.397 × 0.309 × 0.393 | 0.9564 |  |
| 소시지 | 0.106 × 0.104 × 0.245 | 1.0000 |  |
| 토마토 | 0.178 × 0.138 × 0.178 | 1.0000 |  |
| 수박 | 0.262 × 0.240 × 0.265 | 1.0000 |  |
## 생활용품

| 아이템 | 조정 크기 XYZ(m) | 배율 | 비고 |
|---|---|---:|---|
| 병 002 | 0.061 × 0.199 × 0.061 | 1.0000 |  |
| 그릇 001 | 0.196 × 0.087 × 0.196 | 1.0000 |  |
| 컵 003 | 0.127 × 0.050 × 0.127 | 1.0000 |  |
| 포크 001 | 0.040 × 0.024 × 0.197 | 1.0000 |  |
| 유리잔 001 | 0.114 × 0.212 × 0.114 | 1.0000 |  |
| 유리잔 004 | 0.093 × 0.091 × 0.093 | 1.0000 |  |
| 유리잔 005 | 0.101 × 0.118 × 0.101 | 1.0000 |  |
| 유리잔 008 | 0.077 × 0.223 × 0.077 | 1.0000 |  |
| 접시 001 | 0.239 × 0.022 × 0.242 | 1.0000 |  |
| 접시 007 | 0.232 × 0.030 × 0.232 | 1.0000 |  |
| 화분 01 / 성장 1 | 0.393 × 0.395 × 0.393 | 0.6457 | 화분 조합/성장 단계: 서로 다른 식물 종류로 중복 계산하지 않음 |
| 화분 01 / 성장 2 | 0.393 × 0.487 × 0.393 | 0.6457 | 화분 조합/성장 단계: 서로 다른 식물 종류로 중복 계산하지 않음 |
| 화분 01 / 성장 3 | 0.427 × 0.603 × 0.393 | 0.5731 | 화분 조합/성장 단계: 서로 다른 식물 종류로 중복 계산하지 않음 |
| 화분 06 / 성장 1 | 0.393 × 0.493 × 0.393 | 0.6457 | 화분 조합/성장 단계: 서로 다른 식물 종류로 중복 계산하지 않음 |
| 화분 06 / 성장 2 | 0.397 × 0.689 × 0.393 | 0.6457 | 화분 조합/성장 단계: 서로 다른 식물 종류로 중복 계산하지 않음 |
| 화분 06 / 성장 3 | 0.427 × 0.833 × 0.358 | 0.5497 | 화분 조합/성장 단계: 서로 다른 식물 종류로 중복 계산하지 않음 |
| 화분 25 / 성장 1 | 0.393 × 0.465 × 0.393 | 0.6457 | 화분 조합/성장 단계: 서로 다른 식물 종류로 중복 계산하지 않음 |
| 화분 25 / 성장 2 | 0.393 × 0.620 × 0.393 | 0.6457 | 화분 조합/성장 단계: 서로 다른 식물 종류로 중복 계산하지 않음 |
| 화분 25 / 성장 3 | 0.398 × 0.882 × 0.393 | 0.6457 | 화분 조합/성장 단계: 서로 다른 식물 종류로 중복 계산하지 않음 |
| 화분 37 / 성장 1 | 0.393 × 0.368 × 0.393 | 0.6457 | 화분 조합/성장 단계: 서로 다른 식물 종류로 중복 계산하지 않음 |
| 화분 37 / 성장 2 | 0.389 × 0.453 × 0.393 | 0.6384 | 화분 조합/성장 단계: 서로 다른 식물 종류로 중복 계산하지 않음 |
| 화분 37 / 성장 3 | 0.427 × 0.412 × 0.372 | 0.4033 | 화분 조합/성장 단계: 서로 다른 식물 종류로 중복 계산하지 않음 |
| 붕대 | 0.190 × 0.229 × 0.178 | 1.0000 |  |
| 야구방망이 | 0.074 × 0.940 × 0.074 | 1.0000 |  |
| 맥주잔 | 0.199 × 0.236 × 0.160 | 1.0000 |  |
| 쌍안경 | 0.248 × 0.123 × 0.309 | 1.0000 |  |
| 책 Blue | 0.227 × 0.322 × 0.065 | 1.0000 | 색상 변형 (종류 수 중복 제외) |
| 책 Orange | 0.227 × 0.322 × 0.065 | 1.0000 | 색상 변형 (종류 수 중복 제외) |
| 책 Red | 0.227 × 0.322 × 0.065 | 1.0000 | 색상 변형 (종류 수 중복 제외) |
| 책장 | 0.427 × 0.720 × 0.145 | 0.3466 |  |
| 병 1 | 0.144 × 0.390 × 0.166 | 1.0000 |  |
| 병 1Black | 0.144 × 0.390 × 0.166 | 1.0000 | 색상 변형 (종류 수 중복 제외) |
| 병 2 | 0.184 × 0.390 × 0.212 | 1.0000 |  |
| 병 2Purple | 0.184 × 0.390 × 0.212 | 1.0000 | 색상 변형 (종류 수 중복 제외) |
| 병 3 | 0.102 × 0.371 × 0.117 | 1.0000 |  |
| 병 3Blue | 0.102 × 0.371 × 0.117 | 1.0000 | 색상 변형 (종류 수 중복 제외) |
| 병 3Green | 0.102 × 0.371 × 0.117 | 1.0000 | 색상 변형 (종류 수 중복 제외) |
| 그릇 | 0.284 × 0.113 × 0.327 | 1.0000 |  |
| 그릇 001 | 0.284 × 0.113 × 0.327 | 1.0000 |  |
| 서류가방 | 0.427 × 0.382 × 0.099 | 0.6041 |  |
| 서류가방 Grey | 0.427 × 0.382 × 0.099 | 0.6041 | 색상 변형 (종류 수 중복 제외) |
| 식칼 | 0.092 × 0.266 × 0.022 | 1.0000 |  |
| 식칼 Brown | 0.092 × 0.266 × 0.022 | 1.0000 | 색상 변형 (종류 수 중복 제외) |
| 카메라 | 0.210 × 0.167 × 0.139 | 1.0000 |  |
| 의자 | 0.425 × 0.824 × 0.393 | 0.7941 |  |
| 컵 | 0.106 × 0.191 × 0.123 | 1.0000 |  |
| 컵 Brown | 0.106 × 0.191 × 0.123 | 1.0000 | 색상 변형 (종류 수 중복 제외) |
| 컵 Green | 0.106 × 0.191 × 0.123 | 1.0000 | 색상 변형 (종류 수 중복 제외) |
| 컵 Red | 0.106 × 0.191 × 0.123 | 1.0000 | 색상 변형 (종류 수 중복 제외) |
| 책상 | 0.427 × 0.169 × 0.266 | 0.2307 |  |
| 아령 1 | 0.427 × 0.163 × 0.189 | 0.6708 |  |
| 아령 2 | 0.427 × 0.133 × 0.153 | 0.2815 |  |
| 포크 | 0.044 × 0.171 × 0.013 | 1.0000 |  |
| 고블릿 | 0.143 × 0.219 × 0.165 | 1.0000 |  |
| 구급용품 | 0.427 × 0.163 × 0.307 | 0.7315 |  |
| 주방칼 | 0.045 × 0.293 × 0.019 | 1.0000 |  |
| 주방칼 Brown | 0.045 × 0.293 × 0.019 | 1.0000 | 색상 변형 (종류 수 중복 제외) |
| 의학서적 | 0.204 × 0.290 × 0.059 | 1.0000 |  |
| 구급상자 | 0.427 × 0.305 × 0.113 | 0.8244 |  |
| 돈 | 0.263 × 0.097 × 0.143 | 1.0000 |  |
| 머그컵 | 0.148 × 0.124 × 0.127 | 1.0000 |  |
| 머그컵 001 | 0.148 × 0.124 × 0.127 | 1.0000 |  |
| 프라이팬 | 0.427 × 0.051 × 0.271 | 0.4864 |  |
| 프라이팬 2 | 0.427 × 0.088 × 0.369 | 0.5599 |  |
| 연필 | 0.033 × 0.294 × 0.033 | 1.0000 |  |
| 약통 | 0.087 × 0.185 × 0.087 | 1.0000 |  |
| 접시 | 0.340 × 0.025 × 0.393 | 0.9544 |  |
| 접시 001 | 0.340 × 0.025 × 0.393 | 0.9544 |  |
| 뚫어뻥 | 0.182 × 0.483 × 0.182 | 1.0000 |  |
| 냄비 | 0.427 × 0.246 × 0.360 | 0.7449 |  |
| 스마트폰 Blue | 0.129 × 0.265 × 0.032 | 1.0000 | 색상 변형 (종류 수 중복 제외) |
| 스마트폰 Green | 0.129 × 0.265 × 0.032 | 1.0000 | 색상 변형 (종류 수 중복 제외) |
| 스마트폰 Red | 0.129 × 0.265 × 0.032 | 1.0000 | 색상 변형 (종류 수 중복 제외) |
| 스마트폰 White | 0.129 × 0.265 × 0.032 | 1.0000 | 색상 변형 (종류 수 중복 제외) |
| 뒤집개 | 0.098 × 0.318 × 0.025 | 1.0000 |  |
| 숟가락 | 0.052 × 0.161 × 0.015 | 1.0000 |  |
| 주사기 | 0.079 × 0.311 × 0.079 | 1.0000 |  |
| 탁자 | 0.245 × 0.133 × 0.393 | 0.1808 |  |
| 칫솔 Blue | 0.035 × 0.244 × 0.062 | 1.0000 | 색상 변형 (종류 수 중복 제외) |
| 칫솔 Green | 0.035 × 0.244 × 0.062 | 1.0000 | 색상 변형 (종류 수 중복 제외) |
| 칫솔 Red | 0.035 × 0.244 × 0.062 | 1.0000 | 색상 변형 (종류 수 중복 제외) |
| 텔레비전 | 0.427 × 0.301 × 0.073 | 0.2939 |  |
| TV장 | 0.427 × 0.185 × 0.227 | 0.2768 |  |
| 식물 화분 조합 | 0.388 × 0.254 × 0.393 | 0.1483 | 화분 조합/성장 단계: 서로 다른 식물 종류로 중복 계산하지 않음 |
| 선인장 화분 | 0.325 × 1.085 × 0.325 | 0.5625 | 화분 조합/성장 단계: 서로 다른 식물 종류로 중복 계산하지 않음 |
| 인테리어 화분 1 | 0.395 × 0.325 × 0.393 | 0.3465 | 화분 조합/성장 단계: 서로 다른 식물 종류로 중복 계산하지 않음 |
| 인테리어 화분 2 | 0.395 × 0.325 × 0.393 | 0.3465 | 화분 조합/성장 단계: 서로 다른 식물 종류로 중복 계산하지 않음 |
| 인테리어 화분 3 | 0.395 × 0.325 × 0.393 | 0.3465 | 화분 조합/성장 단계: 서로 다른 식물 종류로 중복 계산하지 않음 |
| 인테리어 화분 4 | 0.395 × 0.325 × 0.393 | 0.3465 | 화분 조합/성장 단계: 서로 다른 식물 종류로 중복 계산하지 않음 |
| 인테리어 화분 5 | 0.395 × 0.325 × 0.393 | 0.3465 | 화분 조합/성장 단계: 서로 다른 식물 종류로 중복 계산하지 않음 |
| 인테리어 화분 6 | 0.395 × 0.325 × 0.393 | 0.3465 | 화분 조합/성장 단계: 서로 다른 식물 종류로 중복 계산하지 않음 |
| 인테리어 화분 7 | 0.395 × 0.325 × 0.393 | 0.3465 | 화분 조합/성장 단계: 서로 다른 식물 종류로 중복 계산하지 않음 |
| 테이프 | 0.046 × 0.013 × 0.048 | 1.0000 |  |
| 반창고 | 0.008 × 0.000 × 0.025 | 1.0000 |  |
| 방향제 | 0.035 × 0.064 × 0.036 | 1.0000 |  |
| 붕대 | 0.051 × 0.051 × 0.051 | 1.0000 |  |
| 바구니 | 0.350 × 0.093 × 0.177 | 1.0000 |  |
| 실핀 | 0.005 × 0.001 × 0.048 | 1.0000 |  |
| 병 1 | 0.056 × 0.164 × 0.057 | 1.0000 |  |
| 병 2 | 0.044 × 0.130 × 0.046 | 1.0000 |  |
| 병 3 | 0.069 × 0.164 × 0.045 | 1.0000 |  |
| 빨래집게 | 0.075 × 0.012 × 0.009 | 1.0000 |  |
| 면봉 | 0.005 × 0.005 × 0.075 | 1.0000 |  |
| 헤어롤 | 0.027 × 0.040 × 0.271 | 1.0000 |  |
| 드라이어 | 0.193 × 0.222 × 0.088 | 1.0000 |  |
| 헤어제품 | 0.089 × 0.079 × 0.091 | 1.0000 |  |
| 손거울 | 0.122 × 0.017 × 0.225 | 1.0000 |  |
| 고무장갑 | 0.112 × 0.007 × 0.207 | 1.0000 |  |
| 의료용가위 | 0.059 × 0.005 × 0.156 | 1.0000 |  |
| 손톱깎이 | 0.072 × 0.046 × 0.008 | 1.0000 |  |
| 면도기 | 0.101 × 0.022 × 0.036 | 1.0000 |  |
| 고무오리 | 0.107 × 0.096 × 0.071 | 1.0000 |  |
| 비누 | 0.087 × 0.033 × 0.053 | 1.0000 |  |
| 비누통 | 0.107 × 0.037 × 0.062 | 1.0000 |  |
| 분무기 | 0.030 × 0.104 × 0.031 | 1.0000 |  |
| 티슈 | 0.116 × 0.131 × 0.222 | 1.0000 |  |
| 핀셋 | 0.013 × 0.002 × 0.088 | 1.0000 |  |
| 병 1 | 0.174 × 0.289 × 0.174 | 1.0000 |  |
| 병 2 | 0.116 × 0.289 × 0.110 | 1.0000 |  |
| 접시 | 0.354 × 0.037 × 0.354 | 1.0000 |  |
| 야구방망이 001 | 0.095 × 1.085 × 0.095 | 0.9728 | 스포츠용품 |
| 하키스틱 001 | 0.065 × 1.085 × 0.363 | 0.8593 | 스포츠용품 |
## 공구·작업용품

| 아이템 | 조정 크기 XYZ(m) | 배율 | 비고 |
|---|---|---:|---|
| 쇠지렛대 | 0.167 × 0.561 × 0.028 | 1.0000 |  |
| 손전등 | 0.085 × 0.294 × 0.085 | 1.0000 |  |
| 연료통 | 0.427 × 0.614 × 0.173 | 0.7591 |  |
| 망치 | 0.202 × 0.392 × 0.068 | 1.0000 |  |
| 망치 2 | 0.202 × 0.392 × 0.068 | 1.0000 |  |
| 손도끼 | 0.190 × 0.506 × 0.055 | 1.0000 |  |
| 손도끼 2 | 0.190 × 0.506 × 0.055 | 1.0000 |  |
| 톱 | 0.427 × 0.123 × 0.018 | 0.6706 |  |
| 드라이버 Blue | 0.050 × 0.353 × 0.050 | 1.0000 | 색상 변형 (종류 수 중복 제외) |
| 드라이버 Red | 0.050 × 0.353 × 0.050 | 1.0000 | 색상 변형 (종류 수 중복 제외) |
| 삽 | 0.229 × 0.930 × 0.071 | 1.0000 |  |
| 대형망치 | 0.227 × 0.852 × 0.109 | 1.0000 |  |
| 대형망치 2 | 0.227 × 0.852 × 0.109 | 1.0000 |  |
| 줄자 | 0.063 × 0.097 × 0.137 | 1.0000 |  |
| 공구함 Blue | 0.427 × 0.247 × 0.233 | 0.6133 | 색상 변형 (종류 수 중복 제외) |
| 공구함 Red | 0.427 × 0.247 × 0.233 | 0.6133 | 색상 변형 (종류 수 중복 제외) |
| 렌치 | 0.078 × 0.296 × 0.025 | 1.0000 |  |
| 도끼 001 | 0.082 × 0.941 × 0.393 | 0.8186 | 손도끼 형태 변형 |
| 창고 가스통 | 0.380 × 0.725 × 0.384 | 1.0000 | 기존 프로젝트 에셋으로 보충 |
| 창고 사다리 | 0.427 × 0.779 × 0.194 | 0.4806 | 기존 프로젝트 에셋으로 보충 |
| 창고 작업용콘 | 0.393 × 0.580 × 0.393 | 0.9651 | 기존 프로젝트 에셋으로 보충 |
| 창고 전동드릴 | 0.106 × 0.319 × 0.325 | 1.0000 | 기존 프로젝트 에셋으로 보충 |
| 창고 손전등 | 0.191 × 0.218 × 0.259 | 1.0000 | 기존 프로젝트 에셋으로 보충 |
| 창고 망치 | 0.163 × 0.033 × 0.294 | 1.0000 | 기존 프로젝트 에셋으로 보충 |
| 창고 줄자 | 0.066 × 0.129 × 0.345 | 1.0000 | 기존 프로젝트 에셋으로 보충 |
| 창고 나사 | 0.105 × 0.044 × 0.044 | 1.0000 | 기존 프로젝트 에셋으로 보충 |
| 창고 수평계 | 0.016 × 0.036 × 0.393 | 0.3389 | 기존 프로젝트 에셋으로 보충 |
| 창고 바이스 | 0.427 × 0.187 × 0.232 | 0.8240 | 기존 프로젝트 에셋으로 보충 |
| 창고 십자렌치 | 0.393 × 0.038 × 0.393 | 0.7723 | 기존 프로젝트 에셋으로 보충 |
| 창고 렌치 A | 0.077 × 0.040 × 0.381 | 1.0000 | 기존 프로젝트 에셋으로 보충 |
| 창고 렌치 B | 0.110 × 0.031 × 0.366 | 1.0000 | 기존 프로젝트 에셋으로 보충 |
## 총기·폭발물

| 아이템 | 조정 크기 XYZ(m) | 배율 | 비고 |
|---|---|---:|---|
| AK74소총 | 0.027 × 0.119 × 0.393 | 0.5099 | 원본 팩의 메시·텍스처 참조를 제공된 FBX·팔레트로 복구 |
| 베넬리산탄총 | 0.022 × 0.077 × 0.393 | 0.4025 | 원본 팩의 메시·텍스처 참조를 제공된 FBX·팔레트로 복구 |
| 섬광탄 | 0.041 × 0.126 × 0.045 | 1.0000 | 원본 팩의 메시·텍스처 참조를 제공된 FBX·팔레트로 복구 |
| M107저격소총 | 0.028 × 0.068 × 0.393 | 0.3182 | 원본 팩의 메시·텍스처 참조를 제공된 FBX·팔레트로 복구 |
| M1911권총 | 0.030 × 0.157 × 0.248 | 1.0000 | 원본 팩의 메시·텍스처 참조를 제공된 FBX·팔레트로 복구 |
| M2중기관총 | 0.204 × 0.113 × 0.393 | 0.2082 | 원본 팩의 메시·텍스처 참조를 제공된 FBX·팔레트로 복구 |
| M249기관총 | 0.066 × 0.103 × 0.393 | 0.4562 | 원본 팩의 메시·텍스처 참조를 제공된 FBX·팔레트로 복구 |
| M4소총 | 0.032 × 0.129 × 0.393 | 0.5431 | 원본 팩의 메시·텍스처 참조를 제공된 FBX·팔레트로 복구 |
| 수류탄 | 0.071 × 0.157 × 0.078 | 1.0000 | 원본 팩의 메시·텍스처 참조를 제공된 FBX·팔레트로 복구 |
| RPG7발사기 | 0.036 × 0.078 × 0.393 | 0.2601 | 원본 팩의 메시·텍스처 참조를 제공된 FBX·팔레트로 복구 |
| 연막탄 | 0.076 × 0.153 × 0.083 | 1.0000 | 원본 팩의 메시·텍스처 참조를 제공된 FBX·팔레트로 복구 |
| 우지 | 0.036 × 0.214 × 0.382 | 1.0000 | 원본 팩의 메시·텍스처 참조를 제공된 FBX·팔레트로 복구 |
| 권총 001 | 0.064 × 0.221 × 0.393 | 0.6834 |  |
| 소총 001 | 0.030 × 0.113 × 0.393 | 0.2237 |  |
| 산탄총 001 | 0.030 × 0.088 × 0.393 | 0.2270 |  |
| 저격소총 001 | 0.046 × 0.096 × 0.393 | 0.2168 |  |
## 판타지 소품

| 아이템 | 조정 크기 XYZ(m) | 배율 | 비고 |
|---|---|---:|---|
| 한손도끼 1 | 0.427 × 1.015 × 0.057 | 0.8533 |  |
| 한손도끼 2 | 0.427 × 0.802 × 0.061 | 0.8193 |  |
| 한손도끼 3 | 0.385 × 1.085 × 0.087 | 0.9054 |  |
| 한손도끼 4 | 0.427 × 0.846 × 0.069 | 0.7560 |  |
| 한손도끼 5 | 0.427 × 0.825 × 0.094 | 0.7450 |  |
| 한손도끼 6 | 0.427 × 0.745 × 0.062 | 0.6516 |  |
| 한손도끼 7 | 0.427 × 0.869 × 0.062 | 0.6473 |  |
| 한손도끼 8 | 0.427 × 0.859 × 0.053 | 0.6586 |  |
| 양손도끼 1 | 0.406 × 1.085 × 0.042 | 0.5742 |  |
| 양손도끼 2 | 0.251 × 1.085 × 0.049 | 0.5498 |  |
| 양손도끼 3 | 0.362 × 1.085 × 0.048 | 0.5478 |  |
| 양손도끼 4 | 0.393 × 1.085 × 0.036 | 0.5443 |  |
| 양손도끼 5 | 0.409 × 1.085 × 0.034 | 0.5544 |  |
| 양손도끼 6 | 0.346 × 1.085 × 0.031 | 0.5399 |  |
| 양손도끼 7 | 0.307 × 1.085 × 0.074 | 0.5406 |  |
| 양손도끼 8 | 0.427 × 1.054 × 0.071 | 0.5357 |  |
| 한손검 1 | 0.235 × 1.085 × 0.082 | 0.7835 |  |
| 한손검 2 | 0.289 × 1.085 × 0.039 | 0.8290 |  |
| 한손검 3 | 0.235 × 1.085 × 0.027 | 0.8493 |  |
| 한손검 4 | 0.217 × 1.085 × 0.074 | 0.7416 |  |
| 한손검 5 | 0.213 × 1.085 × 0.057 | 0.8268 |  |
| 한손검 6 | 0.220 × 1.085 × 0.050 | 0.8797 |  |
| 한손검 7 | 0.259 × 1.085 × 0.053 | 0.8479 |  |
| 한손검 8 | 0.134 × 1.085 × 0.046 | 0.6795 |  |
| 한손검 9 | 0.254 × 1.085 × 0.059 | 0.8221 |  |
| 양손검 1 | 0.284 × 1.085 × 0.071 | 0.5957 |  |
| 양손검 2 | 0.235 × 1.085 × 0.041 | 0.6153 |  |
| 양손검 3 | 0.188 × 1.085 × 0.059 | 0.5954 |  |
| 양손검 4 | 0.267 × 1.085 × 0.053 | 0.5811 |  |
| 양손검 5 | 0.284 × 1.085 × 0.045 | 0.5957 |  |
| 화살 01-001 | 0.039 × 0.829 × 0.039 | 1.0000 |  |
| 도끼 01-002 | 0.320 × 1.085 × 0.038 | 0.5667 |  |
| 화살통 01-001 | 0.165 × 0.674 × 0.191 | 1.0000 |  |
| 방패 01-005 | 0.427 × 0.686 × 0.113 | 0.6119 |  |
| 활 02-001 | 0.279 × 1.085 × 0.077 | 0.6931 |  |
| 주먹무기 02-001 | 0.172 × 0.161 × 0.063 | 1.0000 |  |
| 폭탄 03-003 | 0.096 × 0.175 × 0.096 | 1.0000 |  |
| 망치 03-001 | 0.350 × 1.012 × 0.247 | 1.0000 |  |
| 낫 04-001 | 0.427 × 1.039 × 0.050 | 0.5281 |  |
| 마법책 04-003 | 0.264 × 0.350 × 0.122 | 1.0000 |  |
| 차크람 05-001 | 0.374 × 0.404 × 0.036 | 1.0000 |  |
| 다이너마이트 05-003 | 0.055 × 0.257 × 0.040 | 1.0000 |  |
| 물약 06-002 | 0.155 × 0.356 × 0.153 | 1.0000 |  |
| 방패 06-002 | 0.427 × 0.574 × 0.096 | 0.5841 |  |
| 검 06-001 | 0.210 × 1.085 × 0.050 | 0.6246 |  |
| 홀 07-001 | 0.263 × 1.085 × 0.055 | 0.5753 |  |
| 창 07-002 | 0.055 × 1.085 × 0.022 | 0.6082 |  |
| 마법봉 08-001 | 0.235 × 0.994 × 0.077 | 1.0000 |  |
| 뿔피리 09-003 | 0.427 × 0.160 × 0.134 | 0.8794 |  |
| 열쇠 09-002 | 0.043 × 0.162 × 0.012 | 1.0000 |  |
| 창 09-005 | 0.179 × 1.085 × 0.055 | 0.5516 |  |
| 투척칼 09-003 | 0.044 × 0.342 × 0.017 | 1.0000 |  |
| 단검 10-002 | 0.153 × 0.618 × 0.041 | 1.0000 |  |
| 횃불 10-001 | 0.157 × 0.975 × 0.142 | 1.0000 |  |
| 도끼 11-002 | 0.348 × 1.085 × 0.072 | 0.9042 |  |
| 검 11-003 | 0.332 × 1.057 × 0.054 | 1.0000 |  |
## 보류·부품·모형

| 아이템 | 조정 크기 XYZ(m) | 배율 | 비고 |
|---|---|---:|---|
| Frame | 0.427 × 0.000 × 0.295 | 0.2013 | 전시용 프레임 |
| 탄약케이스 | 0.427 × 0.297 × 0.208 | 0.7980 | 탄약 보관함: 총기·폭발물에서 제외 |
| 탄약상자 | 0.427 × 0.153 × 0.242 | 0.3214 | 탄약 보관함: 총기·폭발물에서 제외 |
| mesh_Ambulance_01 | 0.198 × 0.186 × 0.393 | 0.0930 | 차량 10종: 모형 후보, 최소 20개 미달 |
| mesh_Bus_01 | 0.115 × 0.129 × 0.393 | 0.0620 | 차량 10종: 모형 후보, 최소 20개 미달 |
| mesh_Bus_02 | 0.114 × 0.137 × 0.393 | 0.0621 | 차량 10종: 모형 후보, 최소 20개 미달 |
| mesh_Pickup_01 | 0.170 × 0.139 × 0.393 | 0.0794 | 차량 10종: 모형 후보, 최소 20개 미달 |
| mesh_Sedan_01 | 0.198 × 0.126 × 0.393 | 0.1012 | 차량 10종: 모형 후보, 최소 20개 미달 |
| mesh_SportCar_01 | 0.202 × 0.130 × 0.393 | 0.0989 | 차량 10종: 모형 후보, 최소 20개 미달 |
| mesh_Truck_01 | 0.195 × 0.235 × 0.393 | 0.0866 | 차량 10종: 모형 후보, 최소 20개 미달 |
| mesh_Truck_02 | 0.258 × 0.262 × 0.393 | 0.1166 | 차량 10종: 모형 후보, 최소 20개 미달 |
| mesh_TruckContainer_01 | 0.123 × 0.152 × 0.393 | 0.0617 | 차량 10종: 모형 후보, 최소 20개 미달 |
| mesh_Van_01 | 0.240 × 0.162 × 0.393 | 0.1067 | 차량 10종: 모형 후보, 최소 20개 미달 |
| 레이저표적지시기 | 0.053 × 0.028 × 0.102 | 1.0000 | 무기 부속품: 총기 개수에서 제외; 원본 팩의 메시·텍스처 참조를 제공된 FBX·팔레트로 복구 |
| 조준경 | 0.054 × 0.081 × 0.163 | 1.0000 | 무기 부속품: 총기 개수에서 제외; 원본 팩의 메시·텍스처 참조를 제공된 FBX·팔레트로 복구 |
| 조준경 | 0.057 × 0.103 × 0.159 | 1.0000 | 무기 부속품: 총기 개수에서 제외; 원본 팩의 메시·텍스처 참조를 제공된 FBX·팔레트로 복구 |
| 장거리조준경 | 0.047 × 0.058 × 0.292 | 1.0000 | 무기 부속품: 총기 개수에서 제외; 원본 팩의 메시·텍스처 참조를 제공된 FBX·팔레트로 복구 |
| TraderTable | 0.262 × 0.277 × 0.393 | 0.3295 | 환경 구조물/내용물이 담긴 세트 |
| TraderTent | 0.279 × 0.405 × 0.393 | 0.1457 | 환경 구조물/내용물이 담긴 세트 |
| WoodenBox1 Apple | 0.427 × 0.178 × 0.293 | 0.6678 | 환경 구조물/내용물이 담긴 세트 |
| WoodenBox1 Bottle1 | 0.427 × 0.220 × 0.293 | 0.6678 | 환경 구조물/내용물이 담긴 세트 |
| WoodenBox1 Bottle2 | 0.427 × 0.213 × 0.293 | 0.6678 | 환경 구조물/내용물이 담긴 세트 |
| WoodenBox1 Cheese | 0.427 × 0.177 × 0.293 | 0.6678 | 환경 구조물/내용물이 담긴 세트 |
| WoodenBox1 Orange | 0.427 × 0.168 × 0.293 | 0.6678 | 환경 구조물/내용물이 담긴 세트 |
| WoodenBox1 Tomato | 0.427 × 0.182 × 0.293 | 0.6678 | 환경 구조물/내용물이 담긴 세트 |
| WoodenBox1 | 0.427 × 0.145 × 0.293 | 0.6678 | 환경 구조물/내용물이 담긴 세트 |
| WoodenBox2 Pumpkin | 0.425 × 0.246 × 0.393 | 0.6147 | 환경 구조물/내용물이 담긴 세트 |
| WoodenBox2 Watermelon | 0.393 × 0.161 × 0.393 | 0.6147 | 환경 구조물/내용물이 담긴 세트 |
| WoodenBox2 | 0.393 × 0.133 × 0.393 | 0.6147 | 환경 구조물/내용물이 담긴 세트 |
| 칼 001 | 0.067 × 0.553 × 0.152 | 1.0000 | 근접무기 또는 무기 부속품: 총기·폭발물에서 제외 |
| 드럼탄창 001 | 0.090 × 0.092 × 0.076 | 1.0000 | 근접무기 또는 무기 부속품: 총기·폭발물에서 제외 |
| 소총탄창 001 | 0.049 × 0.322 × 0.250 | 1.0000 | 근접무기 또는 무기 부속품: 총기·폭발물에서 제외 |
| 산탄총손잡이 001 | 0.083 × 0.104 × 0.292 | 1.0000 | 근접무기 또는 무기 부속품: 총기·폭발물에서 제외 |
| 조준경 001 | 0.092 × 0.101 × 0.393 | 0.5773 | 근접무기 또는 무기 부속품: 총기·폭발물에서 제외 |

## 전시에서 제외한 원본 프리팹

렌더링 방식별 중복, 식물 구성 부품, 차량 스크립트 프리팹은 아래와 같이 처리했다. 차량은 동일 팩의 FBX 모델로 전시한다.

- `Assets/YelScryptFireStudio/LowPolyRoadVehiclesFreePackage/Vehicles/Ambulance_01/prefVar_Ambulance_01-BasePrefab.prefab` — 프리팹 스크립트 대신 동일 차량 FBX로 전시
- `Assets/YelScryptFireStudio/LowPolyRoadVehiclesFreePackage/Vehicles/Ambulance_01/pref_Ambulance_01.prefab` — 프리팹 스크립트 대신 동일 차량 FBX로 전시
- `Assets/YelScryptFireStudio/LowPolyRoadVehiclesFreePackage/Vehicles/Bus_01/prefVar_Bus_01-BasePrefab.prefab` — 프리팹 스크립트 대신 동일 차량 FBX로 전시
- `Assets/YelScryptFireStudio/LowPolyRoadVehiclesFreePackage/Vehicles/Bus_01/pref_Bus_01.prefab` — 프리팹 스크립트 대신 동일 차량 FBX로 전시
- `Assets/YelScryptFireStudio/LowPolyRoadVehiclesFreePackage/Vehicles/Bus_02/prefVar_Bus_02-BasePrefab.prefab` — 프리팹 스크립트 대신 동일 차량 FBX로 전시
- `Assets/YelScryptFireStudio/LowPolyRoadVehiclesFreePackage/Vehicles/Bus_02/pref_Bus_02.prefab` — 프리팹 스크립트 대신 동일 차량 FBX로 전시
- `Assets/YelScryptFireStudio/LowPolyRoadVehiclesFreePackage/Vehicles/Pickup_01/prefVar_Pickup_01-BasePrefab.prefab` — 프리팹 스크립트 대신 동일 차량 FBX로 전시
- `Assets/YelScryptFireStudio/LowPolyRoadVehiclesFreePackage/Vehicles/Pickup_01/pref_Pickup_01.prefab` — 프리팹 스크립트 대신 동일 차량 FBX로 전시
- `Assets/YelScryptFireStudio/LowPolyRoadVehiclesFreePackage/Vehicles/Sedan_01/prefVar_Sedan_01-BasePrefab.prefab` — 프리팹 스크립트 대신 동일 차량 FBX로 전시
- `Assets/YelScryptFireStudio/LowPolyRoadVehiclesFreePackage/Vehicles/Sedan_01/pref_Sedan_01.prefab` — 프리팹 스크립트 대신 동일 차량 FBX로 전시
- `Assets/YelScryptFireStudio/LowPolyRoadVehiclesFreePackage/Vehicles/SportCar_01/prefVar_SportCar_01-BasePrefab.prefab` — 프리팹 스크립트 대신 동일 차량 FBX로 전시
- `Assets/YelScryptFireStudio/LowPolyRoadVehiclesFreePackage/Vehicles/SportCar_01/pref_SportCar_01.prefab` — 프리팹 스크립트 대신 동일 차량 FBX로 전시
- `Assets/YelScryptFireStudio/LowPolyRoadVehiclesFreePackage/Vehicles/TruckContainer_01/prefVar_TruckContainer_01-BasePrefab.prefab` — 프리팹 스크립트 대신 동일 차량 FBX로 전시
- `Assets/YelScryptFireStudio/LowPolyRoadVehiclesFreePackage/Vehicles/TruckContainer_01/pref_TruckContainer_01.prefab` — 프리팹 스크립트 대신 동일 차량 FBX로 전시
- `Assets/YelScryptFireStudio/LowPolyRoadVehiclesFreePackage/Vehicles/Truck_01/prefVar_Truck_01-BasePrefab.prefab` — 프리팹 스크립트 대신 동일 차량 FBX로 전시
- `Assets/YelScryptFireStudio/LowPolyRoadVehiclesFreePackage/Vehicles/Truck_01/pref_Truck_01.prefab` — 프리팹 스크립트 대신 동일 차량 FBX로 전시
- `Assets/YelScryptFireStudio/LowPolyRoadVehiclesFreePackage/Vehicles/Truck_02/prefVar_Truck_02-BasePrefab.prefab` — 프리팹 스크립트 대신 동일 차량 FBX로 전시
- `Assets/YelScryptFireStudio/LowPolyRoadVehiclesFreePackage/Vehicles/Truck_02/pref_Truck_02.prefab` — 프리팹 스크립트 대신 동일 차량 FBX로 전시
- `Assets/YelScryptFireStudio/LowPolyRoadVehiclesFreePackage/Vehicles/Van_01/prefVar_Van_01-BasePrefab.prefab` — 프리팹 스크립트 대신 동일 차량 FBX로 전시
- `Assets/YelScryptFireStudio/LowPolyRoadVehiclesFreePackage/Vehicles/Van_01/pref_Van_01.prefab` — 프리팹 스크립트 대신 동일 차량 FBX로 전시
- `Assets/CosmicRetro_Plants&PotsPack_Demo/DemoScene/CR_PlantsScene.prefab` — 식물/화분 구성 부품 또는 데모; 완성 조합만 전시
- `Assets/CosmicRetro_Plants&PotsPack_Demo/Prefabs/Configurable Plants/CR_Plant_01.prefab` — 식물/화분 구성 부품 또는 데모; 완성 조합만 전시
- `Assets/CosmicRetro_Plants&PotsPack_Demo/Prefabs/Configurable Plants/CR_Plant_06.prefab` — 식물/화분 구성 부품 또는 데모; 완성 조합만 전시
- `Assets/CosmicRetro_Plants&PotsPack_Demo/Prefabs/Configurable Plants/CR_Plant_25.prefab` — 식물/화분 구성 부품 또는 데모; 완성 조합만 전시
- `Assets/CosmicRetro_Plants&PotsPack_Demo/Prefabs/Configurable Plants/CR_Plant_37.prefab` — 식물/화분 구성 부품 또는 데모; 완성 조합만 전시
- `Assets/CosmicRetro_Plants&PotsPack_Demo/Prefabs/Plants/CS_Plant_01_L1.prefab` — 식물/화분 구성 부품 또는 데모; 완성 조합만 전시
- `Assets/CosmicRetro_Plants&PotsPack_Demo/Prefabs/Plants/CS_Plant_01_L2.prefab` — 식물/화분 구성 부품 또는 데모; 완성 조합만 전시
- `Assets/CosmicRetro_Plants&PotsPack_Demo/Prefabs/Plants/CS_Plant_01_L3.prefab` — 식물/화분 구성 부품 또는 데모; 완성 조합만 전시
- `Assets/CosmicRetro_Plants&PotsPack_Demo/Prefabs/Plants/CS_Plant_06_L1.prefab` — 식물/화분 구성 부품 또는 데모; 완성 조합만 전시
- `Assets/CosmicRetro_Plants&PotsPack_Demo/Prefabs/Plants/CS_Plant_06_L2.prefab` — 식물/화분 구성 부품 또는 데모; 완성 조합만 전시
- `Assets/CosmicRetro_Plants&PotsPack_Demo/Prefabs/Plants/CS_Plant_06_L3.prefab` — 식물/화분 구성 부품 또는 데모; 완성 조합만 전시
- `Assets/CosmicRetro_Plants&PotsPack_Demo/Prefabs/Plants/CS_Plant_25_L1.prefab` — 식물/화분 구성 부품 또는 데모; 완성 조합만 전시
- `Assets/CosmicRetro_Plants&PotsPack_Demo/Prefabs/Plants/CS_Plant_25_L2.prefab` — 식물/화분 구성 부품 또는 데모; 완성 조합만 전시
- `Assets/CosmicRetro_Plants&PotsPack_Demo/Prefabs/Plants/CS_Plant_25_L3.prefab` — 식물/화분 구성 부품 또는 데모; 완성 조합만 전시
- `Assets/CosmicRetro_Plants&PotsPack_Demo/Prefabs/Plants/CS_Plant_37_L1.prefab` — 식물/화분 구성 부품 또는 데모; 완성 조합만 전시
- `Assets/CosmicRetro_Plants&PotsPack_Demo/Prefabs/Plants/CS_Plant_37_L2.prefab` — 식물/화분 구성 부품 또는 데모; 완성 조합만 전시
- `Assets/CosmicRetro_Plants&PotsPack_Demo/Prefabs/Plants/CS_Plant_37_L3.prefab` — 식물/화분 구성 부품 또는 데모; 완성 조합만 전시
- `Assets/CosmicRetro_Plants&PotsPack_Demo/Prefabs/Pots/CS_Plant_Pot_01.prefab` — 식물/화분 구성 부품 또는 데모; 완성 조합만 전시
- `Assets/CosmicRetro_Plants&PotsPack_Demo/Prefabs/Pots/CS_Plant_Pot_02.prefab` — 식물/화분 구성 부품 또는 데모; 완성 조합만 전시
- `Assets/Level13/Low Poly Interior Flower Pots/URP/Prefab/Plants/Cactus.prefab` — 식물/화분 구성 부품; 완성 조합만 전시
- `Assets/Level13/Low Poly Interior Flower Pots/URP/Prefab/Plants/Plant1 1.prefab` — 식물/화분 구성 부품; 완성 조합만 전시
- `Assets/Level13/Low Poly Interior Flower Pots/URP/Prefab/Plants/Plant1.prefab` — 식물/화분 구성 부품; 완성 조합만 전시
- `Assets/Level13/Low Poly Interior Flower Pots/URP/Prefab/Plants/Plant2.prefab` — 식물/화분 구성 부품; 완성 조합만 전시
- `Assets/Level13/Low Poly Interior Flower Pots/URP/Prefab/Plants/Plant3.prefab` — 식물/화분 구성 부품; 완성 조합만 전시
- `Assets/Level13/Low Poly Interior Flower Pots/URP/Prefab/Plants/Plant4.prefab` — 식물/화분 구성 부품; 완성 조합만 전시
- `Assets/Level13/Low Poly Interior Flower Pots/URP/Prefab/Plants/Plant5.prefab` — 식물/화분 구성 부품; 완성 조합만 전시
- `Assets/Level13/Low Poly Interior Flower Pots/URP/Prefab/Plants/Plant6.prefab` — 식물/화분 구성 부품; 완성 조합만 전시
- `Assets/Level13/Low Poly Interior Flower Pots/URP/Prefab/Plants/Plant7.prefab` — 식물/화분 구성 부품; 완성 조합만 전시
- `Assets/Level13/Low Poly Interior Flower Pots/URP/Prefab/Pots/Type1/Pot1.prefab` — 식물/화분 구성 부품; 완성 조합만 전시
- `Assets/Level13/Low Poly Interior Flower Pots/URP/Prefab/Pots/Type1/Pot10.prefab` — 식물/화분 구성 부품; 완성 조합만 전시
- `Assets/Level13/Low Poly Interior Flower Pots/URP/Prefab/Pots/Type1/Pot11.prefab` — 식물/화분 구성 부품; 완성 조합만 전시
- `Assets/Level13/Low Poly Interior Flower Pots/URP/Prefab/Pots/Type1/Pot12.prefab` — 식물/화분 구성 부품; 완성 조합만 전시
- `Assets/Level13/Low Poly Interior Flower Pots/URP/Prefab/Pots/Type1/Pot13.prefab` — 식물/화분 구성 부품; 완성 조합만 전시
- `Assets/Level13/Low Poly Interior Flower Pots/URP/Prefab/Pots/Type1/Pot14.prefab` — 식물/화분 구성 부품; 완성 조합만 전시
- `Assets/Level13/Low Poly Interior Flower Pots/URP/Prefab/Pots/Type1/Pot15.prefab` — 식물/화분 구성 부품; 완성 조합만 전시
- `Assets/Level13/Low Poly Interior Flower Pots/URP/Prefab/Pots/Type1/Pot16.prefab` — 식물/화분 구성 부품; 완성 조합만 전시
- `Assets/Level13/Low Poly Interior Flower Pots/URP/Prefab/Pots/Type1/Pot2.prefab` — 식물/화분 구성 부품; 완성 조합만 전시
- `Assets/Level13/Low Poly Interior Flower Pots/URP/Prefab/Pots/Type1/Pot3.prefab` — 식물/화분 구성 부품; 완성 조합만 전시
- `Assets/Level13/Low Poly Interior Flower Pots/URP/Prefab/Pots/Type1/Pot4.prefab` — 식물/화분 구성 부품; 완성 조합만 전시
- `Assets/Level13/Low Poly Interior Flower Pots/URP/Prefab/Pots/Type1/Pot5.prefab` — 식물/화분 구성 부품; 완성 조합만 전시
- `Assets/Level13/Low Poly Interior Flower Pots/URP/Prefab/Pots/Type1/Pot6.prefab` — 식물/화분 구성 부품; 완성 조합만 전시
- `Assets/Level13/Low Poly Interior Flower Pots/URP/Prefab/Pots/Type1/Pot7.prefab` — 식물/화분 구성 부품; 완성 조합만 전시
- `Assets/Level13/Low Poly Interior Flower Pots/URP/Prefab/Pots/Type1/Pot8.prefab` — 식물/화분 구성 부품; 완성 조합만 전시
- `Assets/Level13/Low Poly Interior Flower Pots/URP/Prefab/Pots/Type1/Pot9.prefab` — 식물/화분 구성 부품; 완성 조합만 전시
- `Assets/Level13/Low Poly Interior Flower Pots/URP/Prefab/Pots/Type2/Pot1.prefab` — 식물/화분 구성 부품; 완성 조합만 전시
- `Assets/Level13/Low Poly Interior Flower Pots/URP/Prefab/Pots/Type2/Pot2.prefab` — 식물/화분 구성 부품; 완성 조합만 전시
- `Assets/Level13/Low Poly Interior Flower Pots/URP/Prefab/Pots/Type2/Pot3.prefab` — 식물/화분 구성 부품; 완성 조합만 전시
- `Assets/Level13/Low Poly Interior Flower Pots/URP/Prefab/Pots/Type2/Pot4.prefab` — 식물/화분 구성 부품; 완성 조합만 전시
- `Assets/Level13/Low Poly Interior Flower Pots/URP/Prefab/Pots/Type2/Pot5.prefab` — 식물/화분 구성 부품; 완성 조합만 전시
- `Assets/Level13/Low Poly Interior Flower Pots/URP/Prefab/Pots/Type2/Pot6.prefab` — 식물/화분 구성 부품; 완성 조합만 전시
- `Assets/Level13/Low Poly Interior Flower Pots/URP/Prefab/Pots/Type2/Pot7.prefab` — 식물/화분 구성 부품; 완성 조합만 전시
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/Built-In/Prefabs/AdhesiveBandage.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/Built-In/Prefabs/AdhesiveTape.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/Built-In/Prefabs/Airfreshener.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/Built-In/Prefabs/BandageRoll.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/Built-In/Prefabs/Basket.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/Built-In/Prefabs/BobbyPin.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/Built-In/Prefabs/Bottle1.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/Built-In/Prefabs/Bottle2.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/Built-In/Prefabs/Bottle3.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/Built-In/Prefabs/ClothesPin.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/Built-In/Prefabs/CottonSwab.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/Built-In/Prefabs/HairCurler.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/Built-In/Prefabs/HairDryer.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/Built-In/Prefabs/HairGrease.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/Built-In/Prefabs/HandMirror.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/Built-In/Prefabs/LatexGloves.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/Built-In/Prefabs/MedicalScissors.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/Built-In/Prefabs/NailClippers.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/Built-In/Prefabs/Razor.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/Built-In/Prefabs/RubberDuck.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/Built-In/Prefabs/Soap.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/Built-In/Prefabs/SoapBox.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/Built-In/Prefabs/SprayBottle.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/Built-In/Prefabs/TissueBox.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/Built-In/Prefabs/Tweezers.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/HDRP/Prefabs/AdhesiveBandage.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/HDRP/Prefabs/AdhesiveTape.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/HDRP/Prefabs/Airfreshener.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/HDRP/Prefabs/BandageRoll.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/HDRP/Prefabs/Basket.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/HDRP/Prefabs/BobbyPin.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/HDRP/Prefabs/Bottle1.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/HDRP/Prefabs/Bottle2.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/HDRP/Prefabs/Bottle3.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/HDRP/Prefabs/ClothesPin.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/HDRP/Prefabs/CottonSwab.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/HDRP/Prefabs/HairCurler.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/HDRP/Prefabs/HairDryer.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/HDRP/Prefabs/HairGrease.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/HDRP/Prefabs/HandMirror.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/HDRP/Prefabs/LatexGloves.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/HDRP/Prefabs/MedicalScissors.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/HDRP/Prefabs/NailClippers.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/HDRP/Prefabs/Razor.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/HDRP/Prefabs/RubberDuck.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/HDRP/Prefabs/Soap.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/HDRP/Prefabs/SoapBox.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/HDRP/Prefabs/SprayBottle.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/HDRP/Prefabs/TissueBox.prefab` — 렌더 파이프라인 중복
- `Assets/CrowAssets/Assets/Stylized Bathroom Set/HDRP/Prefabs/Tweezers.prefab` — 렌더 파이프라인 중복
- `Assets/RPG FOOD PROPS/HD RENDER PIPELINE/Prefabs/Apple.prefab` — 렌더 파이프라인 중복
- `Assets/RPG FOOD PROPS/HD RENDER PIPELINE/Prefabs/Bottle1.prefab` — 렌더 파이프라인 중복
- `Assets/RPG FOOD PROPS/HD RENDER PIPELINE/Prefabs/Bottle2.prefab` — 렌더 파이프라인 중복
- `Assets/RPG FOOD PROPS/HD RENDER PIPELINE/Prefabs/Bread1.prefab` — 렌더 파이프라인 중복
- `Assets/RPG FOOD PROPS/HD RENDER PIPELINE/Prefabs/Bread2.prefab` — 렌더 파이프라인 중복
- `Assets/RPG FOOD PROPS/HD RENDER PIPELINE/Prefabs/Cheese1.prefab` — 렌더 파이프라인 중복
- `Assets/RPG FOOD PROPS/HD RENDER PIPELINE/Prefabs/Cheese2.prefab` — 렌더 파이프라인 중복
- `Assets/RPG FOOD PROPS/HD RENDER PIPELINE/Prefabs/Meat1.prefab` — 렌더 파이프라인 중복
- `Assets/RPG FOOD PROPS/HD RENDER PIPELINE/Prefabs/Meat2.prefab` — 렌더 파이프라인 중복
- `Assets/RPG FOOD PROPS/HD RENDER PIPELINE/Prefabs/Orange.prefab` — 렌더 파이프라인 중복
- `Assets/RPG FOOD PROPS/HD RENDER PIPELINE/Prefabs/Plate.prefab` — 렌더 파이프라인 중복
- `Assets/RPG FOOD PROPS/HD RENDER PIPELINE/Prefabs/Pumpkin.prefab` — 렌더 파이프라인 중복
- `Assets/RPG FOOD PROPS/HD RENDER PIPELINE/Prefabs/Sausage.prefab` — 렌더 파이프라인 중복
- `Assets/RPG FOOD PROPS/HD RENDER PIPELINE/Prefabs/Tomato.prefab` — 렌더 파이프라인 중복
- `Assets/RPG FOOD PROPS/HD RENDER PIPELINE/Prefabs/TraderTable.prefab` — 렌더 파이프라인 중복
- `Assets/RPG FOOD PROPS/HD RENDER PIPELINE/Prefabs/TraderTent.prefab` — 렌더 파이프라인 중복
- `Assets/RPG FOOD PROPS/HD RENDER PIPELINE/Prefabs/Watermelon.prefab` — 렌더 파이프라인 중복
- `Assets/RPG FOOD PROPS/HD RENDER PIPELINE/Prefabs/WoodenBox1 Apple.prefab` — 렌더 파이프라인 중복
- `Assets/RPG FOOD PROPS/HD RENDER PIPELINE/Prefabs/WoodenBox1 Bottle1.prefab` — 렌더 파이프라인 중복
- `Assets/RPG FOOD PROPS/HD RENDER PIPELINE/Prefabs/WoodenBox1 Bottle2.prefab` — 렌더 파이프라인 중복
- `Assets/RPG FOOD PROPS/HD RENDER PIPELINE/Prefabs/WoodenBox1 Cheese.prefab` — 렌더 파이프라인 중복
- `Assets/RPG FOOD PROPS/HD RENDER PIPELINE/Prefabs/WoodenBox1 Orange.prefab` — 렌더 파이프라인 중복
- `Assets/RPG FOOD PROPS/HD RENDER PIPELINE/Prefabs/WoodenBox1 Tomato.prefab` — 렌더 파이프라인 중복
- `Assets/RPG FOOD PROPS/HD RENDER PIPELINE/Prefabs/WoodenBox1.prefab` — 렌더 파이프라인 중복
- `Assets/RPG FOOD PROPS/HD RENDER PIPELINE/Prefabs/WoodenBox2 Pumpkin.prefab` — 렌더 파이프라인 중복
- `Assets/RPG FOOD PROPS/HD RENDER PIPELINE/Prefabs/WoodenBox2 Watermelon 1.prefab` — 렌더 파이프라인 중복
- `Assets/RPG FOOD PROPS/HD RENDER PIPELINE/Prefabs/WoodenBox2.prefab` — 렌더 파이프라인 중복