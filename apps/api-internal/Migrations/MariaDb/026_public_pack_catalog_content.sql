CREATE TABLE IF NOT EXISTS public_pack_catalog_content (
    content_key VARCHAR(64) NOT NULL,
    content_json LONGTEXT NOT NULL,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    PRIMARY KEY (content_key)
);
