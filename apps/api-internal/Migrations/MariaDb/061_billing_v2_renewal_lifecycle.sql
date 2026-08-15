-- ============================================================================
-- Zachary IT - Billing V2
-- Migration 061 : cycle de vie du renouvellement Stripe (Phase 3)
--
-- Quatre sujets, tous additifs :
--
--   1. ancre contractuelle explicite sur l'abonnement : le rang du cycle se
--      derive de cette ancre, jamais de l'heure courante ni de la date d'un
--      webhook ;
--   2. etat de paiement local distinct du statut d'abonnement : un impaye doit
--      etre VISIBLE sans declencher de retrait automatique d'acces ;
--   3. rattachement du document au BillingEvent et au cycle, pour qu'un rejeu
--      ne puisse pas produire une seconde facture du meme cycle ;
--   4. tracabilite du renouvellement sur la tentative de paiement.
--
-- Aucun paiement, aucune emission, aucun appel provider n'est declenche ici.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 1. ANCRE CONTRACTUELLE
--
-- Jusqu'ici le calendrier retombait sur `created_at`. C'est presque toujours
-- juste, mais `created_at` est une date technique : elle bouge si une reprise
-- recree la ligne, et elle ne dit pas quel jour le contrat a reellement pris
-- effet. `billing_anchor_at` fige ce jour une fois pour toutes.
--
-- Valeur initiale deterministe : le demarrage effectif s'il existe, la
-- creation sinon. Aucun abonnement existant ne change de periode.
-- ----------------------------------------------------------------------------

ALTER TABLE billing_v2_subscriptions
    ADD COLUMN IF NOT EXISTS billing_anchor_at DATETIME(6) NULL
        AFTER started_at;

-- statement-break

UPDATE billing_v2_subscriptions
SET billing_anchor_at = COALESCE(started_at, created_at)
WHERE billing_anchor_at IS NULL;

-- statement-break

-- ----------------------------------------------------------------------------
-- 2. ETAT DE PAIEMENT LOCAL (POLITIQUE DE GRACE V2)
--
-- Un renouvellement echoue ne doit PAS, en V2.0, retirer des groupes AD,
-- reduire un quota ou supprimer des donnees. Il doit produire un etat local
-- explicite, visible en administration, sur lequel un humain tranche.
--
-- `payment_state` est donc volontairement SEPARE de `status` : un abonnement
-- peut rester `active` (acces conserve) tout en etant `payment_attention`
-- (impaye a traiter). Confondre les deux, c'est soit couper un client pour un
-- prelevement rejoue, soit oublier un impaye parce que l'acces fonctionne.
-- ----------------------------------------------------------------------------

ALTER TABLE billing_v2_subscriptions
    ADD COLUMN IF NOT EXISTS payment_state VARCHAR(32) NOT NULL DEFAULT 'current'
        AFTER status,
    ADD COLUMN IF NOT EXISTS payment_state_reason_code VARCHAR(96) NULL
        AFTER payment_state,
    ADD COLUMN IF NOT EXISTS payment_state_changed_at DATETIME(6) NULL
        AFTER payment_state_reason_code;

-- statement-break

ALTER TABLE billing_v2_subscriptions
    ADD CONSTRAINT IF NOT EXISTS ck_billing_v2_subscriptions_payment_state
        CHECK (payment_state IN (
            'current',
            'payment_attention',
            'manual_review'));

-- statement-break

CREATE INDEX IF NOT EXISTS idx_billing_v2_subscriptions_payment_state
    ON billing_v2_subscriptions (payment_state, payment_state_changed_at);

-- statement-break

-- ----------------------------------------------------------------------------
-- 3. DOCUMENT DE CYCLE
--
-- Le rattachement 1:1 document <-> BillingEvent existe deja depuis la
-- migration 057 (`uq_billing_v2_subscription_document_billing_event`). Il n'est
-- donc PAS recree ici : un second index sur la meme colonne ne protegerait
-- rien de plus et couterait a chaque ecriture.
--
-- Ce qui manque, c'est le rang du cycle. `uq_..._period` couvre le cas nominal
-- (une periode = un document) mais laisserait passer deux documents si deux
-- calculs de periode divergeaient d'un jour. Le rang, lui, est un entier
-- derive de l'ancre contractuelle : il ne peut pas deriver.
-- ----------------------------------------------------------------------------

ALTER TABLE billing_v2_subscription_documents
    ADD COLUMN IF NOT EXISTS cycle_sequence INT NULL
        AFTER document_kind;

-- statement-break

ALTER TABLE billing_v2_subscription_documents
    ADD CONSTRAINT IF NOT EXISTS ck_billing_v2_subscription_documents_cycle
        CHECK (cycle_sequence IS NULL OR cycle_sequence >= 1);

-- statement-break

CREATE UNIQUE INDEX IF NOT EXISTS uq_billing_v2_subscription_document_cycle
    ON billing_v2_subscription_documents
        (subscription_id, document_kind, cycle_sequence);

-- statement-break

-- ----------------------------------------------------------------------------
-- 4. TRACABILITE DU RENOUVELLEMENT SUR LA TENTATIVE
--
-- Une tentative de renouvellement se relit chez Stripe par l'invoice, pas par
-- une session checkout (il n'y en a pas : le prelevement est automatique).
-- On persiste donc l'identifiant d'invoice observe, pour que le reconciliateur
-- relise exactement le bon objet plutot que de balayer le compte Stripe.
-- ----------------------------------------------------------------------------

ALTER TABLE billing_v2_payment_attempts
    ADD COLUMN IF NOT EXISTS provider_invoice_id VARCHAR(255) NULL
        AFTER provider_payment_id,
    ADD COLUMN IF NOT EXISTS provider_subscription_id VARCHAR(255) NULL
        AFTER provider_invoice_id;

-- statement-break

CREATE INDEX IF NOT EXISTS idx_billing_v2_payment_attempts_provider_invoice
    ON billing_v2_payment_attempts (provider, environment, provider_invoice_id);
