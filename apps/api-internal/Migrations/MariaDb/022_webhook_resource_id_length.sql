ALTER TABLE paypal_webhook_events
    MODIFY COLUMN resource_id VARCHAR(255) NULL;

-- statement-break

ALTER TABLE stripe_webhook_events
    MODIFY COLUMN resource_id VARCHAR(255) NULL;
