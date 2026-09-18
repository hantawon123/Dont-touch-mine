package com.ssafy.d205.domain.chat.service;

import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;

import java.util.List;

import com.ssafy.d205.domain.chat.dto.ChatLogBatchRequest;
import com.ssafy.d205.domain.chat.entity.ChatBlocklist;
import com.ssafy.d205.global.common.TimeProvider;

/**
 * 게임 서버가 보낸 채팅을 저장합니다 (S15P21D205-1027).
 *
 * <p><b>본문을 로그에 남기지 마세요.</b> 이 표를 3일만 두는 것이 의미를 가지려면 채팅이 저장되는
 * 곳이 이 표 하나여야 합니다. 애플리케이션 로그나 요청 로그로 흘리면 그쪽은 지워지지 않습니다.
 * 디버깅이 필요하면 건수나 방 코드만 남깁니다.
 *
 * <p><b>금칙어 판정은 여기서, 저장은 {@link ChatLogWriter} 에서 합니다.</b> 판정은 DB 를 쓰지
 * 않는데 목록이 커지면서 한 묶음에 백 밀리초를 넘길 수 있습니다. 같은 트랜잭션 안에서 하면 그동안
 * 커넥션을 쥐고 있게 되고, 방마다 몇 초 간격으로 묶음이 오므로 커넥션이 그만큼 묶입니다.
 * 트랜잭션 경계를 넘기려면 클래스를 나눠야 합니다. 같은 빈 안에서 부르면 프록시를 타지 않아
 * {@code @Transactional} 이 걸리지 않기 때문입니다.
 */
@Service
@RequiredArgsConstructor
public class ChatLogService {

    private final ChatBlocklist blocklist;
    private final ChatLogWriter writer;
    private final TimeProvider timeProvider;

    /**
     * 묶음을 저장하고 저장한 줄 수를 돌려줍니다.
     *
     * <p>게임 서버가 가렸는지 여부는 따로 받지 않고 여기서 다시 판정합니다. 받아도 믿을 이유가
     * 없고, 여기서 판정하면 게임 서버의 목록이 낡았어도 기록은 최신 기준이 됩니다. 방 서버는 뜰 때
     * 받은 목록을 그 방이 끝날 때까지 쓰므로 목록을 갱신해도 열려 있는 방은 옛 기준입니다.
     */
    public int record(ChatLogBatchRequest request) {
        List<ChatLogBatchRequest.Entry> entries = request.messages();
        boolean[] forbidden = new boolean[entries.size()];
        for (int index = 0; index < entries.size(); index++) {
            forbidden[index] = blocklist.isForbidden(entries.get(index).message());
        }
        return writer.save(entries, forbidden, timeProvider.now());
    }
}
