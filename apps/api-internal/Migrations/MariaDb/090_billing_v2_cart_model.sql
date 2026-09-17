-- ============================================================================
-- Billing V2 : panier commercial persistant. Additif uniquement.
-- Ce schema ne reference ni subscription, ni paiement, ni outbox/provisioning.
-- ============================================================================

SET NAMES utf8mb4;

-- statement-break

ALTER TABLE billing_v2_services
    ADD COLUMN IF NOT EXISTS configuration_policy VARCHAR(48) NOT NULL
        DEFAULT 'not_required' AFTER public_ordering_mode,
    ADD COLUMN IF NOT EXISTS configuration_price_stable_without_reference TINYINT(1)
        NOT NULL DEFAULT 0 AFTER configuration_policy;

-- statement-break

ALTER TABLE billing_v2_service_prices
    -- Contexte explicite de dedoublonnage :
    -- currency + charge_trigger='initial_subscription' + cette cle.
    -- Une cle signifie une unique charge commerciale, jamais un prix unitaire.
    ADD COLUMN IF NOT EXISTS fee_deduplication_key VARCHAR(96) NULL
        AFTER charge_trigger;

-- statement-break

CREATE TABLE IF NOT EXISTS billing_v2_carts (
    id CHAR(36) NOT NULL,
    customer_id CHAR(36) NULL,
    anonymous_session_hash CHAR(64) NULL,
    status VARCHAR(24) NOT NULL DEFAULT 'open',
    currency CHAR(3) NOT NULL,
    commitment_term_id CHAR(36) NULL,
    payment_mode VARCHAR(24) NULL,
    source_preset_id CHAR(36) NULL,
    version INT NOT NULL DEFAULT 1,
    created_at DATETIME(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    last_activity_at DATETIME(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    expires_at DATETIME(6) NOT NULL,
    checked_out_subscription_id CHAR(36) NULL,
    -- Slots physiques : le service les maintient dans la meme transaction
    -- que status/owner. Aucune generated column n'est requise.
    open_customer_slot TINYINT NULL,
    open_anonymous_slot TINYINT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_billing_v2_carts_open_customer_currency
        (customer_id, currency, open_customer_slot),
    UNIQUE KEY uq_billing_v2_carts_open_anonymous_currency
        (anonymous_session_hash, currency, open_anonymous_slot),
    KEY idx_billing_v2_carts_expiry (status, expires_at),
    CONSTRAINT chk_billing_v2_cart_owner CHECK (
        (customer_id IS NOT NULL AND anonymous_session_hash IS NULL)
        OR (customer_id IS NULL AND anonymous_session_hash IS NOT NULL)),
    CONSTRAINT chk_billing_v2_cart_status CHECK (
        status IN ('open', 'checked_out', 'expired')),
    CONSTRAINT chk_billing_v2_cart_open_slots CHECK (
        (status = 'open' AND (
            (customer_id IS NOT NULL AND anonymous_session_hash IS NULL
                AND open_customer_slot = 1 AND open_anonymous_slot IS NULL)
            OR
            (customer_id IS NULL AND anonymous_session_hash IS NOT NULL
                AND open_customer_slot IS NULL AND open_anonymous_slot = 1)
        ))
        OR
        (status <> 'open' AND open_customer_slot IS NULL
            AND open_anonymous_slot IS NULL)
    ),
    CONSTRAINT chk_billing_v2_cart_payment_mode CHECK (
        payment_mode IS NULL OR payment_mode IN ('monthly', 'upfront')),
    CONSTRAINT fk_billing_v2_cart_commitment FOREIGN KEY (commitment_term_id)
        REFERENCES billing_v2_commitment_terms(id)
        ON UPDATE RESTRICT ON DELETE RESTRICT,
    CONSTRAINT fk_billing_v2_cart_preset FOREIGN KEY (source_preset_id)
        REFERENCES billing_v2_offer_presets(id)
        ON UPDATE RESTRICT ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- statement-break

-- Recuperation idempotente : si une tentative precedente a atteint le CREATE
-- TABLE avant son echec, les anciennes colonnes/index generated sont retires
-- sans toucher aux donnees d'owner ou aux Carts eux-memes.
ALTER TABLE billing_v2_carts
    DROP INDEX IF EXISTS uq_billing_v2_carts_open_customer_currency,
    DROP INDEX IF EXISTS uq_billing_v2_carts_open_anonymous_currency;

-- statement-break

ALTER TABLE billing_v2_carts
    DROP COLUMN IF EXISTS open_customer_key,
    DROP COLUMN IF EXISTS open_anonymous_key;

-- statement-break

ALTER TABLE billing_v2_carts
    ADD COLUMN IF NOT EXISTS open_customer_slot TINYINT NULL AFTER checked_out_subscription_id,
    ADD COLUMN IF NOT EXISTS open_anonymous_slot TINYINT NULL AFTER open_customer_slot;

-- statement-break

-- Backfill conservateur pour toute table creee lors d'une tentative interrompue.
UPDATE billing_v2_carts
SET open_customer_slot = CASE
        WHEN status = 'open' AND customer_id IS NOT NULL THEN 1
        ELSE NULL
    END,
    open_anonymous_slot = CASE
        WHEN status = 'open' AND anonymous_session_hash IS NOT NULL THEN 1
        ELSE NULL
    END;

-- statement-break

ALTER TABLE billing_v2_carts
    ADD UNIQUE KEY IF NOT EXISTS uq_billing_v2_carts_open_customer_currency
        (customer_id, currency, open_customer_slot),
    ADD UNIQUE KEY IF NOT EXISTS uq_billing_v2_carts_open_anonymous_currency
        (anonymous_session_hash, currency, open_anonymous_slot),
    ADD CONSTRAINT IF NOT EXISTS chk_billing_v2_cart_open_slots CHECK (
        (status = 'open' AND (
            (customer_id IS NOT NULL AND anonymous_session_hash IS NULL
                AND open_customer_slot = 1 AND open_anonymous_slot IS NULL)
            OR
            (customer_id IS NULL AND anonymous_session_hash IS NOT NULL
                AND open_customer_slot IS NULL AND open_anonymous_slot = 1)
        ))
        OR
        (status <> 'open' AND open_customer_slot IS NULL
            AND open_anonymous_slot IS NULL)
    );

-- statement-break

CREATE TABLE IF NOT EXISTS billing_v2_cart_items (
    id CHAR(36) NOT NULL,
    cart_id CHAR(36) NOT NULL,
    service_id CHAR(36) NOT NULL,
    tier_id CHAR(36) NULL,
    quantity INT NOT NULL DEFAULT 1,
    scope_template VARCHAR(32) NOT NULL,
    subject_binding VARCHAR(255) NULL,
    source_preset_item_id CHAR(36) NULL,
    configuration_kind VARCHAR(48) NULL,
    configuration_reference VARCHAR(128) NULL,
    display_order INT NOT NULL DEFAULT 0,
    created_at DATETIME(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    PRIMARY KEY (id),
    KEY idx_billing_v2_cart_items_cart (cart_id, display_order),
    KEY idx_billing_v2_cart_items_service (service_id),
    CONSTRAINT chk_billing_v2_cart_item_quantity CHECK (quantity > 0),
    CONSTRAINT fk_billing_v2_cart_item_cart FOREIGN KEY (cart_id)
        REFERENCES billing_v2_carts(id) ON UPDATE RESTRICT ON DELETE CASCADE,
    CONSTRAINT fk_billing_v2_cart_item_service FOREIGN KEY (service_id)
        REFERENCES billing_v2_services(id) ON UPDATE RESTRICT ON DELETE RESTRICT,
    CONSTRAINT fk_billing_v2_cart_item_tier FOREIGN KEY (tier_id)
        REFERENCES billing_v2_service_tiers(id) ON UPDATE RESTRICT ON DELETE RESTRICT,
    CONSTRAINT fk_billing_v2_cart_item_preset FOREIGN KEY (source_preset_item_id)
        REFERENCES billing_v2_preset_items(id) ON UPDATE RESTRICT ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- statement-break

CREATE TABLE IF NOT EXISTS billing_v2_cart_quotes (
    id CHAR(36) NOT NULL,
    cart_id CHAR(36) NOT NULL,
    cart_version INT NOT NULL,
    quote_version INT NOT NULL,
    composition_fingerprint CHAR(64) NOT NULL,
    currency CHAR(3) NOT NULL,
    quote_json JSON NOT NULL,
    calculated_at DATETIME(6) NOT NULL,
    expires_at DATETIME(6) NOT NULL,
    created_at DATETIME(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    PRIMARY KEY (id),
    UNIQUE KEY uq_billing_v2_cart_quote_version (cart_id, quote_version),
    KEY idx_billing_v2_cart_quotes_current (cart_id, cart_version, expires_at),
    CONSTRAINT fk_billing_v2_cart_quote_cart FOREIGN KEY (cart_id)
        REFERENCES billing_v2_carts(id) ON UPDATE RESTRICT ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
