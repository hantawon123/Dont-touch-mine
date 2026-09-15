-- 닉네임 검색을 대소문자 무시로 하기 위한 소문자 컬럼과 인덱스.
--
-- V4에서 nickname 컬럼을 utf8mb4_0900_as_cs로 바꿔 대소문자를 구분하게 만들었다.
-- 그래서 uk_users_nickname 으로는 player 를 찾을 때 Player 가 걸리지 않는다.
-- 사용자가 친구를 찾을 때 대소문자를 정확히 맞춰 입력해야 하는 것은 곤란하다.
--
-- 쿼리에서 LOWER(nickname) 을 쓰거나 COLLATE 를 붙이는 방법도 있지만 둘 다
-- 인덱스를 타지 못한다. 컬럼과 콜레이션이 다르거나 함수가 씌워지면 MySQL은
-- 인덱스를 쓸 수 없고 전체 스캔이 된다. 그래서 소문자 값을 컬럼으로 만들고
-- 그 컬럼에 인덱스를 둔다.
--
-- VIRTUAL 이라 값을 저장하지 않는다. 읽을 때 계산하지만 인덱스에는 실체가 있어서
-- 검색은 인덱스만으로 끝난다. STORED 로 두면 행마다 32바이트를 더 쓰는데 얻는
-- 것이 없다.
--
-- 검색은 접두사 일치다. LIKE 'query%' 는 이 인덱스를 타지만 LIKE '%query%' 는
-- 앞의 와일드카드 때문에 타지 못한다. 부분 일치로 바꾸려면 인덱스를 포기하거나
-- 전문 검색으로 가야 한다.
--
-- 조회할 때 검색어를 반드시 소문자로 바꿔서 보내야 한다. 컬럼 콜레이션이
-- as_cs 라 대문자로 보내면 아무것도 걸리지 않는다.
ALTER TABLE users
    ADD COLUMN nickname_lower VARCHAR(32)
        CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_cs
        AS (LOWER(nickname)) VIRTUAL,
    ADD KEY ix_users_nickname_lower (nickname_lower);
