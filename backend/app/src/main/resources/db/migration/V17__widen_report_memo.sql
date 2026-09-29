-- 신고 모달에 본문을 적는 칸이 생기면서 메모 한도를 500자로 올립니다.
-- 화면이 500까지 받는데 컬럼이 200이면, 적힌 글이 서버에서 잘리거나 거절됩니다.

ALTER TABLE user_reports
    MODIFY memo VARCHAR(500) NULL;
