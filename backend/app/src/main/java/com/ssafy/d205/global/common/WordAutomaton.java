package com.ssafy.d205.global.common;

import java.util.ArrayDeque;
import java.util.ArrayList;
import java.util.Deque;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/**
 * 금칙어 전부를 한 번에 찾는 아호-코라식 자동자 (S15P21D205-1048).
 *
 * <p>{@link WordMatcher#containsWord} 는 말 하나마다 문자열을 한 번씩 훑으므로 목록 크기에
 * 비례해 느려집니다. 목록이 41개일 때는 메시지당 0.001ms 였는데 3860개가 되자 0.77ms 가 됐습니다.
 * 여기서는 목록을 트라이 하나로 만들고 실패 링크를 걸어 <b>메시지를 한 번만 훑습니다.</b> 목록이
 * 몇 개든 판정 시간은 메시지 길이에만 달립니다.
 *
 * <p><b>판정 규칙은 {@link WordMatcher#containsWord} 와 글자 하나까지 같아야 합니다.</b> 같은
 * 메시지를 게임 서버는 가리고 백엔드는 기록하는데, 둘의 판정이 갈리면 "가려졌다고 기록된 말을
 * 사람들은 그대로 본" 상태가 됩니다. 그래서 두 구현이 같은 답을 내는지 확인하는 시험을 함께 둡니다
 * (WordAutomatonTest). 규칙을 고칠 일이 생기면 두 곳을 같이 고치고 그 시험으로 확인합니다.
 *
 * <p>영문은 앞뒤가 영문·숫자가 아닐 때만 걸리고 그 밖은 포함되기만 하면 걸리는데, 이 차이를
 * 마디마다 나눠 들고 있습니다. 한글은 낱말 경계가 없어 포함 검사밖에 할 수 없고, 영문에 경계를
 * 두지 않으면 "anal" 이 "Analyst" 를 잡습니다.
 *
 * <p>허용 목록은 여기서 다루지 않습니다. 부르는 쪽이 먼저 지우고 넘깁니다.
 */
public final class WordAutomaton {

    private static final int[] NONE = new int[0];

    private final Node root = new Node();

    private WordAutomaton() {
    }

    /** 소문자로 맞춰 둔 목록을 받습니다. 비어 있으면 아무것도 걸리지 않는 자동자가 됩니다. */
    public static WordAutomaton of(List<String> words) {
        WordAutomaton automaton = new WordAutomaton();
        for (String word : words) {
            if (word != null && !word.isEmpty()) {
                automaton.add(word);
            }
        }
        automaton.link();
        return automaton;
    }

    /**
     * 이 글에 금칙어가 들어 있는가. {@link WordMatcher#containsWord} 와 같은 답을 냅니다.
     */
    public boolean containsWord(String candidate) {
        Node node = root;
        for (int index = 0; index < candidate.length(); index++) {
            node = step(node, candidate.charAt(index));
            if (node.endsFree) {
                return true;
            }
            for (int length : node.asciiLengths) {
                if (standsAlone(candidate, index - length + 1, index)) {
                    return true;
                }
            }
        }
        return false;
    }

    /** 마디 수. 시험과 로그용입니다. */
    public int size() {
        return count(root);
    }

    private void add(String word) {
        Node node = root;
        for (int index = 0; index < word.length(); index++) {
            node = node.next.computeIfAbsent(word.charAt(index), key -> new Node());
        }
        if (isAscii(word)) {
            node.ownAscii.add(word.length());
        } else {
            node.ownFree = true;
        }
    }

    /**
     * 실패 링크를 걸고, 그 링크를 타고 닿는 말까지 마디마다 합쳐 둡니다.
     *
     * <p>합쳐 두지 않으면 판정할 때마다 실패 링크를 거슬러 올라가야 합니다. 너비 우선으로 짓는
     * 동안 부모 쪽이 이미 합쳐져 있으므로 한 번씩만 합치면 됩니다.
     */
    private void link() {
        Deque<Node> queue = new ArrayDeque<>();
        root.fail = root;
        for (Node child : root.next.values()) {
            child.fail = root;
            queue.add(child);
        }
        while (!queue.isEmpty()) {
            Node node = queue.poll();
            node.endsFree = node.ownFree || node.fail.endsFree;
            node.asciiLengths = merge(node.ownAscii, node.fail.asciiLengths);
            for (Map.Entry<Character, Node> entry : node.next.entrySet()) {
                Node fail = node.fail;
                while (fail != root && !fail.next.containsKey(entry.getKey())) {
                    fail = fail.fail;
                }
                Node target = fail.next.get(entry.getKey());
                entry.getValue().fail = (target == null || target == entry.getValue()) ? root : target;
                queue.add(entry.getValue());
            }
        }
    }

    private Node step(Node from, char letter) {
        Node node = from;
        while (node != root && !node.next.containsKey(letter)) {
            node = node.fail;
        }
        Node target = node.next.get(letter);
        return target == null ? root : target;
    }

    /** 앞뒤가 영문·숫자가 아니어야 걸립니다. 한글이 붙어 있는 것은 경계로 봅니다. */
    private static boolean standsAlone(String candidate, int from, int to) {
        boolean openLeft = from == 0 || !isAsciiLetterOrDigit(candidate.charAt(from - 1));
        boolean openRight = to + 1 == candidate.length() || !isAsciiLetterOrDigit(candidate.charAt(to + 1));
        return openLeft && openRight;
    }

    private static boolean isAsciiLetterOrDigit(char letter) {
        return (letter >= 'a' && letter <= 'z') || (letter >= 'A' && letter <= 'Z')
                || (letter >= '0' && letter <= '9');
    }

    private static boolean isAscii(String word) {
        for (int index = 0; index < word.length(); index++) {
            if (word.charAt(index) > 0x7F) {
                return false;
            }
        }
        return true;
    }

    private static int[] merge(List<Integer> own, int[] inherited) {
        if (own.isEmpty()) {
            return inherited;
        }
        List<Integer> all = new ArrayList<>(own);
        for (int length : inherited) {
            if (!all.contains(length)) {
                all.add(length);
            }
        }
        int[] lengths = new int[all.size()];
        for (int index = 0; index < all.size(); index++) {
            lengths[index] = all.get(index);
        }
        return lengths;
    }

    private static int count(Node node) {
        int total = 1;
        for (Node child : node.next.values()) {
            total += count(child);
        }
        return total;
    }

    private static final class Node {
        private final Map<Character, Node> next = new HashMap<>();
        private final List<Integer> ownAscii = new ArrayList<>();
        private Node fail;
        private boolean ownFree;

        /** 경계를 보지 않아도 되는 말이 여기서 끝나는가. 실패 링크로 닿는 것까지 셉니다. */
        private boolean endsFree;

        /** 경계를 봐야 하는 말들의 길이. 실패 링크로 닿는 것까지 합쳐 둡니다. */
        private int[] asciiLengths = NONE;
    }
}
