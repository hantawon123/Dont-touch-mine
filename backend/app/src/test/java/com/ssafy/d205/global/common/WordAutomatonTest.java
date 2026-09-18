package com.ssafy.d205.global.common;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.ArrayList;
import java.util.List;
import java.util.Random;

import static org.assertj.core.api.Assertions.assertThat;

/**
 * 자동자가 {@link WordMatcher#containsWord} 와 같은 답을 내는지 봅니다 (S15P21D205-1048).
 *
 * <p>이 시험이 있어야 하는 이유는 성능이 아니라 <b>갈라짐</b> 때문입니다. 같은 메시지를 게임 서버는
 * 가리고 백엔드는 기록하는데 둘의 판정이 어긋나면, 사람들이 그대로 본 말이 "가려졌다"고 기록되거나
 * 그 반대가 됩니다. 규칙을 고칠 일이 생기면 두 구현을 같이 고치고 여기서 확인합니다.
 */
class WordAutomatonTest {

    private final List<String> blocked = WordMatcher.read("chat-blocklist.txt")
            .stream().map(WordMatcher::lower).toList();
    private final List<String> allowed = WordMatcher.read("chat-allowlist.txt")
            .stream().map(WordMatcher::lower).toList();
    private final WordAutomaton automaton = WordAutomaton.of(blocked);

    private void assertSame(String candidate) {
        boolean scanned = WordMatcher.containsWord(candidate, blocked, allowed);
        boolean matched = automaton.containsWord(WordMatcher.strip(candidate, allowed));
        assertThat(matched)
                .withFailMessage("판정이 갈립니다: [%s] 훑기=%s 자동자=%s", candidate, scanned, matched)
                .isEqualTo(scanned);
    }

    @Test
    @DisplayName("실제 목록의 모든 말에 대해 두 구현의 판정이 같다")
    void everyBlockedWordAgrees() {
        assertThat(blocked).hasSizeGreaterThan(1000);
        for (String word : blocked) {
            assertSame(word);
            assertSame("앞 " + word + " 뒤");
            assertSame(word + "합니다");
        }
    }

    @Test
    @DisplayName("평범한 문장과 우회 표기에서도 판정이 같다")
    void ordinaryAndEvasiveTextAgrees() {
        List<String> samples = List.of(
                "", " ", "여기 상자 뒤에 숨었어 빨리 와봐", "2026년에 다시 하자", "보지 못한 맵이야",
                "처음부터 다시 하자", "도착하기 전까지 기다려", "어려운지 확인해봐", "후다닥 뛰어와",
                "2등신 캐릭터 귀엽다", "시발점이 어디야", "강아지새끼 귀여워", "this game sucks",
                "analyst 모드 있나", "shiitake 버섯 맵", "test 서버로 가자", "ios 버전은 언제나와",
                "hell 난이도 클리어", "시발 뭐야", "시1발 렉", "시 발 이거 뭐야", "s.h.1.t",
                "you are a bitch", "b17ch", "@sshole", "carpet muncher", "ㅅㅂ 렉걸림",
                "fuckfuck", "xxfuckxx", "fuck", "FUCK", "....", "1234567890"
        );
        for (String sample : samples) {
            assertSame(sample);
        }
    }

    @Test
    @DisplayName("금칙어 조각을 섞어 만든 문자열에서도 판정이 같다")
    void randomMixturesAgree() {
        // 씨앗을 고정합니다. 실패하면 같은 입력으로 다시 돌려 볼 수 있어야 합니다.
        Random random = new Random(1048);
        String filler = "가나다라마바사아자차 abcdefg 0123 .,!? 여기저기";
        List<String> pieces = new ArrayList<>();
        for (int index = 0; index < 200; index++) {
            pieces.add(blocked.get(random.nextInt(blocked.size())));
        }
        for (int index = 0; index < 2000; index++) {
            StringBuilder text = new StringBuilder();
            int parts = 1 + random.nextInt(4);
            for (int part = 0; part < parts; part++) {
                if (random.nextBoolean()) {
                    String piece = pieces.get(random.nextInt(pieces.size()));
                    // 통째로 넣기도 하고 잘라 넣기도 합니다. 잘린 조각은 걸리면 안 됩니다.
                    text.append(random.nextInt(3) == 0 && piece.length() > 1
                            ? piece.substring(0, piece.length() - 1) : piece);
                } else {
                    int from = random.nextInt(filler.length());
                    text.append(filler, from, Math.min(filler.length(), from + 1 + random.nextInt(8)));
                }
            }
            assertSame(text.toString());
        }
    }

    @Test
    @DisplayName("빈 목록으로 지으면 아무것도 걸리지 않는다")
    void emptyListMatchesNothing() {
        WordAutomaton empty = WordAutomaton.of(List.of());
        assertThat(empty.containsWord("시발 fuck")).isFalse();
    }
}
