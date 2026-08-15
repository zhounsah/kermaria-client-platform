-- ============================================================================
-- Zachary IT - Billing V2
-- Migration 002 : presets commerciaux + pont legacy
--
-- Prérequis :
--   001_schema.sql ou la migration applicative 047_billing_v2_schema_dormant.sql appliquée
--
-- Cette migration :
--   1. ajoute un tier VPN LEGACY caché pour ne pas inventer le débit des clients
--      existants ;
--   2. crée les 4 presets commerciaux V2 ;
--   3. mappe les 20 PACK-* legacy vers leur lignée commerciale, engagement
--      et mode de paiement ;
--   4. ajoute une table de traduction des anciennes briques techniques ;
--   5. ajoute un mécanisme de price-lock pour préserver le prix des contrats
--      legacy lors d'une migration progressive.
--
-- IMPORTANT :
--   * Aucun abonnement existant n'est repricé par ce fichier.
--   * Les presets V2 ne doivent PAS servir à reconstruire aveuglément les
--     droits historiques d'un client legacy.
--   * La configuration legacy exacte doit être migrée depuis les anciens
--     technical_service_references, via les règles ci-dessous.
-- ============================================================================

SET NAMES utf8mb4;

-- ============================================================================
-- 1. TIER VPN LEGACY CACHÉ
--
-- Le service ACCES-VPN legacy ne contient pas, dans les données fournies,
-- suffisamment d'information pour déterminer de manière certaine s'il
-- correspond à Essentiel / Plus / Performance / Pro.
--
-- On ne devine donc pas : on conserve un tier technique LEGACY non public.
-- ============================================================================

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
-- 7. TABLE DE TRADUCTION DES ANCIENNES BRIQUES
-- ============================================================================

CREATE TABLE IF NOT EXISTS billing_v2_legacy_service_mappings (
    legacy_service_reference        VARCHAR(96)   NOT NULL,
    mapping_kind                    VARCHAR(40)   NOT NULL,

    v2_service_code                 VARCHAR(64)   NULL,
    v2_tier_code                    VARCHAR(64)   NULL,

    notes                           TEXT          NULL,
    created_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    PRIMARY KEY (legacy_service_reference)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


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


-- ============================================================================
-- 8. PRICE LOCK POUR LES CONTRATS LEGACY MIGRÉS
--
-- Objectif :
--   préserver le montant réellement contracté au lieu de le recalculer avec
--   les nouveaux prix V2.
--
-- monthly_recurring :
--   montant mensuel fixe legacy jusqu'à effective_until.
--
-- upfront_prepaid :
--   montant total déjà payé pour la période ; aucune nouvelle facture
--   récurrente n'est due avant effective_until.
-- ============================================================================

CREATE TABLE IF NOT EXISTS billing_v2_subscription_price_locks (
    id                              CHAR(36)      NOT NULL,
    subscription_id                 CHAR(36)      NOT NULL,

    lock_type                       VARCHAR(32)   NOT NULL,
    amount_cents                    BIGINT        NOT NULL,
    currency                        CHAR(3)       NOT NULL DEFAULT 'EUR',

    effective_from                  DATETIME(6)   NOT NULL,
    effective_until                 DATETIME(6)   NOT NULL,

    source_legacy_offer_id          CHAR(36)      NULL,
    reason                          VARCHAR(96)   NOT NULL DEFAULT 'legacy_migration',

    status                          VARCHAR(24)   NOT NULL DEFAULT 'active',
    created_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),
    KEY idx_billing_v2_subscription_price_locks_active
        (subscription_id, status, effective_from, effective_until),

    CONSTRAINT fk_billing_v2_subscription_price_locks_subscription
        FOREIGN KEY (subscription_id)
        REFERENCES billing_v2_subscriptions(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- ============================================================================
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
-- 10. VUE DE CONTRÔLE DU MAPPING LEGACY
-- ============================================================================

CREATE OR REPLACE VIEW billing_v2_legacy_offer_mapping_report AS
SELECT
    m.legacy_offer_id,
    m.legacy_external_reference,
    p.code AS preset_code,
    p.name AS preset_name,
    t.code AS commitment_code,
    t.commitment_months,
    m.payment_mode,
    m.status
FROM billing_v2_legacy_offer_mappings m
LEFT JOIN billing_v2_offer_presets p
       ON p.id = m.preset_id
LEFT JOIN billing_v2_commitment_terms t
       ON t.id = m.commitment_term_id;


-- ============================================================================
-- 11. RÈGLES DE MIGRATION À RESPECTER PAR LE CODE
--
-- EXISTING LEGACY SUBSCRIPTION:
--
--   Commercial lineage:
--       legacy offer -> billing_v2_legacy_offer_mappings -> preset
--
--   Actual entitlements:
--       legacy technical_service_references
--       -> billing_v2_legacy_service_mappings
--       -> exact V2 subscription_items
--
--   Pricing:
--       monthly legacy contract
--         -> monthly_recurring price lock until contractual renewal
--
--       upfront legacy contract
--         -> upfront_prepaid price lock until prepaid period ends
--
--   V2 preset pricing:
--       NEVER used to silently reprice an existing legacy contract.
--
-- NEW V2 SUBSCRIPTION:
--       preset -> preset_items -> current service_prices -> V2 billing rules
--
-- At renewal:
--       explicit transition from legacy lock to dynamic V2 pricing.
-- ============================================================================
