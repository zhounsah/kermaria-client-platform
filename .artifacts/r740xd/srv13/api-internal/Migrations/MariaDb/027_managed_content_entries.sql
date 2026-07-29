CREATE TABLE IF NOT EXISTS managed_content_entries (
    content_key VARCHAR(128) NOT NULL,
    content_type VARCHAR(32) NOT NULL,
    title VARCHAR(200) NOT NULL,
    public_path VARCHAR(255) NOT NULL,
    body_markdown LONGTEXT NOT NULL,
    version_label VARCHAR(160) NULL,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    PRIMARY KEY (content_key)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
