package com.ssafy.d205.domain.invite.repository;

import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.util.List;
import java.util.Optional;

import com.ssafy.d205.domain.invite.entity.RoomInvite;

public interface RoomInviteRepository extends JpaRepository<RoomInvite, Integer> {

    /**
     * 같은 사람이 같은 방으로 이미 불렀는지. uk_room_invites_target 과 같은 세 컬럼입니다.
     */
    Optional<RoomInvite> findByInviteeSeqAndInviterSeqAndRoomCode(
            Integer inviteeSeq, Integer inviterSeq, String roomCode);

    /**
     * 내가 받은, 아직 살아있는 초대.
     *
     * <p>만료된 행을 스윕이 지우기를 기다리지 않고 조회에서 걸러냅니다. 스윕은 30초마다
     * 도는데 그 사이에 만료된 초대를 보여주면 없는 방으로 들어가려는 시도가 됩니다.
     * 저장된 값이 아니라 계산한 값을 준다는 점에서 친구 목록의 접속 상태와 같습니다.
     *
     * <p>같은 사람이 다른 방으로 여러 번 부를 수 있으므로 최신 것이 위로 오게 정렬합니다.
     *
     * <p>고정 길이 UTC 문자열이라 사전순 비교가 시간순 비교와 같습니다.
     */
    @Query(value = """
            SELECT u.public_id  AS userId,
                   u.nickname   AS nickname,
                   i.room_code  AS roomCode,
                   i.created_at AS invitedAt
              FROM room_invites i
              JOIN users u ON u.users_seq = i.inviter_seq
             WHERE i.invitee_seq = :inviteeSeq
               AND i.created_at >= :threshold
             ORDER BY i.created_at DESC
            """, nativeQuery = true)
    List<InviteRow> findInbox(@Param("inviteeSeq") Integer inviteeSeq,
                              @Param("threshold") String threshold);

    /**
     * 받은 쪽이 거절하거나 입장한 뒤 정리합니다.
     *
     * <p>보낸 사람으로 지웁니다. 화면이 목록에서 고르는 것은 "누가 불렀는지"이고 방
     * 코드는 그 행에 딸려 온 값이라, 같은 사람이 여러 방으로 불렀다면 한 번에 정리하는
     * 편이 화면과 맞습니다.
     */
    @Modifying
    @Query(value = """
            DELETE FROM room_invites
             WHERE invitee_seq = :inviteeSeq
               AND inviter_seq = :inviterSeq
            """, nativeQuery = true)
    int deleteFromInviter(@Param("inviteeSeq") Integer inviteeSeq,
                          @Param("inviterSeq") Integer inviterSeq);

    /**
     * 만료된 초대를 지웁니다. 스윕이 부릅니다.
     *
     * <p>접속 상태와 달리 값을 바꾸지 않고 행을 지웁니다. 만료된 초대는 아무 뜻도 없고
     * 남겨서 읽을 것도 없습니다. 수명이 3분이라 테이블이 작게 유지되므로 ix 를 타지
     * 못하고 훑어도 부담이 없습니다.
     */
    @Modifying
    @Query(value = """
            DELETE FROM room_invites
             WHERE created_at < :threshold
            """, nativeQuery = true)
    int deleteExpired(@Param("threshold") String threshold);

    /** 조회 투영. 문자열만 담습니다. */
    interface InviteRow {

        String getUserId();

        String getNickname();

        String getRoomCode();

        String getInvitedAt();
    }
}
