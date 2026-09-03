package com.ssafy.d205.user;

import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.util.List;
import java.util.Optional;

public interface UserRepository extends JpaRepository<User, Integer> {

    Optional<User> findByPublicId(String publicId);

    /**
     * nickname 컬럼 콜레이션이 utf8mb4_0900_as_cs라(V4) 대소문자를 구분합니다.
     * Player가 있어도 player는 없다고 답하고, uk_users_nickname도 같게 동작합니다.
     */
    boolean existsByNickname(String nickname);

    /**
     * 닉네임 접두사로 다른 사용자를 찾습니다.
     *
     * <p><b>prefix 는 반드시 소문자로 넘겨야 합니다.</b> nickname_lower 컬럼이 as_cs
     * 콜레이션이라 대문자로 넘기면 아무것도 걸리지 않습니다. 서비스가 변환합니다.
     *
     * <p>JPQL이 아니라 네이티브 쿼리인 이유가 둘입니다.
     *
     * <p>첫째, nickname_lower 는 V6이 만든 생성 컬럼이고 엔티티에 매핑하지 않았습니다.
     * 값을 쓰는 곳이 이 쿼리 하나뿐인데, 매핑하면 읽기 전용 애너테이션을 정확히 달아야
     * 하고 실수하면 INSERT 가 깨집니다.
     *
     * <p>둘째, 차단 검사가 user_blocks 를 읽는데 그 테이블은 복합 기본키라 엔티티로
     * 만들면 @IdClass 보일러플레이트가 따라옵니다. 차단 API 가 아직 없어 다른 데서
     * 쓸 일도 없습니다.
     *
     * <p>차단은 양방향으로 걸러냅니다. 내가 차단한 사람과 나를 차단한 사람이 모두
     * 결과에서 빠집니다. 한쪽만 보면 차단당한 사람이 상대를 계속 찾아낼 수 있어
     * 차단의 의미가 없어집니다. user_blocks 의 두 인덱스가 각 방향을 담당합니다.
     *
     * <p>정렬을 nickname_lower 로 하는 것은 ix_users_nickname_lower 를 그대로 쓰기
     * 위해서입니다. 다른 컬럼으로 정렬하면 정렬을 위한 별도 작업이 생깁니다.
     */
    @Query(value = """
            SELECT u.public_id AS userId,
                   u.nickname  AS nickname
              FROM users u
             WHERE u.nickname_lower LIKE CONCAT(:prefix, '%')
               AND u.users_seq <> :meSeq
               AND NOT EXISTS (
                   SELECT 1
                     FROM user_blocks b
                    WHERE (b.blocker_seq = :meSeq AND b.blocked_seq = u.users_seq)
                       OR (b.blocker_seq = u.users_seq AND b.blocked_seq = :meSeq))
             ORDER BY u.nickname_lower
            """, nativeQuery = true)
    List<UserSummaryRow> searchByNicknamePrefix(@Param("prefix") String prefix,
                                                @Param("meSeq") Integer meSeq,
                                                Pageable pageable);
}
