package com.ssafy.d205.domain.settings;

import com.ssafy.d205.domain.settings.dto.AccountSettings;
import com.ssafy.d205.domain.settings.repository.SettingsRepository;
import com.ssafy.d205.domain.settings.service.SettingsService;
import org.junit.jupiter.api.Test;
import tools.jackson.databind.json.JsonMapper;
import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;
import java.util.Collections;

class SettingsServiceTest {
    private final SettingsRepository repository = mock(SettingsRepository.class);
    private final JsonMapper json = new JsonMapper();
    private final SettingsService service = new SettingsService(repository, json);
    private AccountSettings settings() {
        return new AccountSettings(1, "ko", Collections.nCopies(20,""), Collections.nCopies(4,"off"),
                Collections.nCopies(3,50), Collections.nCopies(9,"on"), Collections.nCopies(1,"on"),
                Collections.nCopies(5,50), "default", "push");
    }
    @Test void firstSaveAndReadRoundTrip() {
        when(repository.caller("mine",true)).thenReturn(42);
        when(repository.read(42)).thenReturn(new SettingsRepository.Saved(0,null));
        var answer = service.save("mine",0,settings());
        assertTrue(answer.accepted()); assertEquals(1,answer.revision());
        verify(repository).write(42,1,json.writeValueAsString(settings()));
        when(repository.caller("mine",false)).thenReturn(42);
        when(repository.read(42)).thenReturn(new SettingsRepository.Saved(1,json.writeValueAsString(settings())));
        assertEquals(settings(),service.get("mine").settings());
    }
    @Test void staleDeviceReturnsCurrentStateWithoutOverwriting() {
        when(repository.caller("mine",true)).thenReturn(42);
        when(repository.read(42)).thenReturn(new SettingsRepository.Saved(7,json.writeValueAsString(settings())));
        var answer = service.save("mine",6,settings());
        assertFalse(answer.accepted()); assertEquals(7,answer.revision());
        verify(repository,never()).write(anyInt(),anyLong(),anyString());
    }
    @Test void newAccountHasNoSavedSettings() {
        when(repository.caller("new",false)).thenReturn(43);
        when(repository.read(43)).thenReturn(new SettingsRepository.Saved(0,null));
        assertNull(service.get("new").settings());
    }
}
