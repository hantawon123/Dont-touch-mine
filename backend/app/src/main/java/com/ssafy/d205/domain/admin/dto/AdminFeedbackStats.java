package com.ssafy.d205.domain.admin.dto;

/**
 * 피드백 탭의 통계 (S15P21D205-1004). 최근 {@code days} 일 동안 들어온 피드백입니다.
 *
 * <p>플랫폼과 빌드 버전은 클라이언트가 보낸 값 그대로입니다. 안 보낸 것은 "(없음)" 한 줄로 묶입니다.
 * 운영자가 숨긴 피드백은 셋 모두에서 빠집니다.
 *
 * @param days       센 기간(일)
 * @param daily      날짜별(KST) 건수. 없는 날은 0
 * @param byPlatform 플랫폼별 건수. 많은 순
 * @param byBuild    빌드 버전별 건수. 많은 순. 옛 빌드에서 계속 오면 업데이트가 안 된 사람이 있다는 뜻입니다
 */
public record AdminFeedbackStats(
        int days,
        AdminCountTable daily,
        AdminCountTable byPlatform,
        AdminCountTable byBuild
) {
}
