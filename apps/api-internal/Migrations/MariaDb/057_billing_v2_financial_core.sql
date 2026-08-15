-- ============================================================================
-- Zachary IT - Billing V2
-- Migration 057 : coeur financier (Phase 1)
--
-- Objectif :
--   introduire l'intention financiere locale immuable sur laquelle les futurs
--   flux provider, documentaires et de provisioning devront s'appuyer.
--
--   Subscription / SubscriptionChange
--           -> Pricing Engine
--           -> BillingEvent + BillingEventLines
--           -> PaymentAttempt
--           -> Provider
--           -> settlement verifie
--           -> Document
--           -> Entitlement / Provisioning
--
-- Cette migration est ADDITIVE et DORMANTE :
--   - elle ne modifie destructivement aucune table 047-056 ;
--   - elle ne supprime aucune table ;
--   - elle n'active aucun checkout, n'appelle aucun provider et n'emet aucune
--     facture ;
--   - les tables creees ne sont ecrites par aucun flux de production tant que
--     la Phase 2 n'est pas livree.
--
-- Specification : docs/billing-v2/FINANCIAL-CORE.md
--
-- Verifications prealables avant execution sur une base existante :
--
-- SELECT COUNT(*) FROM billing_v2_subscription_changes
-- WHERE idempotency_key_hash IS NOT NULL;
--   -> doit retourner 0 (colonne introduite par cette migration).
--
-- SELECT subscription_id, COUNT(*) FROM billing_v2_subscription_documents
-- GROUP BY subscription_id HAVING COUNT(*) > 1;
--   -> revue manuelle requise avant de s'appuyer sur l'unicite 1:1
--      BillingEvent <-> document.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 1. OPTIMISTIC LOCKING SUR L'ABONNEMENT
--
-- Valeur initiale deterministe : 1 pour toute ligne existante ou nouvelle.
-- Toute mutation doit s'ecrire en compare-and-swap :
--
--   UPDATE billing_v2_subscriptions
--   SET status = @s, version = version + 1, updated_at = UTC_TIMESTAMP(6)
--   WHERE id = @id AND version = @expected_version;
--
-- Zero ligne affectee = conflit de concurrence, qui doit remonter en echec
-- explicite et jamais etre avale en no-op.
-- ----------------------------------------------------------------------------

ALTER TABLE billing_v2_subscriptions
    ADD COLUMN IF NOT EXISTS version BIGINT NOT NULL DEFAULT 1
        AFTER billing_model;

-- statement-break

UPDATE billing_v2_subscriptions
SET version = 1
WHERE version IS NULL
   OR version < 1;

-- statement-break

-- ----------------------------------------------------------------------------
-- 2. SUBSCRIPTION CHANGE : INTENTION UTILISATEUR PERSISTANTE ET IDEMPOTENTE
--
-- Colonnes existantes reutilisees telles quelles :
--   status, requested_at, applied_at, cancelled_at, reason,
--   requested_by_reference, effective_at, change_kind, billing_effect.
--
-- Colonnes ajoutees : ancre d'idempotence, version de base pour le
-- compare-and-swap, expiration, motifs d'echec et de reconciliation.
-- ----------------------------------------------------------------------------

ALTER TABLE billing_v2_subscription_changes
    ADD COLUMN IF NOT EXISTS client_request_id VARCHAR(128) NULL
        AFTER subscription_id,
    ADD COLUMN IF NOT EXISTS idempotency_key_canonical VARCHAR(512) NULL
        AFTER client_request_id,
    ADD COLUMN IF NOT EXISTS idempotency_key_hash CHAR(64) NULL
        AFTER idempotency_key_canonical,
    ADD COLUMN IF NOT EXISTS base_subscription_version BIGINT NULL
        AFTER idempotency_key_hash,
    ADD COLUMN IF NOT EXISTS expires_at DATETIME(6) NULL
        AFTER requested_at,
    ADD COLUMN IF NOT EXISTS failure_reason_code VARCHAR(96) NULL
        AFTER applied_at,
    ADD COLUMN IF NOT EXISTS reconciliation_reason_code VARCHAR(96) NULL
        AFTER failure_reason_code;

-- statement-break

ALTER TABLE billing_v2_subscription_changes
    ADD UNIQUE KEY IF NOT EXISTS uq_billing_v2_subscription_change_idempotency
        (idempotency_key_hash);

-- statement-break

CREATE INDEX IF NOT EXISTS idx_billing_v2_subscription_changes_request
    ON billing_v2_subscription_changes (subscription_id, client_request_id);

-- statement-break

CREATE INDEX IF NOT EXISTS idx_billing_v2_subscription_changes_expiry
    ON billing_v2_subscription_changes (status, expires_at);

-- statement-break

-- ----------------------------------------------------------------------------
-- 3. BILLING EVENT : INTENTION FINANCIERE IMMUABLE
--
-- Immuable apres creation : type, direction, devise, periode, snapshots,
-- montants, cle d'idempotence, lignes.
-- Mutables : les trois statuts, leurs horodatages et les motifs.
--
-- Une correction est un NOUVEL evenement `adjustment` portant
-- corrects_billing_event_id. Une cle d'idempotence n'est jamais reutilisee,
-- meme apres un void.
--
-- Les trois axes de statut sont volontairement separes et ne doivent jamais
-- etre fusionnes en un statut unique.
-- ----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS billing_v2_billing_events (
    id                              CHAR(36)      NOT NULL,

    customer_id                     CHAR(36)      NOT NULL,
    subscription_id                 CHAR(36)      NOT NULL,
    subscription_change_id          CHAR(36)      NULL,

    -- initial_charge / renewal_charge / upgrade_charge /
    -- prepaid_upgrade_charge / downgrade_credit / one_time_charge / adjustment
    event_type                      VARCHAR(48)   NOT NULL,
    direction                       VARCHAR(8)    NOT NULL,

    financial_status                VARCHAR(16)   NOT NULL DEFAULT 'draft',
    settlement_status               VARCHAR(24)   NOT NULL DEFAULT 'none',
    document_status                 VARCHAR(16)   NOT NULL DEFAULT 'none',

    currency                        CHAR(3)       NOT NULL DEFAULT 'EUR',

    period_start                    DATETIME(6)   NOT NULL,
    period_end                      DATETIME(6)   NOT NULL,

    payment_mode_snapshot           VARCHAR(24)   NOT NULL,
    commitment_months_snapshot      INT           NOT NULL DEFAULT 0,
    discount_basis_points_snapshot  INT           NOT NULL DEFAULT 0,

    gross_amount_cents              BIGINT        NOT NULL,
    discount_amount_cents           BIGINT        NOT NULL DEFAULT 0,
    net_amount_cents                BIGINT        NOT NULL,
    tax_amount_cents                BIGINT        NOT NULL DEFAULT 0,
    total_amount_cents              BIGINT        NOT NULL,

    pricing_engine_version          VARCHAR(32)   NOT NULL,

    idempotency_key_canonical       VARCHAR(512)  NOT NULL,
    idempotency_key_hash            CHAR(64)      NOT NULL,

    corrects_billing_event_id       CHAR(36)      NULL,
    references_billing_event_id     CHAR(36)      NULL,

    settlement_deadline_at          DATETIME(6)   NULL,
    reason_code                     VARCHAR(96)   NULL,

    created_at                      DATETIME(6)   NOT NULL
                                                DEFAULT UTC_TIMESTAMP(6),
    finalized_at                    DATETIME(6)   NULL,
    voided_at                       DATETIME(6)   NULL,

    PRIMARY KEY (id),

    UNIQUE KEY uq_billing_v2_billing_events_idempotency
        (idempotency_key_hash),
    KEY idx_billing_v2_billing_events_subscription
        (subscription_id, financial_status, created_at),
    KEY idx_billing_v2_billing_events_customer
        (customer_id, created_at),
    KEY idx_billing_v2_billing_events_settlement
        (settlement_status, settlement_deadline_at),
    KEY idx_billing_v2_billing_events_document
        (document_status, finalized_at),
    KEY idx_billing_v2_billing_events_change
        (subscription_change_id),

    CONSTRAINT fk_billing_v2_billing_events_subscription
        FOREIGN KEY (subscription_id)
        REFERENCES billing_v2_subscriptions(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_billing_events_change
        FOREIGN KEY (subscription_change_id)
        REFERENCES billing_v2_subscription_changes(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_billing_events_corrects
        FOREIGN KEY (corrects_billing_event_id)
        REFERENCES billing_v2_billing_events(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_billing_events_references
        FOREIGN KEY (references_billing_event_id)
        REFERENCES billing_v2_billing_events(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    -- DB-7
    CONSTRAINT ck_billing_v2_billing_events_direction
        CHECK (direction IN ('debit', 'credit')),
    -- DB-8
    CONSTRAINT ck_billing_v2_billing_events_financial_status
        CHECK (financial_status IN ('draft', 'finalized', 'void')),
    -- DB-9
    CONSTRAINT ck_billing_v2_billing_events_settlement_status
        CHECK (settlement_status IN (
            'none',
            'pending',
            'settled',
            'partially_settled',
            'failed',
            'amount_mismatch',
            'refunded')),
    -- DB-10
    CONSTRAINT ck_billing_v2_billing_events_document_status
        CHECK (document_status IN ('none', 'pending', 'issued', 'failed')),
    CONSTRAINT ck_billing_v2_billing_events_event_type
        CHECK (event_type IN (
            'initial_charge',
            'renewal_charge',
            'upgrade_charge',
            'prepaid_upgrade_charge',
            'downgrade_credit',
            'one_time_charge',
            'adjustment')),
    -- DB-5
    CONSTRAINT ck_billing_v2_billing_events_currency
        CHECK (CHAR_LENGTH(TRIM(currency)) = 3),
    -- DB-6
    CONSTRAINT ck_billing_v2_billing_events_period
        CHECK (period_end > period_start),
    -- DB-3
    CONSTRAINT ck_billing_v2_billing_events_amounts_positive
        CHECK (gross_amount_cents >= 0
           AND discount_amount_cents >= 0
           AND net_amount_cents >= 0
           AND tax_amount_cents >= 0
           AND total_amount_cents >= 0),
    -- DB-4
    CONSTRAINT ck_billing_v2_billing_events_discount_bounded
        CHECK (discount_amount_cents <= gross_amount_cents),
    -- DB-2
    CONSTRAINT ck_billing_v2_billing_events_net
        CHECK (net_amount_cents = gross_amount_cents - discount_amount_cents),
    -- DB-1
    CONSTRAINT ck_billing_v2_billing_events_total
        CHECK (total_amount_cents = net_amount_cents + tax_amount_cents),
    CONSTRAINT ck_billing_v2_billing_events_discount_basis_points
        CHECK (discount_basis_points_snapshot BETWEEN 0 AND 10000),
    CONSTRAINT ck_billing_v2_billing_events_commitment_months
        CHECK (commitment_months_snapshot >= 0),
    CONSTRAINT ck_billing_v2_billing_events_payment_mode
        CHECK (payment_mode_snapshot IN ('monthly', 'upfront')),
    CONSTRAINT ck_billing_v2_billing_events_engine_version
        CHECK (CHAR_LENGTH(TRIM(pricing_engine_version)) > 0),
    CONSTRAINT ck_billing_v2_billing_events_idempotency_canonical
        CHECK (CHAR_LENGTH(TRIM(idempotency_key_canonical)) > 0),
    -- DB-11
    CONSTRAINT ck_billing_v2_billing_events_finalized_at
        CHECK (financial_status <> 'finalized' OR finalized_at IS NOT NULL),
    -- DB-12
    CONSTRAINT ck_billing_v2_billing_events_voided_at
        CHECK (financial_status <> 'void' OR voided_at IS NOT NULL),
    CONSTRAINT ck_billing_v2_billing_events_correction_self
        CHECK (corrects_billing_event_id IS NULL
            OR corrects_billing_event_id <> id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- statement-break

-- ----------------------------------------------------------------------------
-- 4. BILLING EVENT LINES : SNAPSHOT IMMUABLE DU DETAIL
--
-- La remise est calculee globalement sur l'evenement puis ventilee ici de
-- facon deterministe (plus grands restes, tri stable sur display_order, id).
-- L'unicite (billing_event_id, display_order) garantit un ordre de ventilation
-- reproductible.
-- ----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS billing_v2_billing_event_lines (
    id                                  CHAR(36)      NOT NULL,
    billing_event_id                    CHAR(36)      NOT NULL,

    service_id                          CHAR(36)      NOT NULL,
    tier_id                             CHAR(36)      NULL,
    service_price_id                    CHAR(36)      NOT NULL,
    subscription_item_id                CHAR(36)      NULL,

    service_code                        VARCHAR(64)   NOT NULL,
    tier_code                           VARCHAR(64)   NULL,
    description                         VARCHAR(200)  NOT NULL,

    quantity                            INT           NOT NULL,
    unit_amount_cents                   BIGINT        NOT NULL,
    gross_amount_cents                  BIGINT        NOT NULL,
    discount_allocated_amount_cents     BIGINT        NOT NULL DEFAULT 0,
    net_amount_cents                    BIGINT        NOT NULL,
    tax_rate_basis_points               INT           NULL,
    tax_amount_cents                    BIGINT        NOT NULL DEFAULT 0,
    total_amount_cents                  BIGINT        NOT NULL,
    currency                            CHAR(3)       NOT NULL DEFAULT 'EUR',

    period_start                        DATETIME(6)   NOT NULL,
    period_end                          DATETIME(6)   NOT NULL,

    display_order                       INT           NOT NULL DEFAULT 0,

    created_at                          DATETIME(6)   NOT NULL
                                                    DEFAULT UTC_TIMESTAMP(6),

    PRIMARY KEY (id),

    UNIQUE KEY uq_billing_v2_billing_event_lines_order
        (billing_event_id, display_order),
    KEY idx_billing_v2_billing_event_lines_event
        (billing_event_id),
    KEY idx_billing_v2_billing_event_lines_item
        (subscription_item_id),

    CONSTRAINT fk_billing_v2_billing_event_lines_event
        FOREIGN KEY (billing_event_id)
        REFERENCES billing_v2_billing_events(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_billing_event_lines_service
        FOREIGN KEY (service_id)
        REFERENCES billing_v2_services(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_billing_event_lines_tier
        FOREIGN KEY (tier_id)
        REFERENCES billing_v2_service_tiers(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_billing_event_lines_price
        FOREIGN KEY (service_price_id)
        REFERENCES billing_v2_service_prices(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_billing_event_lines_item
        FOREIGN KEY (subscription_item_id)
        REFERENCES billing_v2_subscription_items(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    -- DB-17
    CONSTRAINT ck_billing_v2_billing_event_lines_quantity
        CHECK (quantity > 0),
    -- DB-3
    CONSTRAINT ck_billing_v2_billing_event_lines_amounts_positive
        CHECK (unit_amount_cents >= 0
           AND gross_amount_cents >= 0
           AND discount_allocated_amount_cents >= 0
           AND net_amount_cents >= 0
           AND tax_amount_cents >= 0
           AND total_amount_cents >= 0),
    -- DB-16
    CONSTRAINT ck_billing_v2_billing_event_lines_gross
        CHECK (gross_amount_cents = unit_amount_cents * quantity),
    CONSTRAINT ck_billing_v2_billing_event_lines_net
        CHECK (net_amount_cents
             = gross_amount_cents - discount_allocated_amount_cents),
    CONSTRAINT ck_billing_v2_billing_event_lines_total
        CHECK (total_amount_cents = net_amount_cents + tax_amount_cents),
    CONSTRAINT ck_billing_v2_billing_event_lines_discount_bounded
        CHECK (discount_allocated_amount_cents <= gross_amount_cents),
    -- DB-5
    CONSTRAINT ck_billing_v2_billing_event_lines_currency
        CHECK (CHAR_LENGTH(TRIM(currency)) = 3),
    -- DB-6
    CONSTRAINT ck_billing_v2_billing_event_lines_period
        CHECK (period_end > period_start),
    CONSTRAINT ck_billing_v2_billing_event_lines_description
        CHECK (CHAR_LENGTH(TRIM(description)) > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- statement-break

-- ----------------------------------------------------------------------------
-- 5. PAYMENT ATTEMPTS
--
-- Une PaymentAttempt est persistee AVANT tout appel provider. Un retry
-- reutilise la meme ligne et le meme provider_request_key, de sorte que le
-- provider renvoie l'objet existant au lieu d'en creer un second.
--
-- expected_* : ce que NOUS avons decide de facturer (Pricing Engine).
-- settled_*  : ce qui a REELLEMENT ete encaisse, constate chez le provider.
--
-- Un succes exige l'egalite stricte des deux, devise comprise. Tout ecart
-- doit produire `amount_mismatch` et bloquer la suite de la chaine : c'est
-- garanti ici par contrainte, pas seulement par convention applicative.
-- ----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS billing_v2_payment_attempts (
    id                              CHAR(36)      NOT NULL,
    billing_event_id                CHAR(36)      NOT NULL,

    provider                        VARCHAR(32)   NOT NULL,
    environment                     VARCHAR(16)   NOT NULL,

    provider_request_key            VARCHAR(255)  NOT NULL,
    provider_payment_id             VARCHAR(255)  NULL,
    provider_session_id             VARCHAR(255)  NULL,

    expected_amount_cents           BIGINT        NOT NULL,
    expected_currency               CHAR(3)       NOT NULL DEFAULT 'EUR',

    settled_amount_cents            BIGINT        NULL,
    settled_currency                CHAR(3)       NULL,
    provider_fee_cents              BIGINT        NULL,

    status                          VARCHAR(24)   NOT NULL DEFAULT 'created',
    failure_reason_code             VARCHAR(96)   NULL,
    last_error                      TEXT          NULL,

    attempted_at                    DATETIME(6)   NOT NULL
                                                DEFAULT UTC_TIMESTAMP(6),
    responded_at                    DATETIME(6)   NULL,
    reconciled_at                   DATETIME(6)   NULL,

    created_at                      DATETIME(6)   NOT NULL
                                                DEFAULT UTC_TIMESTAMP(6),
    -- Toujours ecrit explicitement en UTC par BillingV2FinancialCoreStore.
    -- Pas de DEFAULT/ON UPDATE CURRENT_TIMESTAMP : ces fonctions renvoient
    -- l'heure LOCALE du serveur MariaDB (Paris) alors que la convention du
    -- projet stocke tout en UTC.
    updated_at                      DATETIME(6)   NOT NULL
                                                DEFAULT UTC_TIMESTAMP(6),

    PRIMARY KEY (id),

    -- DB-15
    UNIQUE KEY uq_billing_v2_payment_attempts_request_key
        (provider, environment, provider_request_key),
    KEY idx_billing_v2_payment_attempts_event
        (billing_event_id, status),
    KEY idx_billing_v2_payment_attempts_provider_payment
        (provider, environment, provider_payment_id),

    CONSTRAINT fk_billing_v2_payment_attempts_event
        FOREIGN KEY (billing_event_id)
        REFERENCES billing_v2_billing_events(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT ck_billing_v2_payment_attempts_status
        CHECK (status IN (
            'created',
            'in_flight',
            'succeeded',
            'failed',
            'abandoned',
            'amount_mismatch')),
    -- DB-20
    CONSTRAINT ck_billing_v2_payment_attempts_expected_amount
        CHECK (expected_amount_cents >= 0),
    -- DB-19
    CONSTRAINT ck_billing_v2_payment_attempts_settled_amount
        CHECK (settled_amount_cents IS NULL OR settled_amount_cents >= 0),
    CONSTRAINT ck_billing_v2_payment_attempts_fee
        CHECK (provider_fee_cents IS NULL OR provider_fee_cents >= 0),
    -- DB-5
    CONSTRAINT ck_billing_v2_payment_attempts_expected_currency
        CHECK (CHAR_LENGTH(TRIM(expected_currency)) = 3),
    CONSTRAINT ck_billing_v2_payment_attempts_settled_currency
        CHECK (settled_currency IS NULL
            OR CHAR_LENGTH(TRIM(settled_currency)) = 3),
    CONSTRAINT ck_billing_v2_payment_attempts_request_key
        CHECK (CHAR_LENGTH(TRIM(provider_request_key)) > 0),
    CONSTRAINT ck_billing_v2_payment_attempts_provider
        CHECK (provider IN ('stripe', 'paypal')),
    -- APP-10 / APP-11 renforces en base : un succes exige settled == expected.
    CONSTRAINT ck_billing_v2_payment_attempts_settled_matches_expected
        CHECK (status <> 'succeeded'
            OR (settled_amount_cents IS NOT NULL
                AND settled_currency IS NOT NULL
                AND settled_amount_cents = expected_amount_cents
                AND settled_currency = expected_currency))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- statement-break

-- ----------------------------------------------------------------------------
-- 6. LIENS AVEC L'EXISTANT
--
-- Uniquement les references necessaires pour que les tables deja en place
-- puissent pointer vers un billing_event_id lors de la Phase 2.
--
-- Aucune table n'est supprimee, aucun workflow provider n'est remplace ici.
-- Les colonnes sont nullables : les lignes existantes restent valides.
-- ----------------------------------------------------------------------------

ALTER TABLE billing_v2_authoritative_checkout_requests
    ADD COLUMN IF NOT EXISTS billing_event_id CHAR(36) NULL
        AFTER subscription_id;

-- statement-break

ALTER TABLE billing_v2_authoritative_checkout_requests
    ADD CONSTRAINT fk_billing_v2_authoritative_checkout_billing_event
        FOREIGN KEY IF NOT EXISTS (billing_event_id)
        REFERENCES billing_v2_billing_events(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT;

-- statement-break

CREATE INDEX IF NOT EXISTS idx_billing_v2_authoritative_checkout_billing_event
    ON billing_v2_authoritative_checkout_requests (billing_event_id);

-- statement-break

ALTER TABLE billing_v2_provider_checkout_sessions
    ADD COLUMN IF NOT EXISTS billing_event_id CHAR(36) NULL
        AFTER subscription_id;

-- statement-break

ALTER TABLE billing_v2_provider_checkout_sessions
    ADD CONSTRAINT fk_billing_v2_provider_checkout_billing_event
        FOREIGN KEY IF NOT EXISTS (billing_event_id)
        REFERENCES billing_v2_billing_events(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT;

-- statement-break

CREATE INDEX IF NOT EXISTS idx_billing_v2_provider_checkout_billing_event
    ON billing_v2_provider_checkout_sessions (billing_event_id);

-- statement-break

ALTER TABLE billing_v2_subscription_documents
    ADD COLUMN IF NOT EXISTS billing_event_id CHAR(36) NULL
        AFTER subscription_id;

-- statement-break

-- DB-18 : BillingEvent <-> document commercial en 1:1 pour V2.0.
-- MariaDB autorise plusieurs NULL dans un index UNIQUE : les documents V2
-- existants, anterieurs au coeur financier, restent valides.
ALTER TABLE billing_v2_subscription_documents
    ADD UNIQUE KEY IF NOT EXISTS
        uq_billing_v2_subscription_document_billing_event
        (billing_event_id);

-- statement-break

ALTER TABLE billing_v2_subscription_documents
    ADD CONSTRAINT fk_billing_v2_subscription_documents_billing_event
        FOREIGN KEY IF NOT EXISTS (billing_event_id)
        REFERENCES billing_v2_billing_events(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT;
