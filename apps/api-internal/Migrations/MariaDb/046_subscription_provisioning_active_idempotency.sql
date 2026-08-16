CREATE TEMPORARY TABLE IF NOT EXISTS tmp_ad_actions_active_idempotency_guard (
    guard_key VARCHAR(16) NOT NULL PRIMARY KEY
);

-- statement-break

DELETE FROM tmp_ad_actions_active_idempotency_guard;

-- statement-break

INSERT INTO tmp_ad_actions_active_idempotency_guard (guard_key)
SELECT 'duplicate'
FROM ad_actions
WHERE idempotency_key_hash IS NOT NULL
  AND status IN ('requested', 'running')
GROUP BY idempotency_key_hash
HAVING COUNT(*) > 1
LIMIT 1;

-- statement-break

INSERT INTO tmp_ad_actions_active_idempotency_guard (guard_key)
SELECT 'duplicate'
FROM tmp_ad_actions_active_idempotency_guard;

-- statement-break

ALTER TABLE ad_actions
    ADD COLUMN IF NOT EXISTS idempotency_active_hash CHAR(64) NULL
    AFTER idempotency_key_hash;

-- statement-break

UPDATE ad_actions
SET idempotency_active_hash = idempotency_key_hash
WHERE idempotency_active_hash IS NULL
  AND idempotency_key_hash IS NOT NULL
  AND status IN ('requested', 'running');

-- statement-break

ALTER TABLE ad_actions
    ADD UNIQUE KEY IF NOT EXISTS ux_ad_actions_active_idempotency
        (idempotency_active_hash);

-- statement-break

DROP TEMPORARY TABLE IF EXISTS tmp_ad_actions_active_idempotency_guard;
