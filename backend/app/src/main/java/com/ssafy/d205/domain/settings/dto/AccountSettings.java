package com.ssafy.d205.domain.settings.dto;

import jakarta.validation.constraints.*;
import java.util.List;

/** Schema 1: graphics and resolution deliberately never leave the PC. */
public record AccountSettings(
        @Min(1) @Max(1) int schemaVersion,
        @NotBlank @Size(max=32) String languageCode,
        @NotNull @Size(min=20,max=20) List<@NotNull @Size(max=128) String> bindings,
        @NotNull @Size(min=4,max=4) List<@NotBlank @Size(max=32) String> toggles,
        @NotNull @Size(min=3,max=3) List<@NotNull @Min(0) @Max(1000) Integer> sensitivities,
        @NotNull @Size(min=9,max=9) List<@NotBlank @Size(max=32) String> interfaceOptions,
        @NotNull @Size(min=1,max=1) List<@NotBlank @Size(max=32) String> notifications,
        @NotNull @Size(min=5,max=5) List<@NotNull @Min(0) @Max(100) Integer> volumes,
        @NotBlank @Size(max=256) String deviceName,
        @NotBlank @Pattern(regexp="push|open|off") String inputMode) {}
