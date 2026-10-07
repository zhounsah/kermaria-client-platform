CREATE TABLE IF NOT EXISTS site_page_layouts (
    page_key VARCHAR(240) NOT NULL PRIMARY KEY,
    area VARCHAR(16) NOT NULL,
    document_json LONGTEXT NOT NULL,
    version BIGINT NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    updated_by CHAR(36) NOT NULL,
    CONSTRAINT chk_site_page_layouts_json CHECK (JSON_VALID(document_json)),
    CONSTRAINT chk_site_page_layouts_version CHECK (version > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
-- statement-break

CREATE TABLE IF NOT EXISTS site_page_layout_revisions (
    page_key VARCHAR(240) NOT NULL,
    version BIGINT NOT NULL,
    document_json LONGTEXT NOT NULL,
    created_at DATETIME(6) NOT NULL,
    created_by CHAR(36) NOT NULL,
    PRIMARY KEY (page_key, version),
    CONSTRAINT fk_site_page_layout_revisions_page FOREIGN KEY (page_key)
        REFERENCES site_page_layouts(page_key) ON UPDATE RESTRICT ON DELETE RESTRICT,
    CONSTRAINT chk_site_page_layout_revisions_json CHECK (JSON_VALID(document_json))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
-- statement-break

CREATE TABLE IF NOT EXISTS site_media_assets (
    id CHAR(36) NOT NULL PRIMARY KEY,
    file_name VARCHAR(180) NOT NULL,
    content_type VARCHAR(32) NOT NULL,
    alt_text VARCHAR(240) NOT NULL,
    byte_length INT NOT NULL,
    data MEDIUMBLOB NOT NULL,
    created_at DATETIME(6) NOT NULL,
    created_by CHAR(36) NOT NULL,
    KEY ix_site_media_assets_created (created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
