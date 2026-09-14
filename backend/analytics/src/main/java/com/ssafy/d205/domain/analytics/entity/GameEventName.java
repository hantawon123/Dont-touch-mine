package com.ssafy.d205.domain.analytics.entity;

import java.util.Arrays;
import java.util.Map;
import java.util.function.Function;
import java.util.stream.Collectors;

/**
 * 받아 주는 이벤트 이름. docs/analytics-events.md 5절의 19개와 같아야 합니다.
 *
 * <p>화이트리스트를 두는 이유는 "일단 다 찍어두고 나중에 보자"를 막기 위해서입니다. 명세에
 * 없는 이름이 들어오면 배치 전체가 400 이라, 클라이언트가 이벤트를 하나 더 만들려면 명세를
 * 먼저 고쳐야 합니다. AnalyticsEventsDocTest 가 이 목록과 문서가 같은지 봅니다.
 *
 * <p>DB 에는 enum 이름이 아니라 {@link #wire} 문자열이 들어갑니다. Metabase 에서 쿼리를 쓰는
 * 사람이 보는 값이라 문서와 같은 snake_case 여야 합니다.
 */
public enum GameEventName {

    // 호스트 발행 - 경기 사실
    MATCH_START("match_start"),
    PHASE_CHANGE("phase_change"),
    FINAL_WARNING("final_warning"),
    HOST_MIGRATED("host_migrated"),
    MATCH_END("match_end"),
    PLAYER_RESULT("player_result"),
    ITEM_HIDDEN("item_hidden"),
    ITEM_PICKED_UP("item_picked_up"),
    ITEM_DESTROYED("item_destroyed"),
    PUNCH_HIT("punch_hit"),
    PLAYER_STUNNED("player_stunned"),

    // 호스트 발행 - 공간
    POSITION_SAMPLE("position_sample"),

    // 클라이언트 발행
    CLIENT_SESSION_START("client_session_start"),
    SCENE_ENTER("scene_enter"),
    CLIENT_QUIT("client_quit"),
    FIRST_INTERACT("first_interact"),
    PUNCH_SWUNG("punch_swung"),
    SETTINGS_CHANGED("settings_changed"),
    PERF_SAMPLE("perf_sample");

    private static final Map<String, GameEventName> BY_WIRE = Arrays.stream(values())
            .collect(Collectors.toUnmodifiableMap(GameEventName::wire, Function.identity()));

    private final String wire;

    GameEventName(String wire) {
        this.wire = wire;
    }

    /** 클라이언트가 보내고 DB 에 저장되는 이름. */
    public String wire() {
        return wire;
    }

    /** 명세에 있는 이름인지. 대소문자를 구분합니다. 명세가 소문자로 정했습니다. */
    public static boolean isKnown(String wire) {
        return wire != null && BY_WIRE.containsKey(wire);
    }
}
