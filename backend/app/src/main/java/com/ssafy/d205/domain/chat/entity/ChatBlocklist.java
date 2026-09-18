package com.ssafy.d205.domain.chat.entity;

import org.springframework.stereotype.Component;

import java.util.List;

import com.ssafy.d205.global.common.WordAutomaton;
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
 * <p><b>가리는 일은 여기서 하지 않습니다.</b> 게임 서버가 중계하면서 가린 말을 뿌리고 원문을
 * 여기로 보냅니다(S15P21D205-1028). 이 클래스는 저장할 때 masked 플래그를 다시 판정하는 데만
 * 쓰입니다. 같은 규칙을 두 언어로 두면 갈라지므로 가리는 코드는 한 곳에만 둡니다.
 *
 * <p>게임 서버가 지켜야 할 규칙이 셋입니다. <b>첫째</b>, 메시지를 통째로 막지 않고 가립니다 -
 * 막으면 보낸 사람은 보냈다고 생각하고 상대는 못 봐서 대화가 어긋납니다. <b>둘째</b>, 원문에
 * 그대로 보이는 말만 그 자리를 가리고, 숫자나 기호로 우회한 것은 어느 글자가 문제인지 특정할 수
 * 없으므로 메시지 전체를 가립니다 - 일부만 가리면 남은 글자로 무슨 말이었는지 읽힙니다.
 * <b>셋째</b>, 가린 뒤에도 길이를 유지해 몇 글자였는지는 남깁니다.
 *
 * <p><b>자리를 셀 때 주의할 것이 있습니다.</b> 소문자로 바꾼 문자열에서 찾은 위치를 원문에
 * 그대로 쓰면 안 됩니다. U+0130(İ) 처럼 소문자화에서 길이가 변하는 글자가 섞이면 자리가 밀려
 * 엉뚱한 글자를 지우거나 예외가 납니다. 사람이 채팅에 넣을 수 있는 글자라 실제로 터집니다.
 * 자리를 셀 때는 글자 하나를 글자 하나로 낮추는 방식을 씁니다.
 *
 * <p><b>목록은 서버 밖으로 나가지 않습니다.</b> 게임 서버만 기동할 때 내부 경로로 받아 가고,
 * 그 경로는 공유 키로 잠겨 있으며 nginx 에 없습니다. 플레이어 빌드에 목록이 들어가면 그 순간
 * 우회 목록이 됩니다.
 */
@Component
public class ChatBlocklist {

    static final String BLOCKLIST = "chat-blocklist.txt";
    static final String ALLOWLIST = "chat-allowlist.txt";

    private final List<String> blocked;
    private final List<String> allowed;
    private final WordAutomaton automaton;

    public ChatBlocklist() {
        this(WordMatcher.read(BLOCKLIST), WordMatcher.read(ALLOWLIST));
    }

    /** 테스트가 목록을 직접 넣을 수 있게 열어 둡니다. 소문자로 맞춰 저장합니다. */
    ChatBlocklist(List<String> blocked, List<String> allowed) {
        this.blocked = blocked.stream().map(WordMatcher::lower).toList();
        this.allowed = allowed.stream().map(WordMatcher::lower).toList();
        // 기동할 때 한 번 짓습니다. 목록은 기동 뒤에 바뀌지 않습니다.
        this.automaton = WordAutomaton.of(this.blocked);
    }

    /**
     * 이 말에 금칙어가 들어 있는가.
     *
     * <p>우회 수단이 서로 <b>겹쳐서</b> 들어옵니다. 숫자만 끼우거나 공백만 끼우는 것은 각각
     * 한 번의 변환으로 풀리지만, "시1 발" 처럼 둘을 같이 쓰면 한쪽만 푼 판본에는 아무것도
     * 걸리지 않습니다. 그래서 조합까지 만들어 봅니다.
     *
     * <ul>
     *   <li>소문자 그대로 - "시발"</li>
     *   <li>숫자를 뺀 것 - "시1발"</li>
     *   <li>숫자를 글자로 바꾼 것 - "sh1t"</li>
     *   <li>숫자를 빼고 기호까지 지운 것 - "시1 발"</li>
     *   <li>숫자를 글자로 바꾸고 기호까지 지운 것 - "s.h.1.t"</li>
     * </ul>
     */
    public boolean isForbidden(String message) {
        if (message == null || message.isEmpty()) {
            return false;
        }
        String lower = WordMatcher.lower(message);
        String withoutDigits = lower.replaceAll("[0-9]", "");
        String leet = WordMatcher.leet(lower);
        return hits(lower)
                || hits(withoutDigits)
                || hits(leet)
                || hits(WordMatcher.stripSymbols(withoutDigits))
                || hits(WordMatcher.stripSymbols(leet));
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

    /**
     * 채팅은 영문 낱말 경계를 볼 수 있으므로 닉네임과 다른 방법을 씁니다.
     *
     * <p>판정은 {@link WordAutomaton} 이 합니다. 목록이 3800개를 넘으면서 말마다 문자열을 훑는
     * 방법이 메시지당 0.77ms 가 됐는데, 자동자는 메시지를 한 번만 훑으므로 목록 크기와 무관합니다.
     * 허용 목록을 먼저 지우는 것은 그대로입니다.
     */
    private boolean hits(String candidate) {
        return automaton.containsWord(WordMatcher.strip(candidate, allowed));
    }
}
