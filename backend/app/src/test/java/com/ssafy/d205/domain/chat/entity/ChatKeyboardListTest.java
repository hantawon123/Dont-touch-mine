package com.ssafy.d205.domain.chat.entity;

import com.ssafy.d205.global.common.HangulShapes;
import com.ssafy.d205.global.common.WordMatcher;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;
import java.util.Set;
import java.util.TreeSet;

import static java.nio.charset.StandardCharsets.UTF_8;
import static org.assertj.core.api.Assertions.assertThat;

/**
 * 금칙어를 두벌식 자판 표기로 만들어 chat-blocklist-keyboard.txt 와 맞춰 봅니다 (S15P21D205-1081).
 *
 * <p>한영 전환을 하지 않고 친 글("tlqkf")이 그대로 나가던 것을 막습니다. 판정에서 자판을
 * 되돌리는 쪽도 만들어 봤지만 영문이 든 말이 통째로 사정권에 들어와 오탐이 57줄이었습니다.
 * 방향을 뒤집어 <b>목록을 미리 만들어 두면</b> 만들어진 말이 영문이라 자동자가 낱말 경계까지
 * 보고, 오탐이 없습니다.
 *
 * <p>다르면 새 목록을 파일에 써 두고 실패합니다. 금칙어를 추가한 뒤 이 테스트가 한 번 실패하는
 * 것은 정상이고, 갱신된 파일을 커밋하면 통과합니다. OpenApiSpecTest 와 같은 방식입니다 -
 * 조용히 고쳐 두면 목록 변경이 커밋에 섞여 들어가 MR 에서 눈에 띄지 않습니다.
 */
class ChatKeyboardListTest {

    /** 테스트의 작업 디렉터리는 backend/ 입니다(Gradle 기본값). */
    private static final Path FILE = Path.of("app", "src", "main", "resources", ChatBlocklist.KEYBOARD);

    /**
     * 네 글자 미만은 버립니다.
     *
     * <p>짧을수록 영어와 부딪힙니다. "좆"은 "wht" 가 되는데 채팅에서 "what" 을 줄여 쓰는 말과
     * 같습니다. 두세 글자를 건지자고 멀쩡한 말을 가리는 것보다 버리는 편이 낫습니다.
     */
    private static final int SHORTEST = 4;

    /**
     * 만들어 놓고 보니 영어 낱말인 것들. 넣으면 그 말을 쓴 사람이 가려집니다.
     *
     * <p>만들어진 1582개를 저장소 전체(영문 포함)에 돌려 부딪히는 것이 없는지 보고, 그와 별개로
     * 다섯 글자 이하를 눈으로 훑어 골랐습니다. 늘어나면 여기에 한 줄씩 적습니다.
     */
    private static final Set<String> EXCLUDED = Set.of("roto", "rotor", "tori");

    private static final String HEADER = """
            # 금칙어를 두벌식 자판 표기로 옮긴 것 (S15P21D205-1081). ChatKeyboardListTest 가 만듭니다.
            #
            # 한영 전환 없이 친 글을 막습니다. "시발" 을 그대로 치면 "tlqkf" 가 됩니다.
            #
            # 손으로 고치지 마세요. chat-blocklist.txt 를 고치고 ChatKeyboardListTest 를 돌리면
            # 이 파일이 다시 만들어집니다. 여기에 손으로 적은 줄은 그때 사라집니다.
            #
            # 쉬프트를 누르는 글자는 소문자로 적습니다. 판정이 소문자로 맞춰 놓고 비교하므로
            # "씨발"(Tlqkf)과 "시발"(tlqkf)은 같은 줄이 됩니다.
            """;

    @Test
    @DisplayName("커밋된 자판 표기 목록이 금칙어 목록과 일치한다")
    void committedListMatchesBlocklist() throws Exception {
        String generated = generate();

        // 자판 표기가 하나도 없는데 파일도 비어서 통과하는 것을 막습니다.
        assertThat(generated)
                .as("자판 표기가 만들어지지 않았습니다. 금칙어 목록을 읽었는지 확인하세요.")
                .contains("tlqkf");

        String committed = Files.exists(FILE) ? Files.readString(FILE, UTF_8) : "";
        if (!generated.equals(committed)) {
            Files.writeString(FILE, generated, UTF_8);
        }

        assertThat(committed)
                .as(ChatBlocklist.KEYBOARD + " 이 금칙어 목록과 다릅니다. 방금 갱신해 두었으니 그 파일을 커밋하세요.")
                .isEqualTo(generated);
    }

    @Test
    @DisplayName("자판 표기로 친 욕이 걸린다")
    void keyboardFormsAreForbidden() {
        ChatBlocklist list = new ChatBlocklist();

        assertThat(list.isForbidden("tlqkf")).isTrue();
        assertThat(list.isForbidden("야 tlqkf 아")).isTrue();
        assertThat(list.isForbidden("qudtls")).isTrue();
    }

    @Test
    @DisplayName("영문 낱말 안에 든 자판 표기는 걸리지 않는다")
    void keyboardFormsNeedToStandAlone() {
        ChatBlocklist list = new ChatBlocklist();

        // 만들어진 말은 영문이므로 자동자가 앞뒤를 봅니다. 이것이 깨지면 멀쩡한 영어가 걸립니다.
        assertThat(list.isForbidden("xxtlqkfxx")).isFalse();
        assertThat(list.isForbidden("hello world")).isFalse();
        assertThat(list.isForbidden("spawn point")).isFalse();
    }

    private String generate() {
        // 정렬해 둡니다. 목록이 바뀔 때 diff 가 바뀐 줄만 보이게 하려는 것입니다.
        Set<String> keys = new TreeSet<>();
        for (String word : WordMatcher.read(ChatBlocklist.BLOCKLIST)) {
            String typed = HangulShapes.toKeyboard(WordMatcher.lower(word));
            if (typed.length() >= SHORTEST && !EXCLUDED.contains(typed)) {
                keys.add(typed);
            }
        }
        return HEADER + "\n" + String.join("\n", List.copyOf(keys)) + "\n";
    }
}
