package com.ssafy.d205.docs;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;
import java.util.regex.Matcher;
import java.util.regex.Pattern;
import java.util.stream.Stream;

import static java.nio.charset.StandardCharsets.UTF_8;
import static org.assertj.core.api.Assertions.assertThat;

/**
 * 관리 화면의 층 표와 구운 평면도가 어긋나지 않는지 봅니다 (S15P21D205-1092).
 *
 * <p>같은 숫자가 두 곳에 있습니다. 화면의 {@code MAPS} 가 평면도를 못 읽었을 때 쓸 층 범위를
 * 들고 있고, 굽기 도구가 쓴 {@code maps/<asset>.json} 도 같은 값을 들고 있습니다. 화면은 구운
 * JSON 을 우선하므로, 둘이 달라지면 <b>평면도가 있을 때와 없을 때 다른 층이 그려집니다.</b>
 * 그건 화면에 티가 안 나서 더 나쁩니다 - 사람이 벽 안에 서 있는 그림도 그럴듯해 보입니다.
 *
 * <p>평면도를 아직 굽지 않은 층은 건너뜁니다. 층을 화면에 먼저 넣고 그림을 나중에 굽기
 * 때문입니다(그동안은 경계만 그립니다).
 */
class AdminHeatmapFloorsTest {

    /** 테스트의 작업 디렉터리는 backend/ 입니다(Gradle 기본값). */
    private static final Path PAGE = Path.of("app", "src", "main", "resources", "static", "admin", "index.html");
    private static final Path MAPS = Path.of("app", "src", "main", "resources", "static", "admin", "maps");

    /** {@code { id: '2f', label: '2층', asset: 'mansion-2f', y0: 5, y1: 9.8 }} */
    private static final Pattern FLOOR = Pattern.compile(
            "\\{ id: '([^']*)', label: '([^']*)', asset: '([^']+)', y0: (-?[\\d.]+), y1: (-?[\\d.]+)");

    /** 구운 JSON 의 {@code "floor": { "label": "2층", "y0": 5, "y1": 9.8 }} */
    private static final Pattern BAKED_FLOOR = Pattern.compile(
            "\"floor\"\\s*:\\s*\\{[^}]*\"y0\"\\s*:\\s*(-?[\\d.]+)\\s*,\\s*\"y1\"\\s*:\\s*(-?[\\d.]+)");

    private record Floor(String id, String label, String asset, double y0, double y1) {
    }

    @Test
    @DisplayName("화면의 층 범위가 구운 평면도의 층 범위와 같다")
    void everyBakedFloorMatchesThePage() throws IOException {
        List<Floor> floors = floorsInPage();

        // 정규식이 아무것도 못 뽑았는데 통과하는 것을 막습니다.
        assertThat(floors)
                .as("index.html 의 MAPS 에서 층을 뽑지 못했습니다. 층 표의 모양이 바뀌었는지 보세요.")
                .hasSizeGreaterThanOrEqualTo(2);

        for (Floor floor : floors) {
            Path baked = MAPS.resolve(floor.asset() + ".json");
            if (!Files.exists(baked)) continue;  // 아직 굽지 않은 층. 화면은 경계만 그립니다.
            Matcher matcher = BAKED_FLOOR.matcher(Files.readString(baked, UTF_8));
            assertThat(matcher.find())
                    .as("%s 에 층이 없습니다. 굽기 창의 「한 층만 담은 그림」을 켜고 다시 구우세요.", baked)
                    .isTrue();
            assertThat(Double.parseDouble(matcher.group(1)))
                    .as("%s 의 층 시작이 화면의 층 표(%s)와 다릅니다.", baked, floor.label())
                    .isEqualTo(floor.y0());
            assertThat(Double.parseDouble(matcher.group(2)))
                    .as("%s 의 층 끝이 화면의 층 표(%s)와 다릅니다.", baked, floor.label())
                    .isEqualTo(floor.y1());
        }
    }

    @Test
    @DisplayName("구운 평면도는 모두 화면이 부르는 이름이다")
    void everyBakedFloorplanIsReachableFromThePage() throws IOException {
        String page = Files.readString(PAGE, UTF_8);
        List<String> assets = new ArrayList<>(floorsInPage().stream().map(Floor::asset).toList());
        // 층이 없는 맵은 맵 id 가 곧 평면도 이름입니다.
        Matcher maps = Pattern.compile("\\n    ([a-z0-9-]+): \\{ label:").matcher(page);
        while (maps.find()) assets.add(maps.group(1));

        try (Stream<Path> files = Files.list(MAPS)) {
            List<String> orphans = files
                    .map(path -> path.getFileName().toString())
                    .filter(name -> name.endsWith(".png"))
                    .map(name -> name.substring(0, name.length() - 4))
                    .filter(name -> !assets.contains(name))
                    .toList();
            assertThat(orphans)
                    .as("이 평면도를 부르는 곳이 없습니다. 맵 id 를 잘못 적고 구우면 다른 맵 그림을 "
                            + "덮어쓰거나 아무도 안 보는 파일이 남습니다.")
                    .isEmpty();
        }
    }

    private List<Floor> floorsInPage() throws IOException {
        List<Floor> floors = new ArrayList<>();
        Matcher matcher = FLOOR.matcher(Files.readString(PAGE, UTF_8));
        while (matcher.find()) {
            floors.add(new Floor(matcher.group(1), matcher.group(2), matcher.group(3),
                    Double.parseDouble(matcher.group(4)), Double.parseDouble(matcher.group(5))));
        }
        return floors;
    }
}
