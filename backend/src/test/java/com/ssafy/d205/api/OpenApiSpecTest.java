package com.ssafy.d205.api;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.test.web.servlet.MockMvc;
import tools.jackson.core.util.DefaultIndenter;
import tools.jackson.core.util.DefaultPrettyPrinter;
import tools.jackson.databind.SerializationFeature;
import tools.jackson.databind.json.JsonMapper;

import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Map;

import static java.nio.charset.StandardCharsets.UTF_8;
import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.ssafy.d205.support.IntegrationTest;

/**
 * API 명세를 코드에서 뽑아 docs/openapi.json 과 맞춰 봅니다.
 *
 * <p>손으로 유지하는 엔드포인트 목록을 두지 않기 위한 장치입니다. 문서를 사람이 적으면
 * API 를 추가할 때마다 두 곳을 고쳐야 하고, 한쪽을 잊으면 문서가 조용히 거짓말을
 * 하게 됩니다. 여기서는 <b>코드가 유일한 출처</b>이고 파일은 그 결과입니다.
 *
 * <p>다르면 새 명세를 파일에 써 두고 실패합니다. 그래서 API 를 바꾼 뒤 이 테스트가 한
 * 번 실패하는 것은 정상이고, 갱신된 파일을 커밋하면 통과합니다. 실패로 알리는 이유는
 * 조용히 고쳐 두면 명세 변경이 커밋에 섞여 들어가 MR 에서 눈에 띄지 않기 때문입니다.
 *
 * <p>배포 서버는 Swagger 를 열지 않습니다(application.yml). 클라이언트 담당자가 서버를
 * 띄우지 않고 계약을 볼 수 있는 곳이 이 파일입니다.
 */
class OpenApiSpecTest extends IntegrationTest {

    /** 테스트의 작업 디렉터리는 backend/ 입니다(Gradle 기본값). */
    private static final Path SPEC = Path.of("docs", "openapi.json");

    @Autowired
    MockMvc mvc;

    @Test
    @DisplayName("커밋된 명세가 코드와 일치한다")
    void committedSpecMatchesCode() throws Exception {
        byte[] body = mvc.perform(get("/v3/api-docs"))
                .andExpect(status().isOk())
                .andReturn().getResponse().getContentAsByteArray();

        String generated = normalize(new String(body, UTF_8));

        // 명세가 비었는데 파일도 비어서 통과하는 것을 막습니다.
        assertThat(generated)
                .as("명세에 엔드포인트가 하나도 없습니다. springdoc 이 꺼져 있는지 확인하세요.")
                .contains("/api/v1/accounts");

        String committed = Files.exists(SPEC) ? Files.readString(SPEC, UTF_8) : "";
        if (!generated.equals(committed)) {
            Files.createDirectories(SPEC.getParent());
            Files.writeString(SPEC, generated, UTF_8);
        }

        assertThat(committed)
                .as("docs/openapi.json 이 코드와 다릅니다. 방금 갱신해 두었으니 그 파일을 커밋하세요.")
                .isEqualTo(generated);
    }

    @Test
    @DisplayName("Swagger UI 가 서빙된다 - 로컬 설정이 실제로 동작하는지")
    void swaggerUiIsServed() throws Exception {
        // application-local.yml 이 켜는 경로입니다. 여기서 404 가 나면 로컬에서
        // 클라이언트 담당자가 열었을 때도 404 입니다. 그때는 원인이 잘 안 보입니다.
        mvc.perform(get("/swagger-ui.html"))
                .andExpect(status().is3xxRedirection());

        // 리다이렉트 대상이 실제로 있는지도 봅니다. webjar 가 빠지면 여기서 404 입니다.
        mvc.perform(get("/swagger-ui/index.html"))
                .andExpect(status().isOk());
    }

    /**
     * 실행마다 같은 바이트가 나오도록 만듭니다.
     *
     * <p>키를 정렬하는 것은 springdoc 의 출력 순서가 스캔 순서에 따라 달라질 수 있어서
     * 내용이 같은데 diff 가 생기는 것을 막기 위한 것입니다.
     *
     * <p>줄바꿈을 \n 으로 못 박는 것은 기본 PrettyPrinter 가 OS 줄바꿈을 쓰기 때문입니다.
     * 그대로 두면 Windows 에서 쓴 파일이 리눅스 젠킨스에서 다르게 읽혀, <b>로컬은 통과하고
     * CI 만 실패합니다.</b> backend/.gitattributes 의 eol=lf 와 짝을 맞춥니다.
     */
    private static String normalize(String json) {
        DefaultIndenter lf = new DefaultIndenter("  ", "\n");
        DefaultPrettyPrinter printer = new DefaultPrettyPrinter()
                .withObjectIndenter(lf)
                .withArrayIndenter(lf);

        JsonMapper mapper = JsonMapper.builder()
                .enable(SerializationFeature.ORDER_MAP_ENTRIES_BY_KEYS)
                .build();

        return mapper.writer().with(printer).writeValueAsString(mapper.readValue(json, Map.class)) + "\n";
    }
}
