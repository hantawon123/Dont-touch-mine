package com.ssafy.d205.domain.report.dto;

/**
 * @param userId              신고당한 사람의 공개 식별자. 상세 조회와 처리에 씁니다.
 * @param nickname            지금 닉네임. 바뀔 수 있으므로 화면 표시에만 씁니다.
 * @param reportCount         신고 건수
 * @param reporterCount       신고한 사람 수. <b>건수와 함께 봐야 합니다.</b>
 *                            "3건 1명"은 신고한 쪽이, "7건 5명"은 신고당한 쪽이
 *                            의심스럽다는 뜻입니다. 합쳐 보여주면 그 구분이 사라집니다.
 * @param fromDeletedAccounts 신고자가 탈퇴해 누구인지 알 수 없는 건수. 인원수를
 *                            복원할 수 없어 reporterCount 에 섞지 않습니다.
 * @param reasons             사유별 건수. 무엇이 문제인지 한눈에 보이는 값입니다.
 * @param lastReportedAt      가장 최근 신고 시각. yyyyMMddHHmmss, UTC.
 */
public record ReportedUserSummary(
        String userId,
        String nickname,
        int reportCount,
        int reporterCount,
        int fromDeletedAccounts,
        java.util.Map<String, Integer> reasons,
        String lastReportedAt,

        /**
         * 지금 정지되어 있는가(S15P21D205-927).
         *
         * <p>시각이 아니라 참/거짓입니다. 화면이 쓰는 것은 버튼을 어느 쪽으로 그릴지뿐이고,
         * 정지한 시각과 사유는 여기서 보여줄 값이 아닙니다 - 사유는 신고 내용에서 나오므로
         * 목록에 늘어놓으면 누가 무엇을 신고했는지가 한눈에 드러납니다.
         */
        boolean suspended
) {
}
