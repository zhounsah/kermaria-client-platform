-- ============================================================================
-- Zachary IT - Billing V2
-- Migration 082 : primitive canonique de remboursement integral
--
-- Cette migration est additive et dormante. Aucun remboursement ne part tant
-- que BILLING_V2_REFUNDS_ENABLED n'est pas explicitement true au demarrage.
-- Le montant est fige depuis billing_v2_billing_events ; aucun champ n'accepte
-- un montant venant du navigateur ou du workflow consommateur.
-- ============================================================================

CREATE TABLE IF NOT EXISTS billing_v2_refunds (
    id                          CHAR(36)      NOT NULL,
    billing_event_id            CHAR(36)      NOT NULL,
    payment_attempt_id          CHAR(36)      NOT NULL,

    provider                    VARCHAR(32)   NOT NULL,
    environment                 VARCHAR(16)   NOT NULL,
    provider_payment_id         VARCHAR(255)  NOT NULL,
    provider_refund_id          VARCHAR(255)  NULL,

    amount_cents                BIGINT        NOT NULL,
    currency                    CHAR(3)       NOT NULL,
    reason_code                 VARCHAR(96)   NOT NULL,
    status                      VARCHAR(32)   NOT NULL DEFAULT 'requested',

    idempotency_key_canonical   VARCHAR(512)  NOT NULL,
    idempotency_key_hash        CHAR(64)      NOT NULL,
    correlation_id              VARCHAR(128)  NULL,

    requested_at                DATETIME(6)   NOT NULL DEFAULT UTC_TIMESTAMP(6),
    provider_confirmed_at       DATETIME(6)   NULL,
    failed_at                   DATETIME(6)   NULL,
    failure_code                VARCHAR(96)   NULL,
    last_error                  TEXT          NULL,
    updated_at                  DATETIME(6)   NOT NULL DEFAULT UTC_TIMESTAMP(6),

    PRIMARY KEY (id),
    -- V1 est integral et couvre au plus une fois le meme BillingEvent.
    UNIQUE KEY uq_billing_v2_refunds_event (billing_event_id),
    UNIQUE KEY uq_billing_v2_refunds_idempotency (idempotency_key_hash),
    UNIQUE KEY uq_billing_v2_refunds_provider_id
        (provider, environment, provider_refund_id),
    KEY idx_billing_v2_refunds_pending
        (status, provider, environment, requested_at),
    KEY idx_billing_v2_refunds_payment_attempt (payment_attempt_id),

    CONSTRAINT fk_billing_v2_refunds_event
        FOREIGN KEY (billing_event_id)
        REFERENCES billing_v2_billing_events(id)
        ON UPDATE RESTRICT ON DELETE RESTRICT,
    CONSTRAINT fk_billing_v2_refunds_attempt
        FOREIGN KEY (payment_attempt_id)
        REFERENCES billing_v2_payment_attempts(id)
        ON UPDATE RESTRICT ON DELETE RESTRICT,
    CONSTRAINT ck_billing_v2_refunds_status CHECK (status IN (
        'requested', 'pending_provider', 'confirmed', 'failed')),
    CONSTRAINT ck_billing_v2_refunds_amount CHECK (amount_cents > 0),
    CONSTRAINT ck_billing_v2_refunds_currency CHECK
        (CHAR_LENGTH(TRIM(currency)) = 3),
    CONSTRAINT ck_billing_v2_refunds_confirmation CHECK
        (status <> 'confirmed' OR (
            provider_refund_id IS NOT NULL AND provider_confirmed_at IS NOT NULL)),
    CONSTRAINT ck_billing_v2_refunds_failure CHECK
        (status <> 'failed' OR (failed_at IS NOT NULL AND failure_code IS NOT NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- statement-break

ALTER TABLE billing_v2_billing_events
    ADD COLUMN IF NOT EXISTS refunded_at DATETIME(6) NULL AFTER settled_at,
    ADD COLUMN IF NOT EXISTS refund_reason_code VARCHAR(96) NULL
        AFTER settlement_reason_code;

-- statement-break

-- Le blocage local est l'autorite qui empeche le moteur de renouvellement de
-- recreer une charge pendant que la resiliation provider converge. Il n'est
-- pas le droit client de resiliation.
ALTER TABLE billing_v2_subscriptions
    ADD COLUMN IF NOT EXISTS renewal_blocked_at DATETIME(6) NULL
        AFTER renews_at,
    ADD COLUMN IF NOT EXISTS renewal_block_reason_code VARCHAR(96) NULL
        AFTER renewal_blocked_at;

-- statement-break

CREATE INDEX IF NOT EXISTS idx_billing_v2_subscriptions_renewal_blocked
    ON billing_v2_subscriptions (renewal_blocked_at, status);
