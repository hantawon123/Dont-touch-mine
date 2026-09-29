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
 * <p>맞춰 보는 쪽을 쓰는 것은 <b>닉네임 검사 하나</b>입니다(S15P21D205-1017). 채팅도 한동안
 * 같은 방식을 썼는데 판정이 게임 서버로 모이면서({@code ChatBlocklist.cs}, S15P21D205-1096)
 * 여기서는 목록을 읽어 주는 {@link #read} 와 소문자화만 쓰게 됐습니다.
 *
 * <p>닉네임은 <b>포함 검사만</b> 할 수 있습니다. 공백이 없어 단어 경계가 없기 때문입니다.
 * 경계를 요구하면 "xxf4ckxx" 를 놓칩니다. 오탐은 허용 목록으로 갚습니다.
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

    /** 허용 목록의 말을 먼저 지웁니다. */
    private static String strip(String candidate, List<String> allowed) {
        String stripped = candidate;
        for (String ok : allowed) {
            stripped = stripped.replace(ok, "");
        }
        return stripped;
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
