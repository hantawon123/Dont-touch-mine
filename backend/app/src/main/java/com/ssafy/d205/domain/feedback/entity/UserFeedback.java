package com.ssafy.d205.domain.feedback.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;

/**
 * 플레이어가 설정 화면에서 보낸 피드백 한 건.
 *
 * <p><b>이 행은 아무 동작도 일으키지 않습니다.</b> 신고와 같습니다. 어느 조회 경로도
 * 이 테이블을 읽지 않고 운영자가 나중에 읽습니다.
 *
 * <p>신고와 다른 점은 <b>검토 상태가 없다</b>는 것입니다. 신고에는 검토 상태가 있어서
 * 운영자가 마무리 표시를 하지만, 피드백에는 판단할 것이 없습니다. 읽었다는 표시를
 * 두면 "읽음"이 "처리했음"처럼 읽히고, 실제로는 아무것도 하지 않은 채 목록만
 * 깨끗해집니다.
 *
 * <p>보낸 사람이 고치거나 취소할 수 있는 API 는 없습니다. 취소를 허용하면 "썼다가 지운
 * 피드백"을 남길지 정해야 하고 그 질문에 답할 근거가 없습니다.
 *
 * <p><b>운영자는 치울 수 있습니다</b>(S15P21D205-900). {@link #hide(String)} 로 목록에서
 * 빼거나 행을 통째로 지웁니다. 이쪽을 연 이유는 조회 상한(FeedbackReadService.MAX_LIMIT)
 * 이 있는 이유와 같습니다 - 피드백은 쌓이기만 하고, 스팸 한 줄이 계속 목록 위쪽을
 * 차지하면 그 아래 진짜 지적이 안 읽힙니다.
 */
@Entity
@Table(name = "user_feedback")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class UserFeedback {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    @Column(name = "user_feedback_seq")
    private Integer seq;

    /**
     * 쓴 사람. <b>탈퇴하면 NULL 이 됩니다.</b>
     *
     * <p>내용을 지우지 않고 작성자만 비우는 이유는 V14 주석에 있습니다. 떠난 사람이
     * 남긴 지적은 그 사람이 떠난 뒤에도 사실입니다.
     */
    @Column(name = "author_seq")
    private Integer authorSeq;

    /** 본문. 500자까지입니다. 클라이언트 화면 상한과 같은 값입니다. */
    @Column(name = "message", nullable = false, length = 500)
    private String message;

    /** 보낸 클라이언트의 빌드. 클라이언트가 안 보내면 null 입니다. */
    @Column(name = "build_ver", length = 32)
    private String buildVer;

    /** 실행 환경. 클라이언트가 안 보내면 null 입니다. */
    @Column(name = "platform", length = 16)
    private String platform;

    /** 보낸 시각. yyyyMMddHHmmss, UTC. */
    @Column(name = "created_at", nullable = false, length = 14)
    private String createdAt;

    /**
     * 운영자가 숨긴 시각. 보이는 피드백은 null 입니다.
     *
     * <p>신고의 같은 컬럼과 뜻이 같습니다. 목록에서 빠지지만 행은 남아 있어, 잘못 치운
     * 것을 DB 에서 되찾을 수 있습니다. <b>화면에서 되돌릴 방법은 없습니다.</b>
     */
    @Column(name = "deleted_at", length = 14)
    private String deletedAt;

    private UserFeedback(Integer authorSeq, String message, String buildVer, String platform, String now) {
        this.authorSeq = authorSeq;
        this.message = message;
        this.buildVer = buildVer;
        this.platform = platform;
        this.createdAt = now;
    }

    public static UserFeedback of(Integer authorSeq, String message,
                                  String buildVer, String platform, String now) {
        return new UserFeedback(authorSeq, message, buildVer, platform, now);
    }

    /**
     * 운영자가 이 피드백을 목록에서 치웁니다.
     *
     * <p>이미 숨긴 것을 다시 숨겨도 시각을 덮어쓰지 않습니다. 처음 치운 시각이 되찾을
     * 때의 단서입니다.
     */
    public void hide(String now) {
        if (this.deletedAt == null) {
            this.deletedAt = now;
        }
    }
}
