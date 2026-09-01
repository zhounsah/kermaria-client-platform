-- Revue technique VPS post-settlement. Cette migration ne touche ni le
-- checkout, ni le pricing, ni le provisioning : elle conserve seulement la
-- decision humaine qui conditionnera un futur provisioning.
SET NAMES utf8mb4;

-- statement-break

ALTER TABLE billing_v2_vps_technical_requests
    ADD COLUMN IF NOT EXISTS technical_review_pending_at DATETIME(6) NULL
        AFTER technical_status,
    ADD COLUMN IF NOT EXISTS approval_type VARCHAR(16) NULL
        AFTER technical_review_pending_at,
    ADD COLUMN IF NOT EXISTS approved_at DATETIME(6) NULL
        AFTER approval_type,
    ADD COLUMN IF NOT EXISTS approved_by_user_id CHAR(36) NULL
        AFTER approved_at,
    ADD INDEX IF NOT EXISTS ix_billing_v2_vps_request_review_queue
        (technical_status, technical_review_pending_at, updated_at),
    ADD CONSTRAINT fk_billing_v2_vps_request_approved_by
        FOREIGN KEY IF NOT EXISTS (approved_by_user_id)
        REFERENCES portal_users(id)
        ON UPDATE RESTRICT ON DELETE RESTRICT,
    ADD CONSTRAINT IF NOT EXISTS chk_billing_v2_vps_request_approval_type
        CHECK (approval_type IS NULL OR approval_type IN ('human', 'automatic'));