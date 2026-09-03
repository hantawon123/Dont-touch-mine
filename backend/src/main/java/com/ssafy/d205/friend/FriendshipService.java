package com.ssafy.d205.friend;

import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.List;
import java.util.Optional;

import com.ssafy.d205.account.AccountNotFoundException;
import com.ssafy.d205.user.User;
import com.ssafy.d205.user.UserRepository;

/**
 * 친구 요청과 친구 관계를 다룹니다.
 *
 * <p>요청과 친구를 한 서비스에 둔 이유는 같은 friendships 행의 상태 차이일 뿐이고,
 * 호출자와 상대를 찾는 절차가 다섯 연산에 모두 같기 때문입니다. 자원이 다르므로
 * 컨트롤러는 /friend-requests 와 /friends 로 나눠 둡니다.
 *
 * <p><b>차단 API를 만들 때 주의할 것</b>이 있습니다. 차단은 user_blocks 에 행을 넣는
 * 것으로 끝나지 않고 <b>그 두 사람 사이의 friendships 행도 함께 지워야</b> 합니다.
 * 그러지 않으면 이미 친구인 상태가 유지되고, 받은 요청 목록에 차단한 사람의 요청이
 * 계속 보입니다. 목록 쿼리에 차단 필터를 붙여 가리는 방법도 있지만, 관계를 남겨두면
 * 언젠가 다른 경로로 새어 나옵니다.
 */
@Service
@RequiredArgsConstructor
public class FriendshipService {

    private static final String INCOMING = "incoming";

    private final FriendshipRepository friendshipRepository;
    private final UserRepository userRepository;

    /**
     * 친구 요청을 보냅니다.
     *
     * <p>상대가 이미 나에게 요청을 보내둔 상태면 <b>그 자리에서 친구가 됩니다.</b>
     * 서로 원한다는 것이 명확한데 "받은 요청을 수락하세요"로 되돌리는 것은 불필요한
     * 한 단계입니다. 응답의 status 로 클라이언트가 구분해 보여주면 됩니다.
     *
     * <p>동시에 서로에게 보내면 둘 다 기존 행을 찾지 못하고 삽입을 시도합니다. 진 쪽은
     * uk_friendships_pair 에 걸려 409 를 받고, 다시 시도하면 자동 수락 경로로
     * 들어갑니다. 애플리케이션 검사만으로는 이 경합을 막을 수 없습니다.
     */
    @Transactional
    public SendFriendRequestResponse send(String callerUserId, String targetUserId) {
        User me = caller(callerUserId);
        User target = target(targetUserId);

        if (me.getSeq().equals(target.getSeq())) {
            throw new SelfFriendRequestException();
        }
        if (friendshipRepository.existsBlockBetween(me.getSeq(), target.getSeq())) {
            // 차단당했다는 사실을 알려주지 않습니다. 없는 사용자와 같게 답합니다.
            throw new TargetUserNotFoundException(targetUserId);
        }

        Optional<Friendship> existing = friendshipRepository.findByPair(me.getSeq(), target.getSeq());
        if (existing.isPresent()) {
            Friendship friendship = existing.get();
            if (!friendship.isPending()) {
                throw new AlreadyFriendsException();
            }
            if (friendship.isRequestedBy(me.getSeq())) {
                throw new FriendRequestAlreadySentException();
            }
            friendship.accept();
            return new SendFriendRequestResponse(FriendshipStatus.ACCEPTED);
        }

        friendshipRepository.save(Friendship.request(me.getSeq(), target.getSeq()));
        return new SendFriendRequestResponse(FriendshipStatus.PENDING);
    }

    /**
     * 받은 요청을 수락합니다.
     *
     * <p>내가 보낸 요청을 내가 수락하는 것은 막습니다. 그 경우 "받은 요청이 없다"가
     * 맞는 설명이라 요청을 찾을 수 없다는 응답으로 통일합니다.
     */
    @Transactional
    public void accept(String callerUserId, String targetUserId) {
        User me = caller(callerUserId);
        User other = target(targetUserId);

        if (friendshipRepository.existsBlockBetween(me.getSeq(), other.getSeq())) {
            // send 에만 검사를 두면 구멍이 남습니다. 차단이 걸리기 전에 만들어진
            // PENDING 요청을 수락해 친구가 될 수 있기 때문입니다. 차단할 때 기존
            // 관계를 지우는 것이 근본 해법이고(클래스 주석 참고), 이 검사는 차단과 수락이
            // 동시에 일어나는 경합까지 막는 두 번째 방어선입니다.
            throw new TargetUserNotFoundException(targetUserId);
        }

        Friendship friendship = pending(me, other);
        if (friendship.isRequestedBy(me.getSeq())) {
            throw new FriendRequestNotFoundException();
        }
        friendship.accept();
    }

    /**
     * 요청을 없앱니다. 받은 쪽이 부르면 거절, 보낸 쪽이 부르면 취소입니다.
     *
     * <p>둘이 같은 연산인 이유는 V2 주석에 있습니다. 거절 상태를 남기면
     * uk_friendships_pair 때문에 같은 상대에게 다시 요청할 수 없게 되므로 행을
     * 지웁니다. 그래서 거절 후 다시 요청하는 것이 가능합니다.
     */
    @Transactional
    public void deleteRequest(String callerUserId, String targetUserId) {
        User me = caller(callerUserId);
        User other = target(targetUserId);

        friendshipRepository.delete(pending(me, other));
    }

    /** 친구를 끊습니다. 관계 자체를 지우므로 나중에 다시 요청할 수 있습니다. */
    @Transactional
    public void unfriend(String callerUserId, String targetUserId) {
        User me = caller(callerUserId);
        User other = target(targetUserId);

        Friendship friendship = friendshipRepository.findByPair(me.getSeq(), other.getSeq())
                .filter(f -> !f.isPending())
                .orElseThrow(NotFriendsException::new);

        friendshipRepository.delete(friendship);
    }

    /**
     * 대기 중인 요청 목록. direction 이 incoming 이면 받은 요청, 아니면 보낸 요청입니다.
     *
     * <p>값 검증은 컨트롤러의 @Pattern 이 이미 했습니다. 여기서 다시 확인하지 않는
     * 대신 incoming 이 아닌 것은 outgoing 으로 처리합니다.
     */
    @Transactional(readOnly = true)
    public FriendRequestListResponse list(String callerUserId, String direction) {
        User me = caller(callerUserId);

        List<FriendRequestRow> rows = INCOMING.equals(direction)
                ? friendshipRepository.findIncoming(me.getSeq())
                : friendshipRepository.findOutgoing(me.getSeq());

        return new FriendRequestListResponse(rows.stream()
                .map(row -> new FriendRequestSummary(row.getUserId(), row.getNickname(), row.getRequestedAt()))
                .toList());
    }

    private Friendship pending(User me, User other) {
        return friendshipRepository.findByPair(me.getSeq(), other.getSeq())
                .filter(Friendship::isPending)
                .orElseThrow(FriendRequestNotFoundException::new);
    }

    /** 부르는 사람. 없으면 클라이언트가 계정 발급을 다시 불러야 하므로 코드를 구분합니다. */
    private User caller(String userId) {
        return userRepository.findByPublicId(userId)
                .orElseThrow(() -> new AccountNotFoundException(userId));
    }

    private User target(String userId) {
        return userRepository.findByPublicId(userId)
                .orElseThrow(() -> new TargetUserNotFoundException(userId));
    }
}
