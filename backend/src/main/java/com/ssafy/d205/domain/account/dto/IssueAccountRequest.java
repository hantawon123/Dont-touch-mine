package com.ssafy.d205.domain.account.dto;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;

/**
 * @param deviceId 클라이언트가 첫 실행에 만들어 저장해 둔 기기 식별자.
 *                 <p>형식을 UUID로 강제하지 않습니다. 스키마 주석은 UUID(36자)를
 *                 전제하지만, Unity의 SystemInfo.deviceUniqueIdentifier는 UUID가
 *                 아니라 플랫폼마다 길이가 다른 해시입니다. 정규식으로 UUID를 요구하면
 *                 클라이언트가 그쪽으로 바꾸는 순간 계정 발급 자체가 막힙니다.
 *                 길이 상한만 컬럼 크기에 맞춰 둡니다.
 */
public record IssueAccountRequest(
        @NotBlank(message = "deviceId는 필수입니다.")
        @Size(max = 36, message = "deviceId는 36자를 넘을 수 없습니다.")
        String deviceId
) {
}
