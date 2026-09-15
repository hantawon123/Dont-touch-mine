package com.ssafy.d205.docs;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.core.io.ClassPathResource;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;

import static java.nio.charset.StandardCharsets.UTF_8;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.ssafy.d205.domain.analytics.query.DashboardQueryDocument;

/**
 * docs/analytics-dashboards.md 가 분석 조회 API 가 읽을 수 있는 모양인지 봅니다 (S15P21D205-976).
 *
 * <p>문서가 SQL 의 원본이라 문서를 고치는 사람이 API 를 깨뜨릴 수 있습니다. 여기서 잡히는 것은 "기동이
 * 안 된다"로 운영에서 드러나기 전에 잡아야 하는 것들입니다 - 절이 빠졌거나, 표식이 없거나 둘이거나,
 * 빌드가 문서를 리소스로 싣지 않았거나.
 *
 * <p>작업 디렉터리는 backend/ 입니다(루트 build.gradle).
 */
class AnalyticsQueryDocTest {

    private static final Path DOC = Path.of("docs", "analytics-dashboards.md");

    @Test
    @DisplayName("문서의 번호 절 여덟 개가 API 이름 순서대로 읽힌다")
    void everyNumberedSectionIsAQuestion() throws IOException {
        DashboardQueryDocument document = new DashboardQueryDocument(Files.readString(DOC, UTF_8));

        assertThat(document.all()).extracting(DashboardQueryDocument.Question::slug)
                .containsExactly("matches", "hiding-time", "hideouts", "dwell", "seeking-time", "combat", "item-life", "dropout");
        assertThat(document.all()).extracting(DashboardQueryDocument.Question::number)
                .containsExactly(1, 2, 3, 4, 5, 6, 7, 8);
        assertThat(document.all()).allSatisfy(question -> {
            assertThat(question.sql()).containsOnlyOnce(DashboardQueryDocument.MARKER);
            assertThat(question.sql()).containsIgnoringCase("match_analysis_summary");
        });
    }

    @Test
    @DisplayName("표식 자리에 조건이 들어가고, 조건이 없으면 문서와 같은 SQL 이다")
    void markerIsWhereTheFilterGoes() throws IOException {
        DashboardQueryDocument document = new DashboardQueryDocument(Files.readString(DOC, UTF_8));
        DashboardQueryDocument.Question matches = document.find("matches").orElseThrow();

        assertThat(matches.render("")).doesNotContain(DashboardQueryDocument.MARKER)
                .contains("WHERE 1 = 1 \nORDER BY started_at_utc DESC");
        assertThat(matches.render(" AND match_id = ?")).contains("WHERE 1 = 1  AND match_id = ?");
    }

    @Test
    @DisplayName("빌드가 문서를 리소스로 실어 두어 실행 중인 서비스가 읽는 것과 저장소의 문서가 같다")
    void theResourceIsTheDocument() throws IOException {
        String resource = new ClassPathResource(DashboardQueryDocument.RESOURCE).getContentAsString(UTF_8);

        assertThat(resource)
                .as("analytics/build.gradle 의 processResources 가 docs/analytics-dashboards.md 를 복사해야 합니다.")
                .isEqualTo(Files.readString(DOC, UTF_8));
    }

    @Test
    @DisplayName("표식이 빠지거나 둘이면, 절이 빠지면 읽기를 거절한다")
    void brokenDocumentsAreRejected() throws IOException {
        String doc = Files.readString(DOC, UTF_8);

        String markerRemoved = doc.replaceFirst("upload_complete = 1 " + java.util.regex.Pattern.quote(DashboardQueryDocument.MARKER),
                "upload_complete = 1");
        assertThatThrownBy(() -> new DashboardQueryDocument(markerRemoved))
                .isInstanceOf(IllegalStateException.class)
                .hasMessageContaining("표식이 0 개");

        String markerDoubled = doc.replaceFirst("WHERE 1 = 1 " + java.util.regex.Pattern.quote(DashboardQueryDocument.MARKER),
                "WHERE 1 = 1 " + DashboardQueryDocument.MARKER + " " + DashboardQueryDocument.MARKER);
        assertThatThrownBy(() -> new DashboardQueryDocument(markerDoubled))
                .isInstanceOf(IllegalStateException.class)
                .hasMessageContaining("표식이 2 개");

        String sectionRenamed = doc.replace("## 8. ", "## 8- ");
        assertThatThrownBy(() -> new DashboardQueryDocument(sectionRenamed))
                .isInstanceOf(IllegalStateException.class)
                .hasMessageContaining("8 번 절");

        String extraSection = doc + "\n## 9. 새 질문\n\n```sql\nSELECT 1 /* @filter */\n```\n";
        assertThatThrownBy(() -> new DashboardQueryDocument(extraSection))
                .isInstanceOf(IllegalStateException.class)
                .hasMessageContaining("API 이름이 없습니다");
    }

    @Test
    @DisplayName("Metabase 만드는 스크립트와 같은 절을 본다 - 스크립트의 VIEWS 번호가 문서의 절과 같다")
    void provisioningScriptCoversTheSameSections() throws IOException {
        String script = Files.readString(Path.of("deploy", "metabase", "provision_dashboards.py"), UTF_8);
        List<String> numbers = java.util.regex.Pattern.compile("^    \"([1-9])\": \\{", java.util.regex.Pattern.MULTILINE)
                .matcher(script).results().map(m -> m.group(1)).toList();

        assertThat(numbers).as("provision_dashboards.py 의 VIEWS 에 절 번호가 여덟 개 있어야 합니다")
                .containsExactly("1", "2", "3", "4", "5", "6", "7", "8");
    }
}
