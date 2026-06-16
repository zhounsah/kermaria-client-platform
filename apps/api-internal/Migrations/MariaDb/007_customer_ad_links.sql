CREATE TABLE IF NOT EXISTS customer_ad_links (
    id CHAR(36) NOT NULL PRIMARY KEY,
    customer_id CHAR(36) NOT NULL,
    object_guid CHAR(36) NOT NULL,
    object_sid VARCHAR(255) NOT NULL,
    object_type VARCHAR(32) NOT NULL,
    sam_account_name VARCHAR(128) NOT NULL,
    user_principal_name VARCHAR(255) NULL,
    display_name VARCHAR(255) NOT NULL,
    distinguished_name VARCHAR(1000) NOT NULL,
    linked_at DATETIME(6) NOT NULL,
    linked_by_user_id CHAR(36) NULL,
    UNIQUE KEY ux_customer_ad_links_object_guid (object_guid),
    KEY ix_customer_ad_links_customer (customer_id),
    KEY ix_customer_ad_links_customer_type (customer_id, object_type),
    CONSTRAINT fk_customer_ad_links_customer
        FOREIGN KEY (customer_id) REFERENCES customers (id),
    CONSTRAINT fk_customer_ad_links_user
        FOREIGN KEY (linked_by_user_id) REFERENCES portal_users (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
