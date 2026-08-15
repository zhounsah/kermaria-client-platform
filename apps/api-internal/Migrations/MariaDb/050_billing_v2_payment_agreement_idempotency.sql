-- ============================================================================
-- Zachary IT - Billing V2
-- Migration 050 : idempotence des accords de paiement provider V2
--
-- Objectif :
--   empêcher deux abonnements V2 locaux de référencer le même abonnement
--   fournisseur Stripe/PayPal.
--
-- Précondition avant exécution sur une base contenant déjà des données V2 :
--
-- SELECT provider, environment, provider_subscription_id, COUNT(*) AS count
-- FROM billing_v2_payment_agreements
-- WHERE provider_subscription_id IS NOT NULL
-- GROUP BY provider, environment, provider_subscription_id
-- HAVING COUNT(*) > 1;
--
-- La requête doit retourner 0 ligne. En cas contraire, revue manuelle requise.
-- ============================================================================

ALTER TABLE billing_v2_payment_agreements
    ADD UNIQUE KEY IF NOT EXISTS
        uq_billing_v2_payment_agreements_provider_subscription
        (provider, environment, provider_subscription_id);
