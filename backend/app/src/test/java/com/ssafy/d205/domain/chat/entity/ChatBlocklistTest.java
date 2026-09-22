package com.ssafy.d205.domain.chat.entity;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.List;

import com.ssafy.d205.global.common.WordMatcher;

import static org.assertj.core.api.Assertions.assertThat;

/**
 * 게임 서버에 내려줄 채팅 금칙어 목록 (S15P21D205-1027).
 *
 * <p><b>판정은 여기서 보지 않습니다</b>(S15P21D205-1096). 한때 백엔드도 저장 시점에 다시
 * 판정했고 그 판정을 고정하는 케이스가 이 파일에 있었는데, 같은 케이스가 게임 서버 쪽
 * {@code ChatModerationTests.cs} 에 그대로 있습니다. 규칙이 한 곳에만 있으므로 그 시험도 한
 * 곳에만 둡니다. 여기서 보는 것은 <b>목록을 제대로 모아 주는지</b>입니다.
 */
class ChatBlocklistTest {

    @Test
    @DisplayName("목록을 소문자로 맞춰 들고 있다")
    void wordsAreLowercased() {
        // 게임 서버도 소문자로 비교하므로 양쪽이 같은 입력을 보게 해야 합니다.
        ChatBlocklist list = new ChatBlocklist(List.of("Shit", "FUCK"), List.of("Shiitake"));

        assertThat(list.words()).containsExactly("shit", "fuck");
        assertThat(list.allowed()).containsExactly("shiitake");
    }

    @Test
    @DisplayName("손으로 적은 목록과 만들어 둔 자판 표기를 합쳐서 준다")
    void bothFilesAreServed() {
        // 파일을 가르는 이유는 한쪽이 생성물이라 사람이 손대면 안 되기 때문입니다.
        // 합치는 것을 빠뜨리면 자판 우회("tlqkf")가 통째로 안 걸립니다.
        ChatBlocklist list = new ChatBlocklist();

        int typed = WordMatcher.read(ChatBlocklist.BLOCKLIST).size();
        int generated = WordMatcher.read(ChatBlocklist.KEYBOARD).size();

        assertThat(list.words()).hasSize(typed + generated);
        assertThat(list.words()).contains("시발", "tlqkf");
        assertThat(list.allowed()).contains("시발점");
    }
}
