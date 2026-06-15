CREATE TABLE IF NOT EXISTS commercial_offers (
    id CHAR(36) NOT NULL PRIMARY KEY,
    name VARCHAR(200) NOT NULL,
    description VARCHAR(1000) NOT NULL,
    category VARCHAR(100) NOT NULL,
    unit_label VARCHAR(40) NOT NULL,
    price_kind VARCHAR(16) NOT NULL DEFAULT 'ht',
    price_amount_cents INT NOT NULL,
    currency CHAR(3) NOT NULL DEFAULT 'EUR',
    status VARCHAR(32) NOT NULL DEFAULT 'active',
    display_order INT NOT NULL DEFAULT 0,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    KEY ix_commercial_offers_status_order (
        status,
        display_order,
        name
    )
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- statement-break

CREATE TABLE IF NOT EXISTS commercial_documents (
    id CHAR(36) NOT NULL PRIMARY KEY,
    customer_id CHAR(36) NOT NULL,
    service_request_id CHAR(36) NULL,
    document_type VARCHAR(32) NOT NULL,
    status VARCHAR(32) NOT NULL,
    title VARCHAR(200) NOT NULL,
    internal_reference VARCHAR(100) NOT NULL,
    currency CHAR(3) NOT NULL DEFAULT 'EUR',
    subtotal_amount_cents INT NOT NULL DEFAULT 0,
    tax_amount_cents INT NOT NULL DEFAULT 0,
    total_amount_cents INT NOT NULL DEFAULT 0,
    disclaimer VARCHAR(500) NOT NULL,
    created_by_user_id CHAR(36) NOT NULL,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    shared_at DATETIME(6) NULL,
    cancelled_at DATETIME(6) NULL,
    UNIQUE KEY ux_commercial_documents_reference (internal_reference),
    KEY ix_commercial_documents_customer_status (
        customer_id,
        status,
        updated_at
    ),
    KEY ix_commercial_documents_service_request (service_request_id),
    KEY ix_commercial_documents_shared (shared_at),
    CONSTRAINT fk_commercial_documents_customer
        FOREIGN KEY (customer_id) REFERENCES customers (id),
    CONSTRAINT fk_commercial_documents_service_request
        FOREIGN KEY (service_request_id) REFERENCES service_requests (id),
    CONSTRAINT fk_commercial_documents_author
        FOREIGN KEY (created_by_user_id) REFERENCES portal_users (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- statement-break

CREATE TABLE IF NOT EXISTS commercial_document_lines (
    id CHAR(36) NOT NULL PRIMARY KEY,
    document_id CHAR(36) NOT NULL,
    offer_id CHAR(36) NULL,
    label VARCHAR(200) NOT NULL,
    description VARCHAR(1000) NOT NULL,
    quantity DECIMAL(18,2) NOT NULL,
    unit_label VARCHAR(40) NOT NULL,
    unit_price_cents INT NOT NULL,
    tax_rate_basis_points INT NULL,
    line_total_cents INT NOT NULL,
    sort_order INT NOT NULL DEFAULT 0,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    KEY ix_commercial_document_lines_document (
        document_id,
        sort_order,
        created_at
    ),
    CONSTRAINT fk_commercial_document_lines_document
        FOREIGN KEY (document_id) REFERENCES commercial_documents (id),
    CONSTRAINT fk_commercial_document_lines_offer
        FOREIGN KEY (offer_id) REFERENCES commercial_offers (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- statement-break

INSERT INTO commercial_offers (
    id,
    name,
    description,
    category,
    unit_label,
    price_kind,
    price_amount_cents,
    currency,
    status,
    display_order,
    created_at,
    updated_at
)
SELECT
    UUID(),
    'Sauvegarde dossier personnel',
    'Sauvegarde informative d’un dossier personnel client. Offre de démonstration commerciale non contractuelle.',
    'Sauvegarde',
    'mois',
    'ht',
    500,
    'EUR',
    'active',
    10,
    NOW(6),
    NOW(6)
WHERE NOT EXISTS (
    SELECT 1
    FROM commercial_offers
    WHERE name = 'Sauvegarde dossier personnel'
);

-- statement-break

INSERT INTO commercial_documents (
    id,
    customer_id,
    service_request_id,
    document_type,
    status,
    title,
    internal_reference,
    currency,
    subtotal_amount_cents,
    tax_amount_cents,
    total_amount_cents,
    disclaimer,
    created_by_user_id,
    created_at,
    updated_at,
    shared_at,
    cancelled_at
)
SELECT
    '70000000-0000-0000-0000-000000000001',
    '10000000-0000-0000-0000-000000000001',
    '50000000-0000-0000-0000-000000000001',
    'quote_draft',
    'shared_with_customer',
    'Proposition d''accompagnement VPN',
    'COM-DEMO-2026-0001',
    'EUR',
    19400,
    3880,
    23280,
    'Document informatif â€” ne constitue pas une facture officielle.',
    '11000000-0000-0000-0000-000000000001',
    '2026-06-12 10:00:00',
    '2026-06-12 10:30:00',
    '2026-06-12 10:30:00',
    NULL
WHERE EXISTS (
    SELECT 1
    FROM customers
    WHERE id = '10000000-0000-0000-0000-000000000001'
)
AND EXISTS (
    SELECT 1
    FROM portal_users
    WHERE id = '11000000-0000-0000-0000-000000000001'
      AND role = 'internal_admin'
)
AND EXISTS (
    SELECT 1
    FROM service_requests
    WHERE id = '50000000-0000-0000-0000-000000000001'
)
AND NOT EXISTS (
    SELECT 1
    FROM commercial_documents
    WHERE id = '70000000-0000-0000-0000-000000000001'
);

-- statement-break

INSERT INTO commercial_documents (
    id,
    customer_id,
    service_request_id,
    document_type,
    status,
    title,
    internal_reference,
    currency,
    subtotal_amount_cents,
    tax_amount_cents,
    total_amount_cents,
    disclaimer,
    created_by_user_id,
    created_at,
    updated_at,
    shared_at,
    cancelled_at
)
SELECT
    '70000000-0000-0000-0000-000000000002',
    '10000000-0000-0000-0000-000000000001',
    NULL,
    'billing_draft',
    'draft',
    'Préparation de document de suivi',
    'COM-DEMO-2026-0002',
    'EUR',
    4500,
    0,
    4500,
    'Document informatif â€” ne constitue pas une facture officielle.',
    '11000000-0000-0000-0000-000000000001',
    '2026-06-13 08:45:00',
    '2026-06-13 08:45:00',
    NULL,
    NULL
WHERE EXISTS (
    SELECT 1
    FROM customers
    WHERE id = '10000000-0000-0000-0000-000000000001'
)
AND EXISTS (
    SELECT 1
    FROM portal_users
    WHERE id = '11000000-0000-0000-0000-000000000001'
      AND role = 'internal_admin'
)
AND NOT EXISTS (
    SELECT 1
    FROM commercial_documents
    WHERE id = '70000000-0000-0000-0000-000000000002'
);

-- statement-break

INSERT INTO commercial_document_lines (
    id,
    document_id,
    offer_id,
    label,
    description,
    quantity,
    unit_label,
    unit_price_cents,
    tax_rate_basis_points,
    line_total_cents,
    sort_order,
    created_at,
    updated_at
)
SELECT
    '80000000-0000-0000-0000-000000000001',
    '70000000-0000-0000-0000-000000000001',
    NULL,
    'Intervention ponctuelle',
    'Qualification informative de l''accès VPN envisagé.',
    2.00,
    'heure',
    8500,
    2000,
    17000,
    10,
    '2026-06-12 10:00:00',
    '2026-06-12 10:00:00'
WHERE EXISTS (
    SELECT 1
    FROM commercial_documents
    WHERE id = '70000000-0000-0000-0000-000000000001'
)
AND NOT EXISTS (
    SELECT 1
    FROM commercial_document_lines
    WHERE id = '80000000-0000-0000-0000-000000000001'
);

-- statement-break

INSERT INTO commercial_document_lines (
    id,
    document_id,
    offer_id,
    label,
    description,
    quantity,
    unit_label,
    unit_price_cents,
    tax_rate_basis_points,
    line_total_cents,
    sort_order,
    created_at,
    updated_at
)
SELECT
    '80000000-0000-0000-0000-000000000002',
    '70000000-0000-0000-0000-000000000001',
    NULL,
    'Sauvegarde additionnelle',
    'Option informative associée à la proposition.',
    1.00,
    'mois',
    2400,
    2000,
    2400,
    20,
    '2026-06-12 10:05:00',
    '2026-06-12 10:05:00'
WHERE EXISTS (
    SELECT 1
    FROM commercial_documents
    WHERE id = '70000000-0000-0000-0000-000000000001'
)
AND NOT EXISTS (
    SELECT 1
    FROM commercial_document_lines
    WHERE id = '80000000-0000-0000-0000-000000000002'
);

-- statement-break

INSERT INTO commercial_document_lines (
    id,
    document_id,
    offer_id,
    label,
    description,
    quantity,
    unit_label,
    unit_price_cents,
    tax_rate_basis_points,
    line_total_cents,
    sort_order,
    created_at,
    updated_at
)
SELECT
    '80000000-0000-0000-0000-000000000003',
    '70000000-0000-0000-0000-000000000002',
    NULL,
    'Accompagnement initial',
    'Brouillon de ligne informative interne.',
    1.00,
    'forfait',
    4500,
    NULL,
    4500,
    10,
    '2026-06-13 08:45:00',
    '2026-06-13 08:45:00'
WHERE EXISTS (
    SELECT 1
    FROM commercial_documents
    WHERE id = '70000000-0000-0000-0000-000000000002'
)
AND NOT EXISTS (
    SELECT 1
    FROM commercial_document_lines
    WHERE id = '80000000-0000-0000-0000-000000000003'
);

-- statement-break

INSERT INTO schema_migrations (migration_id, applied_at)
SELECT '006_commercial_foundation', NOW(6)
WHERE NOT EXISTS (
    SELECT 1
    FROM schema_migrations
    WHERE migration_id = '006_commercial_foundation'
);
