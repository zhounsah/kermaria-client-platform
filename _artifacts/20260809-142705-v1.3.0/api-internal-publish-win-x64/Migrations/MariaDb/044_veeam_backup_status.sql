CREATE TABLE IF NOT EXISTS backup_integrations (
    id CHAR(36) NOT NULL,
    provider VARCHAR(32) NOT NULL,
    external_job_id VARCHAR(160) NOT NULL,
    customer_id CHAR(36) NOT NULL,
    service_id CHAR(36) NOT NULL,
    enabled TINYINT(1) NOT NULL DEFAULT 1,
    expected_interval_minutes INT NOT NULL DEFAULT 1440,
    critical_after_minutes INT NOT NULL DEFAULT 2160,
    stale_after_minutes INT NOT NULL DEFAULT 180,
    last_collected_at DATETIME(6) NULL,
    last_collection_status VARCHAR(32) NULL,
    last_collection_message VARCHAR(280) NULL,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_backup_integrations_provider_job (provider, external_job_id),
    KEY idx_backup_integrations_customer_service (customer_id, service_id),
    KEY idx_backup_integrations_collection (enabled, last_collected_at),
    CONSTRAINT fk_backup_integrations_customer
        FOREIGN KEY (customer_id) REFERENCES customers (id),
    CONSTRAINT fk_backup_integrations_service
        FOREIGN KEY (service_id) REFERENCES customer_services (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
-- statement-break

CREATE TABLE IF NOT EXISTS backup_jobs (
    id CHAR(36) NOT NULL,
    customer_id CHAR(36) NOT NULL,
    service_id CHAR(36) NOT NULL,
    provider VARCHAR(32) NOT NULL,
    external_job_id VARCHAR(160) NOT NULL,
    status VARCHAR(32) NOT NULL,
    protection_status VARCHAR(32) NOT NULL,
    last_run_at DATETIME(6) NULL,
    last_success_at DATETIME(6) NULL,
    last_result VARCHAR(32) NULL,
    protected_bytes BIGINT NULL,
    duration_seconds INT NULL,
    retention_days INT NULL,
    next_run_at DATETIME(6) NULL,
    last_error_public VARCHAR(280) NULL,
    collected_at DATETIME(6) NULL,
    last_verified_at DATETIME(6) NULL,
    verification_status VARCHAR(32) NULL,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_backup_jobs_provider_job (provider, external_job_id),
    KEY idx_backup_jobs_customer_service (customer_id, service_id),
    KEY idx_backup_jobs_protection (customer_id, protection_status),
    CONSTRAINT fk_backup_jobs_customer
        FOREIGN KEY (customer_id) REFERENCES customers (id),
    CONSTRAINT fk_backup_jobs_service
        FOREIGN KEY (service_id) REFERENCES customer_services (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
-- statement-break

CREATE TABLE IF NOT EXISTS backup_runs (
    id CHAR(36) NOT NULL,
    backup_job_id CHAR(36) NOT NULL,
    external_session_id VARCHAR(160) NOT NULL,
    started_at DATETIME(6) NOT NULL,
    finished_at DATETIME(6) NULL,
    result VARCHAR(32) NOT NULL,
    protected_bytes BIGINT NULL,
    duration_seconds INT NULL,
    public_message VARCHAR(280) NULL,
    created_at DATETIME(6) NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_backup_runs_job_session (
        backup_job_id,
        external_session_id
    ),
    KEY idx_backup_runs_job_started (backup_job_id, started_at),
    CONSTRAINT fk_backup_runs_job
        FOREIGN KEY (backup_job_id) REFERENCES backup_jobs (id)
        ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
