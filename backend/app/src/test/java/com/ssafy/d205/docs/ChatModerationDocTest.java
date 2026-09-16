package com.ssafy.d205.docs;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

import static org.assertj.core.api.Assertions.assertThat;

/**
 * docs/chat-moderation.md 가 코드와 어긋나지 않는지 봅니다.
 *
 * <p>이 문서가 특히 어긋나기 쉽습니다. 설명하는 대상이 <b>네 곳에 흩어져</b> 있습니다 - 계정
 * 서비스의 내부 경로와 청소 주기, 게임 서버가 읽는 환경변수, release host 설정입니다. 한 곳을
 * 고치면서 문서를 잊으면, 배포하는 사람은 문서를 보고 없는 이름으로 키를 설정합니다.
 *
 * <p>값을 여기 다시 적지 않고 원본에서 뽑습니다. 적어 두면 그 복사본과 문서를 비교하는 셈이라
 * 실제 코드가 달라져도 통과합니다. AdminGuideTest 가 경로를 SecurityConfig 에서 뽑는 것과 같은
 * 방식입니다.
 */
class ChatModerationDocTest {

    /** 테스트의 작업 디렉터리는 backend/ 입니다(Gradle 기본값). */
    private static final Path DOC = Path.of("docs", "chat-moderation.md");
    private static final Path CONTROLLER = Path.of("app", "src", "main", "java", "com", "ssafy", "d205",
            "domain", "chat", "internalapi", "InternalChatController.java");
    private static final Path SWEEPER = Path.of("app", "src", "main", "java", "com", "ssafy", "d205",
            "domain", "chat", "service", "ChatLogSweeper.java");

    /** {@code @Value("${chat.retention-days:3}")} 에서 이름과 기본값을 뽑습니다. */
    private static final Pattern SETTING = Pattern.compile("\\$\\{(chat\\.[a-z-]+):([^}]*)}");

    @Test
    @DisplayName("보관 설정의 이름과 기본값이 문서와 같다")
    void retentionSettingsMatch() throws IOException {
        var settings = settingsIn(SWEEPER);

        // 정규식이 아무것도 못 뽑았는데 통과하는 것을 막습니다.
        assertThat(settings)
                .as("ChatLogSweeper 에서 설정을 뽑지 못했습니다. @Value 모양이 바뀌었는지 보세요.")
                .hasSizeGreaterThanOrEqualTo(2);

        var doc = doc();
        for (var setting : settings) {
            assertThat(doc)
                    .as("설정 이름이나 기본값을 바꿨으면 docs/chat-moderation.md 의 표도 고치세요. "
                            + "배포하는 사람이 그 표를 보고 값을 넣습니다: " + setting)
                    .contains(setting.name())
                    .contains(setting.defaultValue());
        }
    }

    @Test
    @DisplayName("게임 서버만 부르는 경로가 문서에 있다")
    void internalPathsAreDocumented() throws IOException {
        var controller = Files.readString(CONTROLLER, StandardCharsets.UTF_8);

        assertThat(controller)
                .as("경로 상수를 찾지 못했습니다.")
                .contains("/internal/chat");

        assertThat(doc())
                .as("이 경로는 공개 명세에서 빼 두었으므로(@Hidden) 이 문서가 적어 둔 유일한 곳입니다. "
                        + "경로를 바꾸면 아무도 모르게 거짓말이 됩니다.")
                .contains("/internal/chat/blocklist")
                .contains("/internal/chat");
    }

    @Test
    @DisplayName("비밀 두 개의 이름이 문서에 있다")
    void secretNamesAreDocumented() throws IOException {
        // 이름이 틀리면 배포한 사람은 값을 넣었다고 생각하는데 게임 서버는 404 를 받습니다.
        assertThat(doc())
                .contains("CHAT_INTERNAL_KEY")
                .contains("D205_CHAT_KEY")
                .contains("chat_internal_key");
    }

    @Test
    @DisplayName("분석 수집 문서가 채팅을 따로 담는다고 알려준다")
    void analyticsDocPointsHere() throws IOException {
        // 둘을 뭉뚱그리면 "분석에 채팅이 있다" 는 오해가 생깁니다.
        assertThat(Files.readString(Path.of("docs", "match-analytics.md"), StandardCharsets.UTF_8))
                .contains("chat-moderation.md");
    }

    private String doc() throws IOException {
        return Files.readString(DOC, StandardCharsets.UTF_8);
    }

    private static List<Setting> settingsIn(Path source) throws IOException {
        Matcher matcher = SETTING.matcher(Files.readString(source, StandardCharsets.UTF_8));
        return matcher.results()
                .map(result -> new Setting(result.group(1), result.group(2)))
                .toList();
    }

    private record Setting(String name, String defaultValue) {
    }
}
