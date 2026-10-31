-- Activation de catalogue sous Checkout global fermé, puis déploiement de
-- l'API qui vérifie les bornes par palier avant ouverture du paiement.
-- Les montants, remises, engagements et profils manuels restent inchangés.
UPDATE billing_v2_service_tiers tier
JOIN billing_v2_services service ON service.id = tier.service_id
SET tier.direct_minimum_quantity = 1,
    tier.direct_maximum_quantity = CASE
        WHEN tier.code IN ('ASSIST-PRO-START', 'ASSIST-PRO-BUSINESS', 'ASSIST-PRO-PREMIUM') THEN 10
        ELSE 1
    END
WHERE service.code = 'ASSISTANCE-MAINTENANCE'
  AND tier.code IN ('ASSIST-PERS-ESSENTIEL', 'ASSIST-PERS-SERENITE', 'ASSIST-PERS-PREMIUM',
                    'ASSIST-PRO-START', 'ASSIST-PRO-BUSINESS', 'ASSIST-PRO-PREMIUM');

-- statement-break

UPDATE billing_v2_services
SET self_service_orderable = 1,
    public_ordering_mode = 'direct',
    direct_minimum_quantity = 1,
    direct_maximum_quantity = 10,
    requires_subscription_base = 0
WHERE code = 'ASSISTANCE-MAINTENANCE'
  AND status = 'active';
