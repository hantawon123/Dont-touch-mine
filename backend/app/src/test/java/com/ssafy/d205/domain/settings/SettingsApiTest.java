package com.ssafy.d205.domain.settings;

import com.ssafy.d205.domain.settings.controller.SettingsController;
import com.ssafy.d205.domain.settings.dto.AccountSettings;
import com.ssafy.d205.domain.settings.service.SettingsService;
import com.ssafy.d205.global.web.AccountTokenInterceptor;
import com.ssafy.d205.global.security.AccountTokens;
import com.ssafy.d205.global.exception.GlobalExceptionHandler;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;
import tools.jackson.databind.json.JsonMapper;
import java.util.Collections;
import static org.mockito.Mockito.*;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.*;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.*;

class SettingsApiTest {
    private final SettingsService service=mock(SettingsService.class);
    private final AccountTokens tokens=new AccountTokens("local-unit-test-secret");
    private final JsonMapper json=new JsonMapper();
    private MockMvc mvc;
    @BeforeEach void setup() {
        mvc=MockMvcBuilders.standaloneSetup(new SettingsController(service))
                .setControllerAdvice(new GlobalExceptionHandler())
                .addInterceptors(new AccountTokenInterceptor(tokens)).build();
    }
    private AccountSettings settings() {
        return new AccountSettings(1,"ko",Collections.nCopies(20,""),Collections.nCopies(4,"off"),
                Collections.nCopies(3,50),Collections.nCopies(9,"on"),Collections.nCopies(1,"on"),
                Collections.nCopies(5,50),"default","push");
    }
    @Test void signedRequestUsesAuthenticatedAccount() throws Exception {
        when(service.save(eq("mine"),eq(0L),any())).thenReturn(new SettingsService.Snapshot(true,1,settings()));
        mvc.perform(put("/api/v1/accounts/me/settings").header("X-User-Id","mine")
                .header("X-Account-Token",tokens.issue("mine")).contentType("application/json")
                .content(json.writeValueAsString(new SettingsController.SaveRequest(0,settings()))))
                .andExpect(status().isOk()).andExpect(jsonPath("$.revision").value(1))
                .andExpect(jsonPath("$.settings.volumes[0]").value(50));
        verify(service).save("mine",0,settings());
    }
    @Test void otherAccountsTokenCannotReadOrWrite() throws Exception {
        mvc.perform(get("/api/v1/accounts/me/settings").header("X-User-Id","mine")
                .header("X-Account-Token",tokens.issue("other"))).andExpect(status().isUnauthorized());
        verifyNoInteractions(service);
    }
    @Test void malformedOrUnsupportedSettingsNeverReachStorage() throws Exception {
        String body=json.writeValueAsString(new SettingsController.SaveRequest(0,settings()));
        for(String invalid:new String[]{body.replace("\"schemaVersion\":1","\"schemaVersion\":2"),
                body.replace("\"volumes\":[50,50,50,50,50]","\"volumes\":[101,50,50,50,50]"),
                "{\"expectedRevision\":0,\"settings\":null}"}) {
            mvc.perform(put("/api/v1/accounts/me/settings").header("X-User-Id","mine")
                    .header("X-Account-Token",tokens.issue("mine")).contentType("application/json").content(invalid))
                    .andExpect(status().isBadRequest());
        }
        verifyNoInteractions(service);
    }
}
