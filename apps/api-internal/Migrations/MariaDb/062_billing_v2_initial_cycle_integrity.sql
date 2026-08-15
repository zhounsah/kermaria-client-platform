-- 062 - Integrite du cycle initial Billing V2.
--
-- Trois corrections mesurees pendant la validation Stripe reelle de la phase 4 :
--
-- 1. Le document initial ne portait ni `billing_event_id` ni `cycle_sequence`.
--    L'unicite par cycle reposait donc sur un NULL, et MariaDB accepte
--    plusieurs NULL dans un index UNIQUE : le cycle 1 n'etait pas protege.
-- 2. `document_status` du BillingEvent restait a `none` apres emission.
-- 3. `billing_anchor_at` restait NULL sur les abonnements crees apres la
--    migration 061 : la colonne existait mais rien ne l'ecrivait.
--
-- Migration additive et rejouable : chaque etape est bornee aux lignes encore
-- incoherentes.

UPDATE billing_v2_subscription_documents document_row
INNER JOIN billing_v2_billing_events initial_event
    ON initial_event.subscription_id = document_row.subscription_id
   AND initial_event.cycle_sequence = 1
SET document_row.billing_event_id = initial_event.id
WHERE document_row.billing_event_id IS NULL
  AND document_row.document_kind = 'initial_subscription_invoice';

-- statement-break

UPDATE billing_v2_subscription_documents
SET cycle_sequence = 1
WHERE cycle_sequence IS NULL
  AND document_kind = 'initial_subscription_invoice';

-- statement-break

-- Tout document V2 appartient a un cycle. Le rang devient obligatoire, ce qui
-- rend enfin effectif `uq_billing_v2_subscription_document_cycle`.
UPDATE billing_v2_subscription_documents
SET cycle_sequence = 1
WHERE cycle_sequence IS NULL;

-- statement-break

ALTER TABLE billing_v2_subscription_documents
    MODIFY COLUMN cycle_sequence INT NOT NULL DEFAULT 1;

-- statement-break

UPDATE billing_v2_document_issuance_attempts attempt
INNER JOIN billing_v2_subscription_documents document_row
    ON document_row.commercial_document_id = attempt.commercial_document_id
SET attempt.billing_event_id = document_row.billing_event_id
WHERE attempt.billing_event_id IS NULL
  AND document_row.billing_event_id IS NOT NULL;

-- statement-break

UPDATE billing_v2_billing_events event_row
INNER JOIN billing_v2_subscription_documents document_row
    ON document_row.billing_event_id = event_row.id
SET event_row.document_status = CASE document_row.status
        WHEN 'issued' THEN 'issued'
        WHEN 'paid' THEN 'issued'
        WHEN 'failed' THEN 'failed'
        ELSE 'pending'
    END
WHERE event_row.document_status = 'none';

-- statement-break

-- Ancre contractuelle materialisee pour les abonnements deja demarres. Le
-- fallback COALESCE reste en place comme securite, mais cesse d'etre le
-- fonctionnement normal.
UPDATE billing_v2_subscriptions
SET billing_anchor_at = COALESCE(started_at, created_at)
WHERE billing_anchor_at IS NULL;
