package com.ssafy.d205.domain.admin.service;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import static org.assertj.core.api.Assertions.assertThat;

import com.ssafy.d205.domain.user.entity.NicknameBlocklist;
import com.ssafy.d205.domain.user.entity.NicknamePolicy;

/**
 * 부적절한 닉네임을 치울 때 붙이는 이름이 닉네임 규칙을 지키는지 (S15P21D205-1047).
 *
 * <p>{@code NicknameGeneratorTest} 가 자동 닉네임에 대해 하는 일과 같습니다. 서버가 지은 이름은
 * 금칙어 검사를 지나지 않으므로, 접두사가 목록에 걸리는지 아무도 보지 않습니다. 목록에 "적" 같은
 * 조각이 추가되면 운영자가 붙인 이름을 <b>정작 그 사람은 고칠 수 없는</b> 상태가 되는데, 그때
 * 알려주는 것이 이 테스트입니다.
 */
class AdminNicknameServiceTest {

    @Test
    @DisplayName("붙는 이름은 숫자가 세 자리든 다섯 자리든 닉네임 규칙을 지킨다")
    void forcedNamesSatisfyThePolicyAtEveryLength() {
        // 세 자리에서 시작해 충돌할 때마다 한 자리 늘리고, 다섯 자리가 12자 한도입니다.
        for (String digits : new String[]{"123", "1234", "12345"}) {
            String name = AdminNicknameService.FORCED_PREFIX + digits;
            assertThat(NicknamePolicy.isValid(name)).as(name).isTrue();
        }
    }

    @Test
    @DisplayName("접두사가 금칙어에 걸리지 않는다")
    void forcedPrefixIsNotForbidden() {
        NicknameBlocklist blocklist = new NicknameBlocklist();

        // 이름을 받은 사람이 그 이름으로 다시 저장하거나 고칠 때 이 검사를 지납니다.
        // 걸리면 그 사람은 자기 이름을 바꿀 수 없습니다.
        assertThat(blocklist.isForbidden(AdminNicknameService.FORCED_PREFIX + "123"))
                .as(AdminNicknameService.FORCED_PREFIX)
                .isFalse();
    }
}
