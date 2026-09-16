package com.ssafy.d205.domain.chat.entity;

/**
 * 어디서 한 말인가.
 *
 * <p>신고는 로비에서도 할 수 있어서(경기 전 부적절한 말) 둘 다 담습니다. 나눠 두는 이유는
 * 운영자가 "경기 중에만 그랬나"를 구분해 볼 일이 있기 때문입니다.
 */
public enum ChatScope {
    LOBBY,
    MATCH
}
