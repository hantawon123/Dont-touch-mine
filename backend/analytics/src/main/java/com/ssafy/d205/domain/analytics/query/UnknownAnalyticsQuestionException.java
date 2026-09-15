package com.ssafy.d205.domain.analytics.query;

/** 문서에 없는 질문 이름입니다. 404 QUESTION_NOT_FOUND 로 답합니다. */
public class UnknownAnalyticsQuestionException extends RuntimeException {

    private final String slug;

    public UnknownAnalyticsQuestionException(String slug) {
        super("분석 질문이 없습니다: " + slug);
        this.slug = slug;
    }

    public String slug() {
        return slug;
    }
}
