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
    @DisplayName("자모를 끼워 넣어도 걸린다")
    void insertedJamoStillHits() {
        // 여섯 개를 넣어야 뚫리는 것이 아니라 한 글자면 충분했습니다. ㅣ 는 기호가 아니라
        // 글자라 기호를 지우는 단계가 그대로 남겨 둡니다.
        assertThat(list.isForbidden("시ㅣ발")).isTrue();
        assertThat(list.isForbidden("시ㅣㅣㅣㅣ발")).isTrue();
        assertThat(list.isForbidden("시ㅇ발")).isTrue();
        assertThat(list.isForbidden("시ㅋㅋ발")).isTrue();
        assertThat(list.isForbidden("병ㅣ신")).isTrue();
        assertThat(list.isForbidden("fuㅡck")).isTrue();
    }

    @Test
    @DisplayName("음절을 자모로 풀어 써도 걸린다")
    void decomposedJamoStillHits() {
        assertThat(list.isForbidden("ㅅㅣ발")).isTrue();
        assertThat(list.isForbidden("시ㅂㅏㄹ")).isTrue();
        assertThat(list.isForbidden("ㅅㅣㅂㅏㄹ")).isTrue();
        assertThat(list.isForbidden("ㅂㅕㅇ신")).isTrue();
    }

    @Test
    @DisplayName("숫자로 자모를 흉내 내도 걸린다")
    void digitShapedJamoStillHits() {
        assertThat(list.isForbidden("ㅅ1발")).isTrue();
        assertThat(list.isForbidden("ㅅl발")).isTrue();
    }

    @Test
    @DisplayName("모음을 늘여 써도 걸린다")
    void stretchedVowelStillHits() {
        assertThat(list.isForbidden("시이이발")).isTrue();
        assertThat(list.isForbidden("시이이이이발")).isTrue();
    }

    @Test
    @DisplayName("자모를 되돌리다 멀쩡한 말을 만들지 않는다")
    void normalizingKeepsPlainWords() {
        // 되돌리기를 세게 잡으면 멀쩡한 말이 다른 말이 됩니다. 겹친 자모를 줄이면 "롤리팝"이
        // "로리팝"이 되고, 늘인 모음을 한 번만 나와도 줄이면 "강아지"가 "강지"가 됩니다.
        // 둘 다 만들어 보고 뺀 규칙이라, 그 말들이 그 자리에 그대로 있는지 봅니다.
        ChatBlocklist mangled = new ChatBlocklist(List.of("로리", "강지"), List.of());
        assertThat(mangled.isForbidden("롤리팝 먹자")).isFalse();
        assertThat(mangled.isForbidden("강아지 귀엽다")).isFalse();

        assertThat(list.isForbidden("1인칭 시점")).isFalse();
        assertThat(list.isForbidden("사이좋게 지내자")).isFalse();
        assertThat(list.isForbidden("미이라 같다")).isFalse();
        assertThat(list.isForbidden("ㅋㅋㅋㅋ 웃기다")).isFalse();
    }

    @Test
    @DisplayName("숫자와 공백을 겹쳐 끼워도 걸린다")
    void combinedEvasionStillHits() {
        // 우회 수단이 겹쳐 들어오면 한쪽만 푼 판본에는 아무것도 걸리지 않습니다. 조합을
        // 만들어 봐야 잡힙니다.
        assertThat(list.isForbidden("시1 발")).isTrue();
        assertThat(list.isForbidden("s.h.1.t")).isTrue();
    }
}
