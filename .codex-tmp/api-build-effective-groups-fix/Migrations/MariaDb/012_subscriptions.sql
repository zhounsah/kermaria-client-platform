CREATE TABLE IF NOT EXISTS subscriptions (
    id CHAR(36) NOT NULL PRIMARY KEY,
    customer_id CHAR(36) NOT NULL,
    commercial_offer_id CHAR(36) NOT NULL,
    paypal_subscription_id VARCHAR(64) NULL,
    paypal_plan_id VARCHAR(64) NOT NULL,
    status ENUM(
        'pending_approval',
        'active',
        'suspended',
        'cancelled',
        'expired'
    ) NOT NULL DEFAULT 'pending_approval',
    started_at DATETIME(6) NULL,
    next_billing_at DATETIME(6) NULL,
    cancelled_at DATETIME(6) NULL,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    UNIQUE KEY ux_subscriptions_paypal_id (paypal_subscription_id),
    KEY ix_subscriptions_customer_status (
        customer_id,
        status,
        updated_at
    ),
    KEY ix_subscriptions_offer (commercial_offer_id),
    KEY ix_subscriptions_status (status),
    CONSTRAINT fk_subscriptions_customer
        FOREIGN KEY (customer_id) REFERENCES customers (id),
    CONSTRAINT fk_subscriptions_offer
        FOREIGN KEY (commercial_offer_id) REFERENCES commercial_offers (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
