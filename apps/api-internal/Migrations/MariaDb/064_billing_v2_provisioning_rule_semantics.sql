-- ============================================================================
-- Zachary IT - Billing V2
-- Migration 064 : semantique reelle des regles de provisioning
--
-- Migration de DONNEES uniquement. Aucun CREATE, ALTER ni DROP : la table
-- `billing_v2_provisioning_rules` reste telle que la migration 047 l'a creee.
--
-- Pourquoi :
--   Les 11 regles de stockage seedees en 048 decrivaient un chemin technique
--   qui n'existe pas. Elles nommaient Nextcloud comme pilote du quota alors
--   que la chaine reelle est :
--
--       Billing V2 (tier)
--         -> quota individuel dans la fiche utilisateur KoXo
--         -> KoXo applique la limite sur le serveur de fichiers
--         -> le service de partage expose ensuite ce stockage
--
--   Nextcloud est en bout de chaine, pas a la source : une regle nommee
--   `nextcloud_user_quota` ne designait donc aucune ressource pilotable.
--
--   Par ailleurs 8 des 12 briques du catalogue n'avaient aucune regle. Le
--   planificateur refuse toute ligne sans regle explicite : un abonnement
--   contenant simplement le socle ne pouvait donc jamais etre provisionne.
--
-- Ce que la migration change :
--   1. Les 11 regles de stockage passent au vocabulaire KoXo, en conservant
--      leur identite (service + tier) : ce sont les memes lignes, corrigees.
--   2. 17 regles absentes sont ajoutees.
--   3. VPN et RDS ne sont pas touches.
--
-- Etat cible : 34 regles actives.
--
--   BASE-SERVICE          1     VPN-ACCESS           5
--   STORAGE-PERSONAL      6     RDS-ACCESS           1
--   STORAGE-SHARED        5     USER-ADDITIONAL      1
--   BACKUP-PERSONAL       6     SUPPORT-STANDARD     1
--   BACKUP-SHARED         5     SUPPORT-PLUS         1
--                               INIT-SERVICE         1
--                               MONITORING-INTERNAL  1
--
-- Convergente : les deux UPDATE de stockage ne se bornent pas au seul
-- `target_type`. Une ligne dont la cible est deja corrigee mais dont un autre
-- champ est reste ancien — execution interrompue, correction manuelle
-- partielle, restauration mixte — serait autrement consideree comme saine et
-- laissee dans un etat impossible. Le WHERE compare donc l'etat cible complet,
-- champ par champ, avec l'operateur NULL-safe `<=>` : `target_reference = NULL`
-- rendrait un `=` ordinaire indecidable, et la ligne divergente ne serait pas
-- reparee. Relancer la migration converge vers l'etat cible quel que soit le
-- point d'interruption, et ne touche plus rien une fois converge.
--
-- `status` est deliberement hors du perimetre gere : il n'appartient pas a la
-- semantique de la regle mais a la decision d'exploitation de l'activer. Une
-- regle desactivee volontairement voit son vocabulaire corrige, jamais son
-- activation retablie.
--
-- Bornee : les deux UPDATE joignent `billing_v2_service_tiers` et ne visent que
-- les paliers historiques seedes en 048, puis exigent que le couple
-- (service, tier) ne porte qu'une seule regle. La table n'a aucune unicite sur
-- ce couple — `idx_billing_v2_provisioning_rules_lookup` est un index simple —
-- donc une deuxieme regle tieree pourra un jour y coexister legitimement. Une
-- relance tardive doit alors s'abstenir, pas ecraser. Sur un couple ambigu la
-- migration ne fait rien et le controle de coherence en pied de fichier le
-- rend immediatement visible au dry-run.
--
-- Requiert MariaDB >= 10.3.2 : la garde de cardinalite est une sous-requete sur
-- la table mise a jour. Les versions anterieures rejetaient ce motif
-- (ER_UPDATE_TABLE_USED). La cible de production est en 12.x.
--
-- Chaque INSERT reste garde par un NOT EXISTS sur l'identite (service, tier,
-- cible), comme le seed 048.
--
-- Dormante : aucune de ces regles n'est executable. Le provider de stockage
-- KoXo est dormant, `BILLING_V2_PROVISIONING_ENABLED` reste a false et
-- `BILLING_V2_FIRST_REAL_SUBSCRIPTION_APPROVED` reste a false. Cette migration
-- corrige ce que le catalogue DECRIT, elle n'active rien.
--
-- Verification prealable :
--
-- SELECT rule_type, target_type, COUNT(*)
-- FROM billing_v2_provisioning_rules
-- WHERE status = 'active'
-- GROUP BY rule_type, target_type;
--   -> doit montrer 11 lignes `nextcloud_quota` avant execution, 0 apres.
-- ============================================================================


-- ============================================================================
-- 1. STOCKAGE PERSONNEL : socle technique de l'environnement utilisateur
--
-- Ce n'est pas une ressource parmi d'autres. C'est ce provisioning qui cree et
-- maintient le compte annuaire de l'utilisateur et son dossier personnel. Les
-- acces VPN et RDS se situent en aval et supposent cette identite resolue.
-- ============================================================================

-- statement-break

UPDATE billing_v2_provisioning_rules rule
INNER JOIN billing_v2_services service
    ON service.id = rule.service_id
INNER JOIN billing_v2_service_tiers tier
    ON tier.id = rule.tier_id
   AND tier.service_id = service.id
SET rule.rule_type       = 'infrastructure_action',
    rule.target_type     = 'koxo_user_storage',
    rule.target_reference = 'KOXO-USER-STORAGE',
    rule.value_source    = 'tier_numeric_value',
    rule.static_value    = NULL,
    rule.enable_action   = 'reconcile_storage_quota',
    rule.disable_action  = NULL,
    rule.updated_at      = UTC_TIMESTAMP(6)
WHERE service.code = 'STORAGE-PERSONAL'
  -- Bornage aux tiers historiques seedes en 048. La table ne porte aucune
  -- unicite sur (service_id, tier_id) : seul `idx_..._lookup` existe, et c'est
  -- un index non unique. Rien n'empeche donc une deuxieme regle tieree d'etre
  -- attachee un jour a ce service pour decrire autre chose, et une relance
  -- tardive de 064 la reecrirait en regle de stockage.
  AND tier.code IN ('16', '32', '64', '128', '256', '512')
  -- Garde de cardinalite : on ne reecrit que si le couple porte exactement une
  -- regle, donc si l'intention est sans ambiguite. A plusieurs regles, la
  -- migration s'abstient plutot que de choisir : le cas est signale par le
  -- controle de coherence en pied de fichier et se tranche a la main.
  AND (
      SELECT COUNT(*)
      FROM billing_v2_provisioning_rules other_rule
      WHERE other_rule.service_id = rule.service_id
        AND other_rule.tier_id = rule.tier_id
  ) = 1
  AND NOT (
          rule.rule_type        <=> 'infrastructure_action'
      AND rule.target_type      <=> 'koxo_user_storage'
      AND rule.target_reference <=> 'KOXO-USER-STORAGE'
      AND rule.value_source     <=> 'tier_numeric_value'
      AND rule.static_value     <=> NULL
      AND rule.enable_action    <=> 'reconcile_storage_quota'
      AND rule.disable_action   <=> NULL
  );


-- ============================================================================
-- 2. STOCKAGE PARTAGE : groupe secondaire du client
--
-- Ce n'est pas un espace partage applicatif : c'est le stockage du groupe
-- secondaire CLI-XXXXXX sous le groupe primaire CLIENTS. D'ou le nom de cible,
-- qui doit rester fidele a l'objet reellement pilote.
-- ============================================================================

-- statement-break

UPDATE billing_v2_provisioning_rules rule
INNER JOIN billing_v2_services service
    ON service.id = rule.service_id
INNER JOIN billing_v2_service_tiers tier
    ON tier.id = rule.tier_id
   AND tier.service_id = service.id
SET rule.rule_type       = 'infrastructure_action',
    rule.target_type     = 'koxo_secondary_group_storage',
    rule.target_reference = 'KOXO-SECONDARY-GROUP-STORAGE',
    rule.value_source    = 'tier_numeric_value',
    rule.static_value    = NULL,
    rule.enable_action   = 'reconcile_storage_quota',
    rule.disable_action  = NULL,
    rule.updated_at      = UTC_TIMESTAMP(6)
WHERE service.code = 'STORAGE-SHARED'
  -- Le stockage partage n'a pas de palier 16 Go : la liste n'est pas la meme
  -- que celle du stockage personnel et ne doit pas etre alignee par commodite.
  AND tier.code IN ('32', '64', '128', '256', '512')
  AND (
      SELECT COUNT(*)
      FROM billing_v2_provisioning_rules other_rule
      WHERE other_rule.service_id = rule.service_id
        AND other_rule.tier_id = rule.tier_id
  ) = 1
  AND NOT (
          rule.rule_type        <=> 'infrastructure_action'
      AND rule.target_type      <=> 'koxo_secondary_group_storage'
      AND rule.target_reference <=> 'KOXO-SECONDARY-GROUP-STORAGE'
      AND rule.value_source     <=> 'tier_numeric_value'
      AND rule.static_value     <=> NULL
      AND rule.enable_action    <=> 'reconcile_storage_quota'
      AND rule.disable_action   <=> NULL
  );


-- ============================================================================
-- 3. SOCLE DE SERVICE
-- ============================================================================

-- statement-break

INSERT INTO billing_v2_provisioning_rules
    (id, service_id, tier_id, rule_type, target_type, target_reference,
     value_source, static_value, enable_action, disable_action, status,
     display_order)
SELECT UUID(), s.id, NULL, 'platform_entitlement', 'platform',
       'ZACHARY-IT-BASE', 'none', NULL, 'acknowledge', NULL, 'active', 5
FROM billing_v2_services s
WHERE s.code = 'BASE-SERVICE'
  AND NOT EXISTS (
      SELECT 1
      FROM billing_v2_provisioning_rules rule
      WHERE rule.service_id = s.id
        AND rule.tier_id IS NULL
        AND rule.target_type = 'platform'
  );


-- ============================================================================
-- 4. SAUVEGARDE : couverture heritee, aucun objet par abonnement
--
-- Le volume de donnees est sauvegarde globalement. Un nouveau dossier
-- personnel ou de groupe secondaire place dans ce volume est donc couvert par
-- la politique existante, sans job, repository ni protection group dedie.
-- Les tiers de sauvegarde restent porteurs de la capacite couverte, ce qui
-- reste utile a la facturation et au controle de coherence avec le stockage.
-- ============================================================================

-- statement-break

INSERT INTO billing_v2_provisioning_rules
    (id, service_id, tier_id, rule_type, target_type, target_reference,
     value_source, static_value, enable_action, disable_action, status,
     display_order)
SELECT UUID(), s.id, t.id, 'inherited_coverage', 'backup_policy',
       'VEEAM-KOXODATA', 'tier_numeric_value', NULL, 'inherit_coverage', NULL,
       'active', 50
FROM billing_v2_services s
JOIN billing_v2_service_tiers t ON t.service_id = s.id
WHERE s.code = 'BACKUP-PERSONAL'
  AND NOT EXISTS (
      SELECT 1
      FROM billing_v2_provisioning_rules rule
      WHERE rule.service_id = s.id
        AND rule.tier_id = t.id
        AND rule.target_type = 'backup_policy'
  );

-- statement-break

INSERT INTO billing_v2_provisioning_rules
    (id, service_id, tier_id, rule_type, target_type, target_reference,
     value_source, static_value, enable_action, disable_action, status,
     display_order)
SELECT UUID(), s.id, t.id, 'inherited_coverage', 'backup_policy',
       'VEEAM-KOXODATA', 'tier_numeric_value', NULL, 'inherit_coverage', NULL,
       'active', 60
FROM billing_v2_services s
JOIN billing_v2_service_tiers t ON t.service_id = s.id
WHERE s.code = 'BACKUP-SHARED'
  AND NOT EXISTS (
      SELECT 1
      FROM billing_v2_provisioning_rules rule
      WHERE rule.service_id = s.id
        AND rule.tier_id = t.id
        AND rule.target_type = 'backup_policy'
  );


-- ============================================================================
-- 5. UTILISATEUR SUPPLEMENTAIRE : droit commercial, pas un mecanisme technique
--
-- Cette regle autorise un `billing_v2_subscription_user` de plus. Elle ne cree
-- rien. Le bootstrap technique d'un utilisateur, principal ou supplementaire,
-- passe par le provisioning attache a son STORAGE-PERSONAL : il ne doit
-- exister qu'un seul proprietaire de la creation d'identite.
-- ============================================================================

-- statement-break

INSERT INTO billing_v2_provisioning_rules
    (id, service_id, tier_id, rule_type, target_type, target_reference,
     value_source, static_value, enable_action, disable_action, status,
     display_order)
SELECT UUID(), s.id, NULL, 'contractual_entitlement', 'user_slot',
       'ADDITIONAL', 'none', NULL, 'acknowledge', NULL, 'active', 80
FROM billing_v2_services s
WHERE s.code = 'USER-ADDITIONAL'
  AND NOT EXISTS (
      SELECT 1
      FROM billing_v2_provisioning_rules rule
      WHERE rule.service_id = s.id
        AND rule.tier_id IS NULL
        AND rule.target_type = 'user_slot'
  );


-- ============================================================================
-- 6. SUPPORT ET MISE EN SERVICE : droits contractuels
-- ============================================================================

-- statement-break

INSERT INTO billing_v2_provisioning_rules
    (id, service_id, tier_id, rule_type, target_type, target_reference,
     value_source, static_value, enable_action, disable_action, status,
     display_order)
SELECT UUID(), s.id, NULL, 'contractual_entitlement', 'support_level',
       'STANDARD', 'none', NULL, 'acknowledge', NULL, 'active', 90
FROM billing_v2_services s
WHERE s.code = 'SUPPORT-STANDARD'
  AND NOT EXISTS (
      SELECT 1
      FROM billing_v2_provisioning_rules rule
      WHERE rule.service_id = s.id
        AND rule.tier_id IS NULL
        AND rule.target_type = 'support_level'
  );

-- statement-break

INSERT INTO billing_v2_provisioning_rules
    (id, service_id, tier_id, rule_type, target_type, target_reference,
     value_source, static_value, enable_action, disable_action, status,
     display_order)
SELECT UUID(), s.id, NULL, 'contractual_entitlement', 'support_level',
       'PLUS', 'none', NULL, 'acknowledge', NULL, 'active', 100
FROM billing_v2_services s
WHERE s.code = 'SUPPORT-PLUS'
  AND NOT EXISTS (
      SELECT 1
      FROM billing_v2_provisioning_rules rule
      WHERE rule.service_id = s.id
        AND rule.tier_id IS NULL
        AND rule.target_type = 'support_level'
  );

-- statement-break

INSERT INTO billing_v2_provisioning_rules
    (id, service_id, tier_id, rule_type, target_type, target_reference,
     value_source, static_value, enable_action, disable_action, status,
     display_order)
SELECT UUID(), s.id, NULL, 'service_delivery', 'onboarding',
       'ZACHARY-IT-INIT', 'none', NULL, 'acknowledge', NULL, 'active', 105
FROM billing_v2_services s
WHERE s.code = 'INIT-SERVICE'
  AND NOT EXISTS (
      SELECT 1
      FROM billing_v2_provisioning_rules rule
      WHERE rule.service_id = s.id
        AND rule.tier_id IS NULL
        AND rule.target_type = 'onboarding'
  );


-- ============================================================================
-- 7. SUPERVISION : couverture de plateforme
--
-- La supervision est globale. Il n'existe aucun objet de supervision cree par
-- abonnement, d'ou `inherit_coverage` et non `acknowledge`.
-- ============================================================================

-- statement-break

INSERT INTO billing_v2_provisioning_rules
    (id, service_id, tier_id, rule_type, target_type, target_reference,
     value_source, static_value, enable_action, disable_action, status,
     display_order)
SELECT UUID(), s.id, NULL, 'platform_entitlement', 'monitoring',
       'ZACHARY-IT-INFRA', 'none', NULL, 'inherit_coverage', NULL, 'active', 110
FROM billing_v2_services s
WHERE s.code = 'MONITORING-INTERNAL'
  AND NOT EXISTS (
      SELECT 1
      FROM billing_v2_provisioning_rules rule
      WHERE rule.service_id = s.id
        AND rule.tier_id IS NULL
        AND rule.target_type = 'monitoring'
  );


-- ============================================================================
-- 8. DEPENDANCE : VPN et RDS supposent un stockage personnel
--
-- `scope_relation` et `tier_relation` sont des VARCHAR libres, sans contrainte
-- CHECK ni enumeration : la valeur `same_subscription_user` est donc
-- representable, et `tier_relation = 'any'` (defaut de la table) exprime
-- exactement l'independance des tiers demandee.
--
-- LIMITE ASSUMEE : aucun code ne lit aujourd'hui ces deux colonnes. Ces lignes
-- sont donc de la documentation structuree, pas un garde-fou executable. Le
-- garde-fou reel est dans le planificateur, qui refuse un acces VPN ou RDS
-- accorde a un utilisateur sans stockage personnel.
-- ============================================================================

-- statement-break

INSERT IGNORE INTO billing_v2_service_dependencies
    (id, service_id, required_service_id, scope_relation, tier_relation, status)
SELECT UUID(), access.id, storage.id, 'same_subscription_user', 'any', 'active'
FROM billing_v2_services access
CROSS JOIN billing_v2_services storage
WHERE access.code = 'VPN-ACCESS'
  AND storage.code = 'STORAGE-PERSONAL';

-- statement-break

INSERT IGNORE INTO billing_v2_service_dependencies
    (id, service_id, required_service_id, scope_relation, tier_relation, status)
SELECT UUID(), access.id, storage.id, 'same_subscription_user', 'any', 'active'
FROM billing_v2_services access
CROSS JOIN billing_v2_services storage
WHERE access.code = 'RDS-ACCESS'
  AND storage.code = 'STORAGE-PERSONAL';


-- ============================================================================
-- Verification post-execution attendue :
--
-- SELECT COUNT(*) FROM billing_v2_provisioning_rules WHERE status = 'active';
--   -> 34
--
-- SELECT target_type, COUNT(*) FROM billing_v2_provisioning_rules
-- WHERE status = 'active' GROUP BY target_type ORDER BY target_type;
--   -> ad_group                     6   (5 VPN + 1 RDS, inchangees)
--      backup_policy                11
--      koxo_secondary_group_storage 5
--      koxo_user_storage            6
--      monitoring                   1
--      onboarding                   1
--      platform                     1
--      support_level                2
--      user_slot                    1
--
-- SELECT COUNT(*) FROM billing_v2_provisioning_rules
-- WHERE target_type LIKE 'nextcloud%';
--   -> 0
--
-- Convergence : relancer la migration entiere doit rapporter 0 ligne affectee
-- sur les deux UPDATE et 0 ligne inseree. Toute autre valeur signale une ligne
-- que la migration reecrit en boucle, donc un etat cible mal decrit.
--
-- Coherence des couples de stockage : les UPDATE s'abstiennent des couples
-- ambigus, donc leur silence ne doit pas passer pour un succes. Cette requete
-- rend visible tout couple que la migration a volontairement laisse de cote.
--
-- SELECT service.code AS service_code, tier.code AS tier_code,
--        COUNT(rule.id) AS rule_count
-- FROM billing_v2_services service
-- JOIN billing_v2_service_tiers tier
--      ON tier.service_id = service.id
-- LEFT JOIN billing_v2_provisioning_rules rule
--      ON rule.service_id = service.id
--     AND rule.tier_id = tier.id
-- WHERE (service.code = 'STORAGE-PERSONAL'
--        AND tier.code IN ('16', '32', '64', '128', '256', '512'))
--    OR (service.code = 'STORAGE-SHARED'
--        AND tier.code IN ('32', '64', '128', '256', '512'))
-- GROUP BY service.code, tier.code
-- HAVING COUNT(rule.id) <> 1;
--   -> aucune ligne.
--      rule_count = 0  : palier sans regle, le planificateur refusera la ligne.
--      rule_count > 1  : couple ambigu, non reecrit ; a trancher a la main
--                        avant de conclure que la migration est appliquee.
-- ============================================================================
