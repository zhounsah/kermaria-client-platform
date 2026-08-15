-- ============================================================================
-- Zachary IT - Billing V2
-- Migration 056 : documents et factures Billing V2
--
-- Objectif :
--   rattacher explicitement les documents commerciaux/BPCE emis pour Billing V2
--   sans creer de ligne legacy `subscriptions` ni de ligne `commercial_offers`.
--
-- Cette migration est additive. Elle ne declenche aucune emission BPCE et ne
-- modifie aucune facture historique.
--
-- Verification prealable avant execution sur une base contenant deja des
-- documents Billing V2 :
--
-- SELECT subscription_id, document_kind, period_start, period_end, COUNT(*) AS count
-- FROM billing_v2_subscription_documents
-- GROUP BY subscription_id, document_kind, period_start, period_end
-- HAVING COUNT(*) > 1;
--
-- Cette requete doit retourner 0 ligne avant de s'appuyer sur l'unicite.
-- ============================================================================

CREATE TABLE IF NOT EXISTS billing_v2_subscription_documents (
    id                              CHAR(36)      NOT NULL,
    subscription_id                 CHAR(36)      NOT NULL,
    commercial_document_id          CHAR(36)      NOT NULL,

    document_kind                   VARCHAR(48)   NOT NULL,
    period_start                    DATE          NOT NULL,
    period_end                      DATE          NOT NULL,

    subtotal_amount_cents           BIGINT        NOT NULL,
    discount_amount_cents           BIGINT        NOT NULL DEFAULT 0,
    tax_amount_cents                BIGINT        NOT NULL DEFAULT 0,
    total_amount_cents              BIGINT        NOT NULL,
    currency                        CHAR(3)       NOT NULL DEFAULT 'EUR',

    status                          VARCHAR(32)   NOT NULL DEFAULT 'created',
    reason_code                     VARCHAR(96)   NULL,

    created_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
                                                ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),
    UNIQUE KEY uq_billing_v2_subscription_document_period
        (subscription_id, document_kind, period_start, period_end),
    UNIQUE KEY uq_billing_v2_subscription_document_commercial
        (commercial_document_id),
    KEY idx_billing_v2_subscription_documents_subscription
        (subscription_id, status),

    CONSTRAINT fk_billing_v2_subscription_documents_subscription
        FOREIGN KEY (subscription_id)
        REFERENCES billing_v2_subscriptions(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_subscription_documents_commercial
        FOREIGN KEY (commercial_document_id)
        REFERENCES commercial_documents(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- statement-break

CREATE TABLE IF NOT EXISTS billing_v2_document_line_snapshots (
    id                              CHAR(36)      NOT NULL,
    subscription_document_id        CHAR(36)      NOT NULL,
    commercial_document_line_id     CHAR(36)      NOT NULL,
    subscription_item_id            CHAR(36)      NOT NULL,
    service_price_id                CHAR(36)      NOT NULL,

    service_code                    VARCHAR(64)   NOT NULL,
    tier_code                       VARCHAR(64)   NULL,
    label                           VARCHAR(200)  NOT NULL,

    purchased_quantity              DECIMAL(18,2) NOT NULL,
    gross_unit_amount_cents         BIGINT        NOT NULL,
    gross_line_amount_cents         BIGINT        NOT NULL,
    discount_amount_cents           BIGINT        NOT NULL DEFAULT 0,
    net_line_amount_cents           BIGINT        NOT NULL,
    tax_rate_basis_points           INT           NULL,
    tax_amount_cents                BIGINT        NOT NULL DEFAULT 0,
    final_line_amount_cents         BIGINT        NOT NULL,
    currency                        CHAR(3)       NOT NULL DEFAULT 'EUR',

    created_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),
    UNIQUE KEY uq_billing_v2_document_line_snapshot_commercial
        (commercial_document_line_id),
    KEY idx_billing_v2_document_line_snapshots_document
        (subscription_document_id),
    KEY idx_billing_v2_document_line_snapshots_item
        (subscription_item_id),

    CONSTRAINT fk_billing_v2_document_line_snapshots_document
        FOREIGN KEY (subscription_document_id)
        REFERENCES billing_v2_subscription_documents(id)
        ON UPDATE RESTRICT
        ON DELETE CASCADE,

    CONSTRAINT fk_billing_v2_document_line_snapshots_commercial_line
        FOREIGN KEY (commercial_document_line_id)
        REFERENCES commercial_document_lines(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_document_line_snapshots_item
        FOREIGN KEY (subscription_item_id)
        REFERENCES billing_v2_subscription_items(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_document_line_snapshots_price
        FOREIGN KEY (service_price_id)
        REFERENCES billing_v2_service_prices(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
