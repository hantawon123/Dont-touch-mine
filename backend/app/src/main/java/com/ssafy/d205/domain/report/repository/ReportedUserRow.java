package com.ssafy.d205.domain.report.repository;

/**
 * 신고당한 사람 하나에 대한 요약. 목록 화면의 한 줄입니다.
 *
 * <p>신고를 한 건씩 보여주면 운영자가 판단할 수 없습니다. 같은 사람에 대한 신고 다섯
 * 건이 흩어져 나오면 <b>그 사람이 문제인지 신고한 사람이 문제인지</b> 알 수 없습니다.
 * 그래서 사람 단위로 묶습니다.
 */
public interface ReportedUserRow {

    String getUserId();

    String getNickname();

    /** 신고 건수. */
    int getReportCount();

    /**
     * 신고한 사람 수. <b>건수와 반드시 함께 봐야 합니다.</b>
     *
     * <p>같은 사람을 여러 번 신고하는 것을 막지 않기로 했으므로(V9 주석) 한 사람이
     * 건수를 부풀릴 수 있습니다. "3건 1명"은 신고한 쪽이 의심스럽다는 뜻이고,
     * "7건 5명"은 신고당한 쪽이 의심스럽다는 뜻입니다. 둘을 합쳐 보여주면 그 구분이
     * 사라집니다.
     *
     * <p>탈퇴한 신고자는 여기 세지 않습니다. reporter_seq 가 전부 NULL 이라 몇 명인지
     * 알 수 없기 때문입니다. 그 건수는 따로 셉니다.
     */
    int getReporterCount();

    /**
     * 신고자가 탈퇴해서 누구인지 알 수 없는 건수.
     *
     * <p>기록은 남지만(V9 의 ON DELETE SET NULL) 인원수는 복원할 수 없습니다. 한 명이
     * 두 번 신고하고 탈퇴한 것과 두 명이 각각 신고하고 탈퇴한 것이 구분되지 않습니다.
     * 그래서 reporterCount 에 섞지 않고 따로 보여줍니다. 섞으면 "3명이 신고"라고
     * 사실이 아닌 말을 하게 됩니다.
     */
    int getFromDeletedAccounts();

    /** 가장 최근 신고 시각. yyyyMMddHHmmss, UTC. 목록 정렬의 기준입니다. */
    String getLastReportedAt();

    /**
     * 정지된 시각. 정상이면 null 입니다(S15P21D205-927).
     *
     * <p>목록에 함께 싣는 이유는, 운영자가 이미 조치한 사람을 다시 들여다보지 않게
     * 하려는 것입니다. 행마다 따로 물으면 스무 명일 때 쿼리가 스물한 번 나갑니다.
     */
    String getSuspendedAt();
}
