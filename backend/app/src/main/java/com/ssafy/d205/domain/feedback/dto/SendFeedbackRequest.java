package com.ssafy.d205.domain.feedback.dto;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;

/**
 * @param message  본문. 500자까지입니다. 클라이언트 화면의 상한
 *                 (SettingsStyle.Feedback.MaxLength)과 같은 값이라, 화면에서 다 채워
 *                 보낸 글이 서버에서 거절되지 않습니다. 공백만 있는 것은 피드백이
 *                 아니므로 NotBlank 입니다 - 화면도 같은 규칙으로 보내기 버튼을
 *                 잠그지만, 화면을 믿고 검사를 빼면 화면이 바뀔 때 조용히 뚫립니다.
 * @param buildVer 보낸 클라이언트의 빌드. 비워도 됩니다. 이것이 없으면 "튕긴다"는
 *                 피드백을 받아도 재현할 빌드를 알 수 없습니다. 그래도 필수로 두지
 *                 않는 이유는, 이 값이 빠졌다고 피드백을 버리는 것이 더 큰 손해이기
 *                 때문입니다.
 * @param platform 실행 환경(WebGL, Windows 같은 것). 비워도 됩니다. 값의 목록을 서버가
 *                 정해 검증하지 않습니다. 플랫폼은 클라이언트 사정으로 늘어나고,
 *                 목록을 서버에 박으면 새 플랫폼에서 온 피드백이 400 이 됩니다.
 */
public record SendFeedbackRequest(
        @NotBlank(message = "message는 필수입니다.")
        @Size(max = 500, message = "message는 500자를 넘을 수 없습니다.")
        String message,

        @Size(max = 32, message = "buildVer는 32자를 넘을 수 없습니다.")
        String buildVer,

        @Size(max = 16, message = "platform은 16자를 넘을 수 없습니다.")
        String platform
) {
}
