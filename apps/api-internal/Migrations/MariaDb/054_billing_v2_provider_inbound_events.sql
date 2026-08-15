-- ============================================================================
-- Zachary IT - Billing V2
-- Migration 054 : idempotence des retours et webhooks provider V2 entrants
--
-- Objectif :
--   rattacher idempotemment les retours/webhooks Stripe/PayPal V2 aux sessions
--   checkout locales sans changer les routes legacy ni executer d'effet externe.
--
-- Verifications prealables avant execution :
--
-- SELECT provider, environment, provider_checkout_id, COUNT(*) AS count
-- FROM billing_v2_provider_checkout_sessions
-- WHERE provider_checkout_id IS NOT NULL
-- GROUP BY provider, environment, provider_checkout_id
-- HAVING COUNT(*) > 1;
--
-- SELECT provider, environment, provider_subscription_id, COUNT(*) AS count
-- FROM billing_v2_provider_checkout_sessions
-- WHERE provider_subscription_id IS NOT NULL
-- GROUP BY provider, environment, provider_subscription_id
-- HAVING COUNT(*) > 1;
--
-- Ces requetes doivent retourner 0 ligne. En cas contraire, revue manuelle
-- requise avant d'ajouter les contraintes d'unicite.
-- ============================================================================

ALTER TABLE billing_v2_provider_checkout_sessions
    ADD UNIQUE KEY IF NOT EXISTS
        uq_billing_v2_provider_checkout_external
        (provider, environment, provider_checkout_id);

ALTER TABLE billing_v2_provider_checkout_sessions
    ADD UNIQUE KEY IF NOT EXISTS
        uq_billing_v2_provider_checkout_subscription
        (provider, environment, provider_subscription_id);

CREATE TABLE IF NOT EXISTS billing_v2_provider_events (
    id                              CHAR(36)      NOT NULL,

    provider                        VARCHAR(32)   NOT NULL,
    environment                     VARCHAR(16)   NOT NULL,
    provider_event_id               VARCHAR(255)  NOT NULL,
    event_type                      VARCHAR(96)   NOT NULL,

    provider_checkout_id            VARCHAR(255)  NULL,
    provider_subscription_id        VARCHAR(255)  NULL,

    subscription_id                 CHAR(36)      NULL,
    checkout_session_id             CHAR(36)      NULL,

    status                          VARCHAR(24)   NOT NULL DEFAULT 'processing',
    reason_code                     VARCHAR(96)   NULL,
    last_error                      TEXT          NULL,

    payload_text                    LONGTEXT      NULL,

    created_at                      DATETIME(6)   NOT NULL DEFAULT UTC_TIMESTAMP(6),
    updated_at                      DATETIME(6)   NOT NULL DEFAULT UTC_TIMESTAMP(6)
,
    processed_at                    DATETIME(6)   NULL,

    PRIMARY KEY (id),
    UNIQUE KEY uq_billing_v2_provider_events_provider_event
        (provider, environment, provider_event_id),
    KEY idx_billing_v2_provider_events_subscription
        (subscription_id, status, created_at),
    KEY idx_billing_v2_provider_events_checkout
        (checkout_session_id, status, created_at),

    CONSTRAINT fk_billing_v2_provider_events_subscription
        FOREIGN KEY (subscription_id)
        REFERENCES billing_v2_subscriptions(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_provider_events_checkout
        FOREIGN KEY (checkout_session_id)
        REFERENCES billing_v2_provider_checkout_sessions(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

