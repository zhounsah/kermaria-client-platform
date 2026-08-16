-- ============================================================================
-- Zachary IT - Billing V2
-- Migration 053 : sessions checkout provider V2 locales
--
-- Objectif :
--   conserver localement le resultat d'une creation checkout Stripe/PayPal V2
--   avant de marquer l'evenement outbox comme traite.
--
-- Verification prealable avant execution :
--
-- SELECT idempotency_key_hash, COUNT(*) AS count
-- FROM billing_v2_provider_checkout_sessions
-- WHERE idempotency_key_hash IS NOT NULL
-- GROUP BY idempotency_key_hash
-- HAVING COUNT(*) > 1;
--
-- Cette requete doit retourner 0 ligne si la table existe deja.
-- ============================================================================

CREATE TABLE IF NOT EXISTS billing_v2_provider_checkout_sessions (
    id                              CHAR(36)      NOT NULL,
    subscription_id                 CHAR(36)      NOT NULL,

    provider                        VARCHAR(32)   NOT NULL,
    environment                     VARCHAR(16)   NOT NULL,

    provider_checkout_id            VARCHAR(255)  NULL,
    provider_subscription_id        VARCHAR(255)  NULL,
    approval_url                    TEXT          NULL,

    status                          VARCHAR(32)   NOT NULL DEFAULT 'pending_approval',
    idempotency_key_hash            CHAR(64)      NOT NULL,
    outbox_event_id                 CHAR(36)      NOT NULL,

    created_at                      DATETIME(6)   NOT NULL DEFAULT UTC_TIMESTAMP(6),
    updated_at                      DATETIME(6)   NOT NULL DEFAULT UTC_TIMESTAMP(6)
,

    PRIMARY KEY (id),
    UNIQUE KEY uq_billing_v2_provider_checkout_idempotency
        (idempotency_key_hash),
    KEY idx_billing_v2_provider_checkout_subscription
        (subscription_id, provider, environment, status),
    KEY idx_billing_v2_provider_checkout_external
        (provider, environment, provider_checkout_id),

    CONSTRAINT fk_billing_v2_provider_checkout_subscription
        FOREIGN KEY (subscription_id)
        REFERENCES billing_v2_subscriptions(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_provider_checkout_outbox
        FOREIGN KEY (outbox_event_id)
        REFERENCES billing_v2_outbox_events(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

