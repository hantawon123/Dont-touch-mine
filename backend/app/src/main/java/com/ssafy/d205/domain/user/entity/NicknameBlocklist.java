package com.ssafy.d205.domain.user.entity;

import org.springframework.stereotype.Component;

import java.util.List;

import com.ssafy.d205.global.common.WordMatcher;

/**
 * 닉네임에 들어가면 안 되는 말 (S15P21D205-1017).
 *
 * <p>{@link NicknamePolicy} 가 글자의 종류와 길이를 보고, 이 클래스가 뜻을 봅니다. 둘을 나눈 이유는
 * 정책은 정규식 한 줄이라 DTO 애너테이션에도 들어가지만 목록은 파일이고 바뀌기 때문입니다.
 *
 * <p><b>서버에서만 검사합니다.</b> 목록을 클라이언트에 내려보내지 않습니다. 빌드에 들어간 목록은
 * 곧 우회 목록이 되고, 갱신할 때마다 빌드가 필요해집니다. 클라이언트는 오류 코드 하나만 알면 됩니다.
 *
 * <p><b>비교 방법.</b> 정규식이 공백·특수문자를 이미 막으므로 남은 우회는 대소문자와 숫자 끼우기·치환
 * 둘입니다. 소문자로 바꾸고, 숫자를 전부 뺀 것과 숫자를 흔한 글자로 치환한 것(0→o 1→i 3→e 4→a 5→s
 * 7→t)을 각각 만들어 셋 중 하나에라도 목록의 말이 <b>포함</b>되면 거절합니다. "f4ck", "sh1t", "시1발"
 * 이 여기서 걸립니다. 한글 자모 분리("ㅅㅂ")는 정규식이 완성형만 받아 애초에 들어오지 않습니다.
 *
 * <p><b>포함 검사는 오탐이 있습니다.</b> 닉네임에 띄어쓰기가 없어 단어 경계가 없기 때문입니다.
 * 그래서 허용 목록이 따로 있습니다. 허용 목록의 말을 먼저 지운 뒤 금칙어를 찾으므로 "Analyst" 는
 * "anal" 에 걸리지 않습니다. 세 글자짜리 짧은 영어 욕설은 오탐이 너무 많아 처음 목록에서 뺐고,
 * 실제 신고가 들어오면 그때 추가합니다.
 *
 * <p>목록은 리소스 파일 두 개입니다. 한 줄에 하나, {@code #} 뒤는 주석, 빈 줄 무시. 바꾸면 배포가
 * 필요하지만 그 빈도면 DB 테이블과 관리 API 보다 낫습니다. 기동 때 한 번 읽습니다.
 */
@Component
public class NicknameBlocklist {

    static final String BLOCKLIST = "nickname-blocklist.txt";
    static final String ALLOWLIST = "nickname-allowlist.txt";

    private final List<String> blocked;
    private final List<String> allowed;

    public NicknameBlocklist() {
        this(WordMatcher.read(BLOCKLIST), WordMatcher.read(ALLOWLIST));
    }

    /** 테스트가 목록을 직접 넣을 수 있게 열어 둡니다. 소문자로 맞춰 저장합니다. */
    NicknameBlocklist(List<String> blocked, List<String> allowed) {
        this.blocked = blocked.stream().map(WordMatcher::lower).toList();
        this.allowed = allowed.stream().map(WordMatcher::lower).toList();
    }

    /** 이 닉네임을 거절해야 하는가. 정규식 검사를 통과한 값을 받는다고 가정합니다. */
    public boolean isForbidden(String nickname) {
        if (nickname == null || nickname.isEmpty()) {
            return false;
        }
        String lower = WordMatcher.lower(nickname);
        return hits(lower) || hits(lower.replaceAll("[0-9]", "")) || hits(leet(lower));
    }

    /**
     * 포함 검사입니다. 닉네임에는 공백이 없어 단어 경계가 없으므로 이 방법뿐이고, 그래서 허용
     * 목록이 필요합니다. 채팅은 경계를 볼 수 있어 다른 방법을 씁니다
     * ({@link WordMatcher#containsWord}).
     */
    private boolean hits(String candidate) {
        return WordMatcher.contains(candidate, blocked, allowed);
    }

    /** 숫자를 모양이 비슷한 글자로. 구현은 {@link WordMatcher} 에 있고 여기서는 이름만 빌려 씁니다. */
    static String leet(String lower) {
        return WordMatcher.leet(lower);
    }
}
