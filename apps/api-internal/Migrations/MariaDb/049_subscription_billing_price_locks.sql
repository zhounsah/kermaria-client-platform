-- ============================================================================
-- Zachary IT - Billing legacy
-- Migration 049 : verrou contractuel de prix d'abonnement
--
-- Objectif :
--   empêcher les renouvellements legacy de relire silencieusement le prix
--   courant de commercial_offers lorsque le contrat a déjà un prix applicable.
--
-- Additif et rétrocompatible :
--   * aucune modification de commercial_offers ;
--   * aucune facture historique recalculée ;
--   * les locks sont créés par l'application au moment où elle dispose du
--     prix contractuel de l'abonnement.
--   * le backfill ne lit jamais commercial_offers.price_amount_cents comme
--     preuve contractuelle ; il utilise uniquement des lignes historiques
--     associées à l'abonnement.
-- ============================================================================

CREATE TABLE IF NOT EXISTS subscription_billing_price_locks (
    id CHAR(36) NOT NULL,
    subscription_id CHAR(36) NOT NULL,
    offer_id CHAR(36) NOT NULL,
    unit_price_cents INT NOT NULL,
    tax_rate_basis_points INT NULL,
    currency CHAR(3) NOT NULL DEFAULT 'EUR',
    reason VARCHAR(96) NOT NULL,
    status VARCHAR(24) NOT NULL DEFAULT 'active',
    active_lock_slot TINYINT(1)
        GENERATED ALWAYS AS (
            CASE WHEN status = 'active' THEN 1 ELSE NULL END
        ) STORED,
    created_at DATETIME(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT UTC_TIMESTAMP(6)
,

    PRIMARY KEY (id),
    UNIQUE KEY uq_subscription_billing_price_locks_active
        (subscription_id, active_lock_slot),
    KEY idx_subscription_billing_price_locks_offer
        (offer_id, status),

    CONSTRAINT fk_subscription_billing_price_locks_subscription
        FOREIGN KEY (subscription_id)
        REFERENCES subscriptions(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- statement-break

CREATE TABLE IF NOT EXISTS subscription_billing_price_lock_review_required (
    subscription_id CHAR(36) NOT NULL,
    offer_id CHAR(36) NOT NULL,
    reason VARCHAR(96) NOT NULL,
    review_status VARCHAR(24) NOT NULL DEFAULT 'pending',
    detected_at DATETIME(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT UTC_TIMESTAMP(6)
,

    PRIMARY KEY (subscription_id),
    KEY idx_subscription_price_lock_review_status
        (review_status, detected_at),

    CONSTRAINT fk_subscription_price_lock_review_subscription
        FOREIGN KEY (subscription_id)
        REFERENCES subscriptions(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- statement-break

INSERT INTO subscription_billing_price_locks (
    id,
    subscription_id,
    offer_id,
    unit_price_cents,
    tax_rate_basis_points,
    currency,
    reason,
    status,
    created_at,
    updated_at
)
SELECT
    UUID(),
    historical_price.subscription_id,
    historical_price.offer_id,
    historical_price.unit_price_cents,
    historical_price.tax_rate_basis_points,
    historical_price.currency,
    'legacy_subscription_backfill',
    'active',
    UTC_TIMESTAMP(6),
    UTC_TIMESTAMP(6)
FROM (
    SELECT ranked.*
    FROM (
        SELECT
            candidate.*,
            ROW_NUMBER() OVER (
                PARTITION BY candidate.subscription_id
                ORDER BY
                    candidate.document_created_at,
                    candidate.sort_order,
                    candidate.line_created_at,
                    candidate.line_id
            ) AS row_number_for_subscription
        FROM (
            SELECT DISTINCT
                subscription.id AS subscription_id,
                subscription.commercial_offer_id AS offer_id,
                line.id AS line_id,
                line.unit_price_cents,
                line.tax_rate_basis_points,
                document.currency,
                document.created_at AS document_created_at,
                line.sort_order,
                line.created_at AS line_created_at
            FROM subscriptions subscription
            INNER JOIN commercial_documents document
                ON document.subscription_id = subscription.id
            INNER JOIN commercial_document_lines line
                ON line.document_id = document.id
               AND line.offer_id = subscription.commercial_offer_id
            WHERE subscription.status IN (
                      'pending_approval',
                      'pending_payment',
                      'pending_activation',
                      'pending_cancellation',
                      'active',
                      'suspended'
                  )
              AND document.status <> 'cancelled'
              AND line.unit_price_cents > 0

            UNION

            SELECT DISTINCT
                subscription.id AS subscription_id,
                subscription.commercial_offer_id AS offer_id,
                line.id AS line_id,
                line.unit_price_cents,
                line.tax_rate_basis_points,
                document.currency,
                document.created_at AS document_created_at,
                line.sort_order,
                line.created_at AS line_created_at
            FROM subscriptions subscription
            INNER JOIN commercial_document_line_subscriptions link
                ON link.subscription_id = subscription.id
            INNER JOIN commercial_document_lines line
                ON line.id = link.document_line_id
               AND line.offer_id = subscription.commercial_offer_id
            INNER JOIN commercial_documents document
                ON document.id = line.document_id
            WHERE subscription.status IN (
                      'pending_approval',
                      'pending_payment',
                      'pending_activation',
                      'pending_cancellation',
                      'active',
                      'suspended'
                  )
              AND document.status <> 'cancelled'
              AND line.unit_price_cents > 0
        ) candidate
    ) ranked
    WHERE ranked.row_number_for_subscription = 1
) historical_price
WHERE NOT EXISTS (
      SELECT 1
      FROM subscription_billing_price_locks price_lock
      WHERE price_lock.subscription_id = historical_price.subscription_id
        AND price_lock.status = 'active'
  );

-- statement-break

INSERT INTO subscription_billing_price_lock_review_required (
    subscription_id,
    offer_id,
    reason,
    review_status,
    detected_at,
    updated_at
)
SELECT
    subscription.id,
    subscription.commercial_offer_id,
    'missing_reliable_historical_price',
    'pending',
    UTC_TIMESTAMP(6),
    UTC_TIMESTAMP(6)
FROM subscriptions subscription
WHERE subscription.status IN (
          'pending_approval',
          'pending_payment',
          'pending_activation',
          'pending_cancellation',
          'active',
          'suspended'
      )
  AND NOT EXISTS (
      SELECT 1
      FROM subscription_billing_price_locks price_lock
      WHERE price_lock.subscription_id = subscription.id
        AND price_lock.status = 'active'
  )
  AND NOT EXISTS (
      SELECT 1
      FROM commercial_documents document
      INNER JOIN commercial_document_lines line
          ON line.document_id = document.id
         AND line.offer_id = subscription.commercial_offer_id
      WHERE document.subscription_id = subscription.id
        AND document.status <> 'cancelled'
        AND line.unit_price_cents > 0
  )
  AND NOT EXISTS (
      SELECT 1
      FROM commercial_document_line_subscriptions link
      INNER JOIN commercial_document_lines line
          ON line.id = link.document_line_id
         AND line.offer_id = subscription.commercial_offer_id
      INNER JOIN commercial_documents document
          ON document.id = line.document_id
      WHERE link.subscription_id = subscription.id
        AND document.status <> 'cancelled'
        AND line.unit_price_cents > 0
  )
ON DUPLICATE KEY UPDATE
    reason = VALUES(reason),
    review_status = IF(
        review_status = 'resolved',
        review_status,
        VALUES(review_status)),
    updated_at = UTC_TIMESTAMP(6);
