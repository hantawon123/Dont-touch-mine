package com.ssafy.d205.domain.admin.dto;

/**
 * 닉네임 변경 결과 (S15P21D205-1047).
 *
 * @param changed  이번 요청이 이름을 바꿨는지. 직접 지정에서 지금과 같은 이름을 보내면 false 입니다
 * @param nickname 바꾼 뒤 이름. 부적절한 닉네임 처리는 서버가 지으므로 화면이 이 값으로 알려줍니다
 */
public record AdminRenameResult(boolean changed, String nickname) {
}
