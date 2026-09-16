package com.ssafy.d205.domain.chat.service;

import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

import com.ssafy.d205.domain.chat.dto.ChatLogBatchRequest;
import com.ssafy.d205.domain.chat.entity.ChatBlocklist;
import com.ssafy.d205.domain.chat.entity.ChatLog;
import com.ssafy.d205.domain.chat.repository.ChatLogRepository;
import com.ssafy.d205.domain.user.entity.User;
import com.ssafy.d205.domain.user.repository.UserRepository;
import com.ssafy.d205.global.common.TimeProvider;

/**
 * 게임 서버가 보낸 채팅을 저장합니다 (S15P21D205-1027).
 *
 * <p><b>본문을 로그에 남기지 마세요.</b> 이 표를 3일만 두는 것이 의미를 가지려면 채팅이 저장되는
 * 곳이 이 표 하나여야 합니다. 애플리케이션 로그나 요청 로그로 흘리면 그쪽은 지워지지 않습니다.
 * 디버깅이 필요하면 건수나 방 코드만 남깁니다.
 */
@Service
@RequiredArgsConstructor
public class ChatLogService {

    private final ChatLogRepository chatLogs;
    private final UserRepository users;
    private final ChatBlocklist blocklist;
    private final TimeProvider timeProvider;

    /**
     * 묶음을 저장하고 저장한 줄 수를 돌려줍니다.
     *
     * <p>계정을 못 찾은 줄도 버리지 않고 저장합니다. 대화의 맥락은 그 줄이 있어야 읽히고,
     * 발신자는 senderRef 로 구분됩니다. 여기서 버리면 남은 대화가 한쪽 말만 남아 뜻이 뒤집힙니다.
     *
     * <p>한 트랜잭션으로 묶습니다. 게임 서버는 응답을 기다리지 않으므로 일부만 들어가면
     * 재시도할 주체가 없습니다. 전부 들어가거나 전부 안 들어가는 편이 낫습니다.
     */
    @Transactional
    public int record(ChatLogBatchRequest request) {
        String now = timeProvider.now();

        // 한 묶음 안에 같은 사람이 여러 줄을 말하는 것이 보통입니다. 줄마다 조회하면 같은 질문을
        // 반복합니다.
        Map<String, Integer> resolved = new HashMap<>();
        List<ChatLog> rows = new ArrayList<>(request.messages().size());

        for (ChatLogBatchRequest.Entry entry : request.messages()) {
            rows.add(ChatLog.of(
                    entry.roomCode(),
                    entry.scope(),
                    senderSeq(entry.userPublicId(), resolved),
                    entry.senderRef(),
                    entry.message(),
                    // 게임 서버가 가렸는지 여부를 따로 받지 않습니다. 받아도 믿을 이유가 없고,
                    // 여기서 판정하면 게임 서버의 목록이 낡았어도 기록은 최신 기준이 됩니다.
                    blocklist.isForbidden(entry.message()),
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
