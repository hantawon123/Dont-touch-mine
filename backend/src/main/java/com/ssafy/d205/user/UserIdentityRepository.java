package com.ssafy.d205.user;

import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.util.Optional;

public interface UserIdentityRepository extends JpaRepository<UserIdentity, Integer> {

    /**
     * 신원이 아니라 계정을 바로 돌려줍니다.
     *
     * <p>UserIdentity를 받아 getUser()를 부르는 방식이 자연스러워 보이지만, 그 연관은
     * LAZY이고 open-in-view가 꺼져 있어서 트랜잭션 밖에서 만지면 터집니다. 발급 흐름은
     * 재시도 때문에 일부러 트랜잭션 밖에 있으므로 여기서 조인해 계정을 꺼내옵니다.
     *
     * <p>uk_user_identities_provider(provider, provider_user_id)를 그대로 탑니다.
     */
    @Query("""
            select i.user
              from UserIdentity i
             where i.provider = :provider
               and i.providerUserId = :providerUserId
            """)
    Optional<User> findUserByProviderAndProviderUserId(@Param("provider") AuthProvider provider,
                                                       @Param("providerUserId") String providerUserId);
}
