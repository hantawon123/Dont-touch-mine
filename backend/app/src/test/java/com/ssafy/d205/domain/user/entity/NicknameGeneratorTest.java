package com.ssafy.d205.domain.user.entity;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.HashSet;
import java.util.Set;

import static org.assertj.core.api.Assertions.assertThat;


/**
 * 서버가 지어 주는 임시 닉네임 (S15P21D205-1017).
 *
 * <p>두 가지를 전수로 봅니다. 모든 단어 조합이 닉네임 규칙을 통과하는가 - 5글자 단어를 넣으면 서버가
 * 만든 이름이 변경 API 에서 거부됩니다. 그리고 실제 금칙어 파일에 걸리지 않는가 - 단어를 사람이 골라
 * 넣으므로 조합이 욕이 되는 일은 없어야 하고, 이 테스트가 그 파일을 실제로 읽어 그 사실을 확인합니다.
 */
class NicknameGeneratorTest {

    @Test
    @DisplayName("모든 단어 조합이 닉네임 규칙과 금칙어를 통과한다")
    void everyCombinationIsAValidNickname() {
        NicknameBlocklist blocklist = new NicknameBlocklist();

        for (String adjective : NicknameGenerator.ADJECTIVES) {
            for (String noun : NicknameGenerator.NOUNS) {
                String longest = adjective + noun + "9999";
                assertThat(NicknamePolicy.isValid(longest)).as(longest).isTrue();
                assertThat(blocklist.isForbidden(longest)).as(longest).isFalse();
            }
        }
    }

    @Test
    @DisplayName("단어는 4글자 이하다 - 12자 상한의 근거")
    void wordsAreAtMostFourLetters() {
        for (String word : NicknameGenerator.ADJECTIVES) {
            assertThat(word.length()).as(word).isBetween(3, 4);
        }
        for (String word : NicknameGenerator.NOUNS) {
            assertThat(word.length()).as(word).isBetween(3, 4);
        }
    }

    @Test
    @DisplayName("만든 이름은 영문 두 단어와 숫자 넷이다")
    void generatedNamesLookLikeBoldFox4821() {
        NicknameGenerator generator = new NicknameGenerator();
        Set<String> seen = new HashSet<>();

        for (int i = 0; i < 200; i++) {
            String name = generator.generate();
            assertThat(name).matches("^[A-Z][a-z]{2,3}[A-Z][a-z]{2,3}[0-9]{4}$");
            seen.add(name);
        }

        // 200 번 뽑아 절반 넘게 겹치면 난수가 아니라 상수입니다.
        assertThat(seen.size()).isGreaterThan(100);
    }
}
