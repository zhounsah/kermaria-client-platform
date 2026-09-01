-- Préparation durable non secrète du tunnel VPS, avant checkout.
-- Cette migration ne crée aucun objet de paiement, aucun abonnement, aucun
-- BillingEvent et aucune action de provisioning.
SET NAMES utf8mb4;

-- statement-break

CREATE TABLE IF NOT EXISTS billing_v2_vps_technical_requests (
    id CHAR(36) NOT NULL PRIMARY KEY,
    customer_id CHAR(36) NOT NULL,
    requested_by_user_id CHAR(36) NOT NULL,
    service_code VARCHAR(64) NOT NULL,
    tier_code VARCHAR(64) NOT NULL,
    selection_canonical TEXT NOT NULL,
    selection_fingerprint CHAR(64) NOT NULL,
    technical_status VARCHAR(32) NOT NULL DEFAULT 'draft',
    current_revision INT UNSIGNED NOT NULL,
    configuration_hash CHAR(64) NOT NULL,
    idempotency_key VARCHAR(128) NOT NULL,
    request_fingerprint_hash CHAR(64) NOT NULL,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    UNIQUE KEY ux_billing_v2_vps_request_customer_idempotency (customer_id, idempotency_key),
    KEY ix_billing_v2_vps_request_customer_status (customer_id, technical_status, updated_at),
    CONSTRAINT chk_billing_v2_vps_request_status CHECK (technical_status IN ('draft', 'pending_review', 'changes_required', 'approved', 'rejected', 'superseded')),
    CONSTRAINT fk_billing_v2_vps_request_customer FOREIGN KEY (customer_id) REFERENCES customers(id),
    CONSTRAINT fk_billing_v2_vps_request_user FOREIGN KEY (requested_by_user_id) REFERENCES portal_users(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- statement-break

CREATE TABLE IF NOT EXISTS billing_v2_vps_technical_request_revisions (
    id CHAR(36) NOT NULL PRIMARY KEY,
    technical_request_id CHAR(36) NOT NULL,
    revision_number INT UNSIGNED NOT NULL,
    hostname VARCHAR(253) NOT NULL,
    operating_system VARCHAR(120) NOT NULL,
    usage_description TEXT NOT NULL,
    management_mode VARCHAR(120) NOT NULL,
    internet_exposure VARCHAR(32) NOT NULL,
    comment_text TEXT NOT NULL,
    configuration_hash CHAR(64) NOT NULL,
    selection_fingerprint CHAR(64) NOT NULL,
    created_by_user_id CHAR(36) NOT NULL,
    created_at DATETIME(6) NOT NULL,
    UNIQUE KEY ux_billing_v2_vps_request_revision (technical_request_id, revision_number),
    CONSTRAINT chk_billing_v2_vps_revision_exposure CHECK (internet_exposure IN ('yes', 'no', 'to_confirm')),
    CONSTRAINT fk_billing_v2_vps_revision_request FOREIGN KEY (technical_request_id) REFERENCES billing_v2_vps_technical_requests(id),
    CONSTRAINT fk_billing_v2_vps_revision_user FOREIGN KEY (created_by_user_id) REFERENCES portal_users(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
