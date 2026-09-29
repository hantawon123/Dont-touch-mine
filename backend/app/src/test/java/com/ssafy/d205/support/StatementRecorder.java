package com.ssafy.d205.support;

import org.hibernate.resource.jdbc.spi.StatementInspector;

import java.util.List;
import java.util.Locale;
import java.util.concurrent.CopyOnWriteArrayList;

/**
 * 지정한 구간에서 실제로 나간 SQL 문장을 받아 적습니다.
 *
 * <p>"쓰기 횟수가 접속자 수에 비례하지 않는다"(S15P21D205-890)를 <b>세어서</b> 확인하려고
 * 둡니다. 저장소 메서드가 몇 번 불렸는지를 보는 것으로는 부족합니다 — 한 번의 호출이 안에서
 * 행마다 문장을 내는 경우를 잡지 못하고, 그것이 정확히 걱정하는 상황입니다.
 *
 * <p>MySQL 의 {@code Com_update} 전역 카운터를 읽는 방법도 있지만, 그 값은 이 애플리케이션
 * 밖의 트래픽까지 셉니다. Hibernate 의 검사기는 우리 커넥션에서 나간 문장만 봅니다.
 *
 * <p><b>상태가 static 인 이유</b>는 이 클래스를 스프링이 아니라 Hibernate 가 클래스 이름으로
 * 직접 만들기 때문입니다(application-test.yml 의 statement_inspector). 주입받을 자리가 없어
 * 테스트가 인스턴스에 닿을 수 없습니다.
 *
 * <p>{@link #start()} 를 부르기 전에는 아무것도 쌓지 않습니다. 모든 테스트가 이 검사기를
 * 지나가므로 기본이 꺼짐이어야 합니다.
 */
public class StatementRecorder implements StatementInspector {

    private static final List<String> STATEMENTS = new CopyOnWriteArrayList<>();
    private static volatile boolean recording;

    @Override
    public String inspect(String sql) {
        if (recording) {
            STATEMENTS.add(sql);
        }
        return sql;
    }

    /** 여기부터 받아 적습니다. 이전에 쌓인 것은 버립니다. */
    public static void start() {
        STATEMENTS.clear();
        recording = true;
    }

    /** 받아 적기를 멈추고 그동안의 문장을 돌려줍니다. */
    public static List<String> stop() {
        recording = false;
        return List.copyOf(STATEMENTS);
    }

    /** 한 테이블을 고치는 문장만 골라냅니다. */
    public static List<String> writesOn(List<String> statements, String table) {
        return statements.stream()
                .map(sql -> sql.toLowerCase(Locale.ROOT))
                .filter(sql -> sql.contains(table))
                .filter(sql -> sql.startsWith("update") || sql.startsWith("insert") || sql.startsWith("delete"))
                .toList();
    }
}
