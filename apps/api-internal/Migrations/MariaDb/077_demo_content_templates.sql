-- Modeles de contenu de demonstration administrables.
--
-- Le registre C# `DemoContentTemplateRegistry` reste le repli : tant que la
-- table est vide ou illisible, c'est lui qui fait autorite. La bascule est donc
-- progressive et reversible — vider la table suffit a revenir au code.
--
-- `service_type` reste contraint par le registre ferme des types de service :
-- l'administration ne peut pas inventer un type que le code ne sait pas
-- provisionner, ce qui contournerait les validations metier.
SET NAMES utf8mb4;

-- statement-break

CREATE TABLE IF NOT EXISTS demo_content_templates (
    template_key VARCHAR(64) NOT NULL,
    label VARCHAR(120) NOT NULL,
    description VARCHAR(500) NOT NULL DEFAULT '',
    enabled TINYINT(1) NOT NULL DEFAULT 1,
    display_order INT NOT NULL DEFAULT 100,
    version INT NOT NULL DEFAULT 1,
    updated_by_user_id CHAR(36) NULL,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    PRIMARY KEY (template_key),
    KEY idx_demo_content_templates_enabled (enabled, display_order),
    CONSTRAINT chk_demo_content_templates_version CHECK (version > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- statement-break

CREATE TABLE IF NOT EXISTS demo_content_template_services (
    id CHAR(36) NOT NULL,
    template_key VARCHAR(64) NOT NULL,
    service_type VARCHAR(64) NOT NULL,
    name VARCHAR(160) NOT NULL,
    description VARCHAR(500) NOT NULL,
    scope VARCHAR(300) NOT NULL,
    display_order INT NOT NULL DEFAULT 100,
    PRIMARY KEY (id),
    -- Le nom identifie le service dans la composition a la carte : il doit
    -- rester unique au sein d'un modele.
    UNIQUE KEY uk_demo_template_services_name (template_key, name),
    KEY idx_demo_template_services_order (template_key, display_order),
    CONSTRAINT fk_demo_template_services_template
        FOREIGN KEY (template_key) REFERENCES demo_content_templates (template_key)
        ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- statement-break

CREATE TABLE IF NOT EXISTS demo_content_template_revisions (
    id CHAR(36) NOT NULL,
    template_key VARCHAR(64) NOT NULL,
    version INT NOT NULL,
    payload_json JSON NOT NULL,
    actor_user_id CHAR(36) NULL,
    correlation_id VARCHAR(128) NOT NULL,
    outcome VARCHAR(32) NOT NULL,
    created_at DATETIME(6) NOT NULL,
    PRIMARY KEY (id),
    KEY idx_demo_template_revisions_key (template_key, created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
