package com.ssafy.d205.domain.settings.service;

import com.ssafy.d205.domain.settings.dto.AccountSettings;
import com.ssafy.d205.domain.settings.repository.SettingsRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import tools.jackson.databind.ObjectMapper;

@Service
@RequiredArgsConstructor
public class SettingsService {
    private final SettingsRepository repository;
    private final ObjectMapper json;
    public record Snapshot(boolean accepted, long revision, AccountSettings settings) {}
    @Transactional(readOnly=true)
    public Snapshot get(String user) { return snapshot(repository.read(repository.caller(user, false)), true); }
    @Transactional
    public Snapshot save(String user, long expectedRevision, AccountSettings settings) {
        // Lock the parent even on first save, so two devices cannot both create revision 1.
        int id = repository.caller(user, true);
        var previous = repository.read(id);
        if (previous.revision() != expectedRevision) return snapshot(previous, false);
        long next = previous.revision() + 1;
        repository.write(id, next, json.writeValueAsString(settings));
        return new Snapshot(true, next, settings);
    }
    private Snapshot snapshot(SettingsRepository.Saved row, boolean accepted) {
        return new Snapshot(accepted, row.revision(), row.payload() == null ? null :
                json.readValue(row.payload(), AccountSettings.class));
    }
}
