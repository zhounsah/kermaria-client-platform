-- V0.38 : alignement identite site -> AD `clients.home.bzh`.
-- Le workflow reste mono-utilisateur dans cette tranche, mais le modele
-- stocke des donnees client/utilisateur structurees et des etats AD/KoXo.

ALTER TABLE customers
    ADD COLUMN IF NOT EXISTS customer_type VARCHAR(32) NOT NULL DEFAULT 'professional' AFTER status,
    ADD COLUMN IF NOT EXISTS address_line_1 VARCHAR(255) NULL DEFAULT NULL AFTER phone,
    ADD COLUMN IF NOT EXISTS address_line_2 VARCHAR(255) NULL DEFAULT NULL AFTER address_line_1,
    ADD COLUMN IF NOT EXISTS postal_code VARCHAR(32) NULL DEFAULT NULL AFTER address_line_2;

-- statement-break

ALTER TABLE portal_users
    ADD COLUMN IF NOT EXISTS personal_title VARCHAR(32) NULL DEFAULT NULL AFTER role,
    ADD COLUMN IF NOT EXISTS given_name VARCHAR(120) NULL DEFAULT NULL AFTER personal_title,
    ADD COLUMN IF NOT EXISTS surname VARCHAR(120) NULL DEFAULT NULL AFTER given_name,
    ADD COLUMN IF NOT EXISTS initials VARCHAR(16) NULL DEFAULT NULL AFTER surname,
    ADD COLUMN IF NOT EXISTS phone VARCHAR(40) NULL DEFAULT NULL AFTER initials,
    ADD COLUMN IF NOT EXISTS is_primary_contact BOOLEAN NOT NULL DEFAULT TRUE AFTER phone;

-- statement-break

ALTER TABLE signup_pending
    ADD COLUMN IF NOT EXISTS customer_type VARCHAR(32) NULL DEFAULT NULL AFTER message,
    ADD COLUMN IF NOT EXISTS address_line_1 VARCHAR(255) NULL DEFAULT NULL AFTER customer_type,
    ADD COLUMN IF NOT EXISTS address_line_2 VARCHAR(255) NULL DEFAULT NULL AFTER address_line_1,
    ADD COLUMN IF NOT EXISTS postal_code VARCHAR(32) NULL DEFAULT NULL AFTER address_line_2,
    ADD COLUMN IF NOT EXISTS city_structured VARCHAR(160) NULL DEFAULT NULL AFTER postal_code,
    ADD COLUMN IF NOT EXISTS country_structured VARCHAR(100) NULL DEFAULT NULL AFTER city_structured,
    ADD COLUMN IF NOT EXISTS personal_title VARCHAR(32) NULL DEFAULT NULL AFTER country_structured,
    ADD COLUMN IF NOT EXISTS given_name VARCHAR(120) NULL DEFAULT NULL AFTER personal_title,
    ADD COLUMN IF NOT EXISTS surname VARCHAR(120) NULL DEFAULT NULL AFTER given_name,
    ADD COLUMN IF NOT EXISTS initials VARCHAR(16) NULL DEFAULT NULL AFTER surname,
    ADD COLUMN IF NOT EXISTS is_primary_contact BOOLEAN NOT NULL DEFAULT TRUE AFTER initials;

-- statement-break

ALTER TABLE customer_ad_links
    ADD COLUMN IF NOT EXISTS portal_user_id CHAR(36) NULL DEFAULT NULL AFTER customer_id,
    ADD COLUMN IF NOT EXISTS ad_domain VARCHAR(255) NULL DEFAULT NULL AFTER portal_user_id,
    ADD COLUMN IF NOT EXISTS ad_provisioning_status VARCHAR(32) NULL DEFAULT NULL AFTER ad_domain,
    ADD COLUMN IF NOT EXISTS ad_provisioned_at DATETIME(6) NULL DEFAULT NULL AFTER ad_provisioning_status,
    ADD COLUMN IF NOT EXISTS last_password_sync_at DATETIME(6) NULL DEFAULT NULL AFTER ad_provisioned_at,
    ADD COLUMN IF NOT EXISTS last_password_sync_status VARCHAR(32) NULL DEFAULT NULL AFTER last_password_sync_at,
    ADD COLUMN IF NOT EXISTS koxo_export_status VARCHAR(32) NULL DEFAULT NULL AFTER last_password_sync_status,
    ADD COLUMN IF NOT EXISTS koxo_export_attempt_count INT NOT NULL DEFAULT 0 AFTER koxo_export_status,
    ADD COLUMN IF NOT EXISTS koxo_export_last_attempt_at DATETIME(6) NULL DEFAULT NULL AFTER koxo_export_attempt_count,
    ADD COLUMN IF NOT EXISTS koxo_export_last_success_at DATETIME(6) NULL DEFAULT NULL AFTER koxo_export_last_attempt_at,
    ADD COLUMN IF NOT EXISTS koxo_export_last_error_code VARCHAR(100) NULL DEFAULT NULL AFTER koxo_export_last_success_at,
    ADD COLUMN IF NOT EXISTS koxo_export_last_error_message VARCHAR(500) NULL DEFAULT NULL AFTER koxo_export_last_error_code,
    ADD COLUMN IF NOT EXISTS koxo_artifact_type VARCHAR(16) NULL DEFAULT NULL AFTER koxo_export_last_error_message,
    ADD COLUMN IF NOT EXISTS koxo_artifact_path VARCHAR(1000) NULL DEFAULT NULL AFTER koxo_artifact_type,
    ADD COLUMN IF NOT EXISTS koxo_artifact_checksum CHAR(64) NULL DEFAULT NULL AFTER koxo_artifact_path,
    ADD COLUMN IF NOT EXISTS koxo_correlation_id VARCHAR(128) NULL DEFAULT NULL AFTER koxo_artifact_checksum;

-- statement-break

ALTER TABLE customer_ad_links
    ADD KEY ix_customer_ad_links_portal_user (portal_user_id),
    ADD KEY ix_customer_ad_links_provisioning (ad_provisioning_status, koxo_export_status),
    ADD UNIQUE KEY ux_customer_ad_links_portal_user_type (portal_user_id, object_type),
    ADD CONSTRAINT fk_customer_ad_links_portal_user
        FOREIGN KEY (portal_user_id) REFERENCES portal_users (id);
