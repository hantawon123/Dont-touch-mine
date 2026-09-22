package com.ssafy.d205.domain.chat.service;

import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

import com.ssafy.d205.domain.chat.dto.ChatLogBatchRequest;
import com.ssafy.d205.domain.chat.repository.ChatLogBatchWriter;
import com.ssafy.d205.domain.user.entity.User;
import com.ssafy.d205.domain.user.repository.UserRepository;
import com.ssafy.d205.global.common.TimeProvider;

/**
 * 게임 서버가 보낸 채팅을 저장합니다 (S15P21D205-1027).
 *
 * <p><b>본문을 로그에 남기지 마세요.</b> 이 표를 3일만 두는 것이 의미를 가지려면 채팅이 저장되는
 * 곳이 이 표 하나여야 합니다. 애플리케이션 로그나 요청 로그로 흘리면 그쪽은 지워지지 않습니다.
 * 디버깅이 필요하면 건수나 방 코드만 남깁니다.
 *
 * <p><b>금칙어를 여기서 판정하지 않습니다</b>(S15P21D205-1096). 게임 서버가 가리면서 낸 판정을
 * {@code masked} 로 받아 그대로 적습니다. 다시 판정하면 같은 규칙을 Java 와 C# 두 곳에 두어야 하고,
 * 두 목록이 갈리는 순간 기록이 "가려졌다고 적혔는데 사람들은 그대로 본" 상태가 됩니다. 페이로드의
 * 원문과 발화자와 시각은 이미 그대로 믿고 있으며, 그 신뢰는 공유 키와 loopback 바인딩이 받칩니다.
 *
 * <p>대신 게임 서버가 목록을 못 받아 필터 없이 뜬 방은 {@code masked} 가 전부 false 입니다.
 * 받아들인 것이고 이유는 {@code docs/chat-moderation.md} 에 적었습니다.
 *
 * <p>한때 {@code ChatLogWriter} 로 나뉘어 있었습니다. 나눈 이유는 판정이 DB 를 쓰지 않으면서 한
 * 묶음에 백 밀리초를 넘길 수 있어 트랜잭션 밖에서 끝내야 한다는 것 하나였고, 판정이 없어지면서
 * 남길 이유가 없어졌습니다.
 *
 * <p>넣는 일 자체는 {@link ChatLogBatchWriter} 가 합니다. JPA 로 넣으면 200줄 묶음이 문장 200개가
 * 되기 때문인데, 이유는 그쪽 주석에 적었습니다.
 */
@Service
@RequiredArgsConstructor
public class ChatLogService {

    private final ChatLogBatchWriter chatLogs;
    private final UserRepository users;
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
        List<ChatLogBatchRequest.Entry> entries = request.messages();
        String now = timeProvider.now();

        // 한 묶음 안에 같은 사람이 여러 줄을 말하는 것이 보통입니다. 줄마다 조회하면 같은 질문을
        // 반복합니다.
        Map<String, Integer> resolved = new HashMap<>();
        List<ChatLogBatchWriter.Row> rows = new ArrayList<>(entries.size());

        for (ChatLogBatchRequest.Entry entry : entries) {
            rows.add(new ChatLogBatchWriter.Row(
                    entry.roomCode(),
                    entry.scope(),
                    senderSeq(entry.userPublicId(), resolved),
                    entry.senderRef(),
                    entry.message(),
                    entry.masked(),
                    entry.sentAt(),
                    now));
        }

        return chatLogs.insertAll(rows);
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
