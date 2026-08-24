-- ============================================================================
-- Preflight LECTURE SEULE des migrations 070 et 071
--
-- A executer sur la base cible AVANT toute application, avec un compte en
-- lecture. Aucune de ces requetes n'ecrit, ne verrouille durablement ni ne
-- supprime quoi que ce soit.
--
-- Pourquoi ce fichier existe : la migration 071 est destructive, irreversible
-- et sans rollback SQL. Ses refus internes (`SIGNAL SQLSTATE '45000'`) sont un
-- filet de securite, pas un plan. Un refus survenu en pleine migration laisse
-- la base dans un etat partiellement modifie — MariaDB commite implicitement a
-- chaque DDL. Le controle doit donc avoir lieu ici, a froid.
--
-- Ordre de lecture des resultats :
--   A. volumetrie de ce qui va disparaitre  -> decision d'archivage
--   B. regles de visibilite a traduire      -> requetes 3 a 6, BLOQUANTES
--   C. fenetres tarifaires (070)            -> requete 7, BLOQUANTE
--   D. contraintes reellement presentes     -> requete 8, verification
--   E. colonnes attendues                   -> requete 9, verification
--
-- Sauvegarde prealable obligatoire : `npm run backup:mariadb`.
-- ============================================================================


-- ----------------------------------------------------------------------------
-- 1. Volumetrie des tables qui vont disparaitre
--
-- Une valeur elevee sur `subscriptions` ou `commercial_documents` liees n'est
-- pas un blocage en soi, mais impose de conserver le dump : ces lignes ne sont
-- reconstituables par rien.
-- ----------------------------------------------------------------------------

SELECT 'commercial_offers' AS table_name, COUNT(*) AS rows_to_drop FROM commercial_offers
UNION ALL SELECT 'subscriptions', COUNT(*) FROM subscriptions
UNION ALL SELECT 'cart_items', COUNT(*) FROM cart_items
UNION ALL SELECT 'recurring_checkout_items', COUNT(*) FROM recurring_checkout_items
UNION ALL SELECT 'paypal_webhook_events', COUNT(*) FROM paypal_webhook_events
UNION ALL SELECT 'stripe_webhook_events', COUNT(*) FROM stripe_webhook_events
UNION ALL SELECT 'commercial_document_line_subscriptions', COUNT(*) FROM commercial_document_line_subscriptions
UNION ALL SELECT 'subscription_billing_price_locks', COUNT(*) FROM subscription_billing_price_locks
UNION ALL SELECT 'subscription_billing_price_lock_review_required', COUNT(*) FROM subscription_billing_price_lock_review_required
UNION ALL SELECT 'billing_v2_legacy_offer_mappings', COUNT(*) FROM billing_v2_legacy_offer_mappings
UNION ALL SELECT 'billing_v2_legacy_service_mappings', COUNT(*) FROM billing_v2_legacy_service_mappings
UNION ALL SELECT 'billing_v2_shadow_price_checks', COUNT(*) FROM billing_v2_shadow_price_checks;


-- ----------------------------------------------------------------------------
-- 2. Documents commerciaux encore rattaches a un abonnement legacy
--
-- 071 supprime `commercial_documents.subscription_id`. Le document survit ;
-- c'est le lien qui disparait. Compter avant permet de savoir combien de
-- pieces perdent leur rattachement.
-- ----------------------------------------------------------------------------

SELECT COUNT(*) AS documents_linked_to_legacy_subscription
FROM commercial_documents
WHERE subscription_id IS NOT NULL;


-- ----------------------------------------------------------------------------
-- 3. Regles de visibilite a traduire — inventaire
--
-- C'est le point le plus sensible de 071. Une regle qui n'est pas traduite
-- rendrait une ressource invisible pour ses ayants droit, sans erreur visible :
-- `DownloadService` est fail-closed.
-- ----------------------------------------------------------------------------

SELECT target_type, target_value, COUNT(*) AS rules
FROM download_resource_visibility_rules
WHERE target_type IN ('offer_external_reference', 'public_pack_code')
GROUP BY target_type, target_value
ORDER BY target_type, target_value;


-- ----------------------------------------------------------------------------
-- 4. BLOQUANT — references legacy SANS equivalent Billing V2
--
-- Toute ligne renvoyee ici fera echouer 071 sur le premier `SIGNAL`. Il faut
-- arbitrer chaque cas AVANT : soit supprimer la regle, soit la remplacer par
-- une cible V2 explicite.
--
-- Attention : le catalogue legacy seede par `009_catalog_articles.sql` contient
-- des references qui n'ont deliberement AUCUN service Billing V2 —
-- `AUDIT-SECU-BASE`, `CONFIG-POSTE`, `CONFIG-VPN`, `INTERV-PONCT`,
-- `RESTORE-SAVE`, `MIG-DATA`, `CONFIG-DEVICE-ADD`, `NEXTCLOUD`. Ce sont des
-- prestations ponctuelles, pas des services recurrents. Si une ressource de
-- telechargement les cible, la decision est commerciale, pas technique.
--
-- Sont egalement exclus de la traduction automatique :
--   * `mapping_kind = 'storage_increment'`  (`STOCK-SUP-32`) : traduire
--     elargirait la visibilite de « qui a achete l'increment » a « qui a du
--     stockage personnel » ;
--   * `mapping_kind = 'legacy_one_time_entitlement'` (`DOC-TECH`) : aucun
--     service V2 correspondant.
-- ----------------------------------------------------------------------------

SELECT rule.target_value,
       COUNT(*)                        AS rules,
       MAX(mapping.mapping_kind)       AS mapping_kind,
       MAX(mapping.v2_service_code)    AS mapped_v2_code
FROM download_resource_visibility_rules AS rule
LEFT JOIN billing_v2_legacy_service_mappings AS mapping
    ON mapping.legacy_service_reference = rule.target_value
   AND mapping.mapping_kind NOT IN ('storage_increment', 'legacy_one_time_entitlement')
LEFT JOIN billing_v2_services AS mapped
    ON mapped.code = mapping.v2_service_code
LEFT JOIN billing_v2_services AS native
    ON native.code = rule.target_value
WHERE rule.target_type = 'offer_external_reference'
  AND mapped.code IS NULL
  AND native.code IS NULL
GROUP BY rule.target_value
ORDER BY rules DESC;


-- ----------------------------------------------------------------------------
-- 5. BLOQUANT — codes de packs publics sans formule Billing V2
-- ----------------------------------------------------------------------------

SELECT rule.target_value, COUNT(*) AS rules
FROM download_resource_visibility_rules AS rule
LEFT JOIN billing_v2_offer_presets AS preset
    ON preset.code = rule.target_value
WHERE rule.target_type = 'public_pack_code'
  AND preset.code IS NULL
GROUP BY rule.target_value
ORDER BY rules DESC;


-- ----------------------------------------------------------------------------
-- 6. BLOQUANT — cibles V2 deja orphelines
--
-- Independamment de la traduction : une regle saisie a la main pointant un
-- code inexistant est deja muette aujourd'hui. 071 refusera de s'appliquer
-- tant qu'elle subsiste, et c'est voulu.
-- ----------------------------------------------------------------------------

SELECT 'service_code' AS target_type, rule.target_value, COUNT(*) AS rules
FROM download_resource_visibility_rules AS rule
LEFT JOIN billing_v2_services AS service ON service.code = rule.target_value
WHERE rule.target_type = 'service_code' AND service.code IS NULL
GROUP BY rule.target_value
UNION ALL
SELECT 'preset_code', rule.target_value, COUNT(*)
FROM download_resource_visibility_rules AS rule
LEFT JOIN billing_v2_offer_presets AS preset ON preset.code = rule.target_value
WHERE rule.target_type = 'preset_code' AND preset.code IS NULL
GROUP BY rule.target_value;


-- ----------------------------------------------------------------------------
-- 7. BLOQUANT pour 070 — fenetres tarifaires actives qui se chevauchent
--
-- 070 pose un index, pas une contrainte : MariaDB ne sait pas exprimer
-- declarativement l'absence de recouvrement. Un recouvrement preexistant ne
-- fera donc pas echouer 070, mais rendra chaque revision tarifaire ulterieure
-- refusee par le service d'administration.
-- ----------------------------------------------------------------------------

SELECT a.service_id, a.tier_id, a.currency, a.billing_cadence,
       a.charge_trigger, COUNT(*) AS overlapping_pairs
FROM billing_v2_service_prices a
JOIN billing_v2_service_prices b
  ON b.id <> a.id
 AND b.service_id = a.service_id
 AND b.tier_id <=> a.tier_id
 AND b.currency = a.currency
 AND b.billing_cadence = a.billing_cadence
 AND b.charge_trigger = a.charge_trigger
 AND b.status = 'active'
 AND a.valid_from < COALESCE(b.valid_until, '9999-12-31 23:59:59.999999')
 AND b.valid_from < COALESCE(a.valid_until, '9999-12-31 23:59:59.999999')
WHERE a.status = 'active'
GROUP BY 1, 2, 3, 4, 5;


-- ----------------------------------------------------------------------------
-- 8. Contraintes de cle etrangere reellement presentes vers les tables legacy
--
-- Attendu : exactement les trois contraintes detenues par des tables qui
-- survivent — `fk_commercial_document_lines_offer`,
-- `fk_commercial_documents_subscription`, `fk_ad_actions_subscription` — plus
-- celles detenues par des tables elles-memes supprimees. Une contrainte
-- supplementaire signifie que 071 est incomplete : le `DROP TABLE`
-- correspondant echouerait apres des commits implicites deja passes.
-- ----------------------------------------------------------------------------

SELECT constraint_name, table_name, referenced_table_name
FROM information_schema.referential_constraints
WHERE constraint_schema = DATABASE()
  AND referenced_table_name IN (
        'commercial_offers',
        'subscriptions',
        'cart_items',
        'recurring_checkout_items',
        'paypal_webhook_events',
        'stripe_webhook_events',
        'subscription_billing_price_locks',
        'subscription_billing_price_lock_review_required',
        'commercial_document_line_subscriptions',
        'billing_v2_legacy_offer_mappings',
        'billing_v2_legacy_service_mappings',
        'billing_v2_shadow_price_checks'
      )
ORDER BY referenced_table_name, table_name;


-- ----------------------------------------------------------------------------
-- 9. Colonnes que 071 va supprimer — presence reelle
--
-- `DROP COLUMN IF EXISTS` tolere l'absence ; cette requete sert a verifier
-- qu'on opere bien sur le schema attendu et pas sur une base plus ancienne.
-- ----------------------------------------------------------------------------

SELECT table_name, column_name
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND (
        (table_name = 'commercial_document_lines' AND column_name = 'offer_id')
     OR (table_name = 'commercial_documents' AND column_name = 'subscription_id')
     OR (table_name = 'ad_actions' AND column_name = 'subscription_id')
     OR (table_name = 'billing_v2_subscription_price_locks' AND column_name = 'source_legacy_offer_id')
     OR (table_name = 'billing_v2_authoritative_checkout_requests' AND column_name = 'legacy_offer_id')
     OR (table_name = 'signup_pending' AND column_name = 'pack_selection_snapshot_json')
     OR (table_name = 'billing_v2_provisioning_client_readiness'
         AND column_name IN ('last_shadow_status', 'last_shadow_matches_legacy'))
      )
ORDER BY table_name, column_name;


-- ----------------------------------------------------------------------------
-- 10. Etat des migrations deja appliquees
-- ----------------------------------------------------------------------------

SELECT migration_id, applied_at
FROM schema_migrations
ORDER BY migration_id DESC
LIMIT 10;
