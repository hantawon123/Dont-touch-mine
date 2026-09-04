package com.ssafy.d205.domain.invite.service;

import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.Optional;

import com.ssafy.d205.domain.friend.entity.Friendship;
import com.ssafy.d205.domain.friend.repository.FriendshipRepository;
import com.ssafy.d205.domain.invite.dto.InviteListResponse;
import com.ssafy.d205.domain.invite.dto.InviteSummary;
import com.ssafy.d205.domain.invite.entity.InviteExpiry;
import com.ssafy.d205.domain.invite.entity.RoomInvite;
import com.ssafy.d205.domain.invite.repository.RoomInviteRepository;
import com.ssafy.d205.domain.user.entity.User;
import com.ssafy.d205.domain.user.repository.UserRepository;
import com.ssafy.d205.global.common.TimeProvider;
import com.ssafy.d205.global.exception.NotFriendsException;
import com.ssafy.d205.global.exception.TargetUserNotFoundException;
import com.ssafy.d205.global.exception.UnknownCallerException;

/**
 * 친구를 방으로 부르는 일.
 *
 * <p>서버는 방을 모릅니다. 방 코드가 가리키는 방이 아직 있는지, 자리가 남았는지,
 * 잠겨 있는지 전부 Photon 쪽 이야기입니다. 여기서 하는 일은 <b>코드를 친구에게
 * 건네주는 것뿐</b>이고, 그 코드로 들어갈 수 있는지는 클라이언트가 입장을 시도할 때
 * 알게 됩니다.
 */
@Service
@RequiredArgsConstructor
public class InviteService {

    private final RoomInviteRepository roomInviteRepository;
    private final FriendshipRepository friendshipRepository;
    private final UserRepository userRepository;
    private final TimeProvider timeProvider;

    /**
     * 친구를 방으로 부릅니다.
     *
     * <p><b>친구에게만 보낼 수 있습니다.</b> 화면이 친구 목록에서 부르기도 하지만,
     * 모르는 사람에게 방 코드가 흘러가는 길을 아예 두지 않으려는 것입니다.
     *
     * <p>같은 사람을 같은 방으로 다시 부르면 새 초대가 생기지 않고 시각만 새로 씁니다.
     * 두 번 눌렀다고 초대가 둘이 되지는 않고, 다시 부르면 만료 시계가 처음부터 갑니다.
     */
    @Transactional
    public void send(String callerUserId, String targetUserId, String roomCode) {
        User me = caller(callerUserId);
        User target = target(targetUserId);

        if (me.getSeq().equals(target.getSeq())) {
            // 스키마의 CHECK 가 막지만 여기서 먼저 걸러 제약 위반 대신 뜻이 있는 응답을
            // 줍니다. 자기를 부르는 것은 상대를 찾을 수 없는 것과 같게 답합니다.
            throw new TargetUserNotFoundException(targetUserId);
        }
        Optional<Friendship> friendship = friendshipRepository.findByPair(me.getSeq(), target.getSeq());
        if (friendship.isEmpty() || friendship.get().isPending()) {
            throw new NotFriendsException();
        }

        String now = timeProvider.now();
        Optional<RoomInvite> existing = roomInviteRepository
                .findByInviteeSeqAndInviterSeqAndRoomCode(target.getSeq(), me.getSeq(), roomCode);

        if (existing.isPresent()) {
            existing.get().renew(now);
            return;
        }

        roomInviteRepository.save(RoomInvite.of(me.getSeq(), target.getSeq(), roomCode, now));
    }

    /**
     * 내가 받은 초대 중 아직 살아있는 것.
     *
     * <p>만료된 것은 스윕을 기다리지 않고 여기서 걸러냅니다. 스윕은 주기로 돌기 때문에
     * 그 사이에 만료된 초대를 보여주면 없는 방으로 들어가려는 시도가 됩니다. 저장된
     * 값이 아니라 계산한 값을 준다는 점에서 친구 목록의 접속 상태와 같습니다.
     */
    @Transactional(readOnly = true)
    public InviteListResponse listInbox(String callerUserId) {
        User me = caller(callerUserId);

        // 기준 시각을 한 번만 계산합니다. 행마다 읽으면 판정 기준이 행마다 미세하게
        // 달라집니다. 한 조회는 한 기준으로 판정해야 합니다.
        String thresholdAt = timeProvider.minus(InviteExpiry.LIFETIME);

        return new InviteListResponse(
                roomInviteRepository.findInbox(me.getSeq(), thresholdAt).stream()
                        .map(row -> new InviteSummary(
                                row.getUserId(),
                                row.getNickname(),
                                row.getRoomCode(),
                                row.getInvitedAt()))
                        .toList());
    }

    /**
     * 받은 초대를 없앱니다. 거절하거나, 입장한 뒤 정리할 때 부릅니다.
     *
     * <p><b>없는 초대를 지워도 성공입니다.</b> DELETE 는 멱등해야 하고, 원하는 결과인
     * "그 초대가 없는 상태"가 이미 달성돼 있습니다. 만료돼 사라진 초대를 거절하는 것도
     * 같은 경우입니다.
     *
     * <p>보낸 사람으로 지웁니다. 같은 사람이 여러 방으로 불렀다면 함께 사라집니다.
     * 화면이 목록에서 고르는 것은 사람이고 방 코드는 그 행에 딸려 온 값입니다.
     */
    @Transactional
    public void decline(String callerUserId, String inviterUserId) {
        User me = caller(callerUserId);
        User inviter = target(inviterUserId);

        roomInviteRepository.deleteFromInviter(me.getSeq(), inviter.getSeq());
    }

    private User caller(String userId) {
        return userRepository.findByPublicId(userId)
                .orElseThrow(() -> new UnknownCallerException(userId));
    }

    private User target(String userId) {
        return userRepository.findByPublicId(userId)
                .orElseThrow(() -> new TargetUserNotFoundException(userId));
    }
}
