package com.ssafy.d205.global.common;

import org.springframework.core.io.ClassPathResource;

import java.io.IOException;
import java.io.UncheckedIOException;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

/**
 * 금칙어 목록을 읽고 맞춰 보는 공용 부분입니다.
 *
 * <p>닉네임 검사(S15P21D205-1017)가 먼저 있었고 채팅 검사(S15P21D205-1027)가 같은 방식을 쓰게
 * 되면서, 두 곳이 각자 소문자화와 leet 치환을 들고 있지 않도록 여기로 뺐습니다. 목록 자체는
 * 합치지 않습니다. 두 곳의 입력 성격이 달라서 들어가는 말이 다릅니다
 * ({@link com.ssafy.d205.domain.chat.entity.ChatBlocklist} 주석 참고).
 *
 * <p><b>맞춰 보는 방법이 두 가지입니다.</b> 닉네임은 공백이 없어 단어 경계가 없으므로 포함 검사만
 * 할 수 있고, 채팅은 공백이 있으므로 영문에 한해 경계를 볼 수 있습니다. 섞어 쓰면 한쪽이 망가집니다 -
 * 닉네임에 경계를 요구하면 "xxf4ckxx" 를 놓치고, 채팅에 포함 검사만 하면 "Analyst" 같은 멀쩡한
 * 말이 걸립니다.
 */
public final class WordMatcher {

    private WordMatcher() {
    }

    /** 숫자를 모양이 비슷한 글자로. 사람이 욕을 숨길 때 실제로 쓰는 여섯 가지만 다룹니다. */
    public static String leet(String lower) {
        return lower.replace('0', 'o').replace('1', 'i').replace('3', 'e')
                .replace('4', 'a').replace('5', 's').replace('7', 't');
    }

    public static String lower(String value) {
        return value.toLowerCase(Locale.ROOT);
    }

    /**
     * 글자와 숫자가 아닌 것을 모두 뺍니다. 공백·구두점을 끼워 넣는 우회("시 발", "시.발")를
     * 잡습니다. 닉네임에는 필요 없습니다 - 정규식이 이미 그런 글자를 막습니다.
     */
    public static String stripSymbols(String value) {
        StringBuilder kept = new StringBuilder(value.length());
        for (int index = 0; index < value.length(); index++) {
            char letter = value.charAt(index);
            if (Character.isLetterOrDigit(letter)) {
                kept.append(letter);
            }
        }
        return kept.toString();
    }

    /**
     * 포함 검사. 허용 목록의 말을 먼저 지운 뒤 금칙어를 찾으므로 "Analyst" 가 "anal" 에 걸리지
     * 않습니다. 단어 경계가 없는 닉네임이 쓰는 방법입니다.
     */
    public static boolean contains(String candidate, List<String> blocked, List<String> allowed) {
        String stripped = strip(candidate, allowed);
        for (String bad : blocked) {
            if (stripped.contains(bad)) {
                return true;
            }
        }
        return false;
    }

    /**
     * 영문 낱말은 경계를 보고, 그 밖은 포함으로 봅니다. 채팅이 쓰는 방법입니다.
     *
     * <p>한글에는 낱말 경계가 없어서 규칙을 하나로 둘 수 없습니다. "시발" 은 "시발점" 안에도
     * 들어 있지만 그런 오탐은 허용 목록으로 막고, 영문은 경계를 요구해 "anal" 이 "Analyst" 에
     * 걸리지 않게 합니다. 경계 덕분에 닉네임 목록에서 오탐 때문에 뺐던 짧은 영어 욕설을
     * 채팅 목록에는 넣을 수 있습니다.
     *
     * <p><b>채팅 판정은 이제 {@link WordAutomaton} 이 합니다.</b> 목록이 3800개를 넘으면서 말마다
     * 훑는 이 방법이 메시지당 0.77ms 가 됐기 때문입니다. 이 메서드는 지우지 않고 남겨 둡니다.
     * 자동자가 같은 답을 내는지 확인하는 시험이 이것을 기준으로 삼습니다(WordAutomatonTest).
     * 규칙을 고칠 일이 생기면 둘을 같이 고치고 그 시험으로 확인합니다.
     */
    public static boolean containsWord(String candidate, List<String> blocked, List<String> allowed) {
        String stripped = strip(candidate, allowed);
        for (String bad : blocked) {
            if (isAscii(bad) ? hasStandaloneAscii(stripped, bad) : stripped.contains(bad)) {
                return true;
            }
        }
        return false;
    }

    /** 허용 목록의 말을 먼저 지웁니다. 자동자를 쓰는 쪽도 이 단계는 그대로 거칩니다. */
    public static String strip(String candidate, List<String> allowed) {
        String stripped = candidate;
        for (String ok : allowed) {
            stripped = stripped.replace(ok, "");
        }
        return stripped;
    }

    private static boolean isAscii(String word) {
        for (int index = 0; index < word.length(); index++) {
            if (word.charAt(index) > 0x7F) {
                return false;
            }
        }
        return true;
    }

    /** 앞뒤가 영문·숫자가 아니어야 걸립니다. 한글이 붙어 있는 것은 경계로 봅니다. */
    private static boolean hasStandaloneAscii(String candidate, String word) {
        int from = candidate.indexOf(word);
        while (from >= 0) {
            boolean openLeft = from == 0 || !isAsciiLetterOrDigit(candidate.charAt(from - 1));
            int after = from + word.length();
            boolean openRight = after == candidate.length() || !isAsciiLetterOrDigit(candidate.charAt(after));
            if (openLeft && openRight) {
                return true;
            }
            from = candidate.indexOf(word, from + 1);
        }
        return false;
    }

    private static boolean isAsciiLetterOrDigit(char letter) {
        return (letter >= 'a' && letter <= 'z') || (letter >= 'A' && letter <= 'Z')
                || (letter >= '0' && letter <= '9');
    }

    /**
     * 목록 파일을 읽습니다. 한 줄에 하나, {@code #} 뒤는 주석, 빈 줄은 무시합니다.
     *
     * <p>파일이 없으면 기동을 막습니다. 검사가 조용히 꺼진 채로 떠서 아무도 모르는 것보다 낫습니다.
     */
    public static List<String> read(String resource) {
        try {
            String text = new ClassPathResource(resource).getContentAsString(StandardCharsets.UTF_8);
            List<String> words = new ArrayList<>();
            for (String raw : text.split("\\R")) {
                int hash = raw.indexOf('#');
                String line = (hash >= 0 ? raw.substring(0, hash) : raw).strip();
                if (!line.isEmpty()) {
                    words.add(line);
                }
            }
            return words;
        } catch (IOException e) {
            throw new UncheckedIOException("금칙어 목록을 읽을 수 없습니다: " + resource, e);
        }
    }
}
