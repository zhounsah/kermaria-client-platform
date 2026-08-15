-- V1.1 : comptes de demonstration et d'essai personnalises.
-- Additif, non destructif. Marque les comptes de demo/essai, permet de les
-- isoler des vues metier reelles, et introduit le registre administrable des
-- profils de demo (axe A contenu + axe B capacites + cycle de vie).
--
-- demo_kind : 'showcase' (vitrine inerte, usages 1 & 3) | 'trial' (essai reel
-- restreint, usage 2). Un compte n'est demo que si is_demo = TRUE ; les autres
-- colonnes demo_* restent NULL pour un vrai client.

ALTER TABLE customers
    ADD COLUMN IF NOT EXISTS is_demo BOOLEAN NOT NULL DEFAULT FALSE AFTER customer_type,
    ADD COLUMN IF NOT EXISTS demo_profile_id CHAR(36) NULL DEFAULT NULL AFTER is_demo,
    ADD COLUMN IF NOT EXISTS demo_kind VARCHAR(16) NULL DEFAULT NULL AFTER demo_profile_id,
    ADD COLUMN IF NOT EXISTS demo_expires_at DATETIME(6) NULL DEFAULT NULL AFTER demo_kind,
    ADD COLUMN IF NOT EXISTS demo_created_by_user_id CHAR(36) NULL DEFAULT NULL AFTER demo_expires_at;

-- statement-break

-- Registre administrable des profils de demo. Chaque profil fige un template de
-- contenu (axe A) + une matrice de capacites (axe B) + une duree de vie.
CREATE TABLE IF NOT EXISTS demo_profiles (
    id CHAR(36) NOT NULL PRIMARY KEY,
    profile_key VARCHAR(64) NOT NULL,
    label VARCHAR(200) NOT NULL,
    kind VARCHAR(16) NOT NULL,
    content_template_key VARCHAR(64) NULL DEFAULT NULL,
    email_mode VARCHAR(32) NOT NULL DEFAULT 'off',
    bpce_mode VARCHAR(32) NOT NULL DEFAULT 'off',
    payment_mode VARCHAR(32) NOT NULL DEFAULT 'off',
    ad_provisioning_mode VARCHAR(32) NOT NULL DEFAULT 'off',
    ad_groups_json TEXT NULL DEFAULT NULL,
    storage_quota_go INT NULL DEFAULT NULL,
    rds_session_mode VARCHAR(16) NOT NULL DEFAULT 'off',
    lifetime_days INT NOT NULL DEFAULT 14,
    status VARCHAR(16) NOT NULL DEFAULT 'active',
    created_at DATETIME(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    UNIQUE KEY ux_demo_profiles_key (profile_key)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- statement-break

CREATE INDEX IF NOT EXISTS ix_customers_is_demo
    ON customers (is_demo);

-- statement-break

-- Balayage d'expiration : cible les comptes demo dont l'echeance est passee.
CREATE INDEX IF NOT EXISTS ix_customers_demo_expiry
    ON customers (is_demo, demo_expires_at);

-- statement-break

CREATE INDEX IF NOT EXISTS ix_customers_demo_profile
    ON customers (demo_profile_id);
