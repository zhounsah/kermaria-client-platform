CREATE TABLE IF NOT EXISTS data_subject_requests (
    id CHAR(36) NOT NULL PRIMARY KEY,
    reference VARCHAR(32) NOT NULL,
    customer_id CHAR(36) NOT NULL,
    user_id CHAR(36) NOT NULL,
    request_type VARCHAR(32) NOT NULL,
    details TEXT NOT NULL,
    response_file_name VARCHAR(180) NULL,
    response_content_type VARCHAR(80) NULL,
    response_file_data MEDIUMBLOB NULL,
    status VARCHAR(32) NOT NULL,
    due_at DATETIME(6) NOT NULL,
    deadline_extended_at DATETIME(6) NULL,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    UNIQUE KEY ux_data_subject_requests_reference (reference),
    KEY ix_data_subject_requests_customer (customer_id, created_at),
    KEY ix_data_subject_requests_admin (status, due_at),
    CONSTRAINT fk_data_subject_requests_customer FOREIGN KEY (customer_id)
        REFERENCES customers(id) ON UPDATE RESTRICT ON DELETE RESTRICT,
    CONSTRAINT fk_data_subject_requests_user FOREIGN KEY (user_id)
        REFERENCES portal_users(id) ON UPDATE RESTRICT ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
-- statement-break

ALTER TABLE portal_notifications
    ADD COLUMN user_id CHAR(36) NULL,
    ADD KEY ix_portal_notifications_user_created (user_id, created_at),
    ADD CONSTRAINT fk_portal_notifications_user FOREIGN KEY (user_id)
        REFERENCES portal_users(id) ON UPDATE RESTRICT ON DELETE RESTRICT;
-- statement-break

CREATE TABLE IF NOT EXISTS data_subject_request_messages (
    id CHAR(36) NOT NULL PRIMARY KEY,
    request_id CHAR(36) NOT NULL,
    author_role VARCHAR(16) NOT NULL,
    body TEXT NOT NULL,
    created_at DATETIME(6) NOT NULL,
    KEY ix_data_subject_request_messages_request (request_id, created_at),
    CONSTRAINT fk_data_subject_request_messages_request FOREIGN KEY (request_id)
        REFERENCES data_subject_requests(id) ON UPDATE RESTRICT ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
