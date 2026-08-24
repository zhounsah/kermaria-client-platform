-- ============================================================================
-- Zachary IT - Billing V2
-- Migration 070 : administration du catalogue V2
--
-- Objectif :
--   rendre le catalogue Billing V2 pilotable depuis le back-office sans
--   nouvelle migration a chaque evolution tarifaire, et donner au controle de
--   recouvrement des fenetres de prix un index deterministe.
--
-- Ce que la migration change :
--   1. `billing_v2_service_prices` gagne une tracabilite d'administration
--      (`created_by_reference`, `supersedes_price_id`) : une revision
--      tarifaire designe explicitement la ligne qu'elle remplace, ce qui rend
--      l'historique lisible sans avoir a le reconstruire par comparaison de
--      dates.
--   2. Un index sert le controle de recouvrement applique AVANT toute
--      ecriture de prix. Le controle est APPLICATIF : MariaDB ne sait pas
--      exprimer declarativement « pas deux fenetres actives qui se
--      chevauchent », et cette migration n'ajoute aucune contrainte SQL de
--      ce type. L'index rend la verification deterministe et bornee pour un
--      meme (service, palier, devise, cadence, declencheur).
--   3. `billing_v2_services` et `billing_v2_service_tiers` gagnent
--      `updated_by_reference`, pour que l'audit d'une modification catalogue
--      ne depende pas uniquement du journal applicatif.
--
-- Ce que la migration NE change PAS :
--   * aucun prix, aucun montant, aucune remise, aucune remise d'engagement ;
--   * aucune ligne de `billing_v2_service_prices` n'est modifiee : la table
--     reste versionnee et immuable. Une evolution tarifaire ferme l'ancienne
--     ligne par `valid_until` et en insere une nouvelle, atomiquement, dans le
--     service d'administration ;
--   * aucun preset technique n'est cree : une souscription sans formule est
--     deja representable, `billing_v2_subscriptions.originating_preset_id` et
--     `commitment_term_id` etant nullables depuis la migration 047 ;
--   * aucun appel provider, aucun drapeau d'activation.
--
-- Additive et rejouable.
--
-- Verification prealable — doit retourner 0 ligne, sinon le catalogue porte
-- deja un recouvrement que le controle applicatif refusera :
--
-- SELECT a.service_id, a.tier_id, a.currency, a.billing_cadence,
--        a.charge_trigger, COUNT(*)
-- FROM billing_v2_service_prices a
-- JOIN billing_v2_service_prices b
--   ON b.id <> a.id
--  AND b.service_id = a.service_id
--  AND b.tier_id <=> a.tier_id
--  AND b.currency = a.currency
--  AND b.billing_cadence = a.billing_cadence
--  AND b.charge_trigger = a.charge_trigger
--  AND b.status = 'active'
--  AND a.valid_from < COALESCE(b.valid_until, '9999-12-31 23:59:59.999999')
--  AND b.valid_from < COALESCE(a.valid_until, '9999-12-31 23:59:59.999999')
-- WHERE a.status = 'active'
-- GROUP BY 1, 2, 3, 4, 5;
-- ============================================================================

SET NAMES utf8mb4;

-- statement-break

-- Sert le controle applicatif de recouvrement. MariaDB ne sait pas exprimer
-- declarativement « pas deux fenetres actives qui se chevauchent » ; l'index
-- rend la verification prealable deterministe et bornee.
CREATE INDEX IF NOT EXISTS idx_billing_v2_service_prices_overlap
    ON billing_v2_service_prices
       (service_id, tier_id, currency, billing_cadence, charge_trigger,
        status, valid_from, valid_until);

-- statement-break

-- Tracabilite d'une revision tarifaire. `supersedes_price_id` designe la
-- ligne fermee par celle-ci : l'historique se lit alors par chainage explicite
-- et non par heuristique sur les dates.
ALTER TABLE billing_v2_service_prices
    ADD COLUMN IF NOT EXISTS created_by_reference VARCHAR(255) NULL
        AFTER status,
    ADD COLUMN IF NOT EXISTS supersedes_price_id CHAR(36) NULL
        AFTER created_by_reference;

-- statement-break

CREATE INDEX IF NOT EXISTS idx_billing_v2_service_prices_supersedes
    ON billing_v2_service_prices (supersedes_price_id);

-- statement-break

ALTER TABLE billing_v2_services
    ADD COLUMN IF NOT EXISTS updated_by_reference VARCHAR(255) NULL
        AFTER display_order;

-- statement-break

ALTER TABLE billing_v2_service_tiers
    ADD COLUMN IF NOT EXISTS updated_by_reference VARCHAR(255) NULL
        AFTER display_order;

-- statement-break

-- Controle de coherence, lisible en dry-run : le catalogue ne doit porter
-- aucun recouvrement de fenetre tarifaire active.
SELECT
    COUNT(*) AS overlapping_active_price_windows
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
WHERE a.status = 'active';
