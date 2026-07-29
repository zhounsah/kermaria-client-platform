ALTER TABLE commercial_documents
    ADD COLUMN IF NOT EXISTS subscription_id CHAR(36) NULL DEFAULT NULL;

-- statement-break

ALTER TABLE commercial_documents
    ADD KEY IF NOT EXISTS ix_commercial_documents_subscription
        (subscription_id);

-- statement-break

ALTER TABLE commercial_documents
    ADD CONSTRAINT fk_commercial_documents_subscription
        FOREIGN KEY (subscription_id) REFERENCES subscriptions (id);
