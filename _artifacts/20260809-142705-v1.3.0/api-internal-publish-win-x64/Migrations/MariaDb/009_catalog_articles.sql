ALTER TABLE commercial_offers
    ADD COLUMN IF NOT EXISTS tax_rate_basis_points INT NULL DEFAULT NULL;

-- statement-break

ALTER TABLE commercial_offers
    ADD COLUMN IF NOT EXISTS external_reference VARCHAR(80) NULL DEFAULT NULL;

-- statement-break

ALTER TABLE commercial_offers
    ADD UNIQUE KEY IF NOT EXISTS ux_commercial_offers_external_reference
        (external_reference);

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'Audit sÃ©curitÃ© de base', 'VÃ©rification de base de la configuration de sÃ©curitÃ©\nContrÃ´le des accÃ¨s, mots de passe, sauvegardes et exposition rÃ©seau\nCompte rendu synthÃ©tique\nPrestation ponctuelle', 'Audit', 'Forfait', 'ht', 4000, 'EUR', 2000, 'AUDIT-SECU-BASE', 'active', 10, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'AUDIT-SECU-BASE');

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'Configuration poste utilisateur', 'Configuration de base d''un poste utilisateur\nParamÃ©trage rÃ©seau, accÃ¨s distant ou logiciels nÃ©cessaires\nVÃ©rification du bon fonctionnement\nPrestation ponctuelle', 'Configuration', 'Forfait', 'ht', 1500, 'EUR', 2000, 'CONFIG-POSTE', 'active', 20, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'CONFIG-POSTE');

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'Configuration accÃ¨s VPN', 'Configuration initiale du profil VPN utilisateur\nInstallation ou assistance Ã  la configuration sur un appareil\nVÃ©rification de la connexion\nPrestation ponctuelle', 'Configuration', 'Forfait', 'ht', 1000, 'EUR', 2000, 'CONFIG-VPN', 'active', 30, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'CONFIG-VPN');

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'Mise en service initiale', 'CrÃ©ation et configuration initiale du service souscrit\nConfiguration du compte utilisateur\nVÃ©rification du bon fonctionnement\nPrestation ponctuelle', 'Configuration', 'Forfait', 'ht', 1500, 'EUR', 2000, 'INIT-SERVICE', 'active', 40, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'INIT-SERVICE');

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'Intervention technique ponctuelle', 'Intervention technique Ã  distance\nDiagnostic, configuration ou dÃ©pannage selon la demande\nHors abonnement mensuel', 'Prestation', 'Heure', 'ht', 2500, 'EUR', 2000, 'INTERV-PONCT', 'active', 50, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'INTERV-PONCT');

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'Restauration de sauvegarde', 'Restauration de donnÃ©es depuis une sauvegarde disponible\nVÃ©rification de l''accÃ¨s aux donnÃ©es restaurÃ©es\nPrestation ponctuelle', 'Prestation', 'Forfait', 'ht', 1000, 'EUR', 2000, 'RESTORE-SAVE', 'active', 60, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'RESTORE-SAVE');

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'Migration de donnÃ©es', 'Transfert de donnÃ©es vers le service souscrit\nOrganisation de base des dossiers\nVÃ©rification de l''accÃ¨s aux donnÃ©es transfÃ©rÃ©es\nPrestation ponctuelle', 'Prestation', 'Forfait', 'ht', 2000, 'EUR', 2000, 'MIG-DATA', 'active', 70, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'MIG-DATA');

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'Configuration appareil supplÃ©mentaire', 'Configuration d''un appareil supplÃ©mentaire\nParamÃ©trage de l''accÃ¨s aux services souscrits\nVÃ©rification du bon fonctionnement\nPrestation ponctuelle', 'Configuration', 'Forfait', 'ht', 1000, 'EUR', 2000, 'CONFIG-DEVICE-ADD', 'active', 80, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'CONFIG-DEVICE-ADD');

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'Documentation technique simplifiÃ©e', 'RÃ©daction d''une documentation synthÃ©tique liÃ©e au service configurÃ©\nProcÃ©dure d''accÃ¨s, informations utiles et consignes de base\nPrestation ponctuelle', 'Prestation', 'Forfait', 'ht', 1500, 'EUR', 2000, 'DOC-TECH', 'active', 90, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'DOC-TECH');

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'AccÃ¨s VPN sÃ©curisÃ©', 'AccÃ¨s VPN personnel sÃ©curisÃ©\nConfiguration du profil utilisateur\nMaintenance de l''accÃ¨s incluse\nAssistance de connexion incluse\nFacturation mensuelle', 'Abonnement', 'Mois', 'ht', 500, 'EUR', 2000, 'ACCES-VPN', 'active', 100, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'ACCES-VPN');

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'AccÃ¨s bureau distant RDS', 'AccÃ¨s Ã  un environnement Windows distant\nConfiguration du compte utilisateur\nMaintenance de l''accÃ¨s distant incluse\nFacturation mensuelle', 'Abonnement', 'Mois', 'ht', 1500, 'EUR', 2000, 'ACCES-RDS', 'active', 110, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'ACCES-RDS');

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'Sauvegarde dossier personnel', 'Sauvegarde rÃ©guliÃ¨re selon la politique technique dÃ©finie\nConservation selon la politique de sauvegarde dÃ©finie\nRestauration sur demande\nFacturation mensuelle', 'Abonnement', 'Mois', 'ht', 200, 'EUR', 2000, 'SAVE-PERSO', 'active', 120, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'SAVE-PERSO');

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'HÃ©bergement dossier personnel 32 Go', 'Espace de stockage personnel de 32 Go\nAccÃ¨s distant sÃ©curisÃ©\nMaintenance technique minimale du service incluse\nFacturation mensuelle', 'Abonnement', 'Mois', 'ht', 500, 'EUR', 2000, 'STOCK-PERSO-32', 'active', 130, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'STOCK-PERSO-32');

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'Stockage supplÃ©mentaire 32 Go', 'Extension de l''espace de stockage personnel de 32 Go supplÃ©mentaires\nOption associÃ©e Ã  un forfait d''hÃ©bergement existant\nFacturation mensuelle', 'Abonnement', 'Mois', 'ht', 300, 'EUR', 2000, 'STOCK-SUP-32', 'active', 140, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'STOCK-SUP-32');

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'Supervision de service', 'Surveillance de la disponibilitÃ© du service\nNotification en cas d''indisponibilitÃ© dÃ©tectÃ©e\nVÃ©rification pÃ©riodique du bon fonctionnement\nFacturation mensuelle', 'Abonnement', 'Mois', 'ht', 300, 'EUR', 2000, 'SUPERV-SERVICE', 'active', 150, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'SUPERV-SERVICE');

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'Support technique niveau 1', 'Assistance technique de base par message ou prise en main Ã  distance\nSupport limitÃ© aux services souscrits\nInterventions rÃ©alisÃ©es selon disponibilitÃ©\nFacturation mensuelle', 'Abonnement', 'Mois', 'ht', 500, 'EUR', 2000, 'SUPPORT-LV1', 'active', 160, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'SUPPORT-LV1');

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'Compte utilisateur supplÃ©mentaire', 'CrÃ©ation et configuration d''un compte utilisateur supplÃ©mentaire\nParamÃ©trage des accÃ¨s aux services souscrits\nFacturation mensuelle', 'Abonnement', 'Mois', 'ht', 300, 'EUR', 2000, 'USER-ADD', 'active', 170, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'USER-ADD');
