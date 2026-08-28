-- Diagnostic administrable : configuration versionnee, brouillon et publie.
-- La table ne porte que deux lignes possibles (`draft`, `published`) : le
-- parcours public ne lit que `published`, si bien qu'un brouillon en cours de
-- redaction ne peut jamais fuiter vers un visiteur.
SET NAMES utf8mb4;

-- statement-break

CREATE TABLE IF NOT EXISTS diagnostic_configurations (
    state VARCHAR(16) NOT NULL,
    payload_json JSON NOT NULL,
    version INT NOT NULL DEFAULT 1,
    updated_by_user_id CHAR(36) NULL,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    PRIMARY KEY (state),
    CONSTRAINT chk_diagnostic_configurations_state CHECK (state IN ('draft', 'published')),
    CONSTRAINT chk_diagnostic_configurations_version CHECK (version > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- statement-break

CREATE TABLE IF NOT EXISTS diagnostic_configuration_revisions (
    id CHAR(36) NOT NULL,
    state VARCHAR(16) NOT NULL,
    version INT NOT NULL,
    payload_json JSON NOT NULL,
    actor_user_id CHAR(36) NULL,
    correlation_id VARCHAR(128) NOT NULL,
    outcome VARCHAR(32) NOT NULL,
    created_at DATETIME(6) NOT NULL,
    PRIMARY KEY (id),
    KEY ix_diagnostic_configuration_revisions_state_created (state, created_at),
    CONSTRAINT chk_diagnostic_configuration_revisions_state CHECK (state IN ('draft', 'published'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
