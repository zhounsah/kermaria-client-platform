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
SELECT UUID(), 'Audit sécurité de base', 'Vérification de base de la configuration de sécurité\nContrôle des accès, mots de passe, sauvegardes et exposition réseau\nCompte rendu synthétique\nPrestation ponctuelle', 'Audit', 'Forfait', 'ht', 4000, 'EUR', 2000, 'AUDIT-SECU-BASE', 'active', 10, NOW(6), NOW(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'AUDIT-SECU-BASE');

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'Configuration poste utilisateur', 'Configuration de base d''un poste utilisateur\nParamétrage réseau, accès distant ou logiciels nécessaires\nVérification du bon fonctionnement\nPrestation ponctuelle', 'Configuration', 'Forfait', 'ht', 1500, 'EUR', 2000, 'CONFIG-POSTE', 'active', 20, NOW(6), NOW(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'CONFIG-POSTE');

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'Configuration accès VPN', 'Configuration initiale du profil VPN utilisateur\nInstallation ou assistance à la configuration sur un appareil\nVérification de la connexion\nPrestation ponctuelle', 'Configuration', 'Forfait', 'ht', 1000, 'EUR', 2000, 'CONFIG-VPN', 'active', 30, NOW(6), NOW(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'CONFIG-VPN');

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'Mise en service initiale', 'Création et configuration initiale du service souscrit\nConfiguration du compte utilisateur\nVérification du bon fonctionnement\nPrestation ponctuelle', 'Configuration', 'Forfait', 'ht', 1500, 'EUR', 2000, 'INIT-SERVICE', 'active', 40, NOW(6), NOW(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'INIT-SERVICE');

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'Intervention technique ponctuelle', 'Intervention technique à distance\nDiagnostic, configuration ou dépannage selon la demande\nHors abonnement mensuel', 'Prestation', 'Heure', 'ht', 2500, 'EUR', 2000, 'INTERV-PONCT', 'active', 50, NOW(6), NOW(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'INTERV-PONCT');

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'Restauration de sauvegarde', 'Restauration de données depuis une sauvegarde disponible\nVérification de l''accès aux données restaurées\nPrestation ponctuelle', 'Prestation', 'Forfait', 'ht', 1000, 'EUR', 2000, 'RESTORE-SAVE', 'active', 60, NOW(6), NOW(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'RESTORE-SAVE');

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'Migration de données', 'Transfert de données vers le service souscrit\nOrganisation de base des dossiers\nVérification de l''accès aux données transférées\nPrestation ponctuelle', 'Prestation', 'Forfait', 'ht', 2000, 'EUR', 2000, 'MIG-DATA', 'active', 70, NOW(6), NOW(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'MIG-DATA');

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'Configuration appareil supplémentaire', 'Configuration d''un appareil supplémentaire\nParamétrage de l''accès aux services souscrits\nVérification du bon fonctionnement\nPrestation ponctuelle', 'Configuration', 'Forfait', 'ht', 1000, 'EUR', 2000, 'CONFIG-DEVICE-ADD', 'active', 80, NOW(6), NOW(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'CONFIG-DEVICE-ADD');

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'Documentation technique simplifiée', 'Rédaction d''une documentation synthétique liée au service configuré\nProcédure d''accès, informations utiles et consignes de base\nPrestation ponctuelle', 'Prestation', 'Forfait', 'ht', 1500, 'EUR', 2000, 'DOC-TECH', 'active', 90, NOW(6), NOW(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'DOC-TECH');

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'Accès VPN sécurisé', 'Accès VPN personnel sécurisé\nConfiguration du profil utilisateur\nMaintenance de l''accès incluse\nAssistance de connexion incluse\nFacturation mensuelle', 'Abonnement', 'Mois', 'ht', 500, 'EUR', 2000, 'ACCES-VPN', 'active', 100, NOW(6), NOW(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'ACCES-VPN');

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'Accès bureau distant RDS', 'Accès à un environnement Windows distant\nConfiguration du compte utilisateur\nMaintenance de l''accès distant incluse\nFacturation mensuelle', 'Abonnement', 'Mois', 'ht', 1500, 'EUR', 2000, 'ACCES-RDS', 'active', 110, NOW(6), NOW(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'ACCES-RDS');

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'Sauvegarde dossier personnel', 'Sauvegarde régulière selon la politique technique définie\nConservation selon la politique de sauvegarde définie\nRestauration sur demande\nFacturation mensuelle', 'Abonnement', 'Mois', 'ht', 200, 'EUR', 2000, 'SAVE-PERSO', 'active', 120, NOW(6), NOW(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'SAVE-PERSO');

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'Hébergement dossier personnel 32 Go', 'Espace de stockage personnel de 32 Go\nAccès distant sécurisé\nMaintenance technique minimale du service incluse\nFacturation mensuelle', 'Abonnement', 'Mois', 'ht', 500, 'EUR', 2000, 'STOCK-PERSO-32', 'active', 130, NOW(6), NOW(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'STOCK-PERSO-32');

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'Stockage supplémentaire 32 Go', 'Extension de l''espace de stockage personnel de 32 Go supplémentaires\nOption associée à un forfait d''hébergement existant\nFacturation mensuelle', 'Abonnement', 'Mois', 'ht', 300, 'EUR', 2000, 'STOCK-SUP-32', 'active', 140, NOW(6), NOW(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'STOCK-SUP-32');

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'Supervision de service', 'Surveillance de la disponibilité du service\nNotification en cas d''indisponibilité détectée\nVérification périodique du bon fonctionnement\nFacturation mensuelle', 'Abonnement', 'Mois', 'ht', 300, 'EUR', 2000, 'SUPERV-SERVICE', 'active', 150, NOW(6), NOW(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'SUPERV-SERVICE');

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'Support technique niveau 1', 'Assistance technique de base par message ou prise en main à distance\nSupport limité aux services souscrits\nInterventions réalisées selon disponibilité\nFacturation mensuelle', 'Abonnement', 'Mois', 'ht', 500, 'EUR', 2000, 'SUPPORT-LV1', 'active', 160, NOW(6), NOW(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'SUPPORT-LV1');

-- statement-break

INSERT INTO commercial_offers (id, name, description, category, unit_label, price_kind, price_amount_cents, currency, tax_rate_basis_points, external_reference, status, display_order, created_at, updated_at)
SELECT UUID(), 'Compte utilisateur supplémentaire', 'Création et configuration d''un compte utilisateur supplémentaire\nParamétrage des accès aux services souscrits\nFacturation mensuelle', 'Abonnement', 'Mois', 'ht', 300, 'EUR', 2000, 'USER-ADD', 'active', 170, NOW(6), NOW(6)
WHERE NOT EXISTS (SELECT 1 FROM commercial_offers WHERE external_reference = 'USER-ADD');
