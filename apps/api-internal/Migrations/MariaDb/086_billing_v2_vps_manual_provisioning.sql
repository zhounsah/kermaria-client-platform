-- Mise en service VPS manuelle V1. Cet axe operationnel est volontairement
-- distinct du paiement Billing V2 et de la validation technique humaine.
-- Il ne declare ni provider, ni worker, ni ressource de virtualisation.
SET NAMES utf8mb4;

-- statement-break

ALTER TABLE billing_v2_vps_technical_requests
    ADD COLUMN IF NOT EXISTS provisioning_status VARCHAR(32) NOT NULL DEFAULT 'pending'
        AFTER technical_status,
    ADD COLUMN IF NOT EXISTS infrastructure_target VARCHAR(255) NULL
        AFTER provisioning_status,
    ADD COLUMN IF NOT EXISTS instance_reference VARCHAR(255) NULL
        AFTER infrastructure_target,
    ADD COLUMN IF NOT EXISTS public_ip_address VARCHAR(45) NULL
        AFTER instance_reference,
    ADD COLUMN IF NOT EXISTS operational_notes TEXT NULL
        AFTER public_ip_address,
    ADD COLUMN IF NOT EXISTS provisioning_started_at DATETIME(6) NULL
        AFTER operational_notes,
    ADD COLUMN IF NOT EXISTS provisioning_started_by_user_id CHAR(36) NULL
        AFTER provisioning_started_at,
    ADD COLUMN IF NOT EXISTS activated_at DATETIME(6) NULL
        AFTER provisioning_started_by_user_id,
    ADD COLUMN IF NOT EXISTS activated_by_user_id CHAR(36) NULL
        AFTER activated_at,
    ADD INDEX IF NOT EXISTS ix_billing_v2_vps_request_provisioning_queue
        (provisioning_status, updated_at),
    ADD CONSTRAINT IF NOT EXISTS chk_billing_v2_vps_request_provisioning_status
        CHECK (provisioning_status IN ('pending', 'provisioning', 'active', 'failed')),
    ADD CONSTRAINT fk_billing_v2_vps_request_provisioning_started_by
        FOREIGN KEY IF NOT EXISTS (provisioning_started_by_user_id)
        REFERENCES portal_users(id)
        ON UPDATE RESTRICT ON DELETE RESTRICT,
    ADD CONSTRAINT fk_billing_v2_vps_request_activated_by
        FOREIGN KEY IF NOT EXISTS (activated_by_user_id)
        REFERENCES portal_users(id)
        ON UPDATE RESTRICT ON DELETE RESTRICT;
