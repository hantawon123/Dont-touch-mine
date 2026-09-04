package com.ssafy.d205.domain.report.dto;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;

import com.ssafy.d205.domain.report.entity.ReportReason;

/**
 * @param userId 신고할 사람의 공개 식별자. 닉네임이 아니라 id 로 받는 이유는 친구 요청,
 *               초대와 같습니다 - 닉네임은 바뀌고 대소문자를 구분합니다.
 * @param reason 사유. {@link ReportReason} 의 이름 중 하나입니다. 목록에 없는 값이면
 *               400 INVALID_REQUEST 입니다. 잭슨이 열거형으로 바꾸지 못하는 것을
 *               GlobalExceptionHandler 가 그렇게 옮깁니다.
 * @param memo   신고자가 적는 한 줄. 비워도 됩니다. 사유만으로는 운영자가 판단할 맥락이
 *               없고, 자유 텍스트만 받으면 분류할 수 없어서 둘을 함께 받습니다.
 */
public record SendReportRequest(
        @NotBlank(message = "userId는 필수입니다.")
        @Size(max = 36, message = "userId는 36자를 넘을 수 없습니다.")
        String userId,

        @NotNull(message = "reason은 필수입니다.")
        ReportReason reason,

        @Size(max = 200, message = "memo는 200자를 넘을 수 없습니다.")
        String memo
) {
}
