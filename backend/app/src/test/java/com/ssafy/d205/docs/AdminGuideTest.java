package com.ssafy.d205.docs;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

import static java.nio.charset.StandardCharsets.UTF_8;
import static org.assertj.core.api.Assertions.assertThat;

/**
 * docs/admin-guide.md 가 코드와 어긋나지 않는지 봅니다.
 *
 * <p>이 문서가 특히 낡기 쉽습니다. 관리 API 의 로그인과 로그아웃은 컨트롤러가 아니라
 * Spring Security 의 필터가 받으므로 <b>springdoc 이 잡지 못하고 openapi.json 에도
 * 나오지 않습니다.</b> 즉 이 문서가 그 두 경로를 적어 둔 유일한 곳이고, 경로를 바꾸면
 * 아무도 모르게 거짓말이 됩니다.
 *
 * <p>경로 목록을 여기 다시 적지 않고 SecurityConfig 원본에서 뽑습니다. 적어 두면 그
 * 복사본과 문서를 비교하는 셈이라, 실제 설정이 달라져도 통과합니다. ClientGuideTest 가
 * 오류 코드를 GlobalExceptionHandler 원본에서 뽑는 것과 같은 방식입니다.
 */
class AdminGuideTest {

    /** 테스트의 작업 디렉터리는 backend/ 입니다(Gradle 기본값). */
    private static final Path GUIDE = Path.of("docs", "admin-guide.md");
    private static final Path CONFIG = Path.of("app", "src", "main", "java", "com", "ssafy", "d205",
            "global", "security", "SecurityConfig.java");

    /** SecurityConfig 안에 문자열로 적힌 관리자 경로들. */
    private static final Pattern ADMIN_PATH = Pattern.compile("\"(/api/v1/admin[^\"]*)\"");

    @Test
    @DisplayName("관리자 경로가 전부 문서에 있다")
    void everyAdminPathIsDocumented() throws IOException {
        List<String> paths = adminPathsInConfig();

        // 정규식이 아무것도 못 뽑았는데 통과하는 것을 막습니다.
        assertThat(paths)
                .as("SecurityConfig 에서 경로를 뽑지 못했습니다. 상수 모양이 바뀌었는지 보세요.")
                .hasSizeGreaterThanOrEqualTo(3);

        // /api/v1/admin/** 는 경로가 아니라 범위 표현이라 문서에 그대로 적히지 않습니다.
        List<String> real = paths.stream().filter(path -> !path.endsWith("/**")).toList();

        assertThat(guide())
                .as("관리자 경로를 바꿨으면 docs/admin-guide.md 도 고치세요. "
                        + "로그인과 로그아웃은 필터가 받아서 openapi.json 에 나오지 않으므로, "
                        + "이 문서가 그 경로를 적어 둔 유일한 곳입니다.")
                .contains(real);
    }

    @Test
    @DisplayName("CSRF 헤더 이름이 문서와 같다")
    void theCsrfHeaderMatches() throws IOException {
        // 화면이 이 이름으로 헤더를 넣습니다. 틀리면 상태를 바꾸는 요청이 전부 403 인데,
        // 브라우저에서만 드러나고 서버 로그에는 이유가 남지 않습니다.
        assertThat(guide())
                .as("CookieCsrfTokenRepository 가 쓰는 기본 이름입니다.")
                .contains("XSRF-TOKEN", "X-XSRF-TOKEN");
    }

    @Test
    @DisplayName("로그인이 폼 형식이라는 것이 적혀 있다")
    void theFormEncodingIsStated() throws IOException {
        // JSON 으로 보내면 조용히 401 이 됩니다. 아이디도 비밀번호도 읽히지 않으니
        // 서버 쪽에서는 그냥 빈 로그인 시도로 보입니다. 화면 담당자가 제일 먼저
        // 부딪히는 곳이라 문서에 반드시 있어야 합니다.
        assertThat(guide()).contains("x-www-form-urlencoded");
    }

    private static List<String> adminPathsInConfig() throws IOException {
        Matcher matcher = ADMIN_PATH.matcher(Files.readString(CONFIG, UTF_8));

        return matcher.results()
                .map(result -> result.group(1))
                .distinct()
                .toList();
    }

    private static String guide() throws IOException {
        return Files.readString(GUIDE, UTF_8);
    }
}
