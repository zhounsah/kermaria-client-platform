CREATE TABLE IF NOT EXISTS paypal_webhook_events (
    id CHAR(36) NOT NULL PRIMARY KEY,
    event_id VARCHAR(64) NOT NULL,
    event_type VARCHAR(64) NOT NULL,
    resource_id VARCHAR(64) NULL,
    received_at DATETIME(6) NOT NULL,
    processed_at DATETIME(6) NULL,
    status ENUM(
        'received',
        'processed',
        'failed',
        'ignored'
    ) NOT NULL DEFAULT 'received',
    error_message TEXT NULL,
    raw_payload JSON NOT NULL,
    UNIQUE KEY ux_paypal_webhook_events_event_id (event_id),
    KEY ix_paypal_webhook_events_status (status),
    KEY ix_paypal_webhook_events_received_at (received_at),
    KEY ix_paypal_webhook_events_resource (resource_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
