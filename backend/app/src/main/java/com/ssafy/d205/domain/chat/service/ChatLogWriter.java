package com.ssafy.d205.domain.chat.service;

import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Component;
import org.springframework.transaction.annotation.Transactional;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

import com.ssafy.d205.domain.chat.dto.ChatLogBatchRequest;
import com.ssafy.d205.domain.chat.entity.ChatLog;
import com.ssafy.d205.domain.chat.repository.ChatLogRepository;
import com.ssafy.d205.domain.user.entity.User;
import com.ssafy.d205.domain.user.repository.UserRepository;

/**
 * 채팅 묶음을 표에 넣습니다 (S15P21D205-1027).
 *
 * <p>{@link ChatLogService} 에서 떼어 낸 이유는 트랜잭션 경계 하나뿐입니다. 금칙어 판정은 DB 를
 * 쓰지 않으므로 트랜잭션 밖에서 끝내고, 여기서는 DB 를 쓰는 일만 합니다. 같은 클래스의 메서드를
 * 부르면 프록시를 타지 않아 {@code @Transactional} 이 걸리지 않으므로 클래스를 나눕니다.
 */
@Component
@RequiredArgsConstructor
class ChatLogWriter {

    private final ChatLogRepository chatLogs;
    private final UserRepository users;

    /**
     * 줄을 만들어 한 번에 넣고 넣은 줄 수를 돌려줍니다.
     *
     * <p>계정을 못 찾은 줄도 버리지 않고 저장합니다. 대화의 맥락은 그 줄이 있어야 읽히고,
     * 발신자는 senderRef 로 구분됩니다. 여기서 버리면 남은 대화가 한쪽 말만 남아 뜻이 뒤집힙니다.
     *
     * <p>한 트랜잭션으로 묶습니다. 게임 서버는 응답을 기다리지 않으므로 일부만 들어가면
     * 재시도할 주체가 없습니다. 전부 들어가거나 전부 안 들어가는 편이 낫습니다.
     *
     * @param forbidden 줄마다의 금칙어 판정. entries 와 같은 순서, 같은 길이입니다
     */
    @Transactional
    int save(List<ChatLogBatchRequest.Entry> entries, boolean[] forbidden, String now) {
        // 한 묶음 안에 같은 사람이 여러 줄을 말하는 것이 보통입니다. 줄마다 조회하면 같은 질문을
        // 반복합니다.
        Map<String, Integer> resolved = new HashMap<>();
        List<ChatLog> rows = new ArrayList<>(entries.size());

        for (int index = 0; index < entries.size(); index++) {
            ChatLogBatchRequest.Entry entry = entries.get(index);
            rows.add(ChatLog.of(
                    entry.roomCode(),
                    entry.scope(),
                    senderSeq(entry.userPublicId(), resolved),
                    entry.senderRef(),
                    entry.message(),
                    forbidden[index],
                    entry.sentAt(),
                    now));
        }

        chatLogs.saveAll(rows);
        return rows.size();
    }

    /**
     * 계정 공개 id 를 users.seq 로 바꿉니다. 없거나 못 찾으면 null 입니다.
     *
     * <p>못 찾는 경우가 실제로 있습니다. 탈퇴한 직후 도착한 묶음이 그렇습니다. 그때도 줄은
     * 남기되 사람은 비웁니다. 탈퇴는 기록을 지워 달라는 뜻이므로 되살리지 않습니다.
     */
    private Integer senderSeq(String publicId, Map<String, Integer> resolved) {
        if (publicId == null || publicId.isBlank()) {
            return null;
        }
        // computeIfAbsent 를 쓰지 않는 이유는 그것이 null 을 담아 두지 않아서입니다. 못 찾은 id 를
        // 줄마다 다시 물어보게 됩니다.
        if (!resolved.containsKey(publicId)) {
            resolved.put(publicId, users.findByPublicId(publicId).map(User::getSeq).orElse(null));
        }
        return resolved.get(publicId);
    }
}
