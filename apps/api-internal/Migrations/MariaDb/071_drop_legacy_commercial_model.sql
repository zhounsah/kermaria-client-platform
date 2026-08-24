-- ============================================================================
-- 071 — Suppression definitive du modele commercial legacy
--
-- Cette migration est DESTRUCTIVE et IRREVERSIBLE. Elle n'a pas de rollback
-- SQL : les tables supprimees portent des donnees qu'aucune autre table ne
-- reconstitue. Une sauvegarde prealable (`npm run backup:mariadb`) est
-- bloquante.
--
-- A EXECUTER D'ABORD, en lecture seule, sur la base cible :
-- `docs/billing-v2/PREFLIGHT-070-071.sql`. Les refus internes de cette
-- migration (`SIGNAL SQLSTATE '45000'`) sont un filet, pas un plan : ils
-- s'appliquent avant tout DDL, mais un arbitrage decouvert a ce moment-la
-- oblige a tout recommencer. Le preflight le revele a froid.
--
-- Contexte : Billing V2/V2.1 est desormais la seule autorite commerciale. Le
-- code applicatif ne lit ni n'ecrit plus aucune des structures supprimees ici
-- (verifie par recherche globale sur `commercial_offers`, `subscriptions`,
-- `cart_items`, `recurring_checkout_items`, `legacy_offer_id`,
-- `commercial_offer_id`). Les laisser en place laisserait subsister un second
-- catalogue silencieux, ce que la cible interdit explicitement.
--
-- Ce qui est CONSERVE, et pourquoi :
--   * `commercial_documents` / `commercial_document_lines` : devis et factures.
--     Ce sont des pieces datees, pas du catalogue. Elles deviennent
--     auto-portantes : la ligne garde son libelle, sa quantite, son prix
--     unitaire et son taux, et ne reference plus d'offre.
--   * `billing_v2_subscription_price_locks` : verrou tarifaire V2 toujours lu
--     par la projection. Seule sa colonne d'origine legacy disparait.
--   * `ad_actions` : journal de provisioning generique, sans couplage
--     commercial. Seule sa contrainte vers `subscriptions` disparait.
--   * `download_resource_visibility_rules` : les regles de visibilite. Leurs
--     cibles sont traduites vers les identites V2 en section 0 — traduites,
--     pas renommees : la reference legacy et le code V2 different.
--
-- Ordre impose : traduction des donnees -> contraintes -> index -> colonnes ->
-- tables de liaison -> tables. La traduction passe en premier parce qu'elle est
-- la seule etape encore annulable ; une table referencee par une FK ne peut pas
-- etre supprimee avant elle.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 0. Regles de visibilite des telechargements — traduction reelle
--
-- Cette section vient AVANT tout DDL, et ce n'est pas cosmetique : en MariaDB
-- une instruction DDL provoque un commit implicite. Passe la premiere
-- ALTER TABLE, plus rien n'est annulable. Tout ce qui peut faire refuser la
-- migration doit donc echouer ici, tant que la transaction tient encore.
--
-- Les cibles de visibilite nommaient des concepts legacy :
--   public_pack_code         -> preset_code   (billing_v2_offer_presets.code)
--   offer_external_reference -> service_code  (billing_v2_services.code)
--
-- La reference legacy n'est PAS egale au code V2 : `STOCK-PERSO-32` devient
-- `STORAGE-PERSONAL`, `SUPPORT-LV1` devient `SUPPORT-STANDARD`,
-- `SUPERV-SERVICE` devient `MONITORING-INTERNAL`, `SAVE-PERSO` devient
-- `BACKUP-PERSONAL`. Se contenter de changer `target_type` laisserait des
-- regles qui ne matchent plus jamais : la ressource deviendrait invisible pour
-- ses ayants droit, silencieusement. La correspondance qui fait autorite est
-- `billing_v2_legacy_service_mappings`, posee par la migration 048 et lue ici
-- avant d'etre supprimee plus bas.
--
-- Deux `mapping_kind` sont volontairement exclus de la traduction automatique :
--   * `storage_increment` : la reference designe un increment de capacite, pas
--     un service. La traduire elargirait la visibilite de « qui a achete
--     l'increment » a « qui a du stockage personnel ».
--   * `legacy_one_time_entitlement` : le droit ponctuel n'a pas de service V2
--     correspondant — `DOC-TECH` n'existe pas dans `billing_v2_services`.
-- Dans ces deux cas la migration refuse plutot que de deviner. La regle doit
-- etre arbitree par un exploitant, puis la migration relancee.
--
-- La ressource n'est jamais rendue plus permissive : le mode `targeted` sans
-- aucune regle est ferme (`DownloadService` renvoie false), donc l'echec par
-- defaut est l'invisibilite, pas la fuite.
-- ----------------------------------------------------------------------------

-- 0.a Traduction par le mapping 048. `INSERT IGNORE` absorbe le cas ou deux
--     references legacy d'une meme ressource pointent le meme service V2 :
--     la cle unique (resource_id, target_type, target_value) dedoublonne.
INSERT IGNORE INTO download_resource_visibility_rules
    (id, resource_id, target_type, target_value, created_at, updated_at)
SELECT
    UUID(),
    rule.resource_id,
    'service_code',
    service.code,
    rule.created_at,
    UTC_TIMESTAMP()
FROM download_resource_visibility_rules AS rule
JOIN billing_v2_legacy_service_mappings AS mapping
    ON mapping.legacy_service_reference = rule.target_value
JOIN billing_v2_services AS service
    ON service.code = mapping.v2_service_code
WHERE rule.target_type = 'offer_external_reference'
  AND mapping.mapping_kind NOT IN (
        'storage_increment',
        'legacy_one_time_entitlement'
      );

-- statement-break

-- 0.b Une valeur deja nativement V2 n'a besoin que du changement de type.
INSERT IGNORE INTO download_resource_visibility_rules
    (id, resource_id, target_type, target_value, created_at, updated_at)
SELECT
    UUID(),
    rule.resource_id,
    'service_code',
    service.code,
    rule.created_at,
    UTC_TIMESTAMP()
FROM download_resource_visibility_rules AS rule
JOIN billing_v2_services AS service
    ON service.code = rule.target_value
WHERE rule.target_type = 'offer_external_reference';

-- statement-break

-- 0.c Retrait des regles effectivement traduites. Une regle non traduisible
--     survit volontairement : elle sera signalee en 0.d.
DELETE rule FROM download_resource_visibility_rules AS rule
LEFT JOIN billing_v2_legacy_service_mappings AS mapping
    ON mapping.legacy_service_reference = rule.target_value
   AND mapping.mapping_kind NOT IN (
        'storage_increment',
        'legacy_one_time_entitlement'
      )
LEFT JOIN billing_v2_services AS mapped
    ON mapped.code = mapping.v2_service_code
LEFT JOIN billing_v2_services AS native
    ON native.code = rule.target_value
WHERE rule.target_type = 'offer_external_reference'
  AND (mapped.code IS NOT NULL OR native.code IS NOT NULL);

-- statement-break

-- 0.d Refus explicite. Une reference legacy sans equivalent V2 valide ne doit
--     jamais etre convertie « au mieux » : elle produirait une regle orpheline
--     qui ne matche plus rien, donc une ressource muette.
BEGIN NOT ATOMIC
    DECLARE remaining INT DEFAULT 0;

    SELECT COUNT(*) INTO remaining
    FROM download_resource_visibility_rules
    WHERE target_type = 'offer_external_reference';

    IF remaining > 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
            '071: des regles download_resource_visibility_rules en '
            'offer_external_reference n''ont aucun equivalent Billing V2. '
            'Arbitrer ces regles puis relancer la migration.';
    END IF;
END;

-- statement-break

-- 0.e Codes de formules. Les presets V2 ont repris les codes de packs publics,
--     mais c'est une propriete a verifier, pas a supposer.
INSERT IGNORE INTO download_resource_visibility_rules
    (id, resource_id, target_type, target_value, created_at, updated_at)
SELECT
    UUID(),
    rule.resource_id,
    'preset_code',
    preset.code,
    rule.created_at,
    UTC_TIMESTAMP()
FROM download_resource_visibility_rules AS rule
JOIN billing_v2_offer_presets AS preset
    ON preset.code = rule.target_value
WHERE rule.target_type = 'public_pack_code';

-- statement-break

DELETE rule FROM download_resource_visibility_rules AS rule
JOIN billing_v2_offer_presets AS preset
    ON preset.code = rule.target_value
WHERE rule.target_type = 'public_pack_code';

-- statement-break

BEGIN NOT ATOMIC
    DECLARE remaining INT DEFAULT 0;

    SELECT COUNT(*) INTO remaining
    FROM download_resource_visibility_rules
    WHERE target_type = 'public_pack_code';

    IF remaining > 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
            '071: des regles download_resource_visibility_rules en '
            'public_pack_code ne designent aucun billing_v2_offer_presets.code. '
            'Arbitrer ces regles puis relancer la migration.';
    END IF;
END;

-- statement-break

-- 0.f Aucune reference orpheline apres traduction. Ce controle porte sur la
--     totalite de la table, pas seulement sur les lignes converties : une
--     regle V2 saisie a la main et pointant un code inexistant est tout aussi
--     muette.
BEGIN NOT ATOMIC
    DECLARE orphan_services INT DEFAULT 0;
    DECLARE orphan_presets INT DEFAULT 0;

    SELECT COUNT(*) INTO orphan_services
    FROM download_resource_visibility_rules AS rule
    LEFT JOIN billing_v2_services AS service
        ON service.code = rule.target_value
    WHERE rule.target_type = 'service_code'
      AND service.code IS NULL;

    SELECT COUNT(*) INTO orphan_presets
    FROM download_resource_visibility_rules AS rule
    LEFT JOIN billing_v2_offer_presets AS preset
        ON preset.code = rule.target_value
    WHERE rule.target_type = 'preset_code'
      AND preset.code IS NULL;

    IF orphan_services > 0 OR orphan_presets > 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
            '071: des regles de visibilite designent un service ou une formule '
            'Billing V2 inexistant. La ressource serait invisible pour ses '
            'ayants droit.';
    END IF;
END;

-- statement-break

-- ----------------------------------------------------------------------------
-- 1. Contraintes de cle etrangere vers les tables legacy
-- ----------------------------------------------------------------------------

ALTER TABLE commercial_document_lines
    DROP FOREIGN KEY IF EXISTS fk_commercial_document_lines_offer;

-- statement-break

ALTER TABLE commercial_documents
    DROP FOREIGN KEY IF EXISTS fk_commercial_documents_subscription;

-- statement-break

ALTER TABLE ad_actions
    DROP FOREIGN KEY IF EXISTS fk_ad_actions_subscription;

-- statement-break

-- ----------------------------------------------------------------------------
-- 2. Colonnes devenues sans objet
--
-- `commercial_document_lines.offer_id` : la ligne n'emprunte plus son libelle
-- ni son prix au catalogue. La supprimer garantit qu'aucune reedition future
-- ne puisse re-deriver un montant depuis un tarif courant.
-- ----------------------------------------------------------------------------

ALTER TABLE commercial_document_lines
    DROP COLUMN IF EXISTS offer_id;

-- statement-break

ALTER TABLE commercial_documents
    DROP KEY IF EXISTS ix_commercial_documents_subscription;

-- statement-break

ALTER TABLE commercial_documents
    DROP COLUMN IF EXISTS subscription_id;

-- statement-break

ALTER TABLE ad_actions
    DROP KEY IF EXISTS ix_ad_actions_subscription;

-- statement-break

ALTER TABLE billing_v2_subscription_price_locks
    DROP COLUMN IF EXISTS source_legacy_offer_id;

-- statement-break

ALTER TABLE billing_v2_authoritative_checkout_requests
    DROP COLUMN IF EXISTS legacy_offer_id;

-- statement-break

ALTER TABLE signup_pending
    DROP COLUMN IF EXISTS pack_selection_snapshot_json;

-- statement-break

-- ----------------------------------------------------------------------------
-- 3. Readiness de provisioning : la revue n'est plus une comparaison
--
-- Les deux colonnes portaient le resultat d'un mode fantome qui comparait V2
-- au systeme legacy. Sans legacy, la comparaison n'a plus de second terme :
-- la porte aurait ete definitivement fermee. `last_review_status` porte
-- desormais le resultat de la revue V2 elle-meme.
-- ----------------------------------------------------------------------------

-- L'index porte les deux colonnes supprimees : le retirer d'abord evite de
-- laisser MariaDB le reconstruire implicitement pendant les DROP COLUMN.
ALTER TABLE billing_v2_provisioning_client_readiness
    DROP KEY IF EXISTS idx_billing_v2_provisioning_readiness_ready;

-- statement-break

ALTER TABLE billing_v2_provisioning_client_readiness
    ADD COLUMN IF NOT EXISTS last_review_status VARCHAR(32) NULL;

-- statement-break

UPDATE billing_v2_provisioning_client_readiness
SET last_review_status = last_shadow_status
WHERE last_review_status IS NULL
  AND last_shadow_status IS NOT NULL;

-- statement-break

ALTER TABLE billing_v2_provisioning_client_readiness
    DROP COLUMN IF EXISTS last_shadow_matches_legacy;

-- statement-break

ALTER TABLE billing_v2_provisioning_client_readiness
    DROP COLUMN IF EXISTS last_shadow_status;

-- statement-break

ALTER TABLE billing_v2_provisioning_client_readiness
    ADD KEY IF NOT EXISTS idx_billing_v2_provisioning_readiness_ready
        (ready_for_v2_provisioning, last_review_status);

-- statement-break

-- ----------------------------------------------------------------------------
-- 4. Tables de liaison legacy
-- ----------------------------------------------------------------------------

DROP TABLE IF EXISTS commercial_document_line_subscriptions;

-- statement-break

DROP TABLE IF EXISTS subscription_billing_price_lock_review_required;

-- statement-break

DROP TABLE IF EXISTS subscription_billing_price_locks;

-- statement-break

DROP TABLE IF EXISTS billing_v2_legacy_offer_mappings;

-- statement-break

DROP TABLE IF EXISTS billing_v2_legacy_service_mappings;

-- statement-break

DROP TABLE IF EXISTS billing_v2_shadow_price_checks;

-- statement-break

-- ----------------------------------------------------------------------------
-- 5. Tables du modele commercial legacy
--
-- L'ordre suit les dependances : les paniers et selections referencent les
-- offres ; les abonnements referencent les offres ; les journaux de webhook
-- referencent les abonnements.
-- ----------------------------------------------------------------------------

DROP TABLE IF EXISTS cart_items;

-- statement-break

DROP TABLE IF EXISTS recurring_checkout_items;

-- statement-break

DROP TABLE IF EXISTS paypal_webhook_events;

-- statement-break

DROP TABLE IF EXISTS stripe_webhook_events;

-- statement-break

DROP TABLE IF EXISTS subscriptions;

-- statement-break

DROP TABLE IF EXISTS commercial_offers;

-- statement-break

-- ----------------------------------------------------------------------------
-- 6. Verification post-migration (lecture seule)
--
-- Doit renvoyer 0. Une valeur non nulle signale qu'une table legacy a survecu,
-- auquel cas la porte de lancement Billing V2 reste volontairement fermee
-- (`BILLING_V2_LEGACY_SCHEMA_PRESENT`).
-- ----------------------------------------------------------------------------

SELECT COUNT(*) AS remaining_legacy_tables
FROM information_schema.tables
WHERE table_schema = DATABASE()
  AND table_name IN (
        'commercial_offers',
        'subscriptions',
        'cart_items',
        'recurring_checkout_items'
      );
