-- Centre de configuration : registre ferme cote code, valeurs simples uniquement.
SET NAMES utf8mb4;

-- statement-break

CREATE TABLE IF NOT EXISTS application_settings (
    setting_key VARCHAR(120) NOT NULL,
    category VARCHAR(64) NOT NULL,
    value_json JSON NOT NULL,
    value_type VARCHAR(24) NOT NULL,
    version INT NOT NULL DEFAULT 1,
    updated_by_user_id CHAR(36) NULL,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    PRIMARY KEY (setting_key),
    CONSTRAINT chk_application_settings_version CHECK (version > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- statement-break

CREATE TABLE IF NOT EXISTS application_setting_revisions (
    id CHAR(36) NOT NULL,
    setting_key VARCHAR(120) NOT NULL,
    version INT NOT NULL,
    old_value_json JSON NULL,
    new_value_json JSON NULL,
    actor_user_id CHAR(36) NULL,
    correlation_id VARCHAR(128) NOT NULL,
    outcome VARCHAR(32) NOT NULL,
    created_at DATETIME(6) NOT NULL,
    PRIMARY KEY (id),
    KEY ix_application_setting_revisions_key_created (setting_key, created_at),
    CONSTRAINT fk_application_setting_revisions_setting FOREIGN KEY (setting_key) REFERENCES application_settings(setting_key)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
