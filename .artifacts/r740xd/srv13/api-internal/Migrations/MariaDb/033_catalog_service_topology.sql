ALTER TABLE commercial_offers
    ADD COLUMN IF NOT EXISTS technical_service_references TEXT NULL DEFAULT NULL;

-- statement-break

ALTER TABLE commercial_offers
    ADD COLUMN IF NOT EXISTS provisioning_group_sam_account_names TEXT NULL DEFAULT NULL;

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
    tax_rate_basis_points,
    external_reference,
    technical_service_references,
    provisioning_group_sam_account_names,
    status,
    display_order,
    billing_cadence,
    setup_fee_amount_cents,
    billing_interval_months,
    commitment_months,
    payment_mode,
    public_pack_code,
    created_at,
    updated_at
)
SELECT
    '62000000-0000-0000-0000-000000000001',
    'Hébergement dossier personnel 32 Go',
    'Service technique de stockage personnel 32 Go.',
    'Service technique',
    'service',
    'ht',
    0,
    'EUR',
    2000,
    'STOCK-PERSO-32',
    '["STOCK-PERSO-32"]',
    NULL,
    'inactive',
    901,
    'one_time',
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    UTC_TIMESTAMP(6),
    UTC_TIMESTAMP(6)
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1
    FROM commercial_offers
    WHERE external_reference = 'STOCK-PERSO-32'
);

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
    tax_rate_basis_points,
    external_reference,
    technical_service_references,
    provisioning_group_sam_account_names,
    status,
    display_order,
    billing_cadence,
    setup_fee_amount_cents,
    billing_interval_months,
    commitment_months,
    payment_mode,
    public_pack_code,
    created_at,
    updated_at
)
SELECT
    '62000000-0000-0000-0000-000000000002',
    'Sauvegarde dossier personnel',
    'Service technique de sauvegarde du dossier personnel.',
    'Service technique',
    'service',
    'ht',
    0,
    'EUR',
    2000,
    'SAVE-PERSO',
    '["SAVE-PERSO"]',
    NULL,
    'inactive',
    902,
    'one_time',
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    UTC_TIMESTAMP(6),
    UTC_TIMESTAMP(6)
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1
    FROM commercial_offers
    WHERE external_reference = 'SAVE-PERSO'
);

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
    tax_rate_basis_points,
    external_reference,
    technical_service_references,
    provisioning_group_sam_account_names,
    status,
    display_order,
    billing_cadence,
    setup_fee_amount_cents,
    billing_interval_months,
    commitment_months,
    payment_mode,
    public_pack_code,
    created_at,
    updated_at
)
SELECT
    '62000000-0000-0000-0000-000000000003',
    'Accès VPN',
    'Service technique d''accès VPN.',
    'Service technique',
    'service',
    'ht',
    0,
    'EUR',
    2000,
    'ACCES-VPN',
    '["ACCES-VPN"]',
    '["GG_VPN"]',
    'inactive',
    903,
    'one_time',
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    UTC_TIMESTAMP(6),
    UTC_TIMESTAMP(6)
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1
    FROM commercial_offers
    WHERE external_reference = 'ACCES-VPN'
);

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
    tax_rate_basis_points,
    external_reference,
    technical_service_references,
    provisioning_group_sam_account_names,
    status,
    display_order,
    billing_cadence,
    setup_fee_amount_cents,
    billing_interval_months,
    commitment_months,
    payment_mode,
    public_pack_code,
    created_at,
    updated_at
)
SELECT
    '62000000-0000-0000-0000-000000000004',
    'Supervision du service',
    'Service technique de supervision.',
    'Service technique',
    'service',
    'ht',
    0,
    'EUR',
    2000,
    'SUPERV-SERVICE',
    '["SUPERV-SERVICE"]',
    NULL,
    'inactive',
    904,
    'one_time',
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    UTC_TIMESTAMP(6),
    UTC_TIMESTAMP(6)
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1
    FROM commercial_offers
    WHERE external_reference = 'SUPERV-SERVICE'
);

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
    tax_rate_basis_points,
    external_reference,
    technical_service_references,
    provisioning_group_sam_account_names,
    status,
    display_order,
    billing_cadence,
    setup_fee_amount_cents,
    billing_interval_months,
    commitment_months,
    payment_mode,
    public_pack_code,
    created_at,
    updated_at
)
SELECT
    '62000000-0000-0000-0000-000000000005',
    'Support niveau 1',
    'Service technique de support de premier niveau.',
    'Service technique',
    'service',
    'ht',
    0,
    'EUR',
    2000,
    'SUPPORT-LV1',
    '["SUPPORT-LV1"]',
    NULL,
    'inactive',
    905,
    'one_time',
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    UTC_TIMESTAMP(6),
    UTC_TIMESTAMP(6)
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1
    FROM commercial_offers
    WHERE external_reference = 'SUPPORT-LV1'
);

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
    tax_rate_basis_points,
    external_reference,
    technical_service_references,
    provisioning_group_sam_account_names,
    status,
    display_order,
    billing_cadence,
    setup_fee_amount_cents,
    billing_interval_months,
    commitment_months,
    payment_mode,
    public_pack_code,
    created_at,
    updated_at
)
SELECT
    '62000000-0000-0000-0000-000000000006',
    'Accès bureau distant / RDS',
    'Service technique d''accès RDS.',
    'Service technique',
    'service',
    'ht',
    0,
    'EUR',
    2000,
    'ACCES-RDS',
    '["ACCES-RDS"]',
    '["GG_RDS"]',
    'inactive',
    906,
    'one_time',
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    UTC_TIMESTAMP(6),
    UTC_TIMESTAMP(6)
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1
    FROM commercial_offers
    WHERE external_reference = 'ACCES-RDS'
);

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
    tax_rate_basis_points,
    external_reference,
    technical_service_references,
    provisioning_group_sam_account_names,
    status,
    display_order,
    billing_cadence,
    setup_fee_amount_cents,
    billing_interval_months,
    commitment_months,
    payment_mode,
    public_pack_code,
    created_at,
    updated_at
)
SELECT
    '62000000-0000-0000-0000-000000000007',
    'Utilisateur supplémentaire',
    'Service technique d''ajout d''utilisateur.',
    'Service technique',
    'service',
    'ht',
    0,
    'EUR',
    2000,
    'USER-ADD',
    '["USER-ADD"]',
    NULL,
    'inactive',
    907,
    'one_time',
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    UTC_TIMESTAMP(6),
    UTC_TIMESTAMP(6)
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1
    FROM commercial_offers
    WHERE external_reference = 'USER-ADD'
);

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
    tax_rate_basis_points,
    external_reference,
    technical_service_references,
    provisioning_group_sam_account_names,
    status,
    display_order,
    billing_cadence,
    setup_fee_amount_cents,
    billing_interval_months,
    commitment_months,
    payment_mode,
    public_pack_code,
    created_at,
    updated_at
)
SELECT
    '62000000-0000-0000-0000-000000000008',
    'Stockage supplémentaire 32 Go',
    'Service technique de stockage supplémentaire.',
    'Service technique',
    'service',
    'ht',
    0,
    'EUR',
    2000,
    'STOCK-SUP-32',
    '["STOCK-SUP-32"]',
    NULL,
    'inactive',
    908,
    'one_time',
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    UTC_TIMESTAMP(6),
    UTC_TIMESTAMP(6)
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1
    FROM commercial_offers
    WHERE external_reference = 'STOCK-SUP-32'
);

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
    tax_rate_basis_points,
    external_reference,
    technical_service_references,
    provisioning_group_sam_account_names,
    status,
    display_order,
    billing_cadence,
    setup_fee_amount_cents,
    billing_interval_months,
    commitment_months,
    payment_mode,
    public_pack_code,
    created_at,
    updated_at
)
SELECT
    '62000000-0000-0000-0000-000000000009',
    'Documentation technique',
    'Service technique de documentation.',
    'Service technique',
    'service',
    'ht',
    0,
    'EUR',
    2000,
    'DOC-TECH',
    '["DOC-TECH"]',
    NULL,
    'inactive',
    909,
    'one_time',
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    UTC_TIMESTAMP(6),
    UTC_TIMESTAMP(6)
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1
    FROM commercial_offers
    WHERE external_reference = 'DOC-TECH'
);

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
    tax_rate_basis_points,
    external_reference,
    technical_service_references,
    provisioning_group_sam_account_names,
    status,
    display_order,
    billing_cadence,
    setup_fee_amount_cents,
    billing_interval_months,
    commitment_months,
    payment_mode,
    public_pack_code,
    created_at,
    updated_at
)
SELECT
    '62000000-0000-0000-0000-000000000010',
    'Nextcloud',
    'Service technique Nextcloud.',
    'Service technique',
    'service',
    'ht',
    0,
    'EUR',
    2000,
    'NEXTCLOUD',
    '["NEXTCLOUD"]',
    '["GG_NextCloud"]',
    'inactive',
    910,
    'one_time',
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    UTC_TIMESTAMP(6),
    UTC_TIMESTAMP(6)
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1
    FROM commercial_offers
    WHERE external_reference = 'NEXTCLOUD'
);

-- statement-break

UPDATE commercial_offers
SET technical_service_references = CASE public_pack_code
    WHEN 'pack-dossier-securise' THEN '["STOCK-PERSO-32","SAVE-PERSO"]'
    WHEN 'pack-acces-distance' THEN '["STOCK-PERSO-32","SAVE-PERSO","ACCES-VPN","SUPERV-SERVICE","SUPPORT-LV1"]'
    WHEN 'pack-bureau-windows-distance' THEN '["STOCK-PERSO-32","SAVE-PERSO","ACCES-VPN","ACCES-RDS","SUPERV-SERVICE","SUPPORT-LV1"]'
    WHEN 'pack-pro-association' THEN '["USER-ADD","STOCK-PERSO-32","STOCK-SUP-32","ACCES-VPN","SAVE-PERSO","SUPERV-SERVICE","SUPPORT-LV1","DOC-TECH"]'
    ELSE technical_service_references
END
WHERE public_pack_code IN (
        'pack-dossier-securise',
        'pack-acces-distance',
        'pack-bureau-windows-distance',
        'pack-pro-association'
    )
  AND (technical_service_references IS NULL OR TRIM(technical_service_references) = '');

-- statement-break

UPDATE commercial_offers
SET technical_service_references = CASE external_reference
    WHEN 'STOCK-PERSO-32' THEN '["STOCK-PERSO-32"]'
    WHEN 'SAVE-PERSO' THEN '["SAVE-PERSO"]'
    WHEN 'ACCES-VPN' THEN '["ACCES-VPN"]'
    WHEN 'SUPERV-SERVICE' THEN '["SUPERV-SERVICE"]'
    WHEN 'SUPPORT-LV1' THEN '["SUPPORT-LV1"]'
    WHEN 'ACCES-RDS' THEN '["ACCES-RDS"]'
    WHEN 'USER-ADD' THEN '["USER-ADD"]'
    WHEN 'STOCK-SUP-32' THEN '["STOCK-SUP-32"]'
    WHEN 'DOC-TECH' THEN '["DOC-TECH"]'
    WHEN 'NEXTCLOUD' THEN '["NEXTCLOUD"]'
    ELSE technical_service_references
END
WHERE external_reference IN (
        'STOCK-PERSO-32',
        'SAVE-PERSO',
        'ACCES-VPN',
        'SUPERV-SERVICE',
        'SUPPORT-LV1',
        'ACCES-RDS',
        'USER-ADD',
        'STOCK-SUP-32',
        'DOC-TECH',
        'NEXTCLOUD'
    )
  AND (technical_service_references IS NULL OR TRIM(technical_service_references) = '');

-- statement-break

UPDATE commercial_offers
SET provisioning_group_sam_account_names = CASE external_reference
    WHEN 'ACCES-VPN' THEN '["GG_VPN"]'
    WHEN 'ACCES-RDS' THEN '["GG_RDS"]'
    WHEN 'NEXTCLOUD' THEN '["GG_NextCloud"]'
    ELSE provisioning_group_sam_account_names
END
WHERE external_reference IN ('ACCES-VPN', 'ACCES-RDS', 'NEXTCLOUD')
  AND (provisioning_group_sam_account_names IS NULL OR TRIM(provisioning_group_sam_account_names) = '');

-- statement-break

INSERT IGNORE INTO schema_migrations (migration_id, applied_at)
VALUES ('033_catalog_service_topology', UTC_TIMESTAMP(6));
