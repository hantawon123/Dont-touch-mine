package com.ssafy.d205.global.common;

/**
 * 금칙어를 두벌식 자판 표기로 바꿉니다 (S15P21D205-1081).
 *
 * <p>한때 자모 우회를 되돌리는 네 가지가 여기 함께 있었습니다. 같은 말을 다르게 쓴 한글을 한
 * 꼴로 돌려놓고 판정하는 것이었는데, 그 판정이 게임 서버로 모이면서
 * ({@code ChatBlocklist.cs}, S15P21D205-1096) 여기 남을 이유가 없어졌습니다. 어느 계열을 왜
 * 넣고 무엇을 오탐 때문에 뺐는지는 그쪽 {@code Hangul} 에 옮겨 적혀 있습니다.
 *
 * <p>자판 표기만 남습니다. 이쪽은 판정이 아니라 <b>목록을 만드는</b> 일이고, 목록을 가진 곳이
 * 백엔드이기 때문입니다.
 */
public final class HangulShapes {

    private static final char SYLLABLE_FIRST = 0xAC00;
    private static final char SYLLABLE_LAST = 0xD7A3;

    /** 두벌식 자판. 초성 19개·중성 21개·종성 28개와 자리를 맞춰 둡니다. 쉬프트를 누르는 글자는 소문자로 적습니다. */
    private static final String[] LEAD_KEYS = {
            "r", "r", "s", "e", "e", "f", "a", "q", "q", "t", "t", "d", "w", "w", "c", "z", "x", "v", "g"
    };
    private static final String[] VOWEL_KEYS = {
            "k", "o", "i", "o", "j", "p", "u", "p", "h", "hk", "ho", "hl", "y",
            "n", "nj", "np", "nl", "b", "m", "ml", "l"
    };
    private static final String[] TAIL_KEYS = {
            "", "r", "r", "rt", "s", "sw", "sg", "e", "f", "fr", "fa", "fq", "ft", "fx",
            "fv", "fg", "a", "q", "qt", "t", "t", "d", "w", "c", "z", "x", "v", "g"
    };

    private HangulShapes() {
    }

    /**
     * 금칙어를 두벌식 자판 표기로 바꿉니다. "시발" -> "tlqkf"
     *
     * <p>한영 전환을 하지 않고 친 글입니다. 판정에서 되돌리는 쪽은 오탐이 57줄이라 뺐고,
     * 대신 목록을 만들 때 이것으로 미리 만들어 둡니다. 목록에 넣는 쪽은 오탐이 없습니다 -
     * 만들어진 말은 영문이라 게임 서버가 낱말 경계까지 봅니다.
     *
     * <p>음절로만 된 말에만 씁니다. 그 밖에는 빈 문자열을 돌려주고, 부르는 쪽이 버립니다.
     */
    public static String toKeyboard(String word) {
        StringBuilder keys = new StringBuilder(word.length() * 3);
        for (int index = 0; index < word.length(); index++) {
            char letter = word.charAt(index);
            if (!isSyllable(letter)) {
                return "";
            }
            int code = letter - SYLLABLE_FIRST;
            keys.append(LEAD_KEYS[code / 588]);
            keys.append(VOWEL_KEYS[(code % 588) / 28]);
            keys.append(TAIL_KEYS[code % 28]);
        }
        return keys.toString();
    }

    private static boolean isSyllable(char letter) {
        return letter >= SYLLABLE_FIRST && letter <= SYLLABLE_LAST;
    }
}
