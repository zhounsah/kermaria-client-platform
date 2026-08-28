-- Templates transactionnels et notifications : contenu strictement texte,
-- autorites fermees cote code. Aucun secret ni moteur d'expression.
SET NAMES utf8mb4;

-- statement-break

CREATE TABLE IF NOT EXISTS email_templates (
    template_key VARCHAR(120) NOT NULL,
    display_name VARCHAR(160) NOT NULL,
    subject_template TEXT NOT NULL,
    body_template TEXT NOT NULL,
    enabled TINYINT(1) NOT NULL DEFAULT 1,
    version INT NOT NULL DEFAULT 1,
    updated_by_user_id CHAR(36) NULL,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    PRIMARY KEY (template_key),
    CONSTRAINT chk_email_templates_version CHECK (version > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- statement-break

CREATE TABLE IF NOT EXISTS email_template_revisions (
    id CHAR(36) NOT NULL,
    template_key VARCHAR(120) NOT NULL,
    version INT NOT NULL,
    subject_template TEXT NULL,
    body_template TEXT NULL,
    enabled TINYINT(1) NULL,
    actor_user_id CHAR(36) NULL,
    correlation_id VARCHAR(128) NOT NULL,
    outcome VARCHAR(32) NOT NULL,
    created_at DATETIME(6) NOT NULL,
    PRIMARY KEY (id),
    KEY ix_email_template_revisions_key_created (template_key, created_at),
    CONSTRAINT fk_email_template_revisions_template FOREIGN KEY (template_key)
        REFERENCES email_templates(template_key)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- statement-break

CREATE TABLE IF NOT EXISTS notification_templates (
    template_key VARCHAR(120) NOT NULL,
    display_name VARCHAR(160) NOT NULL,
    title_template TEXT NOT NULL,
    message_template TEXT NOT NULL,
    enabled TINYINT(1) NOT NULL DEFAULT 1,
    version INT NOT NULL DEFAULT 1,
    updated_by_user_id CHAR(36) NULL,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    PRIMARY KEY (template_key),
    CONSTRAINT chk_notification_templates_version CHECK (version > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- statement-break

-- L'historique des notifications suit la meme regle que celui des e-mails :
-- une ligne par mutation, jamais de secret, correlation_id obligatoire.
CREATE TABLE IF NOT EXISTS notification_template_revisions (
    id CHAR(36) NOT NULL,
    template_key VARCHAR(120) NOT NULL,
    version INT NOT NULL,
    title_template TEXT NULL,
    message_template TEXT NULL,
    enabled TINYINT(1) NULL,
    actor_user_id CHAR(36) NULL,
    correlation_id VARCHAR(128) NOT NULL,
    outcome VARCHAR(32) NOT NULL,
    created_at DATETIME(6) NOT NULL,
    PRIMARY KEY (id),
    KEY ix_notification_template_revisions_key_created (template_key, created_at),
    CONSTRAINT fk_notification_template_revisions_template FOREIGN KEY (template_key)
        REFERENCES notification_templates(template_key)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- statement-break

CREATE TABLE IF NOT EXISTS system_snippets (
    snippet_key VARCHAR(120) NOT NULL,
    display_name VARCHAR(160) NOT NULL,
    body_text TEXT NOT NULL,
    version INT NOT NULL DEFAULT 1,
    updated_by_user_id CHAR(36) NULL,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    PRIMARY KEY (snippet_key),
    CONSTRAINT chk_system_snippets_version CHECK (version > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- statement-break

CREATE TABLE IF NOT EXISTS system_snippet_revisions (
    id CHAR(36) NOT NULL,
    snippet_key VARCHAR(120) NOT NULL,
    version INT NOT NULL,
    body_text TEXT NULL,
    actor_user_id CHAR(36) NULL,
    correlation_id VARCHAR(128) NOT NULL,
    outcome VARCHAR(32) NOT NULL,
    created_at DATETIME(6) NOT NULL,
    PRIMARY KEY (id),
    KEY ix_system_snippet_revisions_key_created (snippet_key, created_at),
    CONSTRAINT fk_system_snippet_revisions_snippet FOREIGN KEY (snippet_key)
        REFERENCES system_snippets(snippet_key)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
