ALTER TABLE subscriptions
    ADD COLUMN IF NOT EXISTS rail ENUM('paypal','stripe')
        NOT NULL DEFAULT 'paypal';

-- statement-break

ALTER TABLE subscriptions
    MODIFY COLUMN paypal_plan_id VARCHAR(64) NULL;

-- statement-break

ALTER TABLE subscriptions
    ADD COLUMN IF NOT EXISTS stripe_subscription_id VARCHAR(64) NULL
        DEFAULT NULL;

-- statement-break

ALTER TABLE subscriptions
    ADD COLUMN IF NOT EXISTS stripe_price_id VARCHAR(64) NULL DEFAULT NULL;

-- statement-break

ALTER TABLE subscriptions
    ADD CONSTRAINT ux_subscriptions_stripe_id UNIQUE (stripe_subscription_id);
