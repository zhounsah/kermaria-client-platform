ALTER TABLE subscriptions
    MODIFY COLUMN status ENUM(
        'pending_approval',
        'pending_activation',
        'pending_cancellation',
        'active',
        'suspended',
        'cancelled',
        'expired'
    ) NOT NULL DEFAULT 'pending_approval';

-- statement-break

ALTER TABLE subscriptions
    ADD COLUMN IF NOT EXISTS public_pack_code VARCHAR(64) NULL DEFAULT NULL;

-- statement-break

ALTER TABLE subscriptions
    ADD COLUMN IF NOT EXISTS setup_fee_amount_cents INT NULL DEFAULT NULL;

-- statement-break

ALTER TABLE subscriptions
    ADD COLUMN IF NOT EXISTS billing_interval_months INT NULL DEFAULT NULL;

-- statement-break

ALTER TABLE subscriptions
    ADD COLUMN IF NOT EXISTS commitment_months INT NULL DEFAULT NULL;

-- statement-break

ALTER TABLE subscriptions
    ADD COLUMN IF NOT EXISTS payment_mode ENUM('monthly','upfront')
        NULL DEFAULT NULL;

-- statement-break

ALTER TABLE subscriptions
    ADD COLUMN IF NOT EXISTS paid_cycles_count INT NOT NULL DEFAULT 0;

-- statement-break

ALTER TABLE subscriptions
    ADD COLUMN IF NOT EXISTS commitment_ends_at DATETIME(6) NULL DEFAULT NULL;

-- statement-break

ALTER TABLE subscriptions
    ADD COLUMN IF NOT EXISTS cancel_requested_at DATETIME(6) NULL DEFAULT NULL;

-- statement-break

ALTER TABLE subscriptions
    ADD COLUMN IF NOT EXISTS cancel_at_term_end TINYINT(1)
        NOT NULL DEFAULT 0;

-- statement-break

ALTER TABLE subscriptions
    ADD KEY IF NOT EXISTS ix_subscriptions_cancel_term
        (cancel_at_term_end, next_billing_at, status);

-- statement-break

UPDATE subscriptions subscription
INNER JOIN commercial_offers offer
    ON offer.id = subscription.commercial_offer_id
SET
    subscription.public_pack_code =
        COALESCE(subscription.public_pack_code, offer.public_pack_code),
    subscription.setup_fee_amount_cents =
        COALESCE(subscription.setup_fee_amount_cents, offer.setup_fee_amount_cents, 0),
    subscription.billing_interval_months =
        COALESCE(
            subscription.billing_interval_months,
            offer.billing_interval_months,
            CASE
                WHEN offer.billing_cadence = 'monthly' THEN 1
                ELSE NULL
            END),
    subscription.commitment_months =
        COALESCE(
            subscription.commitment_months,
            offer.commitment_months,
            offer.billing_interval_months,
            CASE
                WHEN offer.billing_cadence = 'monthly' THEN 1
                ELSE NULL
            END),
    subscription.payment_mode =
        COALESCE(subscription.payment_mode, offer.payment_mode, 'monthly'),
    subscription.paid_cycles_count = COALESCE(subscription.paid_cycles_count, 0),
    subscription.commitment_ends_at =
        COALESCE(
            subscription.commitment_ends_at,
            CASE
                WHEN subscription.started_at IS NULL THEN NULL
                ELSE DATE_ADD(
                    subscription.started_at,
                    INTERVAL COALESCE(
                        subscription.commitment_months,
                        offer.commitment_months,
                        offer.billing_interval_months,
                        1
                    ) MONTH)
            END),
    subscription.cancel_at_term_end = COALESCE(subscription.cancel_at_term_end, 0);
