-- ============================================================================
-- Zachary IT - Billing V2
-- Migration 055 : requetes checkout V2 autoritaires idempotentes
--
-- Objectif :
--   preparer le premier vrai nouvel abonnement Billing V2 sans creer de ligne
--   legacy `subscriptions`, avec une cle d'idempotence locale par client.
--
-- Cette migration est additive. Elle n'active aucun checkout V2 et n'execute
-- aucun appel Stripe/PayPal.
--
-- Verification prealable avant execution sur une base contenant deja des
-- demandes checkout V2 :
--
-- SELECT
--     customer_id,
--     idempotency_key,
--     COUNT(*) AS count
-- FROM billing_v2_authoritative_checkout_requests
-- GROUP BY customer_id, idempotency_key
-- HAVING COUNT(*) > 1;
--
-- Cette requete doit retourner 0 ligne avant d'ajouter l'unicite client/cle.
-- ============================================================================

CREATE TABLE IF NOT EXISTS billing_v2_authoritative_checkout_requests (
    id                              CHAR(36)      NOT NULL,

    customer_id                     CHAR(36)      NOT NULL,
    actor_reference                 VARCHAR(255)  NULL,

    idempotency_key                 VARCHAR(128)  NOT NULL,
    request_fingerprint_hash        CHAR(64)      NOT NULL,
    legacy_offer_id                 CHAR(36)      NOT NULL,

    provider                        VARCHAR(32)   NOT NULL,
    environment                     VARCHAR(16)   NOT NULL,

    subscription_id                 CHAR(36)      NOT NULL,
    outbox_event_id                 CHAR(36)      NULL,
    idempotency_key_hash            CHAR(64)      NULL,

    status                          VARCHAR(24)   NOT NULL DEFAULT 'pending',
    reason_code                     VARCHAR(96)   NULL,
    last_error                      TEXT          NULL,

    created_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
                                                ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),
    UNIQUE KEY uq_billing_v2_authoritative_checkout_request
        (customer_id, provider, environment, idempotency_key),
    UNIQUE KEY uq_billing_v2_authoritative_checkout_customer_key
        (customer_id, idempotency_key),
    KEY idx_billing_v2_authoritative_checkout_fingerprint
        (request_fingerprint_hash),
    KEY idx_billing_v2_authoritative_checkout_subscription
        (subscription_id),
    KEY idx_billing_v2_authoritative_checkout_outbox
        (outbox_event_id),

    CONSTRAINT fk_billing_v2_authoritative_checkout_subscription
        FOREIGN KEY (subscription_id)
        REFERENCES billing_v2_subscriptions(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_authoritative_checkout_outbox
        FOREIGN KEY (outbox_event_id)
        REFERENCES billing_v2_outbox_events(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

ALTER TABLE billing_v2_authoritative_checkout_requests
    ADD COLUMN IF NOT EXISTS request_fingerprint_hash CHAR(64) NULL
        AFTER idempotency_key;

UPDATE billing_v2_authoritative_checkout_requests
SET request_fingerprint_hash = SHA2(CONCAT_WS(
        '|',
        customer_id,
        provider,
        environment,
        legacy_offer_id,
        COALESCE(actor_reference, '')
    ), 256)
WHERE request_fingerprint_hash IS NULL;

ALTER TABLE billing_v2_authoritative_checkout_requests
    MODIFY COLUMN request_fingerprint_hash CHAR(64) NOT NULL;

CREATE INDEX IF NOT EXISTS idx_billing_v2_authoritative_checkout_fingerprint
    ON billing_v2_authoritative_checkout_requests (request_fingerprint_hash);

ALTER TABLE billing_v2_authoritative_checkout_requests
    ADD UNIQUE KEY IF NOT EXISTS
        uq_billing_v2_authoritative_checkout_customer_key
        (customer_id, idempotency_key);
