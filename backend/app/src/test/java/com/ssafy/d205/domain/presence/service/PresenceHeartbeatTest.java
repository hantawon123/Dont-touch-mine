package com.ssafy.d205.domain.presence.service;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

import java.time.Duration;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatCode;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.ssafy.d205.domain.presence.entity.PresenceTimeout;

/**
 * 하트비트 갱신 간격이 만료 판정보다 짧아야 한다는 규칙을 고정합니다.
 *
 * <p>스프링을 띄우지 않습니다. 규칙은 숫자 둘의 비교이고, 잘못된 설정으로 컨텍스트가
 * 안 뜨는 것을 보는 테스트는 그 비교를 확인하는 데 30초를 씁니다.
 */
class PresenceHeartbeatTest {

    /**
     * 애너테이션 인자는 컴파일 상수여야 해서 리터럴로 씁니다. 아래
     * {@link #theLiteralMatchesTheRealTimeout()} 이 PresenceTimeout 과 어긋나는 날 잡습니다.
     */
    private static final long TIMEOUT_MS = 90_000;

    @Test
    @DisplayName("이 테스트의 90초 리터럴은 실제 만료 판정과 같다")
    void theLiteralMatchesTheRealTimeout() {
        assertThat(PresenceTimeout.TIMEOUT.toMillis()).isEqualTo(TIMEOUT_MS);
    }

    @ParameterizedTest(name = "{0}ms")
    @ValueSource(longs = { 1_000, 30_000, 60_000, 89_999 })
    @DisplayName("타임아웃보다 짧은 간격은 통과한다")
    void shorterThanTimeoutIsAccepted(long refreshMs) {
        assertThatCode(() -> PresenceHeartbeat.requireShorterThanTimeout(refreshMs))
                .doesNotThrowAnyException();
    }

    @ParameterizedTest(name = "{0}ms")
    @ValueSource(longs = { 90_000, 90_001, 120_000, 600_000 })
    @DisplayName("타임아웃 이상이면 기동을 막는다")
    void timeoutOrLongerIsRefused(long refreshMs) {
        // 이 값으로 두면 갱신 사이마다 접속자가 오프라인으로 읽히는데, 로그에는 아무것도
        // 남지 않습니다. 기동 실패가 그 실수를 보이게 하는 유일한 자리입니다.
        assertThatThrownBy(() -> PresenceHeartbeat.requireShorterThanTimeout(refreshMs))
                .isInstanceOf(IllegalStateException.class)
                .hasMessageContaining("heartbeat-refresh-ms")
                .hasMessageContaining(String.valueOf(TIMEOUT_MS));
    }

    @Test
    @DisplayName("한 시간 이상은 끈 것으로 보고 검사하지 않는다")
    void anHourOrMoreMeansDisabled() {
        // application-test.yml 이 스케줄러를 끄는 방법이 한 시간으로 늘리는 것입니다.
        // 그 값이 기동을 막으면 모든 통합 테스트가 죽습니다.
        long disabled = PresenceHeartbeat.DISABLED_AT.toMillis();

        assertThatCode(() -> PresenceHeartbeat.requireShorterThanTimeout(disabled))
                .doesNotThrowAnyException();
        assertThatCode(() -> PresenceHeartbeat.requireShorterThanTimeout(Duration.ofHours(24).toMillis()))
                .doesNotThrowAnyException();

        // 경계 바로 아래는 여전히 실수로 봅니다.
        assertThatThrownBy(() -> PresenceHeartbeat.requireShorterThanTimeout(disabled - 1))
                .isInstanceOf(IllegalStateException.class);
    }
}
