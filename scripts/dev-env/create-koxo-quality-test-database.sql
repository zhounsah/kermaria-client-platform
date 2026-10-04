-- A executer par un administrateur SQL sur KERMARIA-SRV-06.home.bzh.
-- Cible jetable autorisee : kermaria_koxo_quality_test_dev uniquement.
-- Arreter a la premiere erreur. Aucune donnee operationnelle n'est copiee.
-- Le compte DEV existant est restreint a la source SRV-13 ; aucun mot de passe
-- n'est fourni ou modifie. NO_AUTO_CREATE_USER interdit une creation implicite
-- sans secret si ce compte n'existe pas.
SET SESSION sql_mode = CONCAT_WS(',', @@sql_mode, 'NO_AUTO_CREATE_USER');

CREATE DATABASE kermaria_koxo_quality_test_dev
    CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

GRANT SELECT, INSERT, UPDATE, CREATE, REFERENCES
    ON kermaria_koxo_quality_test_dev.*
    TO 'kermaria_dev_migrator'@'192.168.100.213';

-- Les fixtures du test restent dans cette base pour inspection.
-- Ni DROP ni DELETE ni droit global n'est necessaire pour cette recette.
