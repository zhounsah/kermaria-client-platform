ALTER TABLE commercial_offers
    ADD COLUMN IF NOT EXISTS billing_cadence ENUM('one_time','monthly')
        NOT NULL DEFAULT 'one_time';

-- statement-break

ALTER TABLE commercial_offers
    ADD COLUMN IF NOT EXISTS paypal_plan_id VARCHAR(64) NULL DEFAULT NULL;

-- statement-break

ALTER TABLE commercial_offers
    ADD KEY IF NOT EXISTS ix_commercial_offers_cadence (billing_cadence);
