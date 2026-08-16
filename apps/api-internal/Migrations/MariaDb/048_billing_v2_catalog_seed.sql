-- ============================================================================
-- Zachary IT - Billing V2
-- Migration 048 : seed catalogue V2 dormant
--
-- Cette migration ne lit ni ne modifie la facturation legacy active.
-- Elle alimente uniquement les tables billing_v2_* créées par 047.
-- ============================================================================

SET NAMES utf8mb4;

-- 19. SEED DU CATALOGUE FONCTIONNEL
--
-- Aucun prix n'est seedé ici : ils doivent être décidés séparément.
-- Les INSERT sont idempotents grâce aux codes uniques.
-- ============================================================================


-- statement-break

INSERT IGNORE INTO billing_v2_services
    (id, code, name, description, category, billing_type, default_scope_type,
     pricing_model, mandatory_for_subscription, discount_eligible,
     public_selectable, status, display_order)
VALUES
    (UUID(), 'BASE-SERVICE',
     'Socle de service',
     'Socle récurrent obligatoire : compte client, exploitation de la plateforme, sécurité générale, supervision de l''infrastructure et support lié au fonctionnement normal des services.',
     'Socle', 'recurring', 'subscription', 'fixed', 1, 1, 0, 'active', 10),

    (UUID(), 'STORAGE-PERSONAL',
     'Stockage personnel',
     'Quota de stockage personnel attribué à un utilisateur.',
     'Stockage', 'recurring', 'user', 'tiered', 0, 1, 1, 'active', 20),

    (UUID(), 'STORAGE-SHARED',
     'Stockage partagé',
     'Quota de stockage partagé attribué à l''abonnement ou à l''organisation.',
     'Stockage', 'recurring', 'subscription', 'tiered', 0, 1, 1, 'active', 30),

    (UUID(), 'BACKUP-PERSONAL',
     'Sauvegarde du stockage personnel',
     'Sauvegarde du stockage personnel d''un utilisateur. Le tier doit suivre la capacité de stockage personnel couverte.',
     'Sauvegarde', 'recurring', 'user', 'tiered', 0, 1, 1, 'active', 40),

    (UUID(), 'BACKUP-SHARED',
     'Sauvegarde du stockage partagé',
     'Sauvegarde du stockage partagé. Le tier doit suivre la capacité de stockage partagé couverte.',
     'Sauvegarde', 'recurring', 'subscription', 'tiered', 0, 1, 1, 'active', 50),

    (UUID(), 'VPN-ACCESS',
     'Accès VPN',
     'Accès VPN sécurisé avec niveau de performance commercial.',
     'Accès', 'recurring', 'user', 'tiered', 0, 1, 1, 'active', 60),

    (UUID(), 'RDS-ACCESS',
     'Accès bureau distant RDS',
     'Accès utilisateur à l''environnement Windows distant.',
     'Accès', 'recurring', 'user', 'fixed', 0, 1, 1, 'active', 70),

    (UUID(), 'USER-ADDITIONAL',
     'Utilisateur supplémentaire',
     'Compte utilisateur supplémentaire rattaché à l''abonnement.',
     'Utilisateurs', 'recurring', 'user', 'fixed', 0, 1, 1, 'active', 80),

    (UUID(), 'SUPPORT-STANDARD',
     'Support standard',
     'Support relatif au fonctionnement normal des services Zachary IT. Inclus dans le socle.',
     'Support', 'included', 'subscription', 'fixed', 0, 0, 0, 'active', 90),

    (UUID(), 'SUPPORT-PLUS',
     'Support Plus',
     'Option d''assistance renforcée pour les services souscrits.',
     'Support', 'recurring', 'subscription', 'fixed', 0, 1, 1, 'active', 100),

    (UUID(), 'INIT-SERVICE',
     'Mise en service',
     'Prestation ponctuelle de mise en service, contrôles et activation initiale.',
     'Mise en service', 'one_time', 'subscription', 'fixed', 0, 0, 0, 'active', 105),

    (UUID(), 'MONITORING-INTERNAL',
     'Supervision de l''infrastructure',
     'Supervision interne des services et de l''infrastructure Zachary IT. Incluse dans le socle.',
     'Supervision', 'included', 'subscription', 'fixed', 0, 0, 0, 'active', 110);


-- --------------------------------------------------------------------------
-- Tiers stockage personnel : 16 / 32 / 64 / 128 / 256 / 512 GiB
-- --------------------------------------------------------------------------


-- statement-break

INSERT IGNORE INTO billing_v2_service_tiers
    (id, service_id, code, name, public_label, numeric_value, unit,
     public_selectable, status, display_order)
SELECT UUID(), s.id, x.code, x.name, x.name, x.numeric_value, 'GiB',
       x.public_selectable, 'active', x.display_order
FROM billing_v2_services s
CROSS JOIN (
    SELECT '16'  AS code, '16 Go'  AS name, 16  AS numeric_value, 1 AS public_selectable, 10 AS display_order
    UNION ALL SELECT '32',  '32 Go',  32,  1, 20
    UNION ALL SELECT '64',  '64 Go',  64,  1, 30
    UNION ALL SELECT '128', '128 Go', 128, 1, 40
    UNION ALL SELECT '256', '256 Go', 256, 1, 50
    UNION ALL SELECT '512', '512 Go', 512, 0, 60
) x
WHERE s.code = 'STORAGE-PERSONAL';


-- --------------------------------------------------------------------------
-- Tiers stockage partagé : 32 / 64 / 128 / 256 / 512 GiB
-- --------------------------------------------------------------------------


-- statement-break

INSERT IGNORE INTO billing_v2_service_tiers
    (id, service_id, code, name, public_label, numeric_value, unit,
     public_selectable, status, display_order)
SELECT UUID(), s.id, x.code, x.name, x.name, x.numeric_value, 'GiB',
       x.public_selectable, 'active', x.display_order
FROM billing_v2_services s
CROSS JOIN (
    SELECT '32'  AS code, '32 Go'  AS name, 32  AS numeric_value, 1 AS public_selectable, 10 AS display_order
    UNION ALL SELECT '64',  '64 Go',  64,  1, 20
    UNION ALL SELECT '128', '128 Go', 128, 1, 30
    UNION ALL SELECT '256', '256 Go', 256, 1, 40
    UNION ALL SELECT '512', '512 Go', 512, 0, 50
) x
WHERE s.code = 'STORAGE-SHARED';


-- --------------------------------------------------------------------------
-- Tiers backup : même capacité logique que le stockage couvert.
-- --------------------------------------------------------------------------


-- statement-break

INSERT IGNORE INTO billing_v2_service_tiers
    (id, service_id, code, name, public_label, numeric_value, unit,
     public_selectable, status, display_order)
SELECT UUID(), s.id, x.code, x.name, x.name, x.numeric_value, 'GiB',
       0, 'active', x.display_order
FROM billing_v2_services s
CROSS JOIN (
    SELECT '16'  AS code, '16 Go protégés'  AS name, 16  AS numeric_value, 10 AS display_order
    UNION ALL SELECT '32',  '32 Go protégés',  32,  20
    UNION ALL SELECT '64',  '64 Go protégés',  64,  30
    UNION ALL SELECT '128', '128 Go protégés', 128, 40
    UNION ALL SELECT '256', '256 Go protégés', 256, 50
    UNION ALL SELECT '512', '512 Go protégés', 512, 60
) x
WHERE s.code = 'BACKUP-PERSONAL';



-- statement-break

INSERT IGNORE INTO billing_v2_service_tiers
    (id, service_id, code, name, public_label, numeric_value, unit,
     public_selectable, status, display_order)
SELECT UUID(), s.id, x.code, x.name, x.name, x.numeric_value, 'GiB',
       0, 'active', x.display_order
FROM billing_v2_services s
CROSS JOIN (
    SELECT '32'  AS code, '32 Go protégés'  AS name, 32  AS numeric_value, 10 AS display_order
    UNION ALL SELECT '64',  '64 Go protégés',  64,  20
    UNION ALL SELECT '128', '128 Go protégés', 128, 30
    UNION ALL SELECT '256', '256 Go protégés', 256, 40
    UNION ALL SELECT '512', '512 Go protégés', 512, 50
) x
WHERE s.code = 'BACKUP-SHARED';


-- --------------------------------------------------------------------------
-- Tiers VPN : nom commercial + plafond technique interne.
-- Les chiffres correspondent à des limites techniques, pas à un débit garanti.
-- --------------------------------------------------------------------------


-- statement-break

INSERT IGNORE INTO billing_v2_service_tiers
    (id, service_id, code, name, public_label, description,
     numeric_value, unit, public_selectable, status, display_order)
SELECT UUID(), s.id, x.code, x.name, x.name, x.description,
       x.numeric_value, 'Mbps', x.public_selectable, 'active', x.display_order
FROM billing_v2_services s
CROSS JOIN (
    SELECT 'ESSENTIAL' AS code,
           'VPN Essentiel' AS name,
           'Pour l''accès sécurisé aux fichiers et les usages courants.' AS description,
           100 AS numeric_value, 1 AS public_selectable, 10 AS display_order
    UNION ALL
    SELECT 'PLUS',
           'VPN Plus',
           'Pour une utilisation régulière et des transferts plus importants.',
           250, 1, 20
    UNION ALL
    SELECT 'PERFORMANCE',
           'VPN Performance',
           'Pour les usages intensifs et les transferts volumineux.',
           500, 1, 30
    UNION ALL
    SELECT 'PRO',
           'VPN Pro',
           'Pour les structures ayant des besoins réseau importants.',
           1000, 0, 40
) x
WHERE s.code = 'VPN-ACCESS';


-- ============================================================================
-- 20. SEED DES DÉPENDANCES
-- ============================================================================


-- statement-break

INSERT IGNORE INTO billing_v2_service_dependencies
    (id, service_id, required_service_id, scope_relation, tier_relation, status)
SELECT
    UUID(),
    backup.id,
    storage.id,
    'same_scope',
    'same_numeric_value',
    'active'
FROM billing_v2_services backup
CROSS JOIN billing_v2_services storage
WHERE backup.code = 'BACKUP-PERSONAL'
  AND storage.code = 'STORAGE-PERSONAL';



-- statement-break

INSERT IGNORE INTO billing_v2_service_dependencies
    (id, service_id, required_service_id, scope_relation, tier_relation, status)
SELECT
    UUID(),
    backup.id,
    storage.id,
    'same_scope',
    'same_numeric_value',
    'active'
FROM billing_v2_services backup
CROSS JOIN billing_v2_services storage
WHERE backup.code = 'BACKUP-SHARED'
  AND storage.code = 'STORAGE-SHARED';


-- ============================================================================
-- 21. SEED DES DURÉES D'ENGAGEMENT
--
-- Les remises 6/12 mois restent NULL volontairement tant que leur niveau
-- commercial n'est pas définitivement arrêté.
-- ============================================================================


-- statement-break

INSERT IGNORE INTO billing_v2_commitment_terms
    (id, code, name, commitment_months, discount_basis_points,
     allow_monthly_payment, allow_upfront_payment, status, display_order)
VALUES
    (UUID(), 'FLEX',    'Sans engagement', 1,  0,    1, 0, 'active', 10),
    (UUID(), 'TERM-6',  'Engagement 6 mois', 6, NULL, 1, 1, 'active', 20),
    (UUID(), 'TERM-12', 'Engagement 12 mois', 12, NULL, 1, 1, 'active', 30);


-- ============================================================================
-- 22. SEED DES OPTIONS DE PAIEMENT PAR ENGAGEMENT
-- ============================================================================


-- statement-break

INSERT IGNORE INTO billing_v2_commitment_payment_options
    (id, commitment_term_id, payment_mode, discount_basis_points, status, display_order)
SELECT UUID(), t.id, x.payment_mode, x.discount_basis_points, 'active', x.display_order
FROM billing_v2_commitment_terms t
JOIN (
    SELECT 'FLEX' AS term_code, 'monthly' AS payment_mode, 0 AS discount_basis_points, 10 AS display_order
    UNION ALL SELECT 'TERM-6', 'monthly', 1000, 20
    UNION ALL SELECT 'TERM-12', 'monthly', 1500, 30
    UNION ALL SELECT 'TERM-6', 'upfront', 1500, 40
    UNION ALL SELECT 'TERM-12', 'upfront', 2000, 50
) x ON x.term_code = t.code;


-- ============================================================================

-- ============================================================================
-- PRIX V2 VERSIONNES V1
-- ============================================================================


-- statement-break

INSERT INTO billing_v2_service_prices
    (id, service_id, tier_id, price_code, price_version, amount_cents,
     currency, billing_cadence, tax_rate_basis_points, valid_from, status)
SELECT
    UUID(),
    s.id,
    t.id,
    x.price_code,
    1,
    x.amount_cents,
    'EUR',
    x.billing_cadence,
    NULL,
    '2026-08-12 00:00:00.000000',
    'active'
FROM (
    SELECT 'BASE-SERVICE' AS service_code, NULL AS tier_code, 'BASE-SERVICE-MONTHLY-EUR-V1' AS price_code, 690 AS amount_cents, 'monthly' AS billing_cadence
    UNION ALL SELECT 'STORAGE-PERSONAL', '16', 'STORAGE-PERSONAL-16-MONTHLY-EUR-V1', 200, 'monthly'
    UNION ALL SELECT 'STORAGE-PERSONAL', '32', 'STORAGE-PERSONAL-32-MONTHLY-EUR-V1', 300, 'monthly'
    UNION ALL SELECT 'STORAGE-PERSONAL', '64', 'STORAGE-PERSONAL-64-MONTHLY-EUR-V1', 500, 'monthly'
    UNION ALL SELECT 'STORAGE-PERSONAL', '128', 'STORAGE-PERSONAL-128-MONTHLY-EUR-V1', 700, 'monthly'
    UNION ALL SELECT 'STORAGE-PERSONAL', '256', 'STORAGE-PERSONAL-256-MONTHLY-EUR-V1', 990, 'monthly'
    UNION ALL SELECT 'STORAGE-PERSONAL', '512', 'STORAGE-PERSONAL-512-MONTHLY-EUR-V1', 1590, 'monthly'
    UNION ALL SELECT 'STORAGE-SHARED', '32', 'STORAGE-SHARED-32-MONTHLY-EUR-V1', 390, 'monthly'
    UNION ALL SELECT 'STORAGE-SHARED', '64', 'STORAGE-SHARED-64-MONTHLY-EUR-V1', 590, 'monthly'
    UNION ALL SELECT 'STORAGE-SHARED', '128', 'STORAGE-SHARED-128-MONTHLY-EUR-V1', 890, 'monthly'
    UNION ALL SELECT 'STORAGE-SHARED', '256', 'STORAGE-SHARED-256-MONTHLY-EUR-V1', 1390, 'monthly'
    UNION ALL SELECT 'STORAGE-SHARED', '512', 'STORAGE-SHARED-512-MONTHLY-EUR-V1', 1990, 'monthly'
    UNION ALL SELECT 'BACKUP-PERSONAL', '16', 'BACKUP-PERSONAL-16-MONTHLY-EUR-V1', 100, 'monthly'
    UNION ALL SELECT 'BACKUP-PERSONAL', '32', 'BACKUP-PERSONAL-32-MONTHLY-EUR-V1', 200, 'monthly'
    UNION ALL SELECT 'BACKUP-PERSONAL', '64', 'BACKUP-PERSONAL-64-MONTHLY-EUR-V1', 300, 'monthly'
    UNION ALL SELECT 'BACKUP-PERSONAL', '128', 'BACKUP-PERSONAL-128-MONTHLY-EUR-V1', 400, 'monthly'
    UNION ALL SELECT 'BACKUP-PERSONAL', '256', 'BACKUP-PERSONAL-256-MONTHLY-EUR-V1', 600, 'monthly'
    UNION ALL SELECT 'BACKUP-PERSONAL', '512', 'BACKUP-PERSONAL-512-MONTHLY-EUR-V1', 900, 'monthly'
    UNION ALL SELECT 'BACKUP-SHARED', '32', 'BACKUP-SHARED-32-MONTHLY-EUR-V1', 200, 'monthly'
    UNION ALL SELECT 'BACKUP-SHARED', '64', 'BACKUP-SHARED-64-MONTHLY-EUR-V1', 350, 'monthly'
    UNION ALL SELECT 'BACKUP-SHARED', '128', 'BACKUP-SHARED-128-MONTHLY-EUR-V1', 500, 'monthly'
    UNION ALL SELECT 'BACKUP-SHARED', '256', 'BACKUP-SHARED-256-MONTHLY-EUR-V1', 850, 'monthly'
    UNION ALL SELECT 'BACKUP-SHARED', '512', 'BACKUP-SHARED-512-MONTHLY-EUR-V1', 1200, 'monthly'
    UNION ALL SELECT 'VPN-ACCESS', 'ESSENTIAL', 'VPN-ACCESS-ESSENTIAL-MONTHLY-EUR-V1', 390, 'monthly'
    UNION ALL SELECT 'VPN-ACCESS', 'PLUS', 'VPN-ACCESS-PLUS-MONTHLY-EUR-V1', 590, 'monthly'
    UNION ALL SELECT 'VPN-ACCESS', 'PERFORMANCE', 'VPN-ACCESS-PERFORMANCE-MONTHLY-EUR-V1', 890, 'monthly'
    UNION ALL SELECT 'VPN-ACCESS', 'PRO', 'VPN-ACCESS-PRO-MONTHLY-EUR-V1', 1290, 'monthly'
    UNION ALL SELECT 'RDS-ACCESS', NULL, 'RDS-ACCESS-MONTHLY-EUR-V1', 1590, 'monthly'
    UNION ALL SELECT 'USER-ADDITIONAL', NULL, 'USER-ADDITIONAL-MONTHLY-EUR-V1', 390, 'monthly'
    UNION ALL SELECT 'SUPPORT-PLUS', NULL, 'SUPPORT-PLUS-MONTHLY-EUR-V1', 990, 'monthly'
    UNION ALL SELECT 'INIT-SERVICE', NULL, 'INIT-SERVICE-ONE-TIME-EUR-V1', 1290, 'one_time'
) x
JOIN billing_v2_services s
  ON s.code = x.service_code
LEFT JOIN billing_v2_service_tiers t
  ON t.service_id = s.id
 AND t.code = x.tier_code
WHERE (x.tier_code IS NULL OR t.id IS NOT NULL)
  AND NOT EXISTS (
      SELECT 1
      FROM billing_v2_service_prices existing
      WHERE existing.price_code = x.price_code
  );

-- 1. TIER VPN LEGACY CACHÉ
--
-- Le service ACCES-VPN legacy ne contient pas, dans les données fournies,
-- suffisamment d'information pour déterminer de manière certaine s'il
-- correspond à Essentiel / Plus / Performance / Pro.
--
-- On ne devine donc pas : on conserve un tier technique LEGACY non public.
-- ============================================================================


-- statement-break

INSERT INTO billing_v2_service_tiers
    (
        id,
        service_id,
        code,
        name,
        public_label,
        description,
        numeric_value,
        unit,
        public_selectable,
        status,
        display_order
    )
SELECT
    UUID(),
    s.id,
    'LEGACY',
    'VPN Legacy',
    'VPN historique',
    'Tier interne réservé aux abonnements migrés depuis ACCES-VPN lorsque le plafond technique historique n''est pas encore qualifié.',
    NULL,
    'Mbps',
    0,
    'active',
    999
FROM billing_v2_services s
WHERE s.code = 'VPN-ACCESS'
  AND NOT EXISTS (
      SELECT 1
      FROM billing_v2_service_tiers t
      WHERE t.service_id = s.id
        AND t.code = 'LEGACY'
  );


-- ============================================================================
-- 2. PRESETS COMMERCIAUX V2
-- ============================================================================


-- statement-break

INSERT INTO billing_v2_offer_presets
    (id, code, name, description, status, is_public, display_order)
SELECT
    UUID(),
    'pack-dossier-securise',
    'Dossier sécurisé',
    'Configuration recommandée : socle, 32 Go de stockage personnel et sauvegarde quotidienne.',
    'active',
    1,
    10
WHERE NOT EXISTS (
    SELECT 1 FROM billing_v2_offer_presets
    WHERE code = 'pack-dossier-securise'
);


-- statement-break

INSERT INTO billing_v2_offer_presets
    (id, code, name, description, status, is_public, display_order)
SELECT
    UUID(),
    'pack-acces-distance',
    'Accès sécurisé',
    'Configuration recommandée : socle, 32 Go de stockage personnel, sauvegarde et VPN Essentiel.',
    'active',
    1,
    20
WHERE NOT EXISTS (
    SELECT 1 FROM billing_v2_offer_presets
    WHERE code = 'pack-acces-distance'
);


-- statement-break

INSERT INTO billing_v2_offer_presets
    (id, code, name, description, status, is_public, display_order)
SELECT
    UUID(),
    'pack-bureau-windows-distance',
    'Bureau à distance',
    'Configuration recommandée : socle, 64 Go de stockage personnel, sauvegarde, VPN Plus et accès RDS.',
    'active',
    1,
    30
WHERE NOT EXISTS (
    SELECT 1 FROM billing_v2_offer_presets
    WHERE code = 'pack-bureau-windows-distance'
);


-- statement-break

INSERT INTO billing_v2_offer_presets
    (id, code, name, description, status, is_public, display_order)
SELECT
    UUID(),
    'pack-pro-association',
    'Pro / Association',
    'Configuration recommandée : socle, stockage personnel 64 Go, espace partagé 128 Go, sauvegardes, VPN Plus, un utilisateur supplémentaire et Support Plus.',
    'active',
    1,
    40
WHERE NOT EXISTS (
    SELECT 1 FROM billing_v2_offer_presets
    WHERE code = 'pack-pro-association'
);


-- ============================================================================
-- 3. ITEMS DU PRESET : DOSSIER SÉCURISÉ
-- ============================================================================

-- BASE-SERVICE

-- statement-break

INSERT INTO billing_v2_preset_items
    (id, preset_id, service_id, tier_id, scope_template, quantity,
     required_item, customer_editable, display_order)
SELECT
    UUID(), p.id, s.id, NULL, 'subscription', 1, 1, 0, 10
FROM billing_v2_offer_presets p
JOIN billing_v2_services s ON s.code = 'BASE-SERVICE'
WHERE p.code = 'pack-dossier-securise'
  AND NOT EXISTS (
      SELECT 1
      FROM billing_v2_preset_items pi
      WHERE pi.preset_id = p.id
        AND pi.service_id = s.id
        AND pi.scope_template = 'subscription'
  );

-- STORAGE-PERSONAL 32

-- statement-break

INSERT INTO billing_v2_preset_items
    (id, preset_id, service_id, tier_id, scope_template, quantity,
     required_item, customer_editable, display_order)
SELECT
    UUID(), p.id, s.id, t.id, 'primary_user', 1, 1, 1, 20
FROM billing_v2_offer_presets p
JOIN billing_v2_services s ON s.code = 'STORAGE-PERSONAL'
JOIN billing_v2_service_tiers t ON t.service_id = s.id AND t.code = '32'
WHERE p.code = 'pack-dossier-securise'
  AND NOT EXISTS (
      SELECT 1
      FROM billing_v2_preset_items pi
      WHERE pi.preset_id = p.id
        AND pi.service_id = s.id
        AND pi.scope_template = 'primary_user'
  );

-- BACKUP-PERSONAL 32

-- statement-break

INSERT INTO billing_v2_preset_items
    (id, preset_id, service_id, tier_id, scope_template, quantity,
     required_item, customer_editable, display_order)
SELECT
    UUID(), p.id, s.id, t.id, 'primary_user', 1, 1, 1, 30
FROM billing_v2_offer_presets p
JOIN billing_v2_services s ON s.code = 'BACKUP-PERSONAL'
JOIN billing_v2_service_tiers t ON t.service_id = s.id AND t.code = '32'
WHERE p.code = 'pack-dossier-securise'
  AND NOT EXISTS (
      SELECT 1
      FROM billing_v2_preset_items pi
      WHERE pi.preset_id = p.id
        AND pi.service_id = s.id
        AND pi.scope_template = 'primary_user'
  );


-- ============================================================================
-- 4. ITEMS DU PRESET : ACCÈS SÉCURISÉ
-- ============================================================================

-- Base + storage 32 + backup 32

-- statement-break

INSERT INTO billing_v2_preset_items
    (id, preset_id, service_id, tier_id, scope_template, quantity,
     required_item, customer_editable, display_order)
SELECT UUID(), p.id, s.id, NULL, 'subscription', 1, 1, 0, 10
FROM billing_v2_offer_presets p
JOIN billing_v2_services s ON s.code = 'BASE-SERVICE'
WHERE p.code = 'pack-acces-distance'
  AND NOT EXISTS (
      SELECT 1 FROM billing_v2_preset_items pi
      WHERE pi.preset_id = p.id AND pi.service_id = s.id
        AND pi.scope_template = 'subscription'
  );


-- statement-break

INSERT INTO billing_v2_preset_items
    (id, preset_id, service_id, tier_id, scope_template, quantity,
     required_item, customer_editable, display_order)
SELECT UUID(), p.id, s.id, t.id, 'primary_user', 1, 1, 1, 20
FROM billing_v2_offer_presets p
JOIN billing_v2_services s ON s.code = 'STORAGE-PERSONAL'
JOIN billing_v2_service_tiers t ON t.service_id = s.id AND t.code = '32'
WHERE p.code = 'pack-acces-distance'
  AND NOT EXISTS (
      SELECT 1 FROM billing_v2_preset_items pi
      WHERE pi.preset_id = p.id AND pi.service_id = s.id
        AND pi.scope_template = 'primary_user'
  );


-- statement-break

INSERT INTO billing_v2_preset_items
    (id, preset_id, service_id, tier_id, scope_template, quantity,
     required_item, customer_editable, display_order)
SELECT UUID(), p.id, s.id, t.id, 'primary_user', 1, 1, 1, 30
FROM billing_v2_offer_presets p
JOIN billing_v2_services s ON s.code = 'BACKUP-PERSONAL'
JOIN billing_v2_service_tiers t ON t.service_id = s.id AND t.code = '32'
WHERE p.code = 'pack-acces-distance'
  AND NOT EXISTS (
      SELECT 1 FROM billing_v2_preset_items pi
      WHERE pi.preset_id = p.id AND pi.service_id = s.id
        AND pi.scope_template = 'primary_user'
  );

-- VPN ESSENTIAL

-- statement-break

INSERT INTO billing_v2_preset_items
    (id, preset_id, service_id, tier_id, scope_template, quantity,
     required_item, customer_editable, display_order)
SELECT UUID(), p.id, s.id, t.id, 'primary_user', 1, 1, 1, 40
FROM billing_v2_offer_presets p
JOIN billing_v2_services s ON s.code = 'VPN-ACCESS'
JOIN billing_v2_service_tiers t ON t.service_id = s.id AND t.code = 'ESSENTIAL'
WHERE p.code = 'pack-acces-distance'
  AND NOT EXISTS (
      SELECT 1 FROM billing_v2_preset_items pi
      WHERE pi.preset_id = p.id AND pi.service_id = s.id
        AND pi.scope_template = 'primary_user'
  );


-- ============================================================================
-- 5. ITEMS DU PRESET : BUREAU À DISTANCE
-- ============================================================================


-- statement-break

INSERT INTO billing_v2_preset_items
    (id, preset_id, service_id, tier_id, scope_template, quantity,
     required_item, customer_editable, display_order)
SELECT UUID(), p.id, s.id, NULL, 'subscription', 1, 1, 0, 10
FROM billing_v2_offer_presets p
JOIN billing_v2_services s ON s.code = 'BASE-SERVICE'
WHERE p.code = 'pack-bureau-windows-distance'
  AND NOT EXISTS (
      SELECT 1 FROM billing_v2_preset_items pi
      WHERE pi.preset_id = p.id AND pi.service_id = s.id
        AND pi.scope_template = 'subscription'
  );

-- STORAGE 64

-- statement-break

INSERT INTO billing_v2_preset_items
    (id, preset_id, service_id, tier_id, scope_template, quantity,
     required_item, customer_editable, display_order)
SELECT UUID(), p.id, s.id, t.id, 'primary_user', 1, 1, 1, 20
FROM billing_v2_offer_presets p
JOIN billing_v2_services s ON s.code = 'STORAGE-PERSONAL'
JOIN billing_v2_service_tiers t ON t.service_id = s.id AND t.code = '64'
WHERE p.code = 'pack-bureau-windows-distance'
  AND NOT EXISTS (
      SELECT 1 FROM billing_v2_preset_items pi
      WHERE pi.preset_id = p.id AND pi.service_id = s.id
        AND pi.scope_template = 'primary_user'
  );

-- BACKUP 64

-- statement-break

INSERT INTO billing_v2_preset_items
    (id, preset_id, service_id, tier_id, scope_template, quantity,
     required_item, customer_editable, display_order)
SELECT UUID(), p.id, s.id, t.id, 'primary_user', 1, 1, 1, 30
FROM billing_v2_offer_presets p
JOIN billing_v2_services s ON s.code = 'BACKUP-PERSONAL'
JOIN billing_v2_service_tiers t ON t.service_id = s.id AND t.code = '64'
WHERE p.code = 'pack-bureau-windows-distance'
  AND NOT EXISTS (
      SELECT 1 FROM billing_v2_preset_items pi
      WHERE pi.preset_id = p.id AND pi.service_id = s.id
        AND pi.scope_template = 'primary_user'
  );

-- VPN PLUS

-- statement-break

INSERT INTO billing_v2_preset_items
    (id, preset_id, service_id, tier_id, scope_template, quantity,
     required_item, customer_editable, display_order)
SELECT UUID(), p.id, s.id, t.id, 'primary_user', 1, 1, 1, 40
FROM billing_v2_offer_presets p
JOIN billing_v2_services s ON s.code = 'VPN-ACCESS'
JOIN billing_v2_service_tiers t ON t.service_id = s.id AND t.code = 'PLUS'
WHERE p.code = 'pack-bureau-windows-distance'
  AND NOT EXISTS (
      SELECT 1 FROM billing_v2_preset_items pi
      WHERE pi.preset_id = p.id AND pi.service_id = s.id
        AND pi.scope_template = 'primary_user'
  );

-- RDS

-- statement-break

INSERT INTO billing_v2_preset_items
    (id, preset_id, service_id, tier_id, scope_template, quantity,
     required_item, customer_editable, display_order)
SELECT UUID(), p.id, s.id, NULL, 'primary_user', 1, 1, 1, 50
FROM billing_v2_offer_presets p
JOIN billing_v2_services s ON s.code = 'RDS-ACCESS'
WHERE p.code = 'pack-bureau-windows-distance'
  AND NOT EXISTS (
      SELECT 1 FROM billing_v2_preset_items pi
      WHERE pi.preset_id = p.id AND pi.service_id = s.id
        AND pi.scope_template = 'primary_user'
  );


-- ============================================================================
-- 6. ITEMS DU PRESET : PRO / ASSOCIATION
-- ============================================================================

-- BASE

-- statement-break

INSERT INTO billing_v2_preset_items
    (id, preset_id, service_id, tier_id, scope_template, quantity,
     required_item, customer_editable, display_order)
SELECT UUID(), p.id, s.id, NULL, 'subscription', 1, 1, 0, 10
FROM billing_v2_offer_presets p
JOIN billing_v2_services s ON s.code = 'BASE-SERVICE'
WHERE p.code = 'pack-pro-association'
  AND NOT EXISTS (
      SELECT 1 FROM billing_v2_preset_items pi
      WHERE pi.preset_id = p.id AND pi.service_id = s.id
        AND pi.scope_template = 'subscription'
  );

-- Primary STORAGE 64

-- statement-break

INSERT INTO billing_v2_preset_items
    (id, preset_id, service_id, tier_id, scope_template, quantity,
     required_item, customer_editable, display_order)
SELECT UUID(), p.id, s.id, t.id, 'primary_user', 1, 1, 1, 20
FROM billing_v2_offer_presets p
JOIN billing_v2_services s ON s.code = 'STORAGE-PERSONAL'
JOIN billing_v2_service_tiers t ON t.service_id = s.id AND t.code = '64'
WHERE p.code = 'pack-pro-association'
  AND NOT EXISTS (
      SELECT 1 FROM billing_v2_preset_items pi
      WHERE pi.preset_id = p.id AND pi.service_id = s.id
        AND pi.scope_template = 'primary_user'
  );

-- Primary BACKUP 64

-- statement-break

INSERT INTO billing_v2_preset_items
    (id, preset_id, service_id, tier_id, scope_template, quantity,
     required_item, customer_editable, display_order)
SELECT UUID(), p.id, s.id, t.id, 'primary_user', 1, 1, 1, 30
FROM billing_v2_offer_presets p
JOIN billing_v2_services s ON s.code = 'BACKUP-PERSONAL'
JOIN billing_v2_service_tiers t ON t.service_id = s.id AND t.code = '64'
WHERE p.code = 'pack-pro-association'
  AND NOT EXISTS (
      SELECT 1 FROM billing_v2_preset_items pi
      WHERE pi.preset_id = p.id AND pi.service_id = s.id
        AND pi.scope_template = 'primary_user'
  );

-- Primary VPN PLUS

-- statement-break

INSERT INTO billing_v2_preset_items
    (id, preset_id, service_id, tier_id, scope_template, quantity,
     required_item, customer_editable, display_order)
SELECT UUID(), p.id, s.id, t.id, 'primary_user', 1, 1, 1, 40
FROM billing_v2_offer_presets p
JOIN billing_v2_services s ON s.code = 'VPN-ACCESS'
JOIN billing_v2_service_tiers t ON t.service_id = s.id AND t.code = 'PLUS'
WHERE p.code = 'pack-pro-association'
  AND NOT EXISTS (
      SELECT 1 FROM billing_v2_preset_items pi
      WHERE pi.preset_id = p.id AND pi.service_id = s.id
        AND pi.scope_template = 'primary_user'
  );

-- SHARED STORAGE 128

-- statement-break

INSERT INTO billing_v2_preset_items
    (id, preset_id, service_id, tier_id, scope_template, quantity,
     required_item, customer_editable, display_order)
SELECT UUID(), p.id, s.id, t.id, 'subscription', 1, 1, 1, 50
FROM billing_v2_offer_presets p
JOIN billing_v2_services s ON s.code = 'STORAGE-SHARED'
JOIN billing_v2_service_tiers t ON t.service_id = s.id AND t.code = '128'
WHERE p.code = 'pack-pro-association'
  AND NOT EXISTS (
      SELECT 1 FROM billing_v2_preset_items pi
      WHERE pi.preset_id = p.id AND pi.service_id = s.id
        AND pi.scope_template = 'subscription'
  );

-- SHARED BACKUP 128

-- statement-break

INSERT INTO billing_v2_preset_items
    (id, preset_id, service_id, tier_id, scope_template, quantity,
     required_item, customer_editable, display_order)
SELECT UUID(), p.id, s.id, t.id, 'subscription', 1, 1, 1, 60
FROM billing_v2_offer_presets p
JOIN billing_v2_services s ON s.code = 'BACKUP-SHARED'
JOIN billing_v2_service_tiers t ON t.service_id = s.id AND t.code = '128'
WHERE p.code = 'pack-pro-association'
  AND NOT EXISTS (
      SELECT 1 FROM billing_v2_preset_items pi
      WHERE pi.preset_id = p.id AND pi.service_id = s.id
        AND pi.scope_template = 'subscription'
  );

-- One additional user

-- statement-break

INSERT INTO billing_v2_preset_items
    (id, preset_id, service_id, tier_id, scope_template, quantity,
     required_item, customer_editable, display_order)
SELECT UUID(), p.id, s.id, NULL, 'additional_user', 1, 1, 1, 70
FROM billing_v2_offer_presets p
JOIN billing_v2_services s ON s.code = 'USER-ADDITIONAL'
WHERE p.code = 'pack-pro-association'
  AND NOT EXISTS (
      SELECT 1 FROM billing_v2_preset_items pi
      WHERE pi.preset_id = p.id AND pi.service_id = s.id
        AND pi.scope_template = 'additional_user'
  );

-- SUPPORT PLUS

-- statement-break

INSERT INTO billing_v2_preset_items
    (id, preset_id, service_id, tier_id, scope_template, quantity,
     required_item, customer_editable, display_order)
SELECT UUID(), p.id, s.id, NULL, 'subscription', 1, 1, 1, 80
FROM billing_v2_offer_presets p
JOIN billing_v2_services s ON s.code = 'SUPPORT-PLUS'
WHERE p.code = 'pack-pro-association'
  AND NOT EXISTS (
      SELECT 1 FROM billing_v2_preset_items pi
      WHERE pi.preset_id = p.id AND pi.service_id = s.id
        AND pi.scope_template = 'subscription'
  );


-- ============================================================================

-- ============================================================================
-- RÈGLES DE PROVISIONING V2 DORMANTES
-- ============================================================================

-- statement-break

INSERT INTO billing_v2_provisioning_rules
    (id, service_id, tier_id, rule_type, target_type, target_reference,
     value_source, static_value, enable_action, disable_action, status,
     display_order)
SELECT UUID(), s.id, t.id, 'ad_group_membership', 'ad_group', 'GG_VPN',
       'none', NULL, 'add_member', 'remove_member', 'active', 10
FROM billing_v2_services s
JOIN billing_v2_service_tiers t ON t.service_id = s.id
WHERE s.code = 'VPN-ACCESS'
  AND t.code IN ('LEGACY', 'ESSENTIAL', 'PLUS', 'PERFORMANCE', 'PRO')
  AND NOT EXISTS (
      SELECT 1
      FROM billing_v2_provisioning_rules rule
      WHERE rule.service_id = s.id
        AND rule.tier_id = t.id
        AND rule.rule_type = 'ad_group_membership'
        AND rule.target_type = 'ad_group'
        AND rule.target_reference = 'GG_VPN'
  );

-- statement-break

INSERT INTO billing_v2_provisioning_rules
    (id, service_id, tier_id, rule_type, target_type, target_reference,
     value_source, static_value, enable_action, disable_action, status,
     display_order)
SELECT UUID(), s.id, NULL, 'ad_group_membership', 'ad_group', 'GG_RDS',
       'none', NULL, 'add_member', 'remove_member', 'active', 20
FROM billing_v2_services s
WHERE s.code = 'RDS-ACCESS'
  AND NOT EXISTS (
      SELECT 1
      FROM billing_v2_provisioning_rules rule
      WHERE rule.service_id = s.id
        AND rule.tier_id IS NULL
        AND rule.rule_type = 'ad_group_membership'
        AND rule.target_type = 'ad_group'
        AND rule.target_reference = 'GG_RDS'
  );

-- statement-break

INSERT INTO billing_v2_provisioning_rules
    (id, service_id, tier_id, rule_type, target_type, target_reference,
     value_source, static_value, enable_action, disable_action, status,
     display_order)
SELECT UUID(), s.id, t.id, 'nextcloud_quota', 'nextcloud_user_quota', NULL,
       'tier_numeric_value', NULL, 'set_user_quota', NULL, 'active', 30
FROM billing_v2_services s
JOIN billing_v2_service_tiers t ON t.service_id = s.id
WHERE s.code = 'STORAGE-PERSONAL'
  AND NOT EXISTS (
      SELECT 1
      FROM billing_v2_provisioning_rules rule
      WHERE rule.service_id = s.id
        AND rule.tier_id = t.id
        AND rule.rule_type = 'nextcloud_quota'
        AND rule.target_type = 'nextcloud_user_quota'
  );

-- statement-break

INSERT INTO billing_v2_provisioning_rules
    (id, service_id, tier_id, rule_type, target_type, target_reference,
     value_source, static_value, enable_action, disable_action, status,
     display_order)
SELECT UUID(), s.id, t.id, 'nextcloud_quota', 'nextcloud_shared_quota', NULL,
       'tier_numeric_value', NULL, 'set_shared_quota', NULL, 'active', 40
FROM billing_v2_services s
JOIN billing_v2_service_tiers t ON t.service_id = s.id
WHERE s.code = 'STORAGE-SHARED'
  AND NOT EXISTS (
      SELECT 1
      FROM billing_v2_provisioning_rules rule
      WHERE rule.service_id = s.id
        AND rule.tier_id = t.id
        AND rule.rule_type = 'nextcloud_quota'
        AND rule.target_type = 'nextcloud_shared_quota'
  );

-- ============================================================================

-- ============================================================================
-- MAPPING DES ANCIENNES BRIQUES TECHNIQUES
-- ============================================================================


-- statement-break

INSERT INTO billing_v2_legacy_service_mappings
    (legacy_service_reference, mapping_kind, v2_service_code, v2_tier_code, notes)
VALUES
    ('STOCK-PERSO-32', 'direct',
     'STORAGE-PERSONAL', '32',
     'Base legacy de 32 Go.'),

    ('STOCK-SUP-32', 'storage_increment',
     'STORAGE-PERSONAL', NULL,
     'Ne pas créer un item V2 séparé. Ajouter 32 Go à la capacité legacy résolue. Exemple : STOCK-PERSO-32 + STOCK-SUP-32 => tier V2 64.'),

    ('SAVE-PERSO', 'dependent_tier',
     'BACKUP-PERSONAL', NULL,
     'Le tier V2 doit être identique à la capacité STORAGE-PERSONAL résolue.'),

    ('ACCES-VPN', 'direct',
     'VPN-ACCESS', 'LEGACY',
     'Ne pas deviner le plafond historique. Qualifier ultérieurement le tier réel avant conversion vers ESSENTIAL/PLUS/PERFORMANCE/PRO.'),

    ('ACCES-RDS', 'direct',
     'RDS-ACCESS', NULL,
     'Accès RDS par utilisateur.'),

    ('SUPERV-SERVICE', 'absorbed_in_base',
     'MONITORING-INTERNAL', NULL,
     'Fonctionnalité incluse dans BASE-SERVICE en V2 ; pas de ligne facturable séparée.'),

    ('SUPPORT-LV1', 'absorbed_in_base',
     'SUPPORT-STANDARD', NULL,
     'Support standard inclus dans BASE-SERVICE en V2.'),

    ('SUPPORT-LV2', 'direct',
     'SUPPORT-PLUS', NULL,
     'Mapping fonctionnel du support renforcé.'),

    ('USER-ADD', 'direct',
     'USER-ADDITIONAL', NULL,
     'Utilisateur supplémentaire récurrent.'),

    ('DOC-TECH', 'legacy_one_time_entitlement',
     'DOC-TECH', NULL,
     'Ne pas créer un abonnement récurrent V2. Conserver uniquement l''historique / droit ponctuel déjà inclus dans le pack legacy.')
ON DUPLICATE KEY UPDATE
    mapping_kind = VALUES(mapping_kind),
    v2_service_code = VALUES(v2_service_code),
    v2_tier_code = VALUES(v2_tier_code),
    notes = VALUES(notes);

-- 9. MAPPING DES 20 OFFRES LEGACY
--
-- Ce mapping exprime uniquement :
--   * lignée commerciale / preset
--   * engagement
--   * mode de paiement
--
-- Il NE signifie PAS que les items du preset V2 doivent remplacer les droits
-- historiques lors d'une migration d'abonnement existant.
-- ============================================================================

-- Helper pattern:
-- INSERT ... SELECT legacy_id, preset.id, term.id, mode, external_reference

-- DOSSIER SÉCURISÉ -----------------------------------------------------------


-- statement-break

INSERT INTO billing_v2_legacy_offer_mappings
    (legacy_offer_id, preset_id, commitment_term_id, payment_mode,
     legacy_external_reference, status)
SELECT
    '61000000-0000-0000-0000-000000000101',
    p.id, t.id, 'monthly', 'PACK-DOSSIER-1M-MENS', 'active'
FROM billing_v2_offer_presets p
JOIN billing_v2_commitment_terms t ON t.code = 'FLEX'
WHERE p.code = 'pack-dossier-securise'
ON DUPLICATE KEY UPDATE
    preset_id = VALUES(preset_id),
    commitment_term_id = VALUES(commitment_term_id),
    payment_mode = VALUES(payment_mode),
    legacy_external_reference = VALUES(legacy_external_reference),
    status = VALUES(status);


-- statement-break

INSERT INTO billing_v2_legacy_offer_mappings
    (legacy_offer_id, preset_id, commitment_term_id, payment_mode,
     legacy_external_reference, status)
SELECT
    '61000000-0000-0000-0000-000000000102',
    p.id, t.id, 'monthly', 'PACK-DOSSIER-6M-MENS', 'active'
FROM billing_v2_offer_presets p
JOIN billing_v2_commitment_terms t ON t.code = 'TERM-6'
WHERE p.code = 'pack-dossier-securise'
ON DUPLICATE KEY UPDATE
    preset_id = VALUES(preset_id),
    commitment_term_id = VALUES(commitment_term_id),
    payment_mode = VALUES(payment_mode),
    legacy_external_reference = VALUES(legacy_external_reference),
    status = VALUES(status);


-- statement-break

INSERT INTO billing_v2_legacy_offer_mappings
    (legacy_offer_id, preset_id, commitment_term_id, payment_mode,
     legacy_external_reference, status)
SELECT
    '61000000-0000-0000-0000-000000000103',
    p.id, t.id, 'upfront', 'PACK-DOSSIER-6M-COMPT', 'active'
FROM billing_v2_offer_presets p
JOIN billing_v2_commitment_terms t ON t.code = 'TERM-6'
WHERE p.code = 'pack-dossier-securise'
ON DUPLICATE KEY UPDATE
    preset_id = VALUES(preset_id),
    commitment_term_id = VALUES(commitment_term_id),
    payment_mode = VALUES(payment_mode),
    legacy_external_reference = VALUES(legacy_external_reference),
    status = VALUES(status);


-- statement-break

INSERT INTO billing_v2_legacy_offer_mappings
    (legacy_offer_id, preset_id, commitment_term_id, payment_mode,
     legacy_external_reference, status)
SELECT
    '61000000-0000-0000-0000-000000000104',
    p.id, t.id, 'monthly', 'PACK-DOSSIER-12M-MENS', 'active'
FROM billing_v2_offer_presets p
JOIN billing_v2_commitment_terms t ON t.code = 'TERM-12'
WHERE p.code = 'pack-dossier-securise'
ON DUPLICATE KEY UPDATE
    preset_id = VALUES(preset_id),
    commitment_term_id = VALUES(commitment_term_id),
    payment_mode = VALUES(payment_mode),
    legacy_external_reference = VALUES(legacy_external_reference),
    status = VALUES(status);


-- statement-break

INSERT INTO billing_v2_legacy_offer_mappings
    (legacy_offer_id, preset_id, commitment_term_id, payment_mode,
     legacy_external_reference, status)
SELECT
    '61000000-0000-0000-0000-000000000105',
    p.id, t.id, 'upfront', 'PACK-DOSSIER-12M-COMPT', 'active'
FROM billing_v2_offer_presets p
JOIN billing_v2_commitment_terms t ON t.code = 'TERM-12'
WHERE p.code = 'pack-dossier-securise'
ON DUPLICATE KEY UPDATE
    preset_id = VALUES(preset_id),
    commitment_term_id = VALUES(commitment_term_id),
    payment_mode = VALUES(payment_mode),
    legacy_external_reference = VALUES(legacy_external_reference),
    status = VALUES(status);


-- ACCÈS SÉCURISÉ -------------------------------------------------------------


-- statement-break

INSERT INTO billing_v2_legacy_offer_mappings
    (legacy_offer_id, preset_id, commitment_term_id, payment_mode,
     legacy_external_reference, status)
SELECT '61000000-0000-0000-0000-000000000106', p.id, t.id,
       'monthly', 'PACK-ACCES-1M-MENS', 'active'
FROM billing_v2_offer_presets p
JOIN billing_v2_commitment_terms t ON t.code = 'FLEX'
WHERE p.code = 'pack-acces-distance'
ON DUPLICATE KEY UPDATE preset_id=VALUES(preset_id),
 commitment_term_id=VALUES(commitment_term_id), payment_mode=VALUES(payment_mode),
 legacy_external_reference=VALUES(legacy_external_reference), status=VALUES(status);


-- statement-break

INSERT INTO billing_v2_legacy_offer_mappings
    (legacy_offer_id, preset_id, commitment_term_id, payment_mode,
     legacy_external_reference, status)
SELECT '61000000-0000-0000-0000-000000000107', p.id, t.id,
       'monthly', 'PACK-ACCES-6M-MENS', 'active'
FROM billing_v2_offer_presets p
JOIN billing_v2_commitment_terms t ON t.code = 'TERM-6'
WHERE p.code = 'pack-acces-distance'
ON DUPLICATE KEY UPDATE preset_id=VALUES(preset_id),
 commitment_term_id=VALUES(commitment_term_id), payment_mode=VALUES(payment_mode),
 legacy_external_reference=VALUES(legacy_external_reference), status=VALUES(status);


-- statement-break

INSERT INTO billing_v2_legacy_offer_mappings
    (legacy_offer_id, preset_id, commitment_term_id, payment_mode,
     legacy_external_reference, status)
SELECT '61000000-0000-0000-0000-000000000108', p.id, t.id,
       'upfront', 'PACK-ACCES-6M-COMPT', 'active'
FROM billing_v2_offer_presets p
JOIN billing_v2_commitment_terms t ON t.code = 'TERM-6'
WHERE p.code = 'pack-acces-distance'
ON DUPLICATE KEY UPDATE preset_id=VALUES(preset_id),
 commitment_term_id=VALUES(commitment_term_id), payment_mode=VALUES(payment_mode),
 legacy_external_reference=VALUES(legacy_external_reference), status=VALUES(status);


-- statement-break

INSERT INTO billing_v2_legacy_offer_mappings
    (legacy_offer_id, preset_id, commitment_term_id, payment_mode,
     legacy_external_reference, status)
SELECT '61000000-0000-0000-0000-000000000109', p.id, t.id,
       'monthly', 'PACK-ACCES-12M-MENS', 'active'
FROM billing_v2_offer_presets p
JOIN billing_v2_commitment_terms t ON t.code = 'TERM-12'
WHERE p.code = 'pack-acces-distance'
ON DUPLICATE KEY UPDATE preset_id=VALUES(preset_id),
 commitment_term_id=VALUES(commitment_term_id), payment_mode=VALUES(payment_mode),
 legacy_external_reference=VALUES(legacy_external_reference), status=VALUES(status);


-- statement-break

INSERT INTO billing_v2_legacy_offer_mappings
    (legacy_offer_id, preset_id, commitment_term_id, payment_mode,
     legacy_external_reference, status)
SELECT '61000000-0000-0000-0000-000000000110', p.id, t.id,
       'upfront', 'PACK-ACCES-12M-COMPT', 'active'
FROM billing_v2_offer_presets p
JOIN billing_v2_commitment_terms t ON t.code = 'TERM-12'
WHERE p.code = 'pack-acces-distance'
ON DUPLICATE KEY UPDATE preset_id=VALUES(preset_id),
 commitment_term_id=VALUES(commitment_term_id), payment_mode=VALUES(payment_mode),
 legacy_external_reference=VALUES(legacy_external_reference), status=VALUES(status);


-- BUREAU À DISTANCE ----------------------------------------------------------


-- statement-break

INSERT INTO billing_v2_legacy_offer_mappings
    (legacy_offer_id, preset_id, commitment_term_id, payment_mode,
     legacy_external_reference, status)
SELECT '61000000-0000-0000-0000-000000000111', p.id, t.id,
       'monthly', 'PACK-BUREAU-1M-MENS', 'active'
FROM billing_v2_offer_presets p
JOIN billing_v2_commitment_terms t ON t.code = 'FLEX'
WHERE p.code = 'pack-bureau-windows-distance'
ON DUPLICATE KEY UPDATE preset_id=VALUES(preset_id),
 commitment_term_id=VALUES(commitment_term_id), payment_mode=VALUES(payment_mode),
 legacy_external_reference=VALUES(legacy_external_reference), status=VALUES(status);


-- statement-break

INSERT INTO billing_v2_legacy_offer_mappings
    (legacy_offer_id, preset_id, commitment_term_id, payment_mode,
     legacy_external_reference, status)
SELECT '61000000-0000-0000-0000-000000000112', p.id, t.id,
       'monthly', 'PACK-BUREAU-6M-MENS', 'active'
FROM billing_v2_offer_presets p
JOIN billing_v2_commitment_terms t ON t.code = 'TERM-6'
WHERE p.code = 'pack-bureau-windows-distance'
ON DUPLICATE KEY UPDATE preset_id=VALUES(preset_id),
 commitment_term_id=VALUES(commitment_term_id), payment_mode=VALUES(payment_mode),
 legacy_external_reference=VALUES(legacy_external_reference), status=VALUES(status);


-- statement-break

INSERT INTO billing_v2_legacy_offer_mappings
    (legacy_offer_id, preset_id, commitment_term_id, payment_mode,
     legacy_external_reference, status)
SELECT '61000000-0000-0000-0000-000000000113', p.id, t.id,
       'upfront', 'PACK-BUREAU-6M-COMPT', 'active'
FROM billing_v2_offer_presets p
JOIN billing_v2_commitment_terms t ON t.code = 'TERM-6'
WHERE p.code = 'pack-bureau-windows-distance'
ON DUPLICATE KEY UPDATE preset_id=VALUES(preset_id),
 commitment_term_id=VALUES(commitment_term_id), payment_mode=VALUES(payment_mode),
 legacy_external_reference=VALUES(legacy_external_reference), status=VALUES(status);


-- statement-break

INSERT INTO billing_v2_legacy_offer_mappings
    (legacy_offer_id, preset_id, commitment_term_id, payment_mode,
     legacy_external_reference, status)
SELECT '61000000-0000-0000-0000-000000000114', p.id, t.id,
       'monthly', 'PACK-BUREAU-12M-MENS', 'active'
FROM billing_v2_offer_presets p
JOIN billing_v2_commitment_terms t ON t.code = 'TERM-12'
WHERE p.code = 'pack-bureau-windows-distance'
ON DUPLICATE KEY UPDATE preset_id=VALUES(preset_id),
 commitment_term_id=VALUES(commitment_term_id), payment_mode=VALUES(payment_mode),
 legacy_external_reference=VALUES(legacy_external_reference), status=VALUES(status);


-- statement-break

INSERT INTO billing_v2_legacy_offer_mappings
    (legacy_offer_id, preset_id, commitment_term_id, payment_mode,
     legacy_external_reference, status)
SELECT '61000000-0000-0000-0000-000000000115', p.id, t.id,
       'upfront', 'PACK-BUREAU-12M-COMPT', 'active'
FROM billing_v2_offer_presets p
JOIN billing_v2_commitment_terms t ON t.code = 'TERM-12'
WHERE p.code = 'pack-bureau-windows-distance'
ON DUPLICATE KEY UPDATE preset_id=VALUES(preset_id),
 commitment_term_id=VALUES(commitment_term_id), payment_mode=VALUES(payment_mode),
 legacy_external_reference=VALUES(legacy_external_reference), status=VALUES(status);


-- PRO / ASSOCIATION ----------------------------------------------------------


-- statement-break

INSERT INTO billing_v2_legacy_offer_mappings
    (legacy_offer_id, preset_id, commitment_term_id, payment_mode,
     legacy_external_reference, status)
SELECT '61000000-0000-0000-0000-000000000116', p.id, t.id,
       'monthly', 'PACK-PRO-1M-MENS', 'active'
FROM billing_v2_offer_presets p
JOIN billing_v2_commitment_terms t ON t.code = 'FLEX'
WHERE p.code = 'pack-pro-association'
ON DUPLICATE KEY UPDATE preset_id=VALUES(preset_id),
 commitment_term_id=VALUES(commitment_term_id), payment_mode=VALUES(payment_mode),
 legacy_external_reference=VALUES(legacy_external_reference), status=VALUES(status);


-- statement-break

INSERT INTO billing_v2_legacy_offer_mappings
    (legacy_offer_id, preset_id, commitment_term_id, payment_mode,
     legacy_external_reference, status)
SELECT '61000000-0000-0000-0000-000000000117', p.id, t.id,
       'monthly', 'PACK-PRO-6M-MENS', 'active'
FROM billing_v2_offer_presets p
JOIN billing_v2_commitment_terms t ON t.code = 'TERM-6'
WHERE p.code = 'pack-pro-association'
ON DUPLICATE KEY UPDATE preset_id=VALUES(preset_id),
 commitment_term_id=VALUES(commitment_term_id), payment_mode=VALUES(payment_mode),
 legacy_external_reference=VALUES(legacy_external_reference), status=VALUES(status);


-- statement-break

INSERT INTO billing_v2_legacy_offer_mappings
    (legacy_offer_id, preset_id, commitment_term_id, payment_mode,
     legacy_external_reference, status)
SELECT '61000000-0000-0000-0000-000000000118', p.id, t.id,
       'upfront', 'PACK-PRO-6M-COMPT', 'active'
FROM billing_v2_offer_presets p
JOIN billing_v2_commitment_terms t ON t.code = 'TERM-6'
WHERE p.code = 'pack-pro-association'
ON DUPLICATE KEY UPDATE preset_id=VALUES(preset_id),
 commitment_term_id=VALUES(commitment_term_id), payment_mode=VALUES(payment_mode),
 legacy_external_reference=VALUES(legacy_external_reference), status=VALUES(status);


-- statement-break

INSERT INTO billing_v2_legacy_offer_mappings
    (legacy_offer_id, preset_id, commitment_term_id, payment_mode,
     legacy_external_reference, status)
SELECT '61000000-0000-0000-0000-000000000119', p.id, t.id,
       'monthly', 'PACK-PRO-12M-MENS', 'active'
FROM billing_v2_offer_presets p
JOIN billing_v2_commitment_terms t ON t.code = 'TERM-12'
WHERE p.code = 'pack-pro-association'
ON DUPLICATE KEY UPDATE preset_id=VALUES(preset_id),
 commitment_term_id=VALUES(commitment_term_id), payment_mode=VALUES(payment_mode),
 legacy_external_reference=VALUES(legacy_external_reference), status=VALUES(status);


-- statement-break

INSERT INTO billing_v2_legacy_offer_mappings
    (legacy_offer_id, preset_id, commitment_term_id, payment_mode,
     legacy_external_reference, status)
SELECT '61000000-0000-0000-0000-000000000120', p.id, t.id,
       'upfront', 'PACK-PRO-12M-COMPT', 'active'
FROM billing_v2_offer_presets p
JOIN billing_v2_commitment_terms t ON t.code = 'TERM-12'
WHERE p.code = 'pack-pro-association'
ON DUPLICATE KEY UPDATE preset_id=VALUES(preset_id),
 commitment_term_id=VALUES(commitment_term_id), payment_mode=VALUES(payment_mode),
 legacy_external_reference=VALUES(legacy_external_reference), status=VALUES(status);


-- ============================================================================

-- ============================================================================
-- FIN MIGRATION 048 - SEED CATALOGUE V2 DORMANT
-- ============================================================================
