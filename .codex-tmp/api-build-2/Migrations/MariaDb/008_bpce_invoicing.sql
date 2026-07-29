CREATE TABLE IF NOT EXISTS bpce_customers (
    id CHAR(36) NOT NULL PRIMARY KEY,
    customer_id CHAR(36) NOT NULL,
    bpce_customer_id VARCHAR(64) NOT NULL,
    bpce_external_id VARCHAR(100) NOT NULL,
    synced_at DATETIME(6) NOT NULL,
    UNIQUE KEY ux_bpce_customers_customer (customer_id),
    UNIQUE KEY ux_bpce_customers_bpce_id (bpce_customer_id),
    KEY ix_bpce_customers_external (bpce_external_id),
    CONSTRAINT fk_bpce_customers_customer
        FOREIGN KEY (customer_id) REFERENCES customers (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- statement-break

CREATE TABLE IF NOT EXISTS bpce_invoices (
    id CHAR(36) NOT NULL PRIMARY KEY,
    commercial_document_id CHAR(36) NOT NULL,
    bpce_invoice_id VARCHAR(64) NOT NULL,
    bpce_customer_id VARCHAR(64) NOT NULL,
    status VARCHAR(32) NOT NULL,
    fiscal_number VARCHAR(100) NULL,
    issue_date DATE NOT NULL,
    total_amount_cents INT NOT NULL,
    currency CHAR(3) NOT NULL DEFAULT 'EUR',
    pdf_hash CHAR(64) NULL,
    pdf_content LONGBLOB NULL,
    created_at DATETIME(6) NOT NULL,
    validated_at DATETIME(6) NULL,
    UNIQUE KEY ux_bpce_invoices_document (commercial_document_id),
    UNIQUE KEY ux_bpce_invoices_bpce_id (bpce_invoice_id),
    KEY ix_bpce_invoices_status (status),
    CONSTRAINT fk_bpce_invoices_document
        FOREIGN KEY (commercial_document_id) REFERENCES commercial_documents (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

