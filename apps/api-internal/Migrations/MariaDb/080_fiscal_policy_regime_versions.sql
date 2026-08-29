-- Numero de version monotone par regime fiscal.
--
-- La concurrence optimiste utilisait `COUNT(*)` des mentions du regime comme
-- version. Un decompte n'est pas un numero de version : `TryDeleteScheduledAsync`
-- supprime reellement une ligne, donc le decompte redescend. Une suppression
-- suivie d'un ajout ramene la valeur a ce qu'elle etait, et un `expectedVersion`
-- devenu obsolete redevient valide — un administrateur ecrase alors une version
-- qu'il n'a jamais vue, sans conflit, sur un texte qui s'imprime sur des
-- factures.
--
-- Ce compteur ne redescend jamais : il est incremente a chaque ajout ET a chaque
-- suppression. Il porte aussi le verrou de la transaction d'ecriture. C'est un
-- gain reel par rapport au `SELECT COUNT(*) ... FOR UPDATE` precedent, qui
-- comptait sur un verrou d'intervalle : ce dernier n'existe qu'en REPEATABLE
-- READ, alors qu'un verrou sur une ligne presente vaut aussi en READ COMMITTED.
--
-- Migration additive : la table des mentions n'est pas modifiee, ses contraintes
-- d'unicite restent la garantie de dernier recours.
SET NAMES utf8mb4;

-- statement-break

CREATE TABLE IF NOT EXISTS fiscal_policy_regime_versions (
    regime VARCHAR(32) NOT NULL,
    version INT UNSIGNED NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    PRIMARY KEY (regime),
    CONSTRAINT chk_fiscal_policy_regime_versions_regime
        CHECK (regime IN ('franchise_base', 'standard'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- statement-break

-- Amorce a l'etat courant : le decompte existant devient le point de depart, de
-- sorte qu'un ecran deja ouvert reste coherent au moment de la bascule.
INSERT IGNORE INTO fiscal_policy_regime_versions (regime, version, updated_at)
SELECT regime, COUNT(*), UTC_TIMESTAMP(6)
FROM fiscal_policy_mentions
GROUP BY regime;

-- statement-break

-- Les regimes du registre ferme qui n'ont encore aucune mention.
INSERT IGNORE INTO fiscal_policy_regime_versions (regime, version, updated_at)
VALUES
    ('franchise_base', 0, UTC_TIMESTAMP(6)),
    ('standard', 0, UTC_TIMESTAMP(6));
