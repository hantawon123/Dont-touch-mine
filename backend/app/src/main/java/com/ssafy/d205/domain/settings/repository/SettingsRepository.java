package com.ssafy.d205.domain.settings.repository;

import lombok.RequiredArgsConstructor;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Repository;
import org.springframework.web.server.ResponseStatusException;
import org.springframework.http.HttpStatus;

@Repository
@RequiredArgsConstructor
public class SettingsRepository {
    private final JdbcTemplate jdbc;
    public record Saved(long revision, String payload) {}
    public int caller(String publicId, boolean lock) {
        var ids = jdbc.queryForList("SELECT users_seq FROM users WHERE public_id = ?" +
                (lock ? " FOR UPDATE" : ""), Integer.class, publicId);
        if (ids.isEmpty()) throw new ResponseStatusException(HttpStatus.UNAUTHORIZED);
        return ids.getFirst();
    }
    public Saved read(int user) {
        var rows = jdbc.query("SELECT revision, payload FROM user_settings WHERE user_seq = ?",
                (rs, n) -> new Saved(rs.getLong(1), rs.getString(2)), user);
        return rows.isEmpty() ? new Saved(0, null) : rows.getFirst();
    }
    public void write(int user, long revision, String payload) {
        jdbc.update("""
                INSERT INTO user_settings (user_seq, revision, payload) VALUES (?, ?, ?) AS incoming
                ON DUPLICATE KEY UPDATE revision = incoming.revision, payload = incoming.payload
                """, user, revision, payload);
    }
}
