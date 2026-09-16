package com.ssafy.d205.domain.chat.entity;

import org.springframework.stereotype.Component;

import java.util.List;

import com.ssafy.d205.global.common.WordMatcher;

/**
 * 채팅에 들어가면 안 되는 말 (S15P21D205-1027).
 *
 * <p>닉네임 목록({@link com.ssafy.d205.domain.user.entity.NicknameBlocklist})과 <b>목록을 나눕니다.</b>
 * 검사 방식은 거의 같지만 들어오는 글이 다릅니다.
 *
 * <table>
 *   <tr><th></th><th>닉네임</th><th>채팅</th></tr>
 *   <tr><td>공백</td><td>정규식이 금지</td><td>있음</td></tr>
 *   <tr><td>구두점</td><td>정규식이 금지</td><td>있음</td></tr>
 *   <tr><td>낱말 경계</td><td>없음</td><td>영문은 있음</td></tr>
 * </table>
 *
 * <p>그래서 두 가지가 달라집니다. <b>첫째</b>, 공백과 구두점을 끼워 넣는 우회("시 발", "시.발")가
 * 여기서만 생기므로 그것을 지운 판본을 하나 더 만들어 봅니다. <b>둘째</b>, 영문은 낱말 경계를
 * 볼 수 있으므로 닉네임 목록에서 오탐 때문에 뺐던 짧은 영어 욕설을 이 목록에는 넣을 수 있습니다.
 *
 * <p>한글 자모만 쓴 욕("ㅅㅂ")은 정규화로 잡는 것이 아니라 목록에 그대로 한 줄 넣습니다. 자모를
 * 음절로 되돌리는 규칙은 사람이 실제로 쓰는 몇 개보다 훨씬 복잡하고, 되돌리다 멀쩡한 말을
 * 만들어 냅니다.
 *
 * <p><b>목록은 서버 밖으로 나가지 않습니다.</b> 게임 서버만 기동할 때 내부 경로로 받아 가고,
 * 그 경로는 공유 키로 잠겨 있으며 nginx 에 없습니다. 플레이어 빌드에 목록이 들어가면 그 순간
 * 우회 목록이 됩니다.
 */
@Component
public class ChatBlocklist {

    static final String BLOCKLIST = "chat-blocklist.txt";
    static final String ALLOWLIST = "chat-allowlist.txt";

    /** 가릴 때 쓰는 글자. 길이는 원래 말과 같게 두어 몇 글자였는지가 남습니다. */
    private static final char MASK = '*';

    private final List<String> blocked;
    private final List<String> allowed;

    public ChatBlocklist() {
        this(WordMatcher.read(BLOCKLIST), WordMatcher.read(ALLOWLIST));
    }

    /** 테스트가 목록을 직접 넣을 수 있게 열어 둡니다. 소문자로 맞춰 저장합니다. */
    ChatBlocklist(List<String> blocked, List<String> allowed) {
        this.blocked = blocked.stream().map(WordMatcher::lower).toList();
        this.allowed = allowed.stream().map(WordMatcher::lower).toList();
    }

    /**
     * 이 말에 금칙어가 들어 있는가.
     *
     * <p>소문자로 바꾼 것, 숫자를 뺀 것, 숫자를 글자로 바꾼 것, 공백과 구두점을 지운 것 넷 중
     * 하나에라도 걸리면 참입니다. 각각 "시발", "시1발", "sh1t", "시 발" 을 잡습니다.
     */
    public boolean isForbidden(String message) {
        if (message == null || message.isEmpty()) {
            return false;
        }
        String lower = WordMatcher.lower(message);
        return hits(lower)
                || hits(lower.replaceAll("[0-9]", ""))
                || hits(WordMatcher.leet(lower))
                || hits(WordMatcher.stripSymbols(WordMatcher.leet(lower)));
    }

    /**
     * 금칙어가 있으면 가린 말을, 없으면 받은 말을 그대로 돌려줍니다.
     *
     * <p><b>통째로 막지 않습니다.</b> 막으면 보낸 사람은 보냈다고 생각하고 상대는 못 봐서 대화가
     * 어긋납니다. 가리면 보낸 사람을 포함해 전원이 같은 화면을 보고, 자기 말이 걸렸다는 것도 압니다.
     *
     * <p>가리는 것은 원문에 그대로 보이는 말뿐입니다. 숫자나 구두점으로 우회한 것은 걸러내되
     * 어느 글자가 문제였는지 특정하기 어려우므로, 그런 메시지는 전체를 가립니다. 어설프게 일부만
     * 가리면 남은 글자로 무슨 말이었는지 그대로 읽힙니다.
     */
    public String mask(String message) {
        if (!isForbidden(message)) {
            return message;
        }
        String lower = WordMatcher.lower(message);
        StringBuilder masked = new StringBuilder(message);
        boolean maskedAny = false;
        for (String bad : blocked) {
            int from = lower.indexOf(bad);
            while (from >= 0) {
                for (int index = from; index < from + bad.length(); index++) {
                    masked.setCharAt(index, MASK);
                }
                maskedAny = true;
                from = lower.indexOf(bad, from + bad.length());
            }
        }
        return maskedAny ? masked.toString() : String.valueOf(MASK).repeat(message.length());
    }

    /**
     * 게임 서버에 내려줄 목록. 같은 판정을 내려야 하므로 허용 목록도 함께 나갑니다.
     *
     * <p>소문자로 맞춰 둔 값을 그대로 줍니다. 게임 서버도 소문자로 비교하므로 양쪽이 같은
     * 입력을 보게 됩니다.
     */
    public List<String> words() {
        return blocked;
    }

    public List<String> allowed() {
        return allowed;
    }

    /** 채팅은 영문 낱말 경계를 볼 수 있으므로 닉네임과 다른 방법을 씁니다. */
    private boolean hits(String candidate) {
        return WordMatcher.containsWord(candidate, blocked, allowed);
    }
}
