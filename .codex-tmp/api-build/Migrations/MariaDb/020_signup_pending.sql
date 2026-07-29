-- V0.26 : demandes d'inscription self-service (signup).
-- Le jeton de vérification e-mail et le jeton de définition de mot de
-- passe ne sont stockés qu'en hash SHA-256. Aucun mot de passe en clair
-- n'est persisté ici. Renuméroté depuis le 017 du cadrage V0.26 : les
-- 017/018/019 ont été consommés par V0.29 (Stripe).
CREATE TABLE IF NOT EXISTS signup_pending (
    id CHAR(36) NOT NULL PRIMARY KEY,
    status ENUM(
        'email_pending',
        'email_verified',
        'approved',
        'rejected',
        'expired'
    ) NOT NULL DEFAULT 'email_pending',
    company_name VARCHAR(200) NOT NULL,
    contact_name VARCHAR(200) NOT NULL,
    email VARCHAR(320) NOT NULL,
    phone VARCHAR(40) NULL,
    message TEXT NULL,
    verification_token_hash CHAR(64) NULL,
    verification_token_expires_at DATETIME(6) NULL,
    password_setup_token_hash CHAR(64) NULL,
    password_setup_expires_at DATETIME(6) NULL,
    source_address VARCHAR(45) NULL,
    user_agent VARCHAR(500) NULL,
    approved_user_id CHAR(36) NULL,
    approved_customer_id CHAR(36) NULL,
    approved_at DATETIME(6) NULL,
    rejected_at DATETIME(6) NULL,
    rejected_reason VARCHAR(500) NULL,
    -- Pas de DEFAULT/ON UPDATE CURRENT_TIMESTAMP : heure locale serveur,
    -- la convention est UTC et le code écrit toujours ces colonnes (031).
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    UNIQUE KEY uk_signup_email_status (email, status),
    KEY idx_signup_status (status),
    KEY idx_signup_created (created_at),
    KEY idx_signup_verification_hash (verification_token_hash),
    KEY idx_signup_password_hash (password_setup_token_hash)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
