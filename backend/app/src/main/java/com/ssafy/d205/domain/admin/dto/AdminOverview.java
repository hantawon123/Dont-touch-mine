package com.ssafy.d205.domain.admin.dto;

import java.util.List;

/**
 * 관리 화면 개요 탭이 한 번에 받는 것 전부.
 *
 * @param now     지금 값. 카드로 그립니다
 * @param range   series 가 덮는 범위. "24h" 또는 "7d"
 * @param series  시계열. 24h 는 1분 샘플 그대로, 7d 는 15분 버킷 평균
 * @param matches 경기 통계. 분석 서비스가 분리되어 내부 API 가 생기기 전까지는 null 입니다(1002).
 *                화면은 null 이면 "분리 후 연결" 자리만 보여줍니다
 */
public record AdminOverview(
        Now now,
        String range,
        List<Point> series,
        MatchStats matches
) {

    /**
     * @param online / inLobby / inGame 접속 상태별 인원. 서로 배타적이라 셋을 더하면 접속자 수
     * @param socketConnections        알림 WebSocket 연결 수. 두 기기면 2 라 접속자 수와 다를 수 있음
     * @param totalUsers               전체 계정 수
     * @param signupsToday             오늘(Asia/Seoul 기준) 가입
     * @param deletionsToday           오늘 탈퇴. 샘플의 증분 합에 아직 샘플에 실리지 않은 수를 더한 값. 앱 재시작 사이의 것은 빠질 수 있음
     * @param pendingReports           미검토이고 숨기지 않은 신고 수
     * @param feedbackToday            오늘 들어온 피드백 수
     * @param suspendedUsers           지금 정지된 계정 수
     * @param cpuPct                   시스템 CPU 사용률 0~100
     * @param heapUsedMb / heapMaxMb   JVM 힙
     * @param dbPoolActive / dbPoolMax 게임 DB 와 분석 DB 커넥션 풀 합
     */
    public record Now(
            int online,
            int inLobby,
            int inGame,
            int socketConnections,
            long totalUsers,
            long signupsToday,
            long deletionsToday,
            long pendingReports,
            long feedbackToday,
            long suspendedUsers,
            double cpuPct,
            int heapUsedMb,
            int heapMaxMb,
            int dbPoolActive,
            int dbPoolMax
    ) {
    }

    /** 시계열 한 점. at 은 yyyyMMddHHmmss UTC 이고 7d 버킷은 15분 경계(초 00)입니다. */
    public record Point(
            String at,
            int online,
            int inLobby,
            int inGame,
            int socketConnections,
            int signups,
            int deletions,
            double cpuPct,
            int heapUsedMb,
            int dbPoolActive
    ) {
    }

    /** 경기 통계. 분석 서비스 내부 API(1002)가 채웁니다. 지금은 응답에 null 로 나갑니다. */
    public record MatchStats(
            Long matchesToday,
            Long inProgress,
            Double avgDurationSec,
            Double dropoutRate
    ) {
    }
}
