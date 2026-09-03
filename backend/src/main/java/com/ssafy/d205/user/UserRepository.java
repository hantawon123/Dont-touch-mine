package com.ssafy.d205.user;

import org.springframework.data.jpa.repository.JpaRepository;

import java.util.Optional;

public interface UserRepository extends JpaRepository<User, Integer> {

    Optional<User> findByPublicId(String publicId);

    /**
     * nickname 컬럼 콜레이션이 utf8mb4_0900_as_cs라(V4) 대소문자를 구분합니다.
     * Player가 있어도 player는 없다고 답하고, uk_users_nickname도 같게 동작합니다.
     */
    boolean existsByNickname(String nickname);
}
