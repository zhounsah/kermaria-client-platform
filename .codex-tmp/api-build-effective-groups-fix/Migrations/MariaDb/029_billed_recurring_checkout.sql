-- V0.36 - panier unifie et abonnements factures.
-- Selection persistante des offres recurrentes, liens document-ligne ->
-- souscription, et statut local pending_payment pour les abonnements
-- factures avant leur premier reglement.

ALTER TABLE subscriptions
    MODIFY COLUMN rail ENUM('paypal','stripe','billing')
        NOT NULL DEFAULT 'paypal';

-- statement-break

ALTER TABLE subscriptions
    MODIFY COLUMN status ENUM(
        'pending_approval',
        'pending_payment',
        'pending_activation',
        'pending_cancellation',
        'active',
        'suspended',
        'cancelled',
        'expired'
    ) NOT NULL DEFAULT 'pending_approval';

-- statement-break

CREATE TABLE IF NOT EXISTS recurring_checkout_items (
    id CHAR(36) NOT NULL,
    customer_id CHAR(36) NOT NULL,
    offer_id CHAR(36) NOT NULL,
    commitment_months INT NOT NULL,
    payment_mode ENUM('monthly','upfront') NOT NULL,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_recurring_checkout_customer_offer (customer_id, offer_id),
    KEY ix_recurring_checkout_customer (customer_id),
    CONSTRAINT fk_recurring_checkout_customer
        FOREIGN KEY (customer_id) REFERENCES customers (id)
        ON DELETE CASCADE,
    CONSTRAINT fk_recurring_checkout_offer
        FOREIGN KEY (offer_id) REFERENCES commercial_offers (id)
        ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- statement-break

CREATE TABLE IF NOT EXISTS commercial_document_line_subscriptions (
    id CHAR(36) NOT NULL,
    document_line_id CHAR(36) NOT NULL,
    subscription_id CHAR(36) NOT NULL,
    created_at DATETIME(6) NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_document_line_subscription (
        document_line_id,
        subscription_id
    ),
    KEY ix_document_line_subscription_subscription (subscription_id),
    CONSTRAINT fk_document_line_subscription_line
        FOREIGN KEY (document_line_id) REFERENCES commercial_document_lines (id)
        ON DELETE CASCADE,
    CONSTRAINT fk_document_line_subscription_subscription
        FOREIGN KEY (subscription_id) REFERENCES subscriptions (id)
        ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
