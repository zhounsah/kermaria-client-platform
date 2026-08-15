CREATE TABLE IF NOT EXISTS email_messages (
    id CHAR(36) NOT NULL PRIMARY KEY,
    template VARCHAR(64) NOT NULL,
    recipient VARCHAR(254) NOT NULL,
    subject VARCHAR(255) NOT NULL,
    body MEDIUMTEXT NOT NULL,
    status VARCHAR(32) NOT NULL,
    error_message TEXT NULL,
    related_document_id CHAR(36) NULL,
    correlation_id VARCHAR(64) NOT NULL,
    created_at DATETIME(6) NOT NULL,
    sent_at DATETIME(6) NULL,
    KEY ix_email_messages_status (status),
    KEY ix_email_messages_document (related_document_id),
    KEY ix_email_messages_created (created_at),
    CONSTRAINT fk_email_messages_document
        FOREIGN KEY (related_document_id) REFERENCES commercial_documents (id)
        ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
