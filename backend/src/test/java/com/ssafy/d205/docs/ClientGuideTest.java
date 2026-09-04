package com.ssafy.d205.docs;

import jakarta.validation.constraints.Size;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.io.IOException;
import java.lang.reflect.Field;
import java.nio.file.Files;
import java.nio.file.Path;
import java.time.Instant;
import java.util.Arrays;
import java.util.List;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

import static java.nio.charset.StandardCharsets.UTF_8;
import static org.assertj.core.api.Assertions.assertThat;

import com.ssafy.d205.domain.presence.dto.UpdatePresenceRequest;
import com.ssafy.d205.domain.presence.entity.PresenceStatus;
import com.ssafy.d205.domain.presence.entity.PresenceTimeout;
import com.ssafy.d205.domain.invite.entity.InviteExpiry;
import com.ssafy.d205.domain.invite.entity.RoomCodePolicy;
import com.ssafy.d205.global.common.Timestamps;

/**
 * 클라이언트 안내 문서가 코드와 어긋나지 않는지 봅니다.
 *
 * <p>엔드포인트 목록은 OpenApiSpecTest 가 자동 생성으로 해결했지만, 문서에는 코드에서
 * 손으로 옮긴 값들이 남습니다 — 오류 코드, 접속 상태 이름, 타임아웃, 시각 형식,
 * sessionId 길이. 이것들은 <b>새 값이 추가될 때 문서가 조용히 빠뜨립니다.</b> 손으로
 * 유지하는 목록을 없애자던 이유가 그대로 여기에도 적용됩니다.
 *
 * <p><b>문구가 아니라 값이 있는지만 봅니다.</b> 문장을 다시 써도 깨지지 않고, 코드에 새
 * 값이 생기면 잡힙니다. 문서의 표현까지 고정하면 글을 손볼 때마다 테스트가 막습니다.
 *
 * <p>스프링을 띄우지 않습니다. 이 저장소의 다른 테스트는 모두 IntegrationTest 를
 * 상속하지만, 여기서 하는 일은 텍스트 파일을 읽고 상수를 보는 것뿐이라 MySQL 컨테이너와
 * 애플리케이션 컨텍스트가 필요 없습니다.
 */
class ClientGuideTest {

    /** 테스트의 작업 디렉터리는 backend/ 입니다(Gradle 기본값). */
    private static final Path GUIDE = Path.of("docs", "client-guide.md");
    private static final Path HANDLER = Path.of("src", "main", "java", "com", "ssafy", "d205",
            "global", "exception", "GlobalExceptionHandler.java");

    @Test
    @DisplayName("오류 코드가 전부 문서화되어 있다")
    void everyErrorCodeIsDocumented() throws IOException {
        List<String> codes = errorCodesInHandler();

        // 핸들러에서 코드를 못 뽑았는데 통과하는 것을 막습니다.
        assertThat(codes).hasSizeGreaterThan(5);

        assertThat(guide())
                .as("새 오류 코드를 추가했으면 docs/client-guide.md 의 표에도 넣으세요. "
                        + "클라이언트는 code 로 분기하므로 표에 없는 코드는 처리되지 않습니다.")
                .contains(codes);
    }

    @Test
    @DisplayName("접속 상태 이름이 전부 문서화되어 있다")
    void everyPresenceStatusIsDocumented() throws IOException {
        List<String> names = Arrays.stream(PresenceStatus.values()).map(Enum::name).toList();

        assertThat(guide())
                .as("PresenceStatus 에 값을 추가했으면 문서에도 넣으세요.")
                .contains(names);
    }

    @Test
    @DisplayName("하트비트 타임아웃 값이 문서와 같다")
    void timeoutMatchesDocument() throws IOException {
        // 문서는 이 값을 근거로 "30초마다 보내면 두 번 놓쳐도 버틴다"고 말합니다.
        // 서버 값만 바꾸면 그 설명이 틀리게 됩니다.
        String seconds = PresenceTimeout.TIMEOUT.toSeconds() + "초";

        assertThat(guide())
                .as("PresenceTimeout.TIMEOUT 이 " + seconds + " 로 바뀌었습니다. 문서의 "
                        + "하트비트 주기 설명도 함께 고쳐야 합니다.")
                .contains(seconds);
    }

    @Test
    @DisplayName("초대 만료 시간이 문서와 같다")
    void inviteLifetimeMatchesDocument() throws IOException {
        // 문서는 이 값을 근거로 "로비에서 기다리는 동안은 유효하다"고 말합니다.
        // 서버 값만 바꾸면 그 설명이 조용히 틀리게 됩니다.
        String minutes = InviteExpiry.LIFETIME.toMinutes() + "분";

        assertThat(guide())
                .as("InviteExpiry.LIFETIME 이 " + minutes + " 로 바뀌었습니다. "
                        + "문서의 초대 절도 함께 고쳐야 합니다.")
                .contains(minutes);
    }

    @Test
    @DisplayName("방 코드 길이가 문서와 같다")
    void roomCodeLengthMatchesDocument() throws IOException {
        String length = RoomCodePolicy.LENGTH + "자";

        assertThat(guide())
                .as("RoomCodePolicy.LENGTH 가 " + length + " 로 바뀌었습니다. 문서도 고치세요.")
                .contains(length);
    }

    @Test
    @DisplayName("시각 형식이 문서와 같다")
    void timestampFormatMatchesDocument() throws IOException {
        // 문서에 C# 파싱 예제가 있습니다. 형식이 바뀌면 그 예제가 조용히 틀립니다.
        String formatted = Timestamps.format(Instant.EPOCH);

        assertThat(formatted)
                .as("시각 형식이 14자리 숫자가 아니게 되었습니다.")
                .matches("\\d{14}");
        assertThat(formatted).isEqualTo("19700101000000");
        assertThat(guide()).contains("yyyyMMddHHmmss");
    }

    @Test
    @DisplayName("sessionId 길이 제한이 문서와 같다")
    void sessionIdLimitMatchesDocument() throws IOException {
        int max = sessionIdMaxLength();

        assertThat(guide())
                .as("sessionId 길이 제한이 " + max + " 로 바뀌었습니다. 문서도 고치세요.")
                .contains(String.valueOf(max));
    }

    private static String guide() throws IOException {
        return Files.readString(GUIDE, UTF_8);
    }

    /**
     * 핸들러 소스에서 코드 문자열을 뽑습니다.
     *
     * <p>소스를 읽는 것이 좋은 방법은 아닙니다. 코드가 enum 이면 리플렉션으로 깔끔하게
     * 얻을 수 있지만, 지금은 핸들러 안의 문자열 리터럴이라 다른 방법이 없습니다.
     * enum 으로 바꾸는 것은 핸들러 열네 곳을 고치는 별개 작업입니다.
     *
     * <p>핸들러를 리팩터링하면 이 테스트가 먼저 깨집니다. 조용히 통과하는 것보다 낫습니다.
     */
    private static List<String> errorCodesInHandler() throws IOException {
        Matcher m = Pattern.compile("ErrorResponse\\(\"(\\w+)\"")
                .matcher(Files.readString(HANDLER, UTF_8));

        return m.results().map(r -> r.group(1)).distinct().toList();
    }

    /**
     * 레코드 컴포넌트가 아니라 필드에서 읽습니다.
     *
     * <p>@Size 의 @Target 에 RECORD_COMPONENT 가 없습니다(METHOD, FIELD, ANNOTATION_TYPE,
     * CONSTRUCTOR, PARAMETER, TYPE_USE). 그래서 레코드 컴포넌트에 붙여도 컴파일러가
     * 필드·접근자·생성자 파라미터로 전파하고, <b>RecordComponent.getAnnotation 은 null 을
     * 돌려줍니다.</b> 처음 그렇게 썼다가 이 테스트가 잡았습니다.
     */
    private static int sessionIdMaxLength() {
        for (Field field : UpdatePresenceRequest.class.getDeclaredFields()) {
            Size size = field.getAnnotation(Size.class);
            if (size != null) {
                return size.max();
            }
        }
        throw new AssertionError("UpdatePresenceRequest 에 @Size 가 없습니다. 제한이 사라졌다면 "
                + "문서의 sessionId 설명도 고쳐야 합니다.");
    }
}
