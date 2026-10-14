-- TVA au 01/10/2026 : les prix Billing V2 existants demeurent les montants
-- payables TTC. Les anciens evenements et factures ne sont pas recalcules.
-- La contrainte net = brut - remise devient TTC = brut - remise ; HT + TVA = TTC.

ALTER TABLE billing_v2_billing_events
    DROP CONSTRAINT IF EXISTS ck_billing_v2_billing_events_net,
    DROP CONSTRAINT IF EXISTS ck_billing_v2_billing_events_price_total,
    ADD CONSTRAINT ck_billing_v2_billing_events_price_total
        CHECK (total_amount_cents = gross_amount_cents - discount_amount_cents);

-- statement-break

ALTER TABLE billing_v2_billing_event_lines
    DROP CONSTRAINT IF EXISTS ck_billing_v2_billing_event_lines_net,
    DROP CONSTRAINT IF EXISTS ck_billing_v2_billing_event_lines_price_total,
    ADD CONSTRAINT ck_billing_v2_billing_event_lines_price_total
        CHECK (total_amount_cents = gross_amount_cents - discount_allocated_amount_cents),
    ADD COLUMN IF NOT EXISTS tax_treatment VARCHAR(40) NULL DEFAULT NULL;

-- statement-break

ALTER TABLE commercial_document_lines
    ADD COLUMN IF NOT EXISTS tax_treatment VARCHAR(40) NULL DEFAULT NULL,
    ADD COLUMN IF NOT EXISTS fiscal_mention_snapshot VARCHAR(300) NULL DEFAULT NULL;

-- statement-break

ALTER TABLE billing_v2_document_line_snapshots
    ADD COLUMN IF NOT EXISTS tax_treatment VARCHAR(40) NULL DEFAULT NULL;

-- statement-break

-- L'affichage des documents deja emis conserve leur texte original. Une
-- qualification ulterieure signale une regularisation, sans modifier ce texte.
UPDATE commercial_document_lines
SET fiscal_mention_snapshot = CASE
    WHEN tax_rate_basis_points IS NULL OR tax_rate_basis_points = 0
        THEN 'TVA non applicable, art. 293 B du CGI.'
    ELSE 'TVA au taux en vigueur.'
END
WHERE fiscal_mention_snapshot IS NULL;

-- statement-break

CREATE TABLE IF NOT EXISTS managed_content_legal_versions (
    id CHAR(36) NOT NULL,
    content_key VARCHAR(128) NOT NULL,
    version_label VARCHAR(160) NULL,
    body_markdown LONGTEXT NOT NULL,
    content_sha256 CHAR(64) NOT NULL,
    recorded_at DATETIME(6) NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_managed_content_legal_versions_hash (content_key, content_sha256),
    KEY idx_managed_content_legal_versions_key (content_key, recorded_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- statement-break

INSERT IGNORE INTO managed_content_legal_versions (
    id, content_key, version_label, body_markdown, content_sha256, recorded_at)
SELECT UUID(), content_key, version_label, body_markdown,
       SHA2(body_markdown, 256), UTC_TIMESTAMP(6)
FROM managed_content_entries
WHERE content_key IN ('legal:cgv', 'legal:mentions-legales');

-- statement-break

ALTER TABLE billing_v2_subscriptions
    ADD COLUMN IF NOT EXISTS accepted_cgv_version_id CHAR(36) NULL DEFAULT NULL;
