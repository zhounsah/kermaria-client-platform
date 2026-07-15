CREATE TABLE IF NOT EXISTS download_categories (
    id CHAR(36) NOT NULL,
    slug VARCHAR(80) NOT NULL,
    title VARCHAR(120) NOT NULL,
    description VARCHAR(280) NULL,
    status VARCHAR(20) NOT NULL,
    display_order INT NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    UNIQUE KEY uq_download_categories_slug (slug)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS download_resources (
    id CHAR(36) NOT NULL,
    category_id CHAR(36) NOT NULL,
    title VARCHAR(140) NOT NULL,
    short_description VARCHAR(320) NOT NULL,
    resource_type VARCHAR(30) NOT NULL,
    source_kind VARCHAR(30) NOT NULL,
    visibility_mode VARCHAR(30) NOT NULL,
    status VARCHAR(20) NOT NULL,
    external_url VARCHAR(2048) NULL,
    version_label VARCHAR(80) NULL,
    installation_instructions TEXT NULL,
    display_order INT NOT NULL DEFAULT 0,
    internal_file_storage_key VARCHAR(120) NULL,
    internal_file_original_name VARCHAR(180) NULL,
    internal_file_content_type VARCHAR(160) NULL,
    internal_file_size_bytes BIGINT NULL,
    internal_file_extension VARCHAR(20) NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    KEY idx_download_resources_category (category_id),
    KEY idx_download_resources_status (status),
    CONSTRAINT fk_download_resources_category
        FOREIGN KEY (category_id)
        REFERENCES download_categories (id)
        ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS download_resource_visibility_rules (
    id CHAR(36) NOT NULL,
    resource_id CHAR(36) NOT NULL,
    target_type VARCHAR(40) NOT NULL,
    target_value VARCHAR(160) NOT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    UNIQUE KEY uq_download_rules_resource_target (
        resource_id,
        target_type,
        target_value
    ),
    KEY idx_download_rules_resource (resource_id),
    CONSTRAINT fk_download_rules_resource
        FOREIGN KEY (resource_id)
        REFERENCES download_resources (id)
        ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT IGNORE INTO download_categories (
    id,
    slug,
    title,
    description,
    status,
    display_order,
    created_at,
    updated_at
) VALUES
    ('4fd5306a-4dc5-4784-b59f-3cb2878e8d31', 'logiciels', 'Logiciels', NULL, 'active', 10, UTC_TIMESTAMP(), UTC_TIMESTAMP()),
    ('f911c8cc-569d-4535-b958-f3ccfe55f3d3', 'scripts', 'Scripts', NULL, 'active', 20, UTC_TIMESTAMP(), UTC_TIMESTAMP()),
    ('57e06687-d358-4fd2-9fd2-657e1f33d76a', 'fichiers-rdp', 'Fichiers RDP', NULL, 'active', 30, UTC_TIMESTAMP(), UTC_TIMESTAMP()),
    ('fd52b9a0-ee34-4cb1-aab4-6cc7be14e2a0', 'documentation', 'Documentation', NULL, 'active', 40, UTC_TIMESTAMP(), UTC_TIMESTAMP()),
    ('d62b536b-709b-4432-b49d-c7c4f4cc6887', 'outils-complementaires', 'Outils complémentaires', NULL, 'active', 50, UTC_TIMESTAMP(), UTC_TIMESTAMP());
