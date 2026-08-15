-- ============================================================================
-- Zachary IT - Billing V2
-- Migration 059 : correction UTC des DEFAULT / ON UPDATE (Phase 2.5)
--
-- Probleme corrige :
--   CURRENT_TIMESTAMP / NOW() renvoient l'heure LOCALE du serveur MariaDB
--   (Paris) alors que toute la convention du projet stocke en UTC. Une colonne
--   alimentee par le DEFAULT enregistrait donc une heure decalee de 1 a 2
--   heures selon la saison, dans des colonnes relues comme de l'UTC.
--
--   Ce defaut a deja mordu trois fois (V0.20 BPCE, V0.21 email log,
--   V0.35.1 commercial_documents) ; le garde-fou statique `npm run test:timezone`
--   existe pour cela, mais 032 et 047-056 le violaient encore.
--
-- Deux corrections complementaires, volontairement redondantes :
--
--   1. les fichiers 032 et 047-058 sont corriges A LA SOURCE, pour que toute
--      base reconstruite parte d'un schema juste ;
--   2. cette migration additive rejoue la correction en ALTER, pour les bases
--      ou 032/047-058 sont deja enregistrees dans `schema_migrations` et ne
--      seront donc jamais rejouees.
--
-- ON UPDATE CURRENT_TIMESTAMP est SUPPRIME et non remplace : MariaDB n'accepte
-- que CURRENT_TIMESTAMP a cet endroit. C'est sans effet fonctionnel, un audit
-- de tous les writers C# ayant confirme qu'ils positionnent tous `updated_at`
-- explicitement en UTC.
--
-- Cette migration ne touche aucune donnee : MODIFY COLUMN ne change que la
-- definition de colonne, jamais les lignes existantes.
--
-- Verification apres execution :
--
-- SELECT COUNT(*) FROM information_schema.COLUMNS
-- WHERE TABLE_SCHEMA = DATABASE()
--   AND (COLUMN_DEFAULT LIKE '%current_timestamp%'
--        OR EXTRA LIKE '%on update current_timestamp%');
--   -> doit retourner 0.
-- ============================================================================
ALTER TABLE billing_v2_audit_log
    MODIFY COLUMN created_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6);

-- statement-break

ALTER TABLE billing_v2_authoritative_checkout_requests
    MODIFY COLUMN created_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    MODIFY COLUMN updated_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6);

-- statement-break

ALTER TABLE billing_v2_commitment_payment_options
    MODIFY COLUMN created_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    MODIFY COLUMN updated_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6);

-- statement-break

ALTER TABLE billing_v2_commitment_terms
    MODIFY COLUMN created_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    MODIFY COLUMN updated_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6);

-- statement-break

ALTER TABLE billing_v2_document_line_snapshots
    MODIFY COLUMN created_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6);

-- statement-break

ALTER TABLE billing_v2_legacy_offer_mappings
    MODIFY COLUMN created_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6);

-- statement-break

ALTER TABLE billing_v2_legacy_service_mappings
    MODIFY COLUMN created_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6);

-- statement-break

ALTER TABLE billing_v2_offer_presets
    MODIFY COLUMN created_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    MODIFY COLUMN updated_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6);

-- statement-break

ALTER TABLE billing_v2_outbox_events
    MODIFY COLUMN available_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    MODIFY COLUMN created_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6);

-- statement-break

ALTER TABLE billing_v2_payment_agreements
    MODIFY COLUMN created_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    MODIFY COLUMN updated_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6);

-- statement-break

ALTER TABLE billing_v2_preset_items
    MODIFY COLUMN created_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6);

-- statement-break

ALTER TABLE billing_v2_provider_checkout_sessions
    MODIFY COLUMN created_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    MODIFY COLUMN updated_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6);

-- statement-break

ALTER TABLE billing_v2_provider_events
    MODIFY COLUMN created_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    MODIFY COLUMN updated_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6);

-- statement-break

ALTER TABLE billing_v2_provider_price_mappings
    MODIFY COLUMN created_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    MODIFY COLUMN updated_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6);

-- statement-break

ALTER TABLE billing_v2_provisioning_client_readiness
    MODIFY COLUMN created_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    MODIFY COLUMN updated_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6);

-- statement-break

ALTER TABLE billing_v2_provisioning_rules
    MODIFY COLUMN created_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    MODIFY COLUMN updated_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6);

-- statement-break

ALTER TABLE billing_v2_services
    MODIFY COLUMN created_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    MODIFY COLUMN updated_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6);

-- statement-break

ALTER TABLE billing_v2_service_dependencies
    MODIFY COLUMN created_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6);

-- statement-break

ALTER TABLE billing_v2_service_prices
    MODIFY COLUMN created_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6);

-- statement-break

ALTER TABLE billing_v2_service_tiers
    MODIFY COLUMN created_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    MODIFY COLUMN updated_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6);

-- statement-break

ALTER TABLE billing_v2_shadow_price_checks
    MODIFY COLUMN checked_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6);

-- statement-break

ALTER TABLE billing_v2_subscriptions
    MODIFY COLUMN created_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    MODIFY COLUMN updated_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6);

-- statement-break

ALTER TABLE billing_v2_subscription_changes
    MODIFY COLUMN requested_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    MODIFY COLUMN created_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6);

-- statement-break

ALTER TABLE billing_v2_subscription_change_items
    MODIFY COLUMN created_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6);

-- statement-break

ALTER TABLE billing_v2_subscription_documents
    MODIFY COLUMN created_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    MODIFY COLUMN updated_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6);

-- statement-break

ALTER TABLE billing_v2_subscription_items
    MODIFY COLUMN created_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    MODIFY COLUMN updated_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6);

-- statement-break

ALTER TABLE billing_v2_subscription_item_provisioning
    MODIFY COLUMN created_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    MODIFY COLUMN updated_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6);

-- statement-break

ALTER TABLE billing_v2_subscription_price_locks
    MODIFY COLUMN created_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6);

-- statement-break

ALTER TABLE billing_v2_subscription_users
    MODIFY COLUMN created_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    MODIFY COLUMN updated_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6);

-- statement-break

ALTER TABLE download_categories
    MODIFY COLUMN created_at datetime NOT NULL DEFAULT UTC_TIMESTAMP(),
    MODIFY COLUMN updated_at datetime NOT NULL DEFAULT UTC_TIMESTAMP();

-- statement-break

ALTER TABLE download_resources
    MODIFY COLUMN created_at datetime NOT NULL DEFAULT UTC_TIMESTAMP(),
    MODIFY COLUMN updated_at datetime NOT NULL DEFAULT UTC_TIMESTAMP();

-- statement-break

ALTER TABLE download_resource_visibility_rules
    MODIFY COLUMN created_at datetime NOT NULL DEFAULT UTC_TIMESTAMP(),
    MODIFY COLUMN updated_at datetime NOT NULL DEFAULT UTC_TIMESTAMP();

-- statement-break

ALTER TABLE subscription_billing_price_locks
    MODIFY COLUMN created_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    MODIFY COLUMN updated_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6);

-- statement-break

ALTER TABLE subscription_billing_price_lock_review_required
    MODIFY COLUMN detected_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    MODIFY COLUMN updated_at datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6);
