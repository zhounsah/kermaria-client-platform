-- Mentions fiscales administrables et datees.
--
-- Le *calcul* de la taxe reste dans le code (`FiscalPolicy`) : cette table ne
-- porte que le texte de la mention associee a un regime connu, et la date a
-- partir de laquelle il s'applique. Aucune expression, aucun taux : un regime
-- inconnu du code est ignore.
--
-- La resolution se fait « a la date » de la ligne de document : une facture
-- deja emise conserve donc la mention en vigueur au moment de son emission,
-- meme si le texte est modifie ensuite. C'est l'invariant central de cette
-- migration — une mention n'est jamais retroactive.
SET NAMES utf8mb4;

-- statement-break

CREATE TABLE IF NOT EXISTS fiscal_policy_mentions (
    id CHAR(36) NOT NULL,
    regime VARCHAR(32) NOT NULL,
    mention VARCHAR(300) NOT NULL,
    effective_from DATETIME(6) NOT NULL,
    created_at DATETIME(6) NOT NULL,
    created_by_user_id CHAR(36) NULL,
    correlation_id VARCHAR(128) NOT NULL,
    PRIMARY KEY (id),
    -- Deux versions d'un meme regime ne peuvent pas prendre effet au meme
    -- instant : la version applicable serait indeterminee.
    UNIQUE KEY uk_fiscal_policy_mentions_regime_effective (regime, effective_from),
    KEY idx_fiscal_policy_mentions_regime (regime, effective_from),
    CONSTRAINT chk_fiscal_policy_mentions_regime
        CHECK (regime IN ('franchise_base', 'standard'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
