CREATE TABLE IF NOT EXISTS customers (
    id CHAR(36) NOT NULL PRIMARY KEY,
    external_reference VARCHAR(80) NOT NULL,
    display_name VARCHAR(200) NOT NULL,
    status VARCHAR(32) NOT NULL,
    billing_email VARCHAR(254) NULL,
    phone VARCHAR(40) NULL,
    address VARCHAR(255) NULL,
    city VARCHAR(160) NULL,
    country VARCHAR(100) NULL,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    UNIQUE KEY ux_customers_external_reference (external_reference)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
-- statement-break

CREATE TABLE IF NOT EXISTS portal_users (
    id CHAR(36) NOT NULL PRIMARY KEY,
    customer_id CHAR(36) NOT NULL,
    identity_provider_subject VARCHAR(255) NOT NULL,
    email VARCHAR(254) NOT NULL,
    display_name VARCHAR(200) NOT NULL,
    status VARCHAR(32) NOT NULL,
    last_login_at DATETIME(6) NULL,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    UNIQUE KEY ux_portal_users_subject (identity_provider_subject),
    KEY ix_portal_users_customer (customer_id),
    CONSTRAINT fk_portal_users_customer
        FOREIGN KEY (customer_id) REFERENCES customers (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
-- statement-break

CREATE TABLE IF NOT EXISTS customer_services (
    id CHAR(36) NOT NULL PRIMARY KEY,
    customer_id CHAR(36) NOT NULL,
    external_reference VARCHAR(80) NOT NULL,
    service_type VARCHAR(64) NOT NULL,
    name VARCHAR(200) NOT NULL,
    status VARCHAR(32) NOT NULL,
    description VARCHAR(1000) NOT NULL,
    started_at DATETIME(6) NULL,
    ended_at DATETIME(6) NULL,
    scope VARCHAR(1000) NOT NULL,
    commercial_terms VARCHAR(80) NOT NULL,
    next_step VARCHAR(500) NULL,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    UNIQUE KEY ux_customer_services_reference (external_reference),
    KEY ix_customer_services_customer_status (customer_id, status),
    CONSTRAINT fk_customer_services_customer
        FOREIGN KEY (customer_id) REFERENCES customers (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
-- statement-break

CREATE TABLE IF NOT EXISTS invoices (
    id CHAR(36) NOT NULL PRIMARY KEY,
    customer_id CHAR(36) NOT NULL,
    external_reference VARCHAR(100) NOT NULL,
    invoice_number VARCHAR(100) NOT NULL,
    status VARCHAR(32) NOT NULL,
    issued_at DATETIME(6) NOT NULL,
    due_at DATETIME(6) NULL,
    period_label VARCHAR(100) NOT NULL,
    currency CHAR(3) NOT NULL,
    subtotal_amount DECIMAL(18,2) NOT NULL,
    tax_amount DECIMAL(18,2) NOT NULL,
    total_amount DECIMAL(18,2) NOT NULL,
    document_reference VARCHAR(255) NULL,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    UNIQUE KEY ux_invoices_external_reference (external_reference),
    UNIQUE KEY ux_invoices_number (invoice_number),
    KEY ix_invoices_customer_status (customer_id, status),
    CONSTRAINT fk_invoices_customer
        FOREIGN KEY (customer_id) REFERENCES customers (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
-- statement-break

CREATE TABLE IF NOT EXISTS service_catalog (
    id CHAR(36) NOT NULL PRIMARY KEY,
    name VARCHAR(200) NOT NULL,
    category VARCHAR(100) NOT NULL,
    description VARCHAR(1000) NOT NULL,
    scope VARCHAR(1000) NOT NULL,
    commercial_terms VARCHAR(80) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    sort_order INT NOT NULL DEFAULT 0,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
-- statement-break

CREATE TABLE IF NOT EXISTS support_requests (
    id CHAR(36) NOT NULL PRIMARY KEY,
    customer_id CHAR(36) NOT NULL,
    created_by_user_id CHAR(36) NULL,
    service_id CHAR(36) NULL,
    reference VARCHAR(100) NOT NULL,
    subject VARCHAR(160) NOT NULL,
    description TEXT NOT NULL,
    priority VARCHAR(32) NOT NULL,
    category VARCHAR(64) NOT NULL,
    status VARCHAR(32) NOT NULL,
    closed_at DATETIME(6) NULL,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    UNIQUE KEY ux_support_requests_reference (reference),
    KEY ix_support_requests_customer_status (customer_id, status),
    CONSTRAINT fk_support_requests_customer
        FOREIGN KEY (customer_id) REFERENCES customers (id),
    CONSTRAINT fk_support_requests_user
        FOREIGN KEY (created_by_user_id) REFERENCES portal_users (id),
    CONSTRAINT fk_support_requests_service
        FOREIGN KEY (service_id) REFERENCES customer_services (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
-- statement-break

CREATE TABLE IF NOT EXISTS service_requests (
    id CHAR(36) NOT NULL PRIMARY KEY,
    customer_id CHAR(36) NOT NULL,
    created_by_user_id CHAR(36) NULL,
    catalog_item_id CHAR(36) NOT NULL,
    reference VARCHAR(100) NOT NULL,
    timeline VARCHAR(32) NOT NULL,
    context TEXT NOT NULL,
    status VARCHAR(32) NOT NULL,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    UNIQUE KEY ux_service_requests_reference (reference),
    KEY ix_service_requests_customer_status (customer_id, status),
    CONSTRAINT fk_service_requests_customer
        FOREIGN KEY (customer_id) REFERENCES customers (id),
    CONSTRAINT fk_service_requests_user
        FOREIGN KEY (created_by_user_id) REFERENCES portal_users (id),
    CONSTRAINT fk_service_requests_catalog
        FOREIGN KEY (catalog_item_id) REFERENCES service_catalog (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
-- statement-break

CREATE TABLE IF NOT EXISTS audit_logs (
    id CHAR(36) NOT NULL PRIMARY KEY,
    occurred_at DATETIME(6) NOT NULL,
    correlation_id VARCHAR(128) NOT NULL,
    actor_user_id CHAR(36) NULL,
    actor_service VARCHAR(100) NOT NULL,
    customer_id CHAR(36) NULL,
    action VARCHAR(120) NOT NULL,
    target_type VARCHAR(100) NULL,
    target_reference VARCHAR(160) NULL,
    outcome VARCHAR(32) NOT NULL,
    reason_code VARCHAR(100) NULL,
    source_address VARCHAR(100) NULL,
    metadata_json JSON NULL,
    KEY ix_audit_logs_correlation (correlation_id),
    KEY ix_audit_logs_occurred_at (occurred_at),
    KEY ix_audit_logs_customer (customer_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
-- statement-break

CREATE TABLE IF NOT EXISTS ad_actions (
    id CHAR(36) NOT NULL PRIMARY KEY,
    customer_id CHAR(36) NULL,
    requested_by_user_id CHAR(36) NULL,
    action_type VARCHAR(100) NOT NULL,
    target_reference VARCHAR(255) NOT NULL,
    requested_at DATETIME(6) NOT NULL,
    started_at DATETIME(6) NULL,
    completed_at DATETIME(6) NULL,
    status VARCHAR(32) NOT NULL,
    result_code VARCHAR(100) NULL,
    correlation_id VARCHAR(128) NOT NULL,
    KEY ix_ad_actions_correlation (correlation_id),
    KEY ix_ad_actions_requested_at (requested_at),
    CONSTRAINT fk_ad_actions_customer
        FOREIGN KEY (customer_id) REFERENCES customers (id),
    CONSTRAINT fk_ad_actions_user
        FOREIGN KEY (requested_by_user_id) REFERENCES portal_users (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
