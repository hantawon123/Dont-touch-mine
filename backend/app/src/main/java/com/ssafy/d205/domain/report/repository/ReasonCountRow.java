package com.ssafy.d205.domain.report.repository;

/** 한 사람의 한 사유에 대한 건수. 서비스가 목록에 붙입니다. */
public interface ReasonCountRow {

    String getUserId();

    String getReason();

    int getCount();
}
