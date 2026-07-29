ALTER TABLE portal_users
    ADD COLUMN password_hash VARCHAR(512) NULL AFTER email;
-- statement-break

CREATE UNIQUE INDEX ux_portal_users_email
    ON portal_users (email);
-- statement-break

CREATE TABLE portal_sessions (
    id CHAR(36) NOT NULL PRIMARY KEY,
    user_id CHAR(36) NOT NULL,
    session_token_hash CHAR(64) NOT NULL,
    created_at DATETIME(6) NOT NULL,
    expires_at DATETIME(6) NOT NULL,
    revoked_at DATETIME(6) NULL,
    last_seen_at DATETIME(6) NULL,
    ip_address VARCHAR(100) NULL,
    user_agent VARCHAR(500) NULL,
    UNIQUE KEY ux_portal_sessions_token_hash (session_token_hash),
    KEY ix_portal_sessions_user (user_id),
    KEY ix_portal_sessions_expiry (expires_at),
    CONSTRAINT fk_portal_sessions_user
        FOREIGN KEY (user_id) REFERENCES portal_users (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
