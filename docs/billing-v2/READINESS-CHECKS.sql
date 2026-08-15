-- ============================================================================
-- Billing V2 - contrôles read-only avant activation humaine
--
-- Ne pas exécuter comme migration.
-- Ces requêtes servent à prouver qu'aucun contrat client réel actif n'est à
-- migrer avant de concevoir Billing V2 comme source autoritaire du premier vrai
-- nouvel abonnement.
-- ============================================================================

-- 1. Abonnements actifs de vrais clients, hors démo/essai.
-- Résultat attendu avant première activation V2 : 0 ligne.
SELECT
    subscription.id AS subscription_id,
    subscription.status,
    subscription.customer_id,
    customer.external_reference AS customer_reference,
    customer.display_name AS customer_name,
    subscription.commercial_offer_id,
    subscription.created_at,
    subscription.updated_at
FROM subscriptions subscription
INNER JOIN customers customer
    ON customer.id = subscription.customer_id
WHERE subscription.status IN (
        'active',
        'pending_cancellation',
        'suspended',
        'pending_activation',
        'pending_payment',
        'pending_approval'
  )
  AND COALESCE(customer.is_demo, FALSE) = FALSE
ORDER BY subscription.updated_at DESC, subscription.id DESC;

-- 2. Synthèse de contrôle pour revue humaine.
-- Le compteur real_customer_subscription_count doit être 0 avant de considérer
-- qu'aucune migration progressive de contrats réels n'est nécessaire.
SELECT
    COUNT(*) AS real_customer_subscription_count,
    TRUE AS verified_against_persistent_sql
FROM subscriptions subscription
INNER JOIN customers customer
    ON customer.id = subscription.customer_id
WHERE subscription.status IN (
        'active',
        'pending_cancellation',
        'suspended',
        'pending_activation',
        'pending_payment',
        'pending_approval'
  )
  AND COALESCE(customer.is_demo, FALSE) = FALSE;
