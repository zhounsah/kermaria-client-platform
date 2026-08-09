CREATE TABLE IF NOT EXISTS admin_permission_grants (
    id CHAR(36) NOT NULL,
    user_id CHAR(36) NOT NULL,
    permission_code VARCHAR(120) NOT NULL,
    created_at DATETIME(6) NOT NULL,
    created_by_user_id CHAR(36) NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_admin_permission_grants_user_permission (user_id, permission_code),
    KEY ix_admin_permission_grants_permission (permission_code),
    CONSTRAINT fk_admin_permission_grants_user
        FOREIGN KEY (user_id) REFERENCES portal_users (id),
    CONSTRAINT fk_admin_permission_grants_created_by
        FOREIGN KEY (created_by_user_id) REFERENCES portal_users (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
-- statement-break

CREATE TABLE IF NOT EXISTS editorial_categories (
    id CHAR(36) NOT NULL,
    content_type VARCHAR(32) NOT NULL,
    name VARCHAR(160) NOT NULL,
    slug VARCHAR(100) NOT NULL,
    description VARCHAR(500) NULL,
    sort_order INT NOT NULL DEFAULT 0,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_editorial_categories_type_slug (content_type, slug),
    KEY ix_editorial_categories_type_order (content_type, sort_order)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
-- statement-break

CREATE TABLE IF NOT EXISTS editorial_contents (
    id CHAR(36) NOT NULL,
    content_type VARCHAR(32) NOT NULL,
    title VARCHAR(220) NOT NULL,
    slug VARCHAR(120) NOT NULL,
    summary VARCHAR(600) NULL,
    body_markdown LONGTEXT NOT NULL,
    category_id CHAR(36) NULL,
    status VARCHAR(24) NOT NULL,
    seo_title VARCHAR(220) NULL,
    seo_description VARCHAR(320) NULL,
    canonical_url VARCHAR(2048) NULL,
    no_index TINYINT(1) NOT NULL DEFAULT 0,
    sort_order INT NOT NULL DEFAULT 0,
    published_at DATETIME(6) NULL,
    created_by_user_id CHAR(36) NULL,
    updated_by_user_id CHAR(36) NULL,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_editorial_contents_type_slug (content_type, slug),
    KEY ix_editorial_contents_public (content_type, status, no_index, published_at),
    KEY ix_editorial_contents_category (category_id, status, sort_order),
    FULLTEXT KEY ft_editorial_contents_search (title, summary, body_markdown),
    CONSTRAINT fk_editorial_contents_category
        FOREIGN KEY (category_id) REFERENCES editorial_categories (id)
        ON DELETE SET NULL,
    CONSTRAINT fk_editorial_contents_created_by
        FOREIGN KEY (created_by_user_id) REFERENCES portal_users (id)
        ON DELETE SET NULL,
    CONSTRAINT fk_editorial_contents_updated_by
        FOREIGN KEY (updated_by_user_id) REFERENCES portal_users (id)
        ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
-- statement-break

CREATE TABLE IF NOT EXISTS editorial_faq_scopes (
    scope_key VARCHAR(80) NOT NULL,
    label VARCHAR(160) NOT NULL,
    sort_order INT NOT NULL DEFAULT 0,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    PRIMARY KEY (scope_key)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
-- statement-break

CREATE TABLE IF NOT EXISTS editorial_faq_scope_links (
    content_id CHAR(36) NOT NULL,
    scope_key VARCHAR(80) NOT NULL,
    sort_order INT NOT NULL DEFAULT 0,
    PRIMARY KEY (content_id, scope_key),
    KEY ix_editorial_faq_scope_links_scope (scope_key, sort_order),
    CONSTRAINT fk_editorial_faq_scope_links_content
        FOREIGN KEY (content_id) REFERENCES editorial_contents (id)
        ON DELETE CASCADE,
    CONSTRAINT fk_editorial_faq_scope_links_scope
        FOREIGN KEY (scope_key) REFERENCES editorial_faq_scopes (scope_key)
        ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
-- statement-break

CREATE TABLE IF NOT EXISTS editorial_content_revisions (
    id CHAR(36) NOT NULL,
    content_id CHAR(36) NOT NULL,
    version_number INT NOT NULL,
    action VARCHAR(80) NOT NULL,
    snapshot_json JSON NOT NULL,
    created_at DATETIME(6) NOT NULL,
    created_by_user_id CHAR(36) NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_editorial_revisions_content_version (content_id, version_number),
    KEY ix_editorial_revisions_content_created (content_id, created_at),
    CONSTRAINT fk_editorial_revisions_content
        FOREIGN KEY (content_id) REFERENCES editorial_contents (id)
        ON DELETE CASCADE,
    CONSTRAINT fk_editorial_revisions_created_by
        FOREIGN KEY (created_by_user_id) REFERENCES portal_users (id)
        ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
-- statement-break

CREATE TABLE IF NOT EXISTS editorial_redirects (
    id CHAR(36) NOT NULL,
    content_id CHAR(36) NOT NULL,
    content_type VARCHAR(32) NOT NULL,
    old_path VARCHAR(255) NOT NULL,
    new_path VARCHAR(255) NOT NULL,
    created_at DATETIME(6) NOT NULL,
    created_by_user_id CHAR(36) NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_editorial_redirects_old_path (old_path),
    KEY ix_editorial_redirects_content (content_id),
    CONSTRAINT fk_editorial_redirects_content
        FOREIGN KEY (content_id) REFERENCES editorial_contents (id)
        ON DELETE CASCADE,
    CONSTRAINT fk_editorial_redirects_created_by
        FOREIGN KEY (created_by_user_id) REFERENCES portal_users (id)
        ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
-- statement-break

INSERT IGNORE INTO editorial_faq_scopes (
    scope_key,
    label,
    sort_order,
    created_at,
    updated_at
) VALUES
    ('global', 'Global', 10, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('offres', 'Offres', 20, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('diagnostic', 'Diagnostic', 30, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6));
