package com.ssafy.d205.domain.settings.controller;

import com.ssafy.d205.domain.settings.dto.AccountSettings;
import com.ssafy.d205.domain.settings.service.SettingsService;
import jakarta.validation.Valid;
import jakarta.validation.constraints.*;
import lombok.RequiredArgsConstructor;
import org.springframework.web.bind.annotation.*;

@RestController
@RequestMapping("/api/v1/accounts/me/settings")
@RequiredArgsConstructor
public class SettingsController {
    private final SettingsService service;
    public record SaveRequest(@Min(0) long expectedRevision, @NotNull @Valid AccountSettings settings) {}
    @GetMapping
    public SettingsService.Snapshot get(@RequestHeader("X-User-Id") String user) { return service.get(user); }
    @PutMapping
    public SettingsService.Snapshot put(@RequestHeader("X-User-Id") String user,
            @Valid @RequestBody SaveRequest request) {
        return service.save(user, request.expectedRevision(), request.settings());
    }
}
