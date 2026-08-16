-- ============================================================================
-- Zachary IT - Billing V2
-- Migration 063 : souscription V2 native (configuration personnalisee)
--
-- Objectif :
--   permettre au checkout authoritative de representer une souscription V2
--   sans offre legacy. Jusqu'ici l'identite metier d'une demande etait le
--   `legacy_offer_id` : une configuration personnalisee n'avait donc aucun
--   moyen d'etre exprimee, ni d'etre rendue idempotente.
--
-- Ce que la migration change :
--   1. `legacy_offer_id` devient NULLABLE (les demandes legacy continuent de
--      le renseigner, rien n'est perdu) ;
--   2. `selection_fingerprint` devient l'ancre d'identite metier, renseignee
--      pour les deux chemins ;
--   3. `selection_canonical` conserve la configuration lisible, pour l'audit.
--
-- Additive et retro-compatible : aucune ligne existante n'est invalidee, le
-- backfill derive l'empreinte des demandes legacy deja presentes. Aucun appel
-- Stripe/PayPal, aucune modification du coeur financier (PaymentAttempt,
-- settlement, document, renouvellement).
--
-- Verification prealable :
--
-- SELECT COUNT(*) FROM billing_v2_authoritative_checkout_requests
-- WHERE legacy_offer_id IS NULL;
--   -> doit retourner 0 avant execution.
-- ============================================================================

ALTER TABLE billing_v2_authoritative_checkout_requests
    ADD COLUMN IF NOT EXISTS selection_fingerprint CHAR(64) NULL
        AFTER legacy_offer_id,
    ADD COLUMN IF NOT EXISTS selection_canonical VARCHAR(1024) NULL
        AFTER selection_fingerprint;

-- statement-break

-- Les demandes deja enregistrees sont toutes legacy : leur empreinte est
-- derivee de l'offre, ce qui preserve exactement leur comportement
-- d'idempotence actuel.
UPDATE billing_v2_authoritative_checkout_requests
SET selection_fingerprint = SHA2(
        CONCAT('billing_v2.legacy_offer|', legacy_offer_id),
        256)
WHERE selection_fingerprint IS NULL;

-- statement-break

ALTER TABLE billing_v2_authoritative_checkout_requests
    MODIFY COLUMN selection_fingerprint CHAR(64) NOT NULL;

-- statement-break

-- Une souscription native n'a pas d'offre legacy : la colonne ne peut plus
-- etre obligatoire. Elle reste renseignee par le chemin historique.
ALTER TABLE billing_v2_authoritative_checkout_requests
    MODIFY COLUMN legacy_offer_id CHAR(36) NULL;

-- statement-break

CREATE INDEX IF NOT EXISTS idx_billing_v2_authoritative_checkout_selection
    ON billing_v2_authoritative_checkout_requests (selection_fingerprint);
