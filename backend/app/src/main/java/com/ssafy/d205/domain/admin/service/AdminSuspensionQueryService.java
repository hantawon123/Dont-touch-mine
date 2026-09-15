package com.ssafy.d205.domain.admin.service;

import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.ssafy.d205.domain.admin.dto.AdminSuspendedAccount;
import com.ssafy.d205.domain.admin.dto.AdminSuspensionEntry;
import com.ssafy.d205.domain.admin.dto.AdminSuspensionListResponse;
import com.ssafy.d205.domain.admin.repository.SuspensionAuditRepository;

/**
 * 정지 탭이 읽는 것 (S15P21D205-974).
 *
 * <p>{@link AdminUserQueryService} 와 나눈 이유는 묻는 질문이 다르기 때문입니다. 저쪽은 "이
 * 사람은 누구인가"라 사람 하나에서 시작하고, 이쪽은 "지금 누가 막혀 있나"라 목록에서
 * 시작합니다. 한곳에 두면 사용자 조회 서비스가 정지 목록까지 아는 모양이 됩니다.
 *
 * <p>읽기만 합니다. 정지·해제는 {@link AccountSuspensionService} 가 맡습니다.
 */
@Service
@RequiredArgsConstructor
public class AdminSuspensionQueryService {

    private final SuspensionAuditRepository suspensionAuditRepository;

    /**
     * 지금 정지된 계정과 최근 해제 이력.
     *
     * <p>두 질의를 한 응답에 담습니다. 화면이 두 번 묻고 두 번 기다리게 하지 않으려는 것이고,
     * 운영자가 둘을 같이 보기 때문입니다.
     */
    @Transactional(readOnly = true)
    public AdminSuspensionListResponse list() {
        return new AdminSuspensionListResponse(
                suspensionAuditRepository.findSuspendedAccounts().stream()
                        .map(AdminSuspendedAccount::from).toList(),
                suspensionAuditRepository.findRecentLifts().stream()
                        .map(AdminSuspensionEntry::from).toList());
    }
}
