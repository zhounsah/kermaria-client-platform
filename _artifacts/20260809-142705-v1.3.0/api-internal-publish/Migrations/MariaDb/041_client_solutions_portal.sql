CREATE TABLE IF NOT EXISTS client_solution_portal_settings (
    settings_key VARCHAR(32) NOT NULL,
    eyebrow VARCHAR(120) NULL,
    title VARCHAR(160) NOT NULL,
    description VARCHAR(600) NULL,
    footer_note VARCHAR(600) NULL,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    PRIMARY KEY (settings_key)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS client_solutions (
    id CHAR(36) NOT NULL,
    slug VARCHAR(80) NOT NULL,
    title VARCHAR(120) NOT NULL,
    tagline VARCHAR(280) NULL,
    target_url VARCHAR(2048) NOT NULL,
    opens_in_new_tab TINYINT(1) NOT NULL DEFAULT 1,
    status VARCHAR(20) NOT NULL,
    display_order INT NOT NULL DEFAULT 0,
    logo_content_type VARCHAR(160) NULL,
    logo_original_name VARCHAR(180) NULL,
    logo_size_bytes INT NULL,
    logo_bytes MEDIUMBLOB NULL,
    logo_updated_at DATETIME(6) NULL,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_client_solutions_slug (slug),
    KEY idx_client_solutions_status (status, display_order)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- L'en-tete de la page est administrable : on ne seede que la valeur par
-- defaut, les tuiles elles-memes sont creees depuis le back-office.
INSERT IGNORE INTO client_solution_portal_settings (
    settings_key,
    eyebrow,
    title,
    description,
    footer_note,
    created_at,
    updated_at
) VALUES (
    'default',
    'Portail de services',
    'Accéder à mes solutions',
    'Retrouvez ici les accès directs aux services mis à votre disposition. Cliquez sur une tuile pour ouvrir le service correspondant.',
    NULL,
    UTC_TIMESTAMP(6),
    UTC_TIMESTAMP(6)
);
