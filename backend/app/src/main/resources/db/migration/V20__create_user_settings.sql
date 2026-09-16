CREATE TABLE user_settings (
    user_seq INT UNSIGNED NOT NULL PRIMARY KEY,
    revision BIGINT NOT NULL,
    payload JSON NOT NULL,
    CONSTRAINT fk_user_settings_user FOREIGN KEY (user_seq) REFERENCES users(users_seq) ON DELETE CASCADE
);
