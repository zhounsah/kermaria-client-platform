-- V1.1 Lot 4 : conversion d'un compte d'essai en client reel.
-- Additif, non destructif. La conversion est une BASCULE SUR PLACE : le compte
-- garde son identite, son contenu et son historique ; seuls les marqueurs de
-- demonstration sont leves.
--
-- Apres conversion : is_demo = FALSE et les colonnes demo_profile_id / demo_kind
-- / demo_expires_at sont remises a NULL, ce qui sort definitivement le compte du
-- balayage d'expiration et de la purge. On conserve donc la provenance dans
-- demo_source_profile_key, sinon la trace serait perdue.
--
-- demo_converted_at sert aussi de garde d'idempotence : une conversion rejouee
-- (echec partiel cote AD, double clic admin) ne repasse pas par la bascule.

ALTER TABLE customers
    ADD COLUMN IF NOT EXISTS demo_converted_at DATETIME(6) NULL DEFAULT NULL AFTER demo_revoked_at,
    ADD COLUMN IF NOT EXISTS demo_converted_by_user_id CHAR(36) NULL DEFAULT NULL AFTER demo_converted_at,
    ADD COLUMN IF NOT EXISTS demo_source_profile_key VARCHAR(64) NULL DEFAULT NULL AFTER demo_converted_by_user_id;

-- statement-break

-- Retrouver les comptes issus d'une demo convertie (reporting, support).
CREATE INDEX IF NOT EXISTS ix_customers_demo_converted
    ON customers (demo_converted_at);
