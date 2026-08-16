-- ============================================================================
-- Zachary IT - Billing V2
-- Migration 052 : idempotence de l'outbox provider V2
--
-- Objectif :
--   garantir qu'une demande locale de checkout provider V2 ne puisse pas
--   produire deux evenements outbox equivalents en cas de retry/concurrence.
--
-- Verification prealable avant execution :
--
-- SELECT idempotency_key_hash, COUNT(*) AS count
-- FROM billing_v2_outbox_events
-- WHERE idempotency_key_hash IS NOT NULL
-- GROUP BY idempotency_key_hash
-- HAVING COUNT(*) > 1;
--
-- Cette requete doit retourner 0 ligne avant d'ajouter l'unicite.
-- ============================================================================

ALTER TABLE billing_v2_outbox_events
    ADD COLUMN IF NOT EXISTS idempotency_key_hash CHAR(64) NULL
        AFTER payload_text,
    ADD UNIQUE KEY IF NOT EXISTS uq_billing_v2_outbox_idempotency
        (idempotency_key_hash);

