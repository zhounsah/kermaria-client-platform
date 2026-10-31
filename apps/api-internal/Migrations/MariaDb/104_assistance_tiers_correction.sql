-- Corrige 103 sur les bases ou les six lignes ont deja ete creees.
-- Un service et six paliers; prix propres a chaque palier, livraison manuelle.
-- La suppression ciblee des six lignes erronees exige l'absence de references
-- contractuelles (FK), verifiee sur kermaria_dev avant application.
INSERT IGNORE INTO billing_v2_services
    (id, code, name, description, tier_selector_label, category, billing_type,
     default_scope_type, pricing_model, mandatory_for_subscription,
     discount_eligible, public_selectable, public_visible, self_service_orderable,
     public_ordering_mode, direct_minimum_quantity, direct_maximum_quantity,
     requires_subscription_base, status, display_order)
VALUES
    (UUID(), 'ASSISTANCE-MAINTENANCE', 'Assistance & maintenance',
     'Accompagnement mensuel pour les particuliers et les professionnels, selon la formule choisie.',
     'Formule', 'Assistance', 'recurring', 'subscription', 'tiered',
     0, 0, 1, 1, 0, 'quote', 1, 1, 0, 'active', 500);

-- statement-break

INSERT IGNORE INTO billing_v2_service_tiers
    (id, service_id, code, name, public_label, description, numeric_value,
     unit, public_selectable, status, display_order)
SELECT UUID(), s.id, x.code, x.name, x.name, x.description, x.minutes,
       'min/mois', 1, 'active', x.display_order
FROM billing_v2_services s
JOIN (
    SELECT 'ASSIST-PERS-ESSENTIEL' code, 'Essentiel' name,
           'Particuliers : 1 ordinateur, 15 minutes par mois.' description,
           15 minutes, 10 display_order
    UNION ALL SELECT 'ASSIST-PERS-SERENITE', 'Sérénité',
           'Particuliers : jusqu a 2 ordinateurs, 45 minutes par mois.', 45, 20
    UNION ALL SELECT 'ASSIST-PERS-PREMIUM', 'Premium',
           'Particuliers : jusqu a 4 ordinateurs, 90 minutes par mois.', 90, 30
    UNION ALL SELECT 'ASSIST-PRO-START', 'Pro Start',
           'Professionnels : suivi par poste, 15 minutes par mois.', 15, 40
    UNION ALL SELECT 'ASSIST-PRO-BUSINESS', 'Pro Business',
           'Professionnels : suivi par poste, 45 minutes par mois.', 45, 50
    UNION ALL SELECT 'ASSIST-PRO-PREMIUM', 'Pro Premium',
           'Professionnels : suivi par poste, 90 minutes par mois.', 90, 60
) x ON s.code = 'ASSISTANCE-MAINTENANCE';

-- statement-break

INSERT IGNORE INTO billing_v2_service_tier_attributes
    (id, tier_id, attribute_code, value_numeric, value_text, unit)
SELECT UUID(), t.id, 'audience', NULL,
       IF(t.code LIKE 'ASSIST-PERS-%', 'particuliers', 'professionnels'), NULL
FROM billing_v2_service_tiers t
JOIN billing_v2_services s ON s.id = t.service_id
WHERE s.code = 'ASSISTANCE-MAINTENANCE';

-- statement-break

INSERT INTO billing_v2_service_prices
    (id, service_id, tier_id, price_code, price_version, amount_cents, currency,
     billing_cadence, charge_trigger, valid_from, status)
SELECT UUID(), s.id, t.id, x.price_code, 1, x.amount_cents, 'EUR', 'monthly',
       'initial_subscription', '2026-10-01 00:00:00.000000', 'active'
FROM billing_v2_services s
JOIN billing_v2_service_tiers t ON t.service_id = s.id
JOIN (
    SELECT 'ASSIST-PERS-ESSENTIEL' code, 'ASSISTANCE-MAINTENANCE-PERS-ESSENTIEL-MONTHLY-EUR-V1' price_code, 990 amount_cents
    UNION ALL SELECT 'ASSIST-PERS-SERENITE', 'ASSISTANCE-MAINTENANCE-PERS-SERENITE-MONTHLY-EUR-V1', 1990
    UNION ALL SELECT 'ASSIST-PERS-PREMIUM', 'ASSISTANCE-MAINTENANCE-PERS-PREMIUM-MONTHLY-EUR-V1', 3990
    UNION ALL SELECT 'ASSIST-PRO-START', 'ASSISTANCE-MAINTENANCE-PRO-START-MONTHLY-EUR-V1', 2280
    UNION ALL SELECT 'ASSIST-PRO-BUSINESS', 'ASSISTANCE-MAINTENANCE-PRO-BUSINESS-MONTHLY-EUR-V1', 4680
    UNION ALL SELECT 'ASSIST-PRO-PREMIUM', 'ASSISTANCE-MAINTENANCE-PRO-PREMIUM-MONTHLY-EUR-V1', 8280
) x ON x.code = t.code
WHERE s.code = 'ASSISTANCE-MAINTENANCE'
  AND NOT EXISTS (SELECT 1 FROM billing_v2_service_prices p WHERE p.price_code = x.price_code);

-- statement-break

INSERT INTO billing_v2_service_fulfillment_profiles
    (id, service_id, tier_id, fulfillment_mode, default_backend, status)
SELECT UUID(), s.id, t.id, 'manual_delivery', 'MANUAL', 'active'
FROM billing_v2_services s
JOIN billing_v2_service_tiers t ON t.service_id = s.id
WHERE s.code = 'ASSISTANCE-MAINTENANCE'
  AND NOT EXISTS (SELECT 1 FROM billing_v2_service_fulfillment_profiles p
                  WHERE p.service_id = s.id AND p.tier_id = t.id);

-- statement-break

-- Conserver le service et les prix d'origine si des FK d'historique existent :
-- dans ce cas la migration s'arrete et devra etre adaptee sans effacer l'historique.
DELETE p FROM billing_v2_service_fulfillment_profiles p
JOIN billing_v2_services s ON s.id = p.service_id
WHERE s.code IN ('ASSIST-PERS-ESSENTIEL', 'ASSIST-PERS-SERENITE', 'ASSIST-PERS-PREMIUM',
                 'ASSIST-PRO-START', 'ASSIST-PRO-BUSINESS', 'ASSIST-PRO-PREMIUM');

-- statement-break

DELETE p FROM billing_v2_service_prices p
JOIN billing_v2_services s ON s.id = p.service_id
WHERE s.code IN ('ASSIST-PERS-ESSENTIEL', 'ASSIST-PERS-SERENITE', 'ASSIST-PERS-PREMIUM',
                 'ASSIST-PRO-START', 'ASSIST-PRO-BUSINESS', 'ASSIST-PRO-PREMIUM');

-- statement-break

DELETE FROM billing_v2_services
WHERE code IN ('ASSIST-PERS-ESSENTIEL', 'ASSIST-PERS-SERENITE', 'ASSIST-PERS-PREMIUM',
               'ASSIST-PRO-START', 'ASSIST-PRO-BUSINESS', 'ASSIST-PRO-PREMIUM');
