package com.ssafy.d205.domain.chat.entity;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;

/**
 * 채팅 금칙어 판정 (S15P21D205-1027).
 *
 * <p>목록은 파일이 아니라 여기서 직접 넣습니다. 실제 목록이 바뀔 때마다 테스트가 깨지면
 * 목록을 고치기 어려워지고, 여기서 고정하려는 것은 <b>판정 방식</b>이지 목록의 내용이 아닙니다.
 *
 * <p>닉네임 검사와 갈리는 두 지점을 중점적으로 봅니다 - 공백·구두점 끼우기를 잡는지, 그리고
 * 영문 낱말 경계를 보는지입니다. 두 번째가 깨지면 "Analyst" 같은 멀쩡한 말이 가려집니다.
 */
class ChatBlocklistTest {

    private final ChatBlocklist list = new ChatBlocklist(
            List.of("시발", "병신", "shit", "fuck"),
            List.of("시발점", "shiitake"));

    @Test
    @DisplayName("평범한 말은 통과한다")
    void plainMessagePasses() {
        assertThat(list.isForbidden("어디 숨었어")).isFalse();
        assertThat(list.isForbidden("나 여기 있어")).isFalse();
    }

    @Test
    @DisplayName("그대로 쓴 욕은 걸린다")
    void plainProfanityHits() {
        assertThat(list.isForbidden("야 이 시발아")).isTrue();
        assertThat(list.isForbidden("병신인가")).isTrue();
    }

    @Test
    @DisplayName("공백과 구두점을 끼워 넣어도 걸린다")
    void symbolsInsertedStillHits() {
        assertThat(list.isForbidden("시 발")).isTrue();
        assertThat(list.isForbidden("시.발")).isTrue();
        assertThat(list.isForbidden("시-발")).isTrue();
    }

    @Test
    @DisplayName("숫자로 바꿔 써도 걸린다")
    void leetStillHits() {
        assertThat(list.isForbidden("sh1t")).isTrue();
        assertThat(list.isForbidden("5h1t")).isTrue();
    }

    @Test
    @DisplayName("영문은 낱말 경계를 보므로 멀쩡한 말이 걸리지 않는다")
    void asciiNeedsWordBoundary() {
        assertThat(list.isForbidden("shitake mushroom")).isFalse();
        assertThat(list.isForbidden("bullshitting")).isFalse();
        assertThat(list.isForbidden("that is shit")).isTrue();
        assertThat(list.isForbidden("shit!")).isTrue();
    }

    @Test
    @DisplayName("허용 목록의 말은 통과한다")
    void allowlistPasses() {
        assertThat(list.isForbidden("시발점이 어디야")).isFalse();
    }

    @Test
    @DisplayName("걸린 말만 가리고 나머지는 그대로 둔다")
    void maskReplacesOnlyTheWord() {
        assertThat(list.mask("야 이 시발아")).isEqualTo("야 이 **아");
        assertThat(list.mask("that is shit")).isEqualTo("that is ****");
    }

    @Test
    @DisplayName("걸리지 않은 말은 손대지 않는다")
    void maskLeavesCleanMessage() {
        assertThat(list.mask("어디 숨었어")).isEqualTo("어디 숨었어");
    }

    @Test
    @DisplayName("우회한 말은 어느 글자가 문제인지 특정할 수 없으므로 전체를 가린다")
    void maskCoversEverythingWhenEvaded() {
        // 일부만 가리면 남은 글자로 무슨 말이었는지 그대로 읽힙니다.
        assertThat(list.mask("시 발")).isEqualTo("***");
        assertThat(list.mask("sh1t")).isEqualTo("****");
    }

    @Test
    @DisplayName("숫자와 공백을 겹쳐 끼워도 걸린다")
    void combinedEvasionStillHits() {
        // 한쪽만 푼 판본에는 아무것도 걸리지 않습니다. 조합을 만들어 봐야 잡힙니다.
        assertThat(list.isForbidden("시1 발")).isTrue();
        assertThat(list.isForbidden("s.h.1.t")).isTrue();
    }

    @Test
    @DisplayName("소문자로 바꿀 때 길이가 변하는 글자가 섞여도 터지지 않는다")
    void maskSurvivesLengthChangingLowercase() {
        // U+0130(İ)은 String.toLowerCase 에서 두 글자가 됩니다. 그 문자열의 위치로 원문을
        // 가리면 자리가 밀려 예외가 납니다. 사람이 채팅에 넣을 수 있는 글자입니다.
        assertThat(list.mask("\u0130 시발")).isEqualTo("\u0130 **");
        assertThat(list.mask("\u0130\u0130\u0130")).isEqualTo("\u0130\u0130\u0130");
    }

    @Test
    @DisplayName("가린 뒤에도 길이가 같아 몇 글자였는지는 남는다")
    void maskKeepsLength() {
        String masked = list.mask("병신아");
        assertThat(masked).hasSameSizeAs("병신아");
    }
}
