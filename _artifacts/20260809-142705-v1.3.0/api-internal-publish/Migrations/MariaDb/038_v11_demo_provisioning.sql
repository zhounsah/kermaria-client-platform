-- V1.1 Lot 3 : essai reel (usage 2). Trace du cycle de provisioning/revocation
-- des comptes de demonstration de type 'trial'.
--
-- demo_provisioned_at : horodatage (UTC) du declenchement du provisioning reel
--   (chaine KoXo + ajout des groupes GG_DEMO_*), pose a la creation d'un trial.
-- demo_revoked_at : horodatage (UTC) de la revocation a l'echeance (retrait AD
--   direct des GG_DEMO_* + desactivation). Sert de garde-fou d'idempotence : le
--   balayage d'expiration ne re-revoque pas un compte deja traite, et l'admin
--   distingue un compte expire-revoque d'un compte encore actif.
--
-- Additif, non destructif. Ces colonnes restent NULL pour un vrai client et
-- pour un compte de demo 'showcase' (jamais provisionne, jamais revoque).

ALTER TABLE customers
    ADD COLUMN IF NOT EXISTS demo_provisioned_at DATETIME(6) NULL DEFAULT NULL AFTER demo_created_by_user_id,
    ADD COLUMN IF NOT EXISTS demo_revoked_at DATETIME(6) NULL DEFAULT NULL AFTER demo_provisioned_at;

-- statement-break

-- Balayage de revocation : cible les trials echus non encore revoques.
CREATE INDEX IF NOT EXISTS ix_customers_demo_revoke
    ON customers (is_demo, demo_kind, demo_revoked_at, demo_expires_at);
