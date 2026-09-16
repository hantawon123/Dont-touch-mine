package com.ssafy.d205.domain.feedback.repository;

/**
 * 이름 하나와 건수. 날짜별·플랫폼별·빌드별 분포가 전부 이 모양입니다 (S15P21D205-1004).
 *
 * <p>신고 쪽에 같은 인터페이스가 있지만 합치지 않았습니다. 두 저장소가 서로를 모르는 편이
 * 세 줄 중복보다 낫습니다.
 */
public interface LabelCountRow {

    String getLabel();

    int getCount();
}
