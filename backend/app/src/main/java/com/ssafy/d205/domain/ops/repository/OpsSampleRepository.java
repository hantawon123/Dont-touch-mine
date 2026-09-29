package com.ssafy.d205.domain.ops.repository;

import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.util.List;
import java.util.Optional;

import com.ssafy.d205.domain.ops.entity.OpsSample;

public interface OpsSampleRepository extends JpaRepository<OpsSample, String> {

    /** from 이후(포함) 샘플을 시각순으로. 개요 탭의 시계열이 씁니다. PK 범위 스캔입니다. */
    List<OpsSample> findBySampledAtGreaterThanEqualOrderBySampledAtAsc(String from);

    /** 가장 최근 샘플. 개요 탭의 "지금 값" 중 CPU·힙처럼 매 요청마다 다시 재지 않는 값의 출처입니다. */
    Optional<OpsSample> findTopByOrderBySampledAtDesc();

    /**
     * 오래된 행을 지웁니다. 30일이면 43,200행이 상한이라 한 문장으로 지워도 잠금이 짧습니다.
     *
     * @return 지운 행 수
     */
    @Modifying(clearAutomatically = true, flushAutomatically = true)
    @Query("DELETE FROM OpsSample s WHERE s.sampledAt < :threshold")
    int deleteOlderThan(@Param("threshold") String threshold);
}
