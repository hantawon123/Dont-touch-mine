package com.ssafy.d205.domain.analytics.query;

import org.springframework.core.io.ClassPathResource;
import org.springframework.stereotype.Component;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * {@code docs/analytics-dashboards.md} 에서 질문별 SQL 을 읽습니다 (S15P21D205-976).
 *
 * <p>그 문서가 SQL 의 원본입니다. Metabase 화면은 {@code deploy/metabase/provision_dashboards.py} 가
 * 같은 문서를 읽어 만들고, 관리 화면의 분석 탭은 이 클래스를 지나 같은 SQL 을 실행합니다. 두 화면이
 * 다른 숫자를 보이면 둘 중 하나가 문서를 떠난 것입니다. 파싱 규칙도 그 스크립트와 같습니다 -
 * {@code ## N.} 으로 시작하는 절의 첫 {@code ```sql} 블록.
 *
 * <p>문서의 SQL 에는 매개변수가 없습니다. 기간·경기 조건은 각 절이 경기를 고르는 {@code WHERE} 끝에 둔
 * {@link #MARKER} 자리에 끼워 넣습니다. 표식이 정확히 하나가 아닌 절이 있으면 <b>기동에 실패</b>합니다.
 * 표식이 없는 쿼리를 그냥 돌리면 필터를 걸어도 전체 결과가 나오는데, 그건 틀린 값을 그럴듯하게
 * 보이는 것이라 안 도는 것보다 나쁩니다.
 *
 * <p>리소스는 {@code analytics/build.gradle} 의 processResources 가 문서에서 복사합니다.
 */
@Component
public class DashboardQueryDocument {

    public static final String RESOURCE = "docs/analytics-dashboards.md";

    /** 문서의 SQL 에서 필터 조건이 들어갈 자리. 이 문자열 그대로 문서에 있어야 합니다. */
    public static final String MARKER = "/* @filter */";

    /**
     * 절 번호 → API 이름. 인덱스 0 이 1 번 절입니다. 문서에 절이 늘면 여기에 이름을 하나 더 넣어야 하고,
     * 안 넣으면 기동이 그 절을 가리키며 멈춥니다. 이름은 계정 서비스의 프록시 경로와 관리 화면이
     * 그대로 씁니다.
     */
    static final List<String> SLUGS = List.of(
            "matches", "hiding-time", "hideouts", "dwell", "seeking-time", "combat", "item-life", "dropout");

    private static final Pattern SECTION =
            Pattern.compile("^## (([1-9])\\.[^\\n]*)\\n(.*?)(?=^## |\\z)", Pattern.MULTILINE | Pattern.DOTALL);
    private static final Pattern SQL_BLOCK = Pattern.compile("```sql\\n(.*?)```", Pattern.DOTALL);

    private final Map<String, Question> questions;

    public DashboardQueryDocument() {
        this(readResource());
    }

    /** 문서 본문을 직접 넘기는 생성자. 테스트가 깨진 문서를 만들어 거절되는지 볼 때 씁니다. */
    public DashboardQueryDocument(String markdown) {
        this.questions = parse(markdown);
    }

    public Optional<Question> find(String slug) {
        return Optional.ofNullable(questions.get(slug));
    }

    /** 문서 순서대로. 화면이 카드를 놓는 순서이기도 합니다. */
    public List<Question> all() {
        return List.copyOf(questions.values());
    }

    static Map<String, Question> parse(String markdown) {
        Map<String, Question> found = new LinkedHashMap<>();
        Matcher section = SECTION.matcher(markdown);
        while (section.find()) {
            String title = section.group(1).strip();
            int number = Integer.parseInt(section.group(2));
            if (number > SLUGS.size()) {
                throw new IllegalStateException("문서에 " + number + " 번 절('" + title + "')이 있지만 API 이름이 없습니다. "
                        + "DashboardQueryDocument.SLUGS 에 한 칸을 넣으세요.");
            }
            Matcher sql = SQL_BLOCK.matcher(section.group(3));
            if (!sql.find()) {
                throw new IllegalStateException("문서의 '" + title + "' 절에 sql 코드 블록이 없습니다.");
            }
            String body = sql.group(1).strip();
            int markers = countMarkers(body);
            if (markers != 1) {
                throw new IllegalStateException("문서의 '" + title + "' 절 SQL 에 " + MARKER + " 표식이 " + markers
                        + " 개입니다. 경기를 고르는 WHERE 끝에 정확히 하나 있어야 합니다.");
            }
            String slug = SLUGS.get(number - 1);
            found.put(slug, new Question(slug, number, title, body));
        }
        for (int i = 0; i < SLUGS.size(); i++) {
            if (!found.containsKey(SLUGS.get(i))) {
                throw new IllegalStateException("문서에서 " + (i + 1) + " 번 절(" + SLUGS.get(i) + ")을 찾지 못했습니다.");
            }
        }
        return Collections.unmodifiableMap(found);
    }

    private static int countMarkers(String sql) {
        int count = 0;
        for (int at = sql.indexOf(MARKER); at >= 0; at = sql.indexOf(MARKER, at + MARKER.length())) {
            count++;
        }
        return count;
    }

    private static String readResource() {
        try {
            return new ClassPathResource(RESOURCE).getContentAsString(StandardCharsets.UTF_8);
        } catch (IOException e) {
            throw new IllegalStateException("클래스패스에 " + RESOURCE + " 가 없습니다. analytics/build.gradle 의 "
                    + "processResources 가 docs/analytics-dashboards.md 를 복사합니다.", e);
        }
    }

    /**
     * 문서의 절 하나.
     *
     * @param slug   API 이름. {@code /internal/admin/analytics/{slug}}
     * @param number 문서의 절 번호
     * @param title  절 제목. "1. 경기 목록과 수집 상태" 처럼 번호가 들어 있습니다
     * @param sql    표식이 든 원문 SQL
     */
    public record Question(String slug, int number, String title, String sql) {

        /** 표식 자리에 조건을 넣은 실행용 SQL. 조건이 없으면 빈 문자열을 넣어 문서와 같은 쿼리가 됩니다. */
        public String render(String conditions) {
            return sql.replace(MARKER, conditions);
        }
    }
}
