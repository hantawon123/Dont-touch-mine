package com.ssafy.d205.domain.chat.entity;

import org.springframework.stereotype.Component;

import java.util.ArrayList;
import java.util.List;

import com.ssafy.d205.global.common.WordMatcher;

/**
 * 채팅에 들어가면 안 되는 말 (S15P21D205-1027).
 *
 * <p><b>여기서는 판정하지 않습니다</b>(S15P21D205-1096). 목록을 읽어 들고 있다가 기동한 게임
 * 서버에 내려주는 일만 합니다. 가릴지 판정하고 실제로 가리는 것은 게임 서버이고
 * ({@code ChatBlocklist.cs}), 저장할 때도 그쪽이 낸 판정을 그대로 받아 적습니다. 한때 저장
 * 시점에 백엔드가 다시 판정했는데, 그러려면 같은 규칙을 Java 와 C# 두 곳에 두어야 하고 두
 * 목록이 갈리는 순간 기록이 "가려졌다고 적혔는데 사람들은 그대로 본" 상태가 됩니다. 규칙과
 * 그것을 고른 근거는 이제 게임 서버 쪽에만 적혀 있습니다.
 *
 * <p>닉네임 목록({@link com.ssafy.d205.domain.user.entity.NicknameBlocklist})과 <b>목록을 나눕니다.</b>
 * 들어오는 글이 달라서입니다.
 *
 * <table>
 *   <tr><th></th><th>닉네임</th><th>채팅</th></tr>
 *   <tr><td>공백</td><td>정규식이 금지</td><td>있음</td></tr>
 *   <tr><td>구두점</td><td>정규식이 금지</td><td>있음</td></tr>
 *   <tr><td>낱말 경계</td><td>없음</td><td>영문은 있음</td></tr>
 * </table>
 *
 * <p>그래서 채팅 목록에는 닉네임 목록에서 오탐 때문에 뺐던 짧은 영어 욕설이 들어 있습니다.
 * 게임 서버가 영문 낱말 경계를 보기 때문입니다. 공백·구두점·자모를 끼워 넣는 우회도 목록이
 * 아니라 그쪽 판정이 다룹니다.
 *
 * <p><b>목록은 서버 밖으로 나가지 않습니다.</b> 게임 서버만 기동할 때 내부 경로로 받아 가고,
 * 그 경로는 공유 키로 잠겨 있으며 nginx 에 없습니다. 플레이어 빌드에 목록이 들어가면 그 순간
 * 우회 목록이 됩니다.
 */
@Component
public class ChatBlocklist {

    static final String BLOCKLIST = "chat-blocklist.txt";
    static final String KEYBOARD = "chat-blocklist-keyboard.txt";
    static final String ALLOWLIST = "chat-allowlist.txt";

    private final List<String> blocked;
    private final List<String> allowed;

    public ChatBlocklist() {
        this(both(WordMatcher.read(BLOCKLIST), WordMatcher.read(KEYBOARD)), WordMatcher.read(ALLOWLIST));
    }

    /**
     * 손으로 적는 목록과 만들어 둔 자판 표기를 합칩니다.
     *
     * <p>파일을 가르는 이유는 한쪽이 <b>생성물</b>이기 때문입니다. 섞어 두면 사람이 자판 표기를
     * 손으로 고치고, 다음 생성 때 그 손질이 사라집니다. 만드는 쪽은 ChatKeyboardListTest 입니다.
     *
     * <p>한영 전환 없이 친 글("tlqkf")을 판정이 아니라 목록으로 막는 이유도 거기 적혀 있습니다.
     */
    private static List<String> both(List<String> typed, List<String> generated) {
        List<String> all = new ArrayList<>(typed.size() + generated.size());
        all.addAll(typed);
        all.addAll(generated);
        return all;
    }

    /** 테스트가 목록을 직접 넣을 수 있게 열어 둡니다. 소문자로 맞춰 저장합니다. */
    ChatBlocklist(List<String> blocked, List<String> allowed) {
        this.blocked = blocked.stream().map(WordMatcher::lower).toList();
        this.allowed = allowed.stream().map(WordMatcher::lower).toList();
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
}
