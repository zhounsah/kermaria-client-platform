ALTER TABLE signup_pending
    ADD COLUMN IF NOT EXISTS birth_date DATE NULL DEFAULT NULL AFTER surname;

ALTER TABLE portal_users
    ADD COLUMN IF NOT EXISTS birth_date DATE NULL DEFAULT NULL AFTER surname,
    ADD COLUMN IF NOT EXISTS koxo_unique_identifier VARCHAR(32) NULL DEFAULT NULL AFTER birth_date;

CREATE UNIQUE INDEX IF NOT EXISTS uk_portal_users_koxo_unique_identifier
    ON portal_users (koxo_unique_identifier);

CREATE TABLE IF NOT EXISTS koxo_identifier_counters (
    counter_name VARCHAR(64) NOT NULL,
    next_value BIGINT NOT NULL,
    PRIMARY KEY (counter_name)
);

CREATE TEMPORARY TABLE tmp_koxo_backfill_seed (
    base_value BIGINT NOT NULL
);

INSERT INTO tmp_koxo_backfill_seed (
    base_value
)
SELECT COALESCE(
    MAX(CAST(SUBSTRING(koxo_unique_identifier, 5) AS UNSIGNED)),
    0
)
FROM portal_users
WHERE koxo_unique_identifier IS NOT NULL;

CREATE TEMPORARY TABLE tmp_koxo_backfill_users (
    sequence_id BIGINT NOT NULL AUTO_INCREMENT,
    portal_user_id CHAR(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    PRIMARY KEY (sequence_id),
    UNIQUE KEY uk_tmp_koxo_backfill_users_portal_user_id (portal_user_id)
);

INSERT INTO tmp_koxo_backfill_users (
    portal_user_id
)
SELECT id
FROM portal_users
WHERE koxo_unique_identifier IS NULL
ORDER BY created_at ASC, id ASC;

UPDATE portal_users portal_user
INNER JOIN tmp_koxo_backfill_users backfill_user
    ON backfill_user.portal_user_id = portal_user.id
INNER JOIN tmp_koxo_backfill_seed backfill_seed
    ON 1 = 1
SET portal_user.koxo_unique_identifier = CONCAT(
    'CLI-',
    LPAD(backfill_seed.base_value + backfill_user.sequence_id, 6, '0')
)
WHERE portal_user.koxo_unique_identifier IS NULL;

INSERT INTO koxo_identifier_counters (
    counter_name,
    next_value
) VALUES (
    'portal_user',
    (
        SELECT COALESCE(
            MAX(CAST(SUBSTRING(koxo_unique_identifier, 5) AS UNSIGNED)),
            0
        ) + 1
        FROM portal_users
        WHERE koxo_unique_identifier IS NOT NULL
    )
)
ON DUPLICATE KEY UPDATE
    next_value = GREATEST(next_value, VALUES(next_value));

DROP TEMPORARY TABLE IF EXISTS tmp_koxo_backfill_users;
DROP TEMPORARY TABLE IF EXISTS tmp_koxo_backfill_seed;

CREATE TABLE IF NOT EXISTS koxo_export_runs (
    id CHAR(36) NOT NULL,
    source VARCHAR(32) NOT NULL,
    status VARCHAR(32) NOT NULL,
    schema_version INT NULL,
    user_count INT NOT NULL DEFAULT 0,
    invalid_user_count INT NOT NULL DEFAULT 0,
    correlation_id VARCHAR(128) NOT NULL,
    source_address VARCHAR(128) NULL,
    summary_message VARCHAR(500) NOT NULL,
    generated_at DATETIME(6) NULL,
    preview_json LONGTEXT NULL,
    validation_errors_json LONGTEXT NULL,
    created_at DATETIME(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    PRIMARY KEY (id),
    KEY ix_koxo_export_runs_created_at (created_at),
    KEY ix_koxo_export_runs_status (status, created_at),
    KEY ix_koxo_export_runs_source (source, created_at)
);
