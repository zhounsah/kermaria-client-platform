-- ============================================================================
-- Zachary IT - Billing V2
-- Migration 060 : hardening du socle (Phase 2.5)
--
-- Trois sujets, tous additifs :
--
--   1. modele de cycle de renouvellement : un renouvellement est identifie par
--      (subscription_id, cycle_sequence), JAMAIS par l'heure courante ;
--   2. reconciliation des tentatives de paiement Stripe non terminees, avec
--      bail (lease) pour supporter plusieurs workers ;
--   3. intention d'emission documentaire persistee AVANT l'appel BPCE, pour
--      fermer la fenetre "BPCE cree la facture, le reseau coupe, le retry en
--      cree une seconde".
--
-- Aucun paiement, aucune emission, aucun appel provider n'est declenche ici.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 1. CYCLE DE RENOUVELLEMENT
--
-- Le rang du cycle est une donnee contractuelle derivee de l'ancre de
-- l'abonnement, pas de l'horloge. Deux executions du meme cycle - worker
-- rejoue, deux instances concurrentes, rattrapage manuel - doivent viser la
-- meme ligne et donc entrer en collision sur l'unicite plutot que produire
-- deux facturations.
--
-- cycle_sequence NULL reste autorise (evenements hors cycle : ajustements,
-- prestations ponctuelles). MariaDB autorise plusieurs NULL dans un index
-- UNIQUE, les lignes existantes restent donc valides.
--
-- Convention : la charge initiale porte le cycle 1, le premier renouvellement
-- le cycle 2, et ainsi de suite.
-- ----------------------------------------------------------------------------

ALTER TABLE billing_v2_billing_events
    ADD COLUMN IF NOT EXISTS cycle_sequence INT NULL
        AFTER commitment_months_snapshot;

-- statement-break

ALTER TABLE billing_v2_billing_events
    ADD CONSTRAINT IF NOT EXISTS ck_billing_v2_billing_events_cycle_sequence
        CHECK (cycle_sequence IS NULL OR cycle_sequence >= 1);

-- statement-break

-- Au plus un evenement de facturation par (abonnement, type, cycle).
-- C'est cette contrainte qui rend impossible un double renouvellement du
-- cycle 17, quel que soit le nombre de workers.
CREATE UNIQUE INDEX IF NOT EXISTS uq_billing_v2_billing_events_cycle
    ON billing_v2_billing_events (subscription_id, event_type, cycle_sequence);

-- statement-break

-- ----------------------------------------------------------------------------
-- 2. RECONCILIATION DES TENTATIVES DE PAIEMENT
--
-- Un webhook perdu ne doit pas laisser une tentative en suspens indefiniment.
-- Le reconciliateur relit Stripe avec les identifiants deja persistes ; il ne
-- cree jamais de nouveau checkout.
--
-- `reconciliation_lease_until` est le bail : un worker ne prend une tentative
-- que si le bail est expire, et le prolonge en une seule ecriture
-- conditionnelle. Deux workers ne peuvent donc pas traiter la meme tentative
-- en meme temps.
-- ----------------------------------------------------------------------------

ALTER TABLE billing_v2_payment_attempts
    ADD COLUMN IF NOT EXISTS reconciliation_attempts INT NOT NULL DEFAULT 0
        AFTER verification_reason_code,
    ADD COLUMN IF NOT EXISTS next_reconciliation_at DATETIME(6) NULL
        AFTER reconciliation_attempts,
    ADD COLUMN IF NOT EXISTS reconciliation_lease_until DATETIME(6) NULL
        AFTER next_reconciliation_at;

-- statement-break

CREATE INDEX IF NOT EXISTS idx_billing_v2_payment_attempts_reconciliation
    ON billing_v2_payment_attempts
        (status, next_reconciliation_at, reconciliation_lease_until);

-- statement-break

-- `reconciliation_required` rejoint l'enumeration : etat terminal cote
-- automatisme, explicitement en attente d'une decision humaine.
ALTER TABLE billing_v2_payment_attempts
    DROP CONSTRAINT IF EXISTS ck_billing_v2_payment_attempts_status;

-- statement-break

ALTER TABLE billing_v2_payment_attempts
    ADD CONSTRAINT IF NOT EXISTS ck_billing_v2_payment_attempts_status
        CHECK (status IN (
            'created',
            'in_flight',
            'succeeded',
            'failed',
            'abandoned',
            'amount_mismatch',
            'reconciliation_required'));

-- statement-break

-- ----------------------------------------------------------------------------
-- 3. INTENTION D'EMISSION DOCUMENTAIRE (BPCE)
--
-- Fenetre fermee ici :
--
--   BPCE cree la facture -> timeout reseau -> aucune trace locale
--     -> retry -> SECONDE facture, second numero fiscal consomme.
--
-- L'intention est ecrite AVANT l'appel, avec une identite stable
-- (`external_reference`) derivee du document. Au retour indetermine, le
-- systeme tente d'abord une reconciliation par cette reference ; s'il ne peut
-- pas, il passe en `reconciliation_required` et n'emet RIEN de plus.
--
-- L'unicite sur `commercial_document_id` garantit une seule intention par
-- document, donc une seule facture logique.
-- ----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS billing_v2_document_issuance_attempts (
    id                          CHAR(36)      NOT NULL,

    commercial_document_id      CHAR(36)      NOT NULL,
    billing_event_id            CHAR(36)      NULL,

    -- Identite stable derivee du document : c'est la cle de recherche cote
    -- BPCE en cas de reprise apres appel indetermine.
    external_reference          VARCHAR(190)  NOT NULL,

    status                      VARCHAR(32)   NOT NULL DEFAULT 'created',
    reason_code                 VARCHAR(96)   NULL,

    provider_invoice_id         VARCHAR(255)  NULL,
    provider_invoice_number     VARCHAR(255)  NULL,

    attempt_count               INT           NOT NULL DEFAULT 0,
    last_error                  TEXT          NULL,

    created_at                  DATETIME(6)   NOT NULL
                                            DEFAULT UTC_TIMESTAMP(6),
    updated_at                  DATETIME(6)   NOT NULL
                                            DEFAULT UTC_TIMESTAMP(6),
    attempted_at                DATETIME(6)   NULL,
    resolved_at                 DATETIME(6)   NULL,

    PRIMARY KEY (id),

    UNIQUE KEY uq_billing_v2_document_issuance_document
        (commercial_document_id),
    UNIQUE KEY uq_billing_v2_document_issuance_reference
        (external_reference),
    KEY idx_billing_v2_document_issuance_status
        (status, updated_at),

    CONSTRAINT fk_billing_v2_document_issuance_document
        FOREIGN KEY (commercial_document_id)
        REFERENCES commercial_documents(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_document_issuance_event
        FOREIGN KEY (billing_event_id)
        REFERENCES billing_v2_billing_events(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT ck_billing_v2_document_issuance_status
        CHECK (status IN (
            'created',
            'in_flight',
            'succeeded',
            'failed',
            'reconciliation_required')),
    CONSTRAINT ck_billing_v2_document_issuance_reference
        CHECK (CHAR_LENGTH(TRIM(external_reference)) > 0),
    CONSTRAINT ck_billing_v2_document_issuance_attempt_count
        CHECK (attempt_count >= 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
