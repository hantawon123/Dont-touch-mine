package com.ssafy.d205.domain.highlight;

import java.net.URI;
import java.net.http.*;
import java.time.Duration;
import java.util.*;
import java.util.concurrent.*;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;
import tools.jackson.databind.ObjectMapper;

@Service
public class HighlightDirectorService {
    public record Pick(int id, String title, String summary) {}
    public record DirectorReply(boolean available, List<Pick> picks) {
        public static DirectorReply unavailable() { return new DirectorReply(false,List.of()); }
    }
    private final String key;
    private final URI endpoint;
    private final ObjectMapper json;
    private final HttpClient http;
    private final Semaphore capacity=new Semaphore(4);
    @org.springframework.beans.factory.annotation.Autowired
    public HighlightDirectorService(@Value("${GMS_KEY:}") String key,
            @Value("${GMS_GENERATE_URL:}") String url, ObjectMapper json) {
        this(key,url,json,HttpClient.newBuilder().connectTimeout(Duration.ofSeconds(1)).build());
    }
    HighlightDirectorService(String key,String url,ObjectMapper json,HttpClient http) {
        this.http=http;
        this.key=key; this.json=json;
        URI parsed=null;
        try { if(!url.isBlank()) parsed=URI.create(url); } catch(IllegalArgumentException ignored) { }
        endpoint=parsed!=null && "https".equals(parsed.getScheme()) && parsed.getHost()!=null &&
                parsed.getUserInfo()==null ? parsed : null;
    }
    public boolean enabled() { return !key.isBlank() && endpoint!=null; }
    public DirectorReply direct(List<HighlightDirectorController.Candidate> candidates) {
        if(!enabled() || !capacity.tryAcquire()) return DirectorReply.unavailable();
        CompletableFuture<HttpResponse<String>> pending=null;
        try {
            String prompt="""
                    숨바꼭질/물건 찾기 게임의 하이라이트 편집자다. 후보에서 재미와 사건의 명확성을 기준으로
                    최대 3개(후보가 3개 미만이면 전부)를 순서대로 선택하라. 제공된 사건 외의 행동/승패/인물을
                    지어내지 말 것. FirstBlood=첫 물건 파괴, TteTanMulgun=여러 사람이 만진 물건,
                    FinalMoment=종료 직전 사건, LongestHidden=오래 숨겨진 물건, MostStunned=반복 기절,
                    ItemDestroyed=물건 파괴, PlayerStunned=기절, ItemRecovered=원주인의 물건 회수.
                    제목은 한국어 24자 이내, 요약은 한국어 한 줄 70자 이내. 욕설/비하/성적 표현/혐오/개인정보/
                    마크업/URL을 사용하지 말 것. 재미있는 상황을 설명하되 플레이어를 조롱하지 말 것.
                    오직 JSON {"picks":[{"id":0,"title":"제목","summary":"한 줄 해설"}]}로 반환.
                    id는 입력 후보의 id만 사용하며 중복 없이 선택한다. 후보 JSON:
                    """+json.writeValueAsString(candidates);
            var body=Map.of("contents",List.of(Map.of("parts",List.of(Map.of("text",prompt)))),
                    "generationConfig",Map.of("responseMimeType","application/json","maxOutputTokens",1200,
                            "thinkingConfig",Map.of("thinkingLevel","minimal","includeThoughts",false)));
            var request=HttpRequest.newBuilder(endpoint).timeout(Duration.ofMillis(2600))
                    .header("Content-Type","application/json").header("x-goog-api-key",key)
                    .POST(HttpRequest.BodyPublishers.ofString(json.writeValueAsString(body))).build();
            pending=http.sendAsync(request,HttpResponse.BodyHandlers.ofString());
            var response=pending.get(2600,TimeUnit.MILLISECONDS);
            if(response.statusCode()!=200 || response.body().length()>32768) return DirectorReply.unavailable();
            return parse(response.body(),candidates);
        } catch(InterruptedException e) {
            Thread.currentThread().interrupt(); return DirectorReply.unavailable();
        } catch(Exception ignored) {
            // Provider credentials, URL, response, and exceptions never enter client replies or logs.
            return DirectorReply.unavailable();
        } finally {
            if(pending!=null && !pending.isDone()) pending.cancel(true);
            capacity.release();
        }
    }
    DirectorReply parse(String body,List<HighlightDirectorController.Candidate> candidates) {
        try {
            var root=json.readTree(body);
            if(root.has("promptFeedback") && root.path("promptFeedback").has("blockReason")) return DirectorReply.unavailable();
            var response=root.path("candidates").path(0);
            if(!"STOP".equals(response.path("finishReason").asText())) return DirectorReply.unavailable();
            for(var rating:response.path("safetyRatings"))
                if(rating.path("blocked").asBoolean() || Set.of("MEDIUM","HIGH").contains(rating.path("probability").asText()))
                    return DirectorReply.unavailable();
            var picks=json.readTree(response.path("content").path("parts").path(0).path("text").asText()).path("picks");
            if(!picks.isArray() || picks.size()!=Math.min(3,candidates.size())) return DirectorReply.unavailable();
            Set<Integer> valid=new HashSet<>(); candidates.forEach(c -> valid.add(c.id()));
            Set<Integer> used=new HashSet<>(); List<Pick> result=new ArrayList<>();
            for(var pick:picks) {
                if(!pick.path("id").isIntegralNumber() || !pick.path("title").isString() || !pick.path("summary").isString())
                    return DirectorReply.unavailable();
                int id=pick.path("id").asInt();
                String title=pick.path("title").asText(), summary=pick.path("summary").asText();
                if(!valid.contains(id) || !used.add(id) || !safe(title,24) || !safe(summary,70)) return DirectorReply.unavailable();
                result.add(new Pick(id,title,summary));
            }
            return new DirectorReply(true,List.copyOf(result));
        } catch(Exception ignored) { return DirectorReply.unavailable(); }
    }
    static boolean safe(String text,int limit) {
        if(text==null || text.isBlank() || text.length()>limit || !text.equals(text.strip())) return false;
        if(text.matches("(?s).*[<>\\p{Cntrl}].*") || text.contains("://") || text.contains("@")) return false;
        String compact=text.toLowerCase(Locale.ROOT).replaceAll("[\\s\\p{Punct}]","");
        // Defense in depth, not a complete language classifier; blocked/uncertain provider output also falls back.
        return List.of("시발","씨발","병신","개새끼","꺼져","죽어","fuck","shit","bitch")
                .stream().noneMatch(compact::contains);
    }
}
