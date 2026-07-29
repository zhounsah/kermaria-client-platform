ALTER TABLE portal_users
    ADD COLUMN role VARCHAR(32) NOT NULL DEFAULT 'client_user'
        AFTER status,
    ADD COLUMN failed_login_count INT NOT NULL DEFAULT 0
        AFTER last_login_at,
    ADD COLUMN last_failed_login_at DATETIME(6) NULL
        AFTER failed_login_count,
    ADD COLUMN locked_until DATETIME(6) NULL
        AFTER last_failed_login_at;
-- statement-break

CREATE INDEX ix_portal_users_role_status
    ON portal_users (role, status);
-- statement-break

CREATE INDEX ix_portal_sessions_active
    ON portal_sessions (revoked_at, expires_at);

