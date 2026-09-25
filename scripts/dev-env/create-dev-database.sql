-- Environnement DEV Zachary IT : base et comptes dedies.
--
-- Additif uniquement : aucune instruction ne touche `kermaria` ni un compte
-- existant. Les mots de passe sont injectes a l'execution a la place des
-- jetons __DEV_SQL_PASSWORD__ / __DEV_SQL_MIGRATOR_PASSWORD__ (jamais
-- versionnes). Les deux comptes ne sont joignables que depuis SRV-13.
--
-- SRV-06 est primaire de replication sans filtre de binlog : la base et les
-- comptes DEV sont repliques sur les secondaires comme toute autre base.

CREATE DATABASE IF NOT EXISTS `kermaria_dev`
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

-- Compte runtime de l'API DEV : DML seulement, comme en production.
CREATE USER IF NOT EXISTS 'kermaria_dev'@'192.168.100.213'
  IDENTIFIED BY '__DEV_SQL_PASSWORD__';
GRANT SELECT, INSERT, UPDATE, DELETE, EXECUTE
  ON `kermaria_dev`.* TO 'kermaria_dev'@'192.168.100.213';

-- Compte de migration DEV : DDL limite a la base DEV.
CREATE USER IF NOT EXISTS 'kermaria_dev_migrator'@'192.168.100.213'
  IDENTIFIED BY '__DEV_SQL_MIGRATOR_PASSWORD__';
GRANT SELECT, INSERT, UPDATE, DELETE, EXECUTE, CREATE, DROP, REFERENCES,
      INDEX, ALTER, CREATE TEMPORARY TABLES, CREATE VIEW, TRIGGER
  ON `kermaria_dev`.* TO 'kermaria_dev_migrator'@'192.168.100.213';

-- Verification : aucune ligne ne doit viser `kermaria`.* ni *.* hors USAGE.
SHOW GRANTS FOR 'kermaria_dev'@'192.168.100.213';
SHOW GRANTS FOR 'kermaria_dev_migrator'@'192.168.100.213';
