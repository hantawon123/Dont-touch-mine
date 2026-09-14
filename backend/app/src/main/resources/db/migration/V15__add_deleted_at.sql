-- 운영자가 신고와 피드백을 숨길 수 있게 합니다 (S15P21D205-900).
--
-- 관리 화면에 지우는 동작이 둘 생깁니다. 완전 삭제는 행을 지우므로 컬럼이 필요 없고,
-- 숨김이 이 컬럼을 씁니다. 둘을 함께 두는 이유는 신고 쪽에 있습니다. ReportStatus 는
-- "봤다"가 아니라 "실제로 문제였나"를 남겨 무고성 신고를 세는 근거인데, 행을 지우면
-- 그 셈이 그만큼 빕니다. 눈앞에서 치우고 싶을 뿐이라면 숨김이 맞습니다.
--
-- NULL 이 "살아 있음"입니다. 반대로 두면(deleted BOOLEAN NOT NULL DEFAULT FALSE) 언제
-- 치웠는지가 남지 않습니다. 잘못 숨긴 것을 되찾을 때 시각이 유일한 단서입니다.
--
-- 형식은 다른 시각 컬럼과 같은 CHAR(14) UTC 입니다(yyyyMMddHHmmss). 이 스키마의 시각은
-- 전부 이 모양이고, 여기만 DATETIME 으로 두면 TimeProvider 를 쓰지 못합니다.

ALTER TABLE user_reports
    ADD COLUMN deleted_at CHAR(14) NULL AFTER reviewed_by;

ALTER TABLE user_feedback
    ADD COLUMN deleted_at CHAR(14) NULL AFTER created_at;

-- 인덱스를 두지 않습니다.
--
-- 조회는 전부 deleted_at IS NULL 을 깔고 가지만, 그 조건 하나만으로 행을 고르는 조회는
-- 없습니다. 신고는 status 로, 피드백은 created_at 정렬로 먼저 좁히고 거기에 이 조건이
-- 붙습니다. 숨긴 행은 소수라 남은 것을 걸러내는 비용이 작습니다.
--
-- 숨긴 행이 전체의 상당수가 되면 그때 기존 인덱스에 컬럼을 더하는 편이 낫습니다.
-- deleted_at 단독 인덱스는 그 경우에도 답이 아닙니다 - 선택도가 낮습니다.
