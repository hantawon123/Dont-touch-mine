package com.ssafy.d205.domain.highlight;
import org.junit.jupiter.api.Test;
import tools.jackson.databind.json.JsonMapper;
import java.util.*;
import java.net.http.*;
import java.util.concurrent.*;
import static org.mockito.Mockito.*;
import static org.junit.jupiter.api.Assertions.*;

class HighlightDirectorTest {
    private final JsonMapper json=new JsonMapper();
    private final HighlightDirectorService service=new HighlightDirectorService("","",json);
    private final List<HighlightDirectorController.Candidate> candidates=List.of(
        new HighlightDirectorController.Candidate(0,"FirstBlood",8,1,30,2,60),
        new HighlightDirectorController.Candidate(1,"PlayerStunned",7,1,10,2,50),
        new HighlightDirectorController.Candidate(2,"ItemRecovered",6,2,5,2,50));
    private String body(String picks,String finish) {
        return json.writeValueAsString(Map.of("candidates",List.of(Map.of("finishReason",finish,
            "content",Map.of("parts",List.of(Map.of("text","{\"picks\":"+picks+"}")))))));
    }
    private String picks() { return """
      [{"id":2,"title":"다시 내 손에","summary":"원주인이 물건을 회수했습니다."},
       {"id":0,"title":"첫 파괴","summary":"첫 물건이 파괴됐습니다."},
       {"id":1,"title":"잠깐의 빈틈","summary":"기절이 발생했습니다."}]
      """; }
    @Test void validJsonPreservesAiRankingAndCaptions() {
        var answer=service.parse(body(picks(),"STOP"),candidates);
        assertTrue(answer.available()); assertEquals(List.of(2,0,1),answer.picks().stream().map(HighlightDirectorService.Pick::id).toList());
    }
    @Test void invalidDuplicateMissingIdsOrUnsafeCaptionsFallBack() {
        for(String value:List.of(picks().replace("\"id\":2","\"id\":9"),
            picks().replace("\"id\":2","\"id\":0"),"[]",
            picks().replace("다시 내 손에","<b>다시</b>"),picks().replace("다시 내 손에","씨발")))
            assertFalse(service.parse(body(value,"STOP"),candidates).available());
        assertFalse(service.parse(body(picks(),"MAX_TOKENS"),candidates).available());
        assertFalse(service.parse("not json",candidates).available());
    }
    @Test void providerIsCalledOnceAndHttpErrorsFallBack() throws Exception {
        HttpClient http=mock(HttpClient.class);
        HttpResponse<String> response=mock(HttpResponse.class);
        when(response.statusCode()).thenReturn(200); when(response.body()).thenReturn(body(picks(),"STOP"));
        when(http.sendAsync(any(HttpRequest.class),any(HttpResponse.BodyHandler.class)))
            .thenReturn(CompletableFuture.completedFuture(response));
        var connected=new HighlightDirectorService("test-only","https://example.invalid/model",json,http);
        assertTrue(connected.direct(candidates).available());
        verify(http,times(1)).sendAsync(any(HttpRequest.class),any(HttpResponse.BodyHandler.class));
        when(response.statusCode()).thenReturn(401);
        assertFalse(connected.direct(candidates).available());
    }
    @Test void hungProviderIsCanceledWithinBoundedTime() {
        HttpClient http=mock(HttpClient.class);
        CompletableFuture<HttpResponse<String>> pending=new CompletableFuture<>();
        when(http.sendAsync(any(HttpRequest.class),any(HttpResponse.BodyHandler.class))).thenReturn(pending);
        var connected=new HighlightDirectorService("test-only","https://example.invalid/model",json,http);
        assertTimeout(java.time.Duration.ofSeconds(4),()->assertFalse(connected.direct(candidates).available()));
        assertTrue(pending.isCancelled());
    }
    @Test void missingConfigDoesNotCallProvider() {
        assertFalse(service.enabled()); assertFalse(service.direct(candidates).available());
        assertFalse(new HighlightDirectorService("test","http://localhost/unsafe",json).enabled());
    }
}
