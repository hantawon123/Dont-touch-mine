package com.ssafy.d205.global.common;

import java.text.Normalizer;

/**
 * 같은 말을 다르게 쓴 한글을 한 꼴로 되돌립니다 (S15P21D205-1081).
 *
 * <p>기호와 숫자를 끼워 넣는 우회는 {@link WordMatcher#stripSymbols} 와 숫자 치환이 막고 있었지만
 * 자모를 쓰는 우회는 통째로 비어 있었습니다. {@code ㅣ} 는 기호가 아니라 글자라
 * ({@code Character.isLetterOrDigit} 이 참을 냅니다) 기호를 지우는 단계가 그대로 남겨 두기
 * 때문입니다. 여섯 개를 넣어야 뚫리는 것이 아니라 <b>한 글자면 충분했습니다</b> - "시ㅣ발".
 *
 * <p>여기 있는 네 가지가 각각 다른 계열을 맡습니다. 하나로 합칠 수 없습니다.
 *
 * <table>
 *   <tr><th>메서드</th><th>막는 것</th><th>못 막는 것</th></tr>
 *   <tr><td>{@link #stripJamo}</td><td>시ㅣ발, 시ㅇ발, 시ㅋㅋ발</td><td>ㅅㅣ발 - 자모를 다 지워 "발"만 남습니다</td></tr>
 *   <tr><td>{@link #rejoin}</td><td>ㅅㅣ발, 시ㅂㅏㄹ, ㅂㅕㅇ신</td><td>시ㅋㅋ발 - ㅋ 이 받침으로 붙어 "싴발"이 됩니다</td></tr>
 *   <tr><td>{@link #digitAsVowel}</td><td>ㅅ1발, ㅅl발, ㅂ1ㅇ신</td><td>시bal - 로마자 혼용은 아래 참고</td></tr>
 *   <tr><td>{@link #unstretch}</td><td>시이이발, 씨이이발</td><td>시바알 - 이미 다른 판본이 잡습니다</td></tr>
 * </table>
 *
 * <p><b>넣지 않은 것이 셋입니다.</b> 셋 다 만들어서 저장소의 한글 문장 16430줄에 돌려 보고 뺐습니다.
 *
 * <ul>
 *   <li>겹친 자모를 하나로 줄이기 - 새 오탐 12줄. "롤리팝"이 "로리팝"이 됩니다. 이것이 잡으려던
 *       "시ㅣㅣㅣ발"은 {@link #stripJamo} 가 이미 잡으므로 필요가 없었습니다.</li>
 *   <li>늘인 모음을 한 번만 나와도 줄이기 - 새 오탐은 0줄이지만 더 잡히는 것도 "꺼어져",
 *       "닥아쳐" 둘뿐입니다. "시이발"이나 "개애새끼"는 이미 다른 판본이 잡습니다. 반면
 *       "강아지"가 "강지"가 되는 성질은 그대로 남아, 목록에 두 글자짜리 말이 늘면 언제든
 *       오탐이 됩니다. 얻는 것이 작고 잃을 수 있는 것이 커서 두 번 이상일 때만 봅니다.</li>
 *   <li>로마자를 자모로 바꿔 보기 - 새 오탐 80줄. "시bal"은 잡히지만 영문이 든 말이 통째로
 *       사정권에 들어옵니다. 한영 혼용은 조합이 무한하므로 목록에 직접 적는 편이 낫습니다.</li>
 * </ul>
 *
 * <p>자판 우회("tlqkf")도 판정에 넣어 봤다가 오탐 57줄로 뺐습니다. 대신 방향을 뒤집어
 * {@link #toKeyboard} 로 <b>목록을 자판 표기로 미리 만들어</b> 둡니다. 자동자는 메시지 길이만큼만
 * 훑으므로 목록이 늘어도 판정 비용이 늘지 않습니다.
 */
public final class HangulShapes {

    /** 초성 19개. 자리가 곧 초성 번호입니다. */
    public static final String LEAD = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";

    /** 중성 21개. */
    public static final String VOWEL = "ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ";

    /** 종성 28개. 0번은 받침이 없는 자리라 쓰이지 않는 글자를 채워 둡니다. */
    public static final String TAIL = "_ㄱㄲㄳㄴㄵㄶㄷㄹㄺㄻㄼㄽㄾㄿㅀㅁㅂㅄㅅㅆㅇㅈㅊㅋㅌㅍㅎ";

    private static final char SYLLABLE_FIRST = 0xAC00;
    private static final char SYLLABLE_LAST = 0xD7A3;

    /** 두벌식 자판. 위 세 표와 자리를 맞춰 둡니다. 쉬프트를 누르는 글자는 소문자로 적습니다. */
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

    public static boolean isSyllable(char letter) {
        return letter >= SYLLABLE_FIRST && letter <= SYLLABLE_LAST;
    }

    /** 홀로 선 자모인가. 호환 자모와 조합용 자모를 모두 봅니다. */
    public static boolean isJamo(char letter) {
        return (letter >= 0x3131 && letter <= 0x318E) || (letter >= 0x1100 && letter <= 0x11FF)
                || (letter >= 0xA960 && letter <= 0xA97C) || (letter >= 0xD7B0 && letter <= 0xD7FB);
    }

    /**
     * 홀로 선 자모를 지웁니다. "시ㅣㅣㅣ발" -> "시발"
     *
     * <p>먼저 NFC 로 모읍니다. 조합용 자모로 적힌 "시발"이 자모만 남기고 사라지지 않게 하려는
     * 것입니다. 붙을 수 있는 것은 음절이 되고, 그러고도 남은 것만 지웁니다.
     */
    public static String stripJamo(String value) {
        String composed = Normalizer.normalize(value, Normalizer.Form.NFC);
        StringBuilder kept = new StringBuilder(composed.length());
        for (int index = 0; index < composed.length(); index++) {
            char letter = composed.charAt(index);
            if (!isJamo(letter)) {
                kept.append(letter);
            }
        }
        return kept.toString();
    }

    /**
     * 음절을 자모로 풀었다가 다시 붙입니다. "ㅅㅣ발" -> "시발", "ㅂㅕㅇ신" -> "병신"
     *
     * <p>푼 다음 붙이는 이유는, 사람이 쓴 자모와 음절 안에 든 자모를 <b>같은 자리에 놓고</b>
     * 보기 위해서입니다. 풀지 않으면 "ㅅ" 과 "시" 안의 초성이 서로 다른 글자로 남습니다.
     *
     * <p>붙지 못하고 남은 자모는 버립니다. 남겨 두면 "ㅅㅣ발"이 "시ㅅ발" 같은 꼴이 되어
     * 아무것도 걸리지 않습니다.
     */
    public static String rejoin(String value) {
        return join(flatten(value));
    }

    /**
     * 홀로 선 자음 바로 뒤의 숫자·라틴 한 글자를 모음으로 보고 붙입니다. "ㅅ1발" -> "시발"
     *
     * <p><b>앞 글자가 홀로 선 자음일 때만</b> 바꿉니다. 그냥 "한글에 붙은 숫자"로 두면
     * "롤1인칭"이 "로리인칭"이 되어 "로리"에 걸립니다. 자음 뒤라는 조건이 그 오탐을 없앱니다 -
     * 사람이 자모를 숫자로 흉내 내는 자리가 바로 거기입니다.
     */
    public static String digitAsVowel(String value) {
        char[] letters = value.toCharArray();
        for (int index = 1; index < letters.length; index++) {
            char vowel = lookalikeVowel(letters[index]);
            if (vowel != 0 && LEAD.indexOf(letters[index - 1]) >= 0) {
                letters[index] = vowel;
            }
        }
        return rejoin(new String(letters));
    }

    /**
     * 앞 글자의 모음을 ㅇ 으로 늘여 쓴 것을 줄입니다. "시이이발" -> "시발"
     *
     * <p><b>두 번 이상 이어질 때만</b> 줄입니다. 한 번까지 줄이면 "강아지"가 "강지"가 되는데,
     * 그렇게 해서 더 잡히는 것은 "꺼어져", "닥아쳐" 정도뿐입니다. 한 번 늘인 "시이발"이나
     * "개애새끼"는 이미 다른 판본이 잡습니다.
     *
     * <p>받침이 있는 음절은 늘이기로 보지 않습니다. "가 알" 의 "알"을 지우면 ㄹ 이 앞으로 붙어
     * 없던 말이 생깁니다.
     */
    public static String unstretch(String value) {
        String composed = Normalizer.normalize(value, Normalizer.Form.NFC);
        StringBuilder kept = new StringBuilder(composed.length());
        StringBuilder held = new StringBuilder();
        char previousVowel = 0;
        for (int index = 0; index < composed.length(); index++) {
            char letter = composed.charAt(index);
            if (isSyllable(letter)) {
                int code = letter - SYLLABLE_FIRST;
                char lead = LEAD.charAt(code / 588);
                char vowel = VOWEL.charAt((code % 588) / 28);
                boolean stretched = lead == 'ㅇ' && vowel == previousVowel && code % 28 == 0;
                if (stretched) {
                    held.append(letter);
                    continue;
                }
                previousVowel = vowel;
            } else {
                previousVowel = 0;
            }
            flush(kept, held);
            kept.append(letter);
        }
        flush(kept, held);
        return rejoin(kept.toString());
    }

    /**
     * 금칙어를 두벌식 자판 표기로 바꿉니다. "시발" -> "tlqkf"
     *
     * <p>한영 전환을 하지 않고 친 글입니다. 판정에서 되돌리는 쪽은 오탐이 57줄이라 뺐고,
     * 대신 목록을 만들 때 이것으로 미리 만들어 둡니다. 목록에 넣는 쪽은 오탐이 없습니다 -
     * 만들어진 말은 영문이라 자동자가 낱말 경계까지 봅니다.
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

    /**
     * 음절을 자모로 풉니다. 자모가 아닌 글자는 그대로 둡니다.
     *
     * <p>NFKC 를 먼저 거칩니다. 전각으로 쓴 글자를 반각으로 모으려는 것인데, 이때 호환 자모가
     * 조합용 자모로 바뀌므로 {@link #toCompat} 으로 되돌립니다. 되돌리지 않으면 사람이 친
     * "ㅅ" 과 음절에서 푼 "ㅅ" 이 서로 다른 글자가 됩니다.
     */
    public static String flatten(String value) {
        String normalized = Normalizer.normalize(value, Normalizer.Form.NFKC);
        StringBuilder flat = new StringBuilder(normalized.length() * 3);
        for (int index = 0; index < normalized.length(); index++) {
            char letter = toCompat(normalized.charAt(index));
            if (isSyllable(letter)) {
                int code = letter - SYLLABLE_FIRST;
                flat.append(LEAD.charAt(code / 588));
                flat.append(VOWEL.charAt((code % 588) / 28));
                int tail = code % 28;
                if (tail > 0) {
                    flat.append(TAIL.charAt(tail));
                }
            } else {
                flat.append(letter);
            }
        }
        return flat.toString();
    }

    /**
     * 자모를 음절로 붙입니다. 붙지 못한 자모는 버리고, 자모가 아닌 글자는 그대로 둡니다.
     *
     * <p>받침을 가져갈지는 그다음 글자를 보고 정합니다. 뒤에 모음이 오면 그 자음은 다음 음절의
     * 초성이므로 받침으로 쓰면 안 됩니다 - "ㅂㅏㄹㅏ" 는 "발ㅏ" 가 아니라 "바라" 입니다.
     */
    public static String join(String jamo) {
        StringBuilder built = new StringBuilder(jamo.length());
        int index = 0;
        while (index < jamo.length()) {
            char letter = jamo.charAt(index);
            int lead = LEAD.indexOf(letter);
            int vowel = index + 1 < jamo.length() ? VOWEL.indexOf(jamo.charAt(index + 1)) : -1;
            if (lead >= 0 && vowel >= 0) {
                int tail = 0;
                if (index + 2 < jamo.length()) {
                    int candidate = TAIL.indexOf(jamo.charAt(index + 2));
                    boolean nextIsVowel = index + 3 < jamo.length()
                            && VOWEL.indexOf(jamo.charAt(index + 3)) >= 0;
                    if (candidate > 0 && !nextIsVowel) {
                        tail = candidate;
                    }
                }
                built.append((char) (SYLLABLE_FIRST + (lead * 21 + vowel) * 28 + tail));
                index += tail > 0 ? 3 : 2;
                continue;
            }
            if (!isJamo(letter)) {
                built.append(letter);
            }
            index++;
        }
        return built.toString();
    }

    /** 조합용 자모를 호환 자모로. NFKC 가 반대로 바꿔 놓은 것을 되돌립니다. */
    private static char toCompat(char letter) {
        if (letter >= 0x1100 && letter <= 0x1112) {
            return LEAD.charAt(letter - 0x1100);
        }
        if (letter >= 0x1161 && letter <= 0x1175) {
            return VOWEL.charAt(letter - 0x1161);
        }
        if (letter >= 0x11A8 && letter <= 0x11C2) {
            return TAIL.charAt(letter - 0x11A8 + 1);
        }
        return letter;
    }

    /** 모음을 흉내 내는 데 쓰는 숫자·라틴. 사람이 실제로 쓰는 것만 봅니다. */
    private static char lookalikeVowel(char letter) {
        return switch (letter) {
            case '1', 'l', 'i', '|' -> 'ㅣ';
            case '0', 'o' -> 'ㅗ';
            default -> 0;
        };
    }

    /** 늘인 것으로 본 음절이 두 개가 안 되면 늘이기가 아니었으므로 되돌려 놓습니다. */
    private static void flush(StringBuilder kept, StringBuilder held) {
        if (held.length() < 2) {
            kept.append(held);
        }
        held.setLength(0);
    }
}
