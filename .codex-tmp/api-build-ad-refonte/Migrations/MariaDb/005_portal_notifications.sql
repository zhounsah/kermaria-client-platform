CREATE TABLE IF NOT EXISTS portal_notifications (
    id CHAR(36) NOT NULL PRIMARY KEY,
    customer_id CHAR(36) NOT NULL,
    request_type VARCHAR(32) NULL,
    request_id CHAR(36) NULL,
    notification_type VARCHAR(64) NOT NULL,
    title VARCHAR(160) NOT NULL,
    message VARCHAR(500) NOT NULL,
    link_url VARCHAR(300) NULL,
    read_at DATETIME(6) NULL,
    created_at DATETIME(6) NOT NULL,
    KEY ix_portal_notifications_customer_created (
        customer_id,
        created_at
    ),
    KEY ix_portal_notifications_customer_read (
        customer_id,
        read_at,
        created_at
    ),
    CONSTRAINT fk_portal_notifications_customer
        FOREIGN KEY (customer_id) REFERENCES customers (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;