-- Le signup Cart cree un compte avant la preuve de possession de l'e-mail.
-- La verification est donc materialisee sur l'identite portail, pas deduite
-- d'une session ni d'un libelle frontend.
ALTER TABLE portal_users
    ADD COLUMN IF NOT EXISTS email_verified_at DATETIME(6) NULL AFTER email;
-- statement-break
ALTER TABLE signup_pending
    ADD COLUMN IF NOT EXISTS email_verified_at DATETIME(6) NULL AFTER verification_token_expires_at,
    ADD COLUMN IF NOT EXISTS self_service_flow VARCHAR(32) NULL AFTER user_agent;
-- statement-break
CREATE INDEX IF NOT EXISTS idx_signup_pending_self_service_flow
    ON signup_pending (self_service_flow, status);
-- statement-break
-- Ne jamais deduire une preuve de possession de l'adresse a partir de la
-- date de creation du compte. Les inscriptions historiques ne materialisaient
-- pas cette preuve et l'ancien self-service VPS pouvait la simuler. Elles
-- restent donc explicitement "inconnues" (NULL) et ne sont pas backfillees.
-- L'index sert a retrouver l'unique workflow self-service lie a l'identite
-- portail courante, sans transformer une provenance historique en verification.
CREATE INDEX IF NOT EXISTS idx_signup_pending_approved_user_self_service
    ON signup_pending (approved_user_id, self_service_flow, status);
