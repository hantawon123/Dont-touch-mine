package com.ssafy.d205.docs;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Arrays;
import java.util.List;

import static java.nio.charset.StandardCharsets.UTF_8;
import static org.assertj.core.api.Assertions.assertThat;

import com.ssafy.d205.domain.analytics.entity.GameEventName;

/**
 * 이벤트 명세 문서와 서버 화이트리스트가 같은지 봅니다.
 *
 * <p>둘이 어긋나는 방향은 둘입니다. 문서에 이벤트를 추가했는데 서버가 모르면 클라이언트가 보낸
 * 배치가 전부 400 이고, 서버에 추가했는데 문서에 없으면 아무도 그 이벤트를 보내지 않습니다.
 * 어느 쪽이든 조용히 지나가므로 여기서 잡습니다.
 *
 * <p>ClientGuideTest 와 같은 방식으로 문구가 아니라 값만 봅니다.
 */
class AnalyticsEventsDocTest {

    private static final Path SPEC = Path.of("docs", "analytics-events.md");

    @Test
    @DisplayName("서버가 받는 이벤트 이름이 전부 명세 문서에 있다")
    void everyEventNameIsDocumented() throws IOException {
        String spec = Files.readString(SPEC, UTF_8);
        List<String> names = Arrays.stream(GameEventName.values())
                .map(name -> "`" + name.wire() + "`")
                .toList();

        assertThat(spec)
                .as("GameEventName 에 값을 추가했으면 docs/analytics-events.md 5절 표에도 넣으세요.")
                .contains(names);
    }

    @Test
    @DisplayName("문서가 말하는 이벤트 개수가 서버 목록과 같다")
    void eventCountMatchesDocument() throws IOException {
        String spec = Files.readString(SPEC, UTF_8);

        assertThat(spec)
                .as("문서의 '이벤트 목록 (N개)' 와 GameEventName 개수가 다릅니다.")
                .contains("이벤트 목록 (" + GameEventName.values().length + "개)");
    }
}
