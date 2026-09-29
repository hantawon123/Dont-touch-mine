package com.ssafy.d205.domain.user.repository;

import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import com.ssafy.d205.domain.user.entity.UserAppearance;

public interface UserAppearanceRepository extends JpaRepository<UserAppearance, Integer> {

    /**
     * 넣거나 덮어씁니다. 한 문장이라 경합이 없습니다.
     *
     * <p>"조회해서 없으면 save" 로 쓰면 같은 계정이 동시에 두 번 저장할 때 한쪽이 PK 위반으로
     * 죽습니다. 그걸 잡아 재시도하려면 AccountRegistrar 처럼 트랜잭션 밖의 빈이 하나 더
     * 필요합니다. ON DUPLICATE KEY UPDATE 는 그 문제를 DB 가 처리하게 넘깁니다.
     *
     * <p>{@code VALUES(col)} 대신 행 별칭({@code AS incoming})을 씁니다. 전자는 MySQL 8.0.20
     * 부터 폐기 예정이라 8.4 에서 경고가 나옵니다.
     *
     * <p>{@code clearAutomatically} 는 같은 트랜잭션에서 곧바로 다시 읽기 때문입니다. 네이티브
     * 쿼리는 영속성 컨텍스트를 지나치므로, 비우지 않으면 이전에 읽어 둔 옛 행이 그대로
     * 돌아옵니다.
     */
    @Modifying(clearAutomatically = true)
    @Query(value = """
            INSERT INTO user_appearances (user_seq, body_color, hood, shoes, face, updated_at)
            VALUES (:userSeq, :bodyColor, :hood, :shoes, :face, :now) AS incoming
            ON DUPLICATE KEY UPDATE
                body_color = incoming.body_color,
                hood       = incoming.hood,
                shoes      = incoming.shoes,
                face       = incoming.face,
                updated_at = incoming.updated_at
            """, nativeQuery = true)
    void upsert(@Param("userSeq") Integer userSeq,
                @Param("bodyColor") String bodyColor,
                @Param("hood") String hood,
                @Param("shoes") String shoes,
                @Param("face") String face,
                @Param("now") String now);
}
