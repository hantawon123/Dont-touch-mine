package com.ssafy.d205.domain.chat.internalapi;

import io.swagger.v3.oas.annotations.Hidden;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

import java.util.List;

import com.ssafy.d205.domain.chat.dto.ChatLogBatchRequest;
import com.ssafy.d205.domain.chat.entity.ChatBlocklist;
import com.ssafy.d205.domain.chat.service.ChatLogService;

/**
 * 게임 서버만 부르는 경로입니다 (S15P21D205-1027).
 *
 * <p><b>이 경로는 바깥에 없습니다.</b> nginx 에 {@code /internal} 이 없고 app 컨테이너는
 * 127.0.0.1:8080 에만 바인딩돼 있으므로, 정상적으로는 같은 EC2 에서 도는 게임 서버 프로세스만
 * 닿습니다. 그 위에 {@link com.ssafy.d205.global.security.InternalKeyFilter} 가 공유 키를 확인합니다.
 *
 * <p><b>공개 명세에서 뺍니다({@code @Hidden}).</b> docs/openapi.json 은 커밋해서 클라이언트에
 * 전달하는 파일이고 Swagger UI 로도 열립니다. 404 로 존재를 숨겨 놓고 명세에 주소와 요청 모양을
 * 그대로 실으면 숨긴 의미가 없습니다.
 *
 * <p>잠그는 이유가 두 가지입니다. 목록을 아무나 받아 가면 그것이 곧 우회 목록이 되고, 채팅을
 * 아무나 올릴 수 있으면 남의 이름으로 가짜 대화를 심어 누명을 씌울 수 있습니다. 채팅 기록은
 * 정지의 근거가 되므로 위조 가능한 기록은 없는 것보다 나쁩니다.
 */
@Hidden
@RestController
@RequestMapping("/internal/chat")
@RequiredArgsConstructor
public class InternalChatController {

    private final ChatBlocklist blocklist;
    private final ChatLogService chatLogService;

    /**
     * 금칙어 목록. 게임 서버가 기동할 때 한 번 받아 갑니다.
     *
     * <p>허용 목록도 함께 줍니다. 게임 서버가 백엔드와 <b>같은 판정</b>을 내려야 하기 때문입니다.
     * 금칙어만 주면 게임 서버는 "Analyst" 를 가리고 백엔드는 안 가려서, 사람들이 본 화면과
     * 운영자가 보는 기록이 어긋납니다.
     */
    @GetMapping("/blocklist")
    public BlocklistResponse blocklist() {
        return new BlocklistResponse(blocklist.words(), blocklist.allowed());
    }

    /**
     * 채팅 묶음을 받아 저장합니다.
     *
     * <p>게임 서버는 응답을 기다리지 않습니다. 채팅 전달이 백엔드 생사에 묶이면 안 되기
     * 때문입니다. 그래서 202 로 답하고, 실패하면 게임 서버는 그 묶음을 버립니다.
     */
    @PostMapping
    @ResponseStatus(HttpStatus.ACCEPTED)
    public void record(@Valid @RequestBody ChatLogBatchRequest request) {
        chatLogService.record(request);
    }

    /**
     * @param blocked 가려야 할 말
     * @param allowed 금칙어를 찾기 전에 먼저 지울 말. 오탐을 막습니다.
     */
    public record BlocklistResponse(List<String> blocked, List<String> allowed) {
    }
}
