package com.ssafy.d205.domain.report.repository;

import org.springframework.data.jpa.repository.JpaRepository;

import com.ssafy.d205.domain.report.entity.UserReport;

/**
 * 신고를 저장합니다.
 *
 * <p>조회 메서드가 하나도 없는 것이 이상해 보일 수 있는데 의도입니다. 신고를 읽는 것은
 * 운영자 도구의 일이고 그 도구는 아직 없습니다. 쓰지 않을 조회를 미리 만들면 그것이
 * 계약처럼 굳어져, 나중에 실제로 필요한 모양과 어긋난 채로 남습니다.
 */
public interface UserReportRepository extends JpaRepository<UserReport, Integer> {
}
