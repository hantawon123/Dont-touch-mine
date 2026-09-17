package com.ssafy.d205.domain.chat.service;

import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.Duration;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

import com.ssafy.d205.domain.chat.dto.AdminChatLine;
import com.ssafy.d205.domain.chat.repository.AdminChatRow;
import com.ssafy.d205.domain.chat.repository.ChatLogRepository;
import com.ssafy.d205.domain.user.repository.UserRepository;
import com.ssafy.d205.global.exception.TargetUserNotFoundException;
import com.ssafy.d205.global.common.TimeProvider;
import com.ssafy.d205.global.common.Timestamps;

/**
 * 신고를 판단하려는 운영자에게 그때 무슨 말이 오갔는지 보여줍니다 (S15P21D205-1030).
 */
@Service
@RequiredArgsConstructor
public class AdminChatService {

    /**
     * 운영자가 열 수 있는 가장 넓은 구간.
     *
     * <p><b>보관 청소가 지키는 구간과 같은 값이어야 합니다</b>({@link ChatLogSweeper}). 청소가 더
     * 좁게 지키면 운영자가 넓혀 읽던 줄이 다음 청소에 사라집니다 - 화면에 보이던 것이 다음 날
     * 없어지는데 아무 신호가 없습니다. 그래서 두 곳이 같은 숫자를 따로 적지 않고 이것을 씁니다.
     */
    public static final int MAX_WINDOW_MINUTES = 180;

    /** 방 코드와 경기 수를 가르는 글자. 신고의 context_key 가 {@code 7K2M9P#2} 꼴입니다. */
    private static final char CONTEXT_SEPARATOR = '#';

    /** 이름을 모르는 발화자에게 붙이는 딱지. */
    private static final String UNKNOWN_SPEAKER = "참가자 ";

    private final ChatLogRepository chatLogs;
    private final UserRepository users;
    private final TimeProvider timeProvider;

    /**
     * 신고의 경기 키가 가리키는 방에서, 신고 시각 앞뒤 구간의 대화.
     *
     * <p><b>키에서 방 코드만 씁니다.</b> 뒤의 숫자는 피어마다 자기가 본 경기 수를 센 값이라
     * 늦게 들어온 사람은 같은 경기에 다른 번호를 붙입니다. 전역 식별자가 아니므로 조인에
     * 쓰지 않고, 대신 방 코드와 시각 구간으로 찾습니다.
     *
     * <p>구간 폭을 넉넉히 두는 이유는 사람이 대화가 끝난 뒤에 신고를 누르기 때문입니다.
     * 신고 시각만 보면 정작 문제가 된 말은 구간 밖에 있습니다.
     */
    @Transactional(readOnly = true)
    public AdminChatLine.ListResponse around(String contextKey, String reportedAt, int minutes) {
        var roomCode = roomCodeOf(contextKey);
        if (roomCode == null) {
            // 옛 클라이언트가 키 없이 보낸 신고입니다. 어느 방인지 알 수 없으므로 찾을 것이 없습니다.
            return new AdminChatLine.ListResponse(List.of(), null, null, 0);
        }

        var center = Timestamps.parse(reportedAt);
        var window = Duration.ofMinutes(minutes);
        var from = Timestamps.format(center.minus(window));
        var to = Timestamps.format(center.plus(window));

        return response(chatLogs.findConversation(roomCode, from, to), from, to);
    }

    /**
     * 이 사람이 최근에 한 말 전부. 방을 가리지 않습니다.
     *
     * <p>한 방에서 한 번은 실수일 수 있어도 여러 방에서 반복하면 다른 판단이 됩니다.
     */
    @Transactional(readOnly = true)
    public AdminChatLine.ListResponse bySpeaker(String userId, int days) {
        // 말한 적이 없는 사람과 없는 계정을 구분합니다. 구분하지 않으면 운영자가 id 를 잘못
        // 눌렀을 때와 정상 조회가 똑같이 빈 목록으로 보입니다. 신고 상세가 같은 이유로 404 를
        // 냅니다(ReportReviewService.target).
        if (users.findByPublicId(userId).isEmpty()) {
            throw new TargetUserNotFoundException(userId);
        }

        var from = timeProvider.minus(Duration.ofDays(days));
        return response(chatLogs.findBySpeaker(userId, from), from, timeProvider.now());
    }

    /**
     * 줄들을 화면이 쓸 모양으로 바꿉니다.
     *
     * <p>이름을 모르는 발화자에게는 그 응답 안에서만 통하는 딱지를 붙입니다. 전부 "알 수 없음"
     * 으로 두면 세 사람이 한 말인지 한 사람이 세 번 말한 것인지 구분되지 않아 대화를 읽을 수
     * 없습니다. 계정을 모르는 줄이 생기는 경우는 좁습니다 - 백엔드가 죽어 있을 때 저장된
     * 크리덴셜로 들어온 클라이언트(S15P21D205-925)와 탈퇴한 사람입니다.
     */
    private AdminChatLine.ListResponse response(List<AdminChatRow> rows, String from, String to) {
        Map<String, String> labels = new HashMap<>();
        List<AdminChatLine> lines = new ArrayList<>(rows.size());
        var maskedCount = 0;

        for (AdminChatRow row : rows) {
            var masked = Boolean.TRUE.equals(row.getMasked());
            if (masked) {
                maskedCount++;
            }
            lines.add(new AdminChatLine(
                    row.getId(),
                    row.getScope(),
                    row.getRoomCode(),
                    speaker(row, labels),
                    row.getUserId(),
                    row.getMessage(),
                    masked,
                    row.getSentAt()));
        }

        return new AdminChatLine.ListResponse(lines, from, to, maskedCount);
    }

    private static String speaker(AdminChatRow row, Map<String, String> labels) {
        if (row.getNickname() != null) {
            return row.getNickname();
        }
        // 나타난 순서대로 참가자 A, B, C. 방이 끝나면 의미가 사라지는 값이라 사람을 특정하지
        // 않으면서 발화자만 구분합니다.
        return labels.computeIfAbsent(row.getSenderRef(),
                ref -> UNKNOWN_SPEAKER + (char) ('A' + Math.min(labels.size(), 25)));
    }

    /** 신고의 경기 키에서 방 코드만. 키가 없거나 모양이 아니면 null 입니다. */
    private static String roomCodeOf(String contextKey) {
        if (contextKey == null || contextKey.isBlank()) {
            return null;
        }
        var separator = contextKey.indexOf(CONTEXT_SEPARATOR);
        var roomCode = separator < 0 ? contextKey.strip() : contextKey.substring(0, separator).strip();
        return roomCode.isEmpty() ? null : roomCode;
    }

}
