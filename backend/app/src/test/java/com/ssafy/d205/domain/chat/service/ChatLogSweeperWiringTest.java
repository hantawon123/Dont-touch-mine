package com.ssafy.d205.domain.chat.service;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.scheduling.annotation.Scheduled;

import java.io.IOException;
import java.lang.reflect.Method;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;

import static org.assertj.core.api.Assertions.assertThat;

/**
 * 청소가 조용히 멈추거나 조용히 좁아지지 않는지 봅니다 (S15P21D205-1031).
 *
 * <p>이 표에는 가리기 전의 채팅 원문이 들어 있고 지우는 것은 {@link ChatLogSweeper} 한 곳뿐입니다.
 * 여기서 잡으려는 두 가지는 <b>둘 다 아무 신호 없이 지나간다</b>는 공통점이 있습니다.
 *
 * <ol>
 *   <li>{@code @Scheduled} 가 사라지면 개인정보가 계속 쌓입니다. 다른 테스트는 {@code sweep()} 을
 *       직접 부르므로 그래도 전부 통과합니다 - 삭제가 맞는지와 그것이 실제로 도는지는 다른
 *       질문입니다.</li>
 *   <li>보호 구간이 관리 화면보다 좁아지면 운영자가 넓혀 읽던 줄이 다음 청소에 사라집니다.
 *       화면에서 본 것이 다음 날 없어지는데 오류는 나지 않습니다.</li>
 * </ol>
 */
class ChatLogSweeperWiringTest {

    /** 테스트의 작업 디렉터리는 backend/ 입니다(Gradle 기본값). */
    private static final Path SWEEPER = Path.of("app", "src", "main", "java", "com", "ssafy", "d205",
            "domain", "chat", "service", "ChatLogSweeper.java");

    @Test
    @DisplayName("sweep 이 주기 실행에 걸려 있다")
    void sweepIsScheduled() throws NoSuchMethodException {
        Method sweep = ChatLogSweeper.class.getMethod("sweep");

        assertThat(sweep.getAnnotation(Scheduled.class))
                .as("@Scheduled 가 빠지면 채팅 원문이 지워지지 않고 계속 쌓입니다. "
                        + "테스트는 sweep() 을 직접 부르므로 그래도 전부 통과합니다.")
                .isNotNull();
    }

    /**
     * 애노테이션의 기본값은 컴파일에 상수로 굳어 리플렉션으로는 원래 식을 볼 수 없습니다. 그래서
     * 원본을 읽습니다. AdminGuideTest 가 경로를 SecurityConfig 원본에서 뽑는 것과 같은 방식입니다.
     */
    @Test
    @DisplayName("보호 구간의 기본값이 관리 화면 상한과 한 상수로 묶여 있다")
    void protectedWindowIsBoundToTheScreenLimit() throws IOException {
        var source = Files.readString(SWEEPER, StandardCharsets.UTF_8);

        assertThat(source)
                .as("보호 구간 기본값을 숫자로 적지 마세요. 관리 화면 상한과 따로 적히는 순간, "
                        + "한쪽을 넓혀도 다른 쪽이 따라가지 않아 운영자가 읽던 대화가 지워집니다.")
                .contains("chat.protect-window-minutes:\" + AdminChatService.MAX_WINDOW_MINUTES");
    }
}
