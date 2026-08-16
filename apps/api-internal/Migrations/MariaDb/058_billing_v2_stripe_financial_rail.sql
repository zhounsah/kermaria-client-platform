-- ============================================================================
-- Zachary IT - Billing V2
-- Migration 058 : raccordement du rail Stripe au coeur financier (Phase 2)
--
-- Objectif :
--   permettre au premier checkout Stripe V2 de suivre la chaine
--
--     SubscriptionChange -> BillingEvent finalized -> PaymentAttempt
--       -> Stripe -> refetch/verification -> settlement -> activation locale
--
--   sans qu'aucun montant contractuel ne soit determine par Stripe.
--
-- Cette migration est ADDITIVE :
--   - elle ne supprime aucune table et aucune colonne ;
--   - elle ne seed aucun mapping provider ;
--   - elle ne reutilise aucun Stripe Price ID legacy ;
--   - elle n'active aucun checkout et n'appelle aucun provider.
--
-- Specification : docs/billing-v2/FINANCIAL-CORE.md
--                 docs/billing-v2/STRIPE-RAIL.md
--
-- Verification prealable :
--
-- SELECT COUNT(*) FROM billing_v2_billing_event_lines
-- WHERE billing_cadence IS NULL;
--   -> colonne introduite ici, doit etre absente avant execution.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 1. CADENCE SUR LES LIGNES D'EVENEMENT FINANCIER
--
-- Le rail Stripe doit distinguer ce qui est recurrent de ce qui est one-shot
-- (setup fee) SANS relire le catalogue apres finalisation. La cadence doit donc
-- etre figee dans le snapshot de ligne, comme le reste.
--
-- 'monthly'      : ligne recurrente mensuelle
-- 'upfront_term' : ligne recurrente prepayee pour toute la duree d'engagement
-- 'one_time'     : prestation ponctuelle / frais de mise en service
-- ----------------------------------------------------------------------------

ALTER TABLE billing_v2_billing_event_lines
    ADD COLUMN IF NOT EXISTS billing_cadence VARCHAR(24) NOT NULL DEFAULT 'monthly'
        AFTER description;

-- statement-break

ALTER TABLE billing_v2_billing_event_lines
    ADD CONSTRAINT IF NOT EXISTS ck_billing_v2_billing_event_lines_cadence
        CHECK (billing_cadence IN ('monthly', 'upfront_term', 'one_time'));

-- statement-break

-- ----------------------------------------------------------------------------
-- 2. HORODATAGE DU SETTLEMENT SUR L'EVENEMENT
-- ----------------------------------------------------------------------------

ALTER TABLE billing_v2_billing_events
    ADD COLUMN IF NOT EXISTS settled_at DATETIME(6) NULL
        AFTER finalized_at,
    ADD COLUMN IF NOT EXISTS settlement_reason_code VARCHAR(96) NULL
        AFTER settled_at;

-- statement-break

-- ----------------------------------------------------------------------------
-- 3. INTENTION UTILISATEUR RATTACHEE A LA DEMANDE DE CHECKOUT
--
-- L'ancre d'idempotence metier devient le SubscriptionChange. La demande de
-- checkout existante n'est pas supprimee : elle est reliee a l'intention et a
-- l'evenement financier (billing_event_id ajoute en 057).
-- ----------------------------------------------------------------------------

ALTER TABLE billing_v2_authoritative_checkout_requests
    ADD COLUMN IF NOT EXISTS subscription_change_id CHAR(36) NULL
        AFTER subscription_id;

-- statement-break

ALTER TABLE billing_v2_authoritative_checkout_requests
    ADD CONSTRAINT fk_billing_v2_authoritative_checkout_change
        FOREIGN KEY IF NOT EXISTS (subscription_change_id)
        REFERENCES billing_v2_subscription_changes(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT;

-- statement-break

CREATE INDEX IF NOT EXISTS idx_billing_v2_authoritative_checkout_change
    ON billing_v2_authoritative_checkout_requests (subscription_change_id);

-- statement-break

-- ----------------------------------------------------------------------------
-- 4. SESSION PROVIDER RATTACHEE A SA TENTATIVE DE PAIEMENT
--
-- Une session checkout locale doit pouvoir remonter a la PaymentAttempt qui l'a
-- produite, pour que la verification de settlement compare au montant attendu
-- persiste avant l'appel, et non a un montant relu du catalogue.
-- ----------------------------------------------------------------------------

ALTER TABLE billing_v2_provider_checkout_sessions
    ADD COLUMN IF NOT EXISTS payment_attempt_id CHAR(36) NULL
        AFTER billing_event_id;

-- statement-break

ALTER TABLE billing_v2_provider_checkout_sessions
    ADD CONSTRAINT fk_billing_v2_provider_checkout_payment_attempt
        FOREIGN KEY IF NOT EXISTS (payment_attempt_id)
        REFERENCES billing_v2_payment_attempts(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT;

-- statement-break

CREATE INDEX IF NOT EXISTS idx_billing_v2_provider_checkout_payment_attempt
    ON billing_v2_provider_checkout_sessions (payment_attempt_id);

-- statement-break

-- ----------------------------------------------------------------------------
-- 5. TRACE DE VERIFICATION SUR LA TENTATIVE DE PAIEMENT
--
-- Un webhook n'est qu'un signal. La transition financiere s'appuie sur une
-- RELECTURE de l'objet chez Stripe. On conserve ce qui a ete reellement
-- constate lors de cette relecture, afin qu'un ecart soit auditable et pas
-- seulement journalise.
-- ----------------------------------------------------------------------------

ALTER TABLE billing_v2_payment_attempts
    ADD COLUMN IF NOT EXISTS provider_customer_reference VARCHAR(255) NULL
        AFTER provider_session_id,
    ADD COLUMN IF NOT EXISTS provider_payment_status VARCHAR(48) NULL
        AFTER provider_customer_reference,
    ADD COLUMN IF NOT EXISTS provider_mode VARCHAR(24) NULL
        AFTER provider_payment_status,
    ADD COLUMN IF NOT EXISTS verification_reason_code VARCHAR(96) NULL
        AFTER failure_reason_code,
    ADD COLUMN IF NOT EXISTS verified_at DATETIME(6) NULL
        AFTER responded_at;

-- statement-break

ALTER TABLE billing_v2_payment_attempts
    ADD CONSTRAINT IF NOT EXISTS ck_billing_v2_payment_attempts_provider_mode
        CHECK (provider_mode IS NULL
            OR provider_mode IN ('subscription', 'payment'));

-- statement-break

-- ----------------------------------------------------------------------------
-- 6. MAPPINGS PROVIDER : REFERENCE, PLUS JAMAIS SOURCE DU MONTANT
--
-- Les mappings restent utiles comme metadata (produit/prix Stripe de
-- reference). Ils ne determinent plus le total contractuel : celui-ci vient du
-- BillingEvent finalise.
--
-- Les deux colonnes ajoutees servent UNIQUEMENT de controle croise facultatif :
-- si elles sont renseignees et divergent du montant local, le mapping est
-- refuse. Elles ne sont jamais lues comme prix a facturer.
--
-- Aucun seed ici : aucun mapping de production n'est cree par cette migration.
-- ----------------------------------------------------------------------------

ALTER TABLE billing_v2_provider_price_mappings
    ADD COLUMN IF NOT EXISTS expected_amount_cents BIGINT NULL
        AFTER external_plan_id,
    ADD COLUMN IF NOT EXISTS expected_currency CHAR(3) NULL
        AFTER expected_amount_cents,
    ADD COLUMN IF NOT EXISTS amount_authority VARCHAR(24) NOT NULL DEFAULT 'local'
        AFTER expected_currency;

-- statement-break

-- 'local' est la seule valeur autorisee : le montant vient toujours du
-- BillingEvent. La contrainte existe pour qu'un futur contributeur ne puisse
-- pas redonner l'autorite au provider par simple UPDATE.
ALTER TABLE billing_v2_provider_price_mappings
    ADD CONSTRAINT IF NOT EXISTS ck_billing_v2_provider_price_mappings_amount_authority
        CHECK (amount_authority = 'local');

-- statement-break

ALTER TABLE billing_v2_provider_price_mappings
    ADD CONSTRAINT IF NOT EXISTS ck_billing_v2_provider_price_mappings_expected_amount
        CHECK (expected_amount_cents IS NULL OR expected_amount_cents >= 0);

-- statement-break

ALTER TABLE billing_v2_provider_price_mappings
    ADD CONSTRAINT IF NOT EXISTS ck_billing_v2_provider_price_mappings_expected_currency
        CHECK (expected_currency IS NULL
            OR CHAR_LENGTH(TRIM(expected_currency)) = 3);
