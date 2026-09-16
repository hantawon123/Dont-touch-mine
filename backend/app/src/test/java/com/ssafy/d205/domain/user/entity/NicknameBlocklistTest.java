package com.ssafy.d205.domain.user.entity;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;


/**
 * 닉네임 금칙어 비교 규칙 (S15P21D205-1017). 스프링 없이 목록을 직접 넣어 봅니다.
 *
 * <p>실제 리소스 파일의 내용은 여기서 고정하지 않습니다. 목록은 바뀌는 것이고, 바뀔 때마다 테스트를
 * 고치게 하면 아무도 목록을 안 고칩니다. 파일이 읽히는지는 {@link NicknameGeneratorTest} 가 실제
 * 컴포넌트를 만들어 확인합니다.
 */
class NicknameBlocklistTest {

    private final NicknameBlocklist list = new NicknameBlocklist(
            List.of("Fuck", "shit", "시발", "anal"),
            List.of("analyst", "canal"));

    @ParameterizedTest
    @DisplayName("대소문자, 숫자 끼우기, 숫자 치환을 넘어 찾는다")
    @ValueSource(strings = {"fuck", "FUCK", "FuckYou", "sh1t", "5hit", "F1u2c3k", "시발", "시1발", "개시발놈", "AnalBoy"})
    void findsThroughCommonDisguises(String nickname) {
        assertThat(list.isForbidden(nickname)).as(nickname).isTrue();
    }

    @ParameterizedTest
    @DisplayName("허용 목록의 말은 금칙어를 품고 있어도 통과한다")
    @ValueSource(strings = {"Analyst01", "GrandCanal", "canalyst"})
    void allowlistedWordsPass(String nickname) {
        assertThat(list.isForbidden(nickname)).as(nickname).isFalse();
    }

    @ParameterizedTest
    @DisplayName("멀쩡한 이름은 통과한다")
    @ValueSource(strings = {"BoldFox4821", "가나다Abc123456", "시바견", "Firetruck", "발시"})
    void ordinaryNamesPass(String nickname) {
        assertThat(list.isForbidden(nickname)).as(nickname).isFalse();
    }

    @Test
    @DisplayName("빈 값은 거절하지 않는다 - 그건 정규식의 일이다")
    void emptyIsNotForbidden() {
        assertThat(list.isForbidden(null)).isFalse();
        assertThat(list.isForbidden("")).isFalse();
    }

    @Test
    @DisplayName("치환은 여섯 글자만 다룬다")
    void leetMapsSixDigits() {
        assertThat(NicknameBlocklist.leet("0134579")).isEqualTo("oieast9");
    }
}
