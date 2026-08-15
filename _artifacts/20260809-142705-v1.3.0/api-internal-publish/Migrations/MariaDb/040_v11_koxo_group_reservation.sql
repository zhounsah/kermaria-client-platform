-- V1.1 Lot 5 : reservation du code de groupe KoXo pour les comptes de demo.
-- Additif, non destructif.
--
-- KoXo place une identite dans l'OU nommee d'apres le champ « GroupeSecondaire »
-- de l'export, et cree cette OU si elle n'existe pas. On s'appuie sur ce seul
-- levier pour la conversion essai -> reel : aucun deplacement AD n'est fait par
-- l'application.
--
-- Le code definitif (CLI-XXXXXX) est donc alloue des la creation du compte de
-- demo, mais RETENU : tant que le compte est en demonstration, l'export publie
-- « CLI-DEMO » et l'identite reste dans l'OU de demonstration commune. La
-- conversion se contente de publier le code reserve ; KoXo cree alors l'OU
-- cible et prend la main sur l'arborescence.
--
-- Interet principal : la reference client (customers.external_reference) ne
-- change JAMAIS. Sans cette reservation, convertir imposerait de renommer la
-- reference, ce qui cascaderait sur les factures, documents et abonnements.
--
-- Colonne NULL pour les clients reels ordinaires : leur OU est deja nommee
-- d'apres leur reference, il n'y a rien a reserver.

ALTER TABLE customers
    ADD COLUMN IF NOT EXISTS koxo_group_reference VARCHAR(32) NULL DEFAULT NULL AFTER demo_source_profile_key;

-- statement-break

-- Le code reserve doit etre unique au meme titre qu'une reference client : il en
-- devient une a la conversion. MariaDB autorise les NULL multiples sous index
-- unique, ce qui laisse les clients reels ordinaires hors contrainte.
CREATE UNIQUE INDEX IF NOT EXISTS ux_customers_koxo_group_reference
    ON customers (koxo_group_reference);
