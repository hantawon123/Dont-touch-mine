package com.ssafy.d205.domain.account.dto;

/**
 * 발급 결과.
 *
 * @param created 이번 요청이 계정을 새로 만들었는지. 컨트롤러가 201과 200을 가르는
 *                데만 씁니다. 발급은 멱등해서 같은 기기가 다시 부르면 기존 계정을
 *                그대로 돌려주는데, 그때도 201을 주면 클라이언트에게 거짓말이 됩니다.
 */
public record IssuedAccount(
        AccountResponse account,
        boolean created
) {
}
