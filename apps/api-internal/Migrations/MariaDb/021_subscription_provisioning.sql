ALTER TABLE ad_actions
    ADD COLUMN IF NOT EXISTS subscription_id CHAR(36) NULL DEFAULT NULL
        AFTER customer_id;

-- statement-break

ALTER TABLE ad_actions
    ADD COLUMN IF NOT EXISTS idempotency_key_hash CHAR(64) NULL DEFAULT NULL
        AFTER result_code;

-- statement-break

ALTER TABLE ad_actions
    ADD COLUMN IF NOT EXISTS changed BOOLEAN NULL DEFAULT NULL
        AFTER idempotency_key_hash;

-- statement-break

ALTER TABLE ad_actions
    ADD COLUMN IF NOT EXISTS details_json JSON NULL
        AFTER changed;

-- statement-break

ALTER TABLE ad_actions
    ADD KEY IF NOT EXISTS ix_ad_actions_subscription
        (subscription_id, requested_at);

-- statement-break

ALTER TABLE ad_actions
    ADD KEY IF NOT EXISTS ix_ad_actions_idempotency
        (idempotency_key_hash);

-- statement-break

ALTER TABLE ad_actions
    ADD CONSTRAINT fk_ad_actions_subscription
        FOREIGN KEY (subscription_id) REFERENCES subscriptions (id);
