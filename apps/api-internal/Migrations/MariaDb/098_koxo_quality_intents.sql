-- Etat desire autorise pour KoXo, distinct des appartenances deja appliquees.
-- Revision comparee avant publication et accuse : une execution ancienne
-- ne peut pas confirmer une revision plus recente.
CREATE TABLE IF NOT EXISTS koxo_quality_intents (
    customer_id CHAR(36) NOT NULL,
    revision BIGINT NOT NULL,
    applied_revision BIGINT NOT NULL DEFAULT 0,
    desired_json LONGTEXT NOT NULL,
    updated_at DATETIME(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    applied_at DATETIME(6) NULL,
    lease_token CHAR(36) NULL,
    lease_expires_at DATETIME(6) NULL,
    attempt_count INT NOT NULL DEFAULT 0,
    next_attempt_at DATETIME(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    last_result_code VARCHAR(64) NULL,
    PRIMARY KEY (customer_id),
    KEY idx_koxo_quality_intents_pending (next_attempt_at, lease_expires_at),
    CONSTRAINT fk_koxo_quality_intents_customer FOREIGN KEY (customer_id)
        REFERENCES customers(id) ON UPDATE RESTRICT ON DELETE RESTRICT,
    CONSTRAINT chk_koxo_quality_intents_json CHECK (JSON_VALID(desired_json)),
    CONSTRAINT chk_koxo_quality_intents_revision CHECK
        (revision > 0 AND applied_revision >= 0 AND applied_revision <= revision)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
