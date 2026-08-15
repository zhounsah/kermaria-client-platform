-- V0.35.1 : durcissement UTC.
-- signup_pending portait DEFAULT CURRENT_TIMESTAMP(6) / ON UPDATE
-- CURRENT_TIMESTAMP(6), soit l'heure LOCALE du serveur MariaDB (Paris)
-- alors que la convention du projet stocke tous les horodatages en UTC.
-- Le code applicatif (MariaDbSignupRepository) écrit déjà explicitement
-- created_at/updated_at en UTC_TIMESTAMP(6) sur chaque INSERT/UPDATE :
-- supprimer les défauts est sans impact fonctionnel et neutralise le
-- piège pour tout futur UPDATE qui omettrait updated_at.
ALTER TABLE signup_pending
    MODIFY created_at DATETIME(6) NOT NULL,
    MODIFY updated_at DATETIME(6) NOT NULL;
