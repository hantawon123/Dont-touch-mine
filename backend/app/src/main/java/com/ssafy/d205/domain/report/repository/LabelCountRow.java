package com.ssafy.d205.domain.report.repository;

/** 이름 하나와 건수. 사유별·상태별·날짜별 분포가 전부 이 모양입니다 (S15P21D205-1004). */
public interface LabelCountRow {

    String getLabel();

    int getCount();
}
