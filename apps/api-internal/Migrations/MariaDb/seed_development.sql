INSERT INTO customers (
    id, external_reference, display_name, status, billing_email, phone,
    address, city, country, created_at, updated_at
) VALUES (
    '10000000-0000-0000-0000-000000000001',
    'CLI-DEMO-0060',
    'Zachary HOUNSA-HOUNKPA EI - Client démo',
    'active',
    'client.demo@example.invalid',
    '+33 0 00 00 00 00',
    '1 rue de la Démonstration',
    '44000 Nantes',
    'France',
    UTC_TIMESTAMP(6),
    UTC_TIMESTAMP(6)
);
-- statement-break

INSERT INTO portal_users (
    id, customer_id, identity_provider_subject, email, display_name, status,
    created_at, updated_at
) VALUES (
    '11000000-0000-0000-0000-000000000001',
    '10000000-0000-0000-0000-000000000001',
    'mock-subject-v0.6',
    'client.demo@example.invalid',
    'Client Démo',
    'active',
    UTC_TIMESTAMP(6),
    UTC_TIMESTAMP(6)
);
-- statement-break

INSERT INTO customer_services (
    id, customer_id, external_reference, service_type, name, status,
    description, started_at, scope, commercial_terms, next_step,
    created_at, updated_at
) VALUES
(
    '20000000-0000-0000-0000-000000000001',
    '10000000-0000-0000-0000-000000000001',
    'SVC-HDP-001',
    'personal_hosting',
    'Hébergement dossier personnel',
    'active',
    'Espace de démonstration selon le périmètre convenu.',
    '2026-01-15 00:00:00',
    'Espace personnel et accès nominatif de démonstration',
    'Selon devis',
    NULL,
    UTC_TIMESTAMP(6),
    UTC_TIMESTAMP(6)
),
(
    '20000000-0000-0000-0000-000000000002',
    '10000000-0000-0000-0000-000000000001',
    'SVC-SAV-004',
    'backup',
    'Sauvegarde dossier personnel',
    'active',
    'Sauvegarde planifiée avec vérifications prévues.',
    '2026-01-15 00:00:00',
    'Dossier personnel inclus dans la démonstration',
    'Inclus selon périmètre',
    NULL,
    UTC_TIMESTAMP(6),
    UTC_TIMESTAMP(6)
),
(
    '20000000-0000-0000-0000-000000000003',
    '10000000-0000-0000-0000-000000000001',
    'SVC-VPN-007',
    'vpn',
    'Accès VPN privé',
    'pending',
    'Accès VPN chiffré en cours de qualification.',
    NULL,
    'Un accès nominatif sous réserve de validation technique',
    'Selon devis',
    'Vérifications techniques prévues avant activation',
    UTC_TIMESTAMP(6),
    UTC_TIMESTAMP(6)
),
(
    '20000000-0000-0000-0000-000000000004',
    '10000000-0000-0000-0000-000000000001',
    'SVC-RDS-003',
    'rds',
    'Accès bureau distant / RDS',
    'suspended',
    'Accès distant fictif suspendu dans la démonstration.',
    '2025-10-20 00:00:00',
    'Un environnement distant défini selon le besoin',
    'Selon devis',
    'Une revue du besoin est nécessaire avant toute reprise',
    UTC_TIMESTAMP(6),
    UTC_TIMESTAMP(6)
),
(
    '20000000-0000-0000-0000-000000000005',
    '10000000-0000-0000-0000-000000000001',
    'SVC-SUP-014',
    'support',
    'Support technique niveau 1',
    'active',
    'Premier niveau d’assistance selon le périmètre convenu.',
    '2026-02-01 00:00:00',
    'Diagnostic initial et accompagnement selon périmètre',
    'Inclus selon périmètre',
    NULL,
    UTC_TIMESTAMP(6),
    UTC_TIMESTAMP(6)
);
-- statement-break

INSERT INTO service_catalog (
    id, name, category, description, scope, commercial_terms, is_active,
    sort_order, created_at, updated_at
) VALUES
('catalog-personal-hosting', 'Hébergement de dossiers personnels', 'Hébergement', 'Espace adapté au volume et aux usages convenus.', 'Dimensionnement et modalités d’accès à définir', 'Selon devis', TRUE, 10, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
('catalog-backup', 'Sauvegarde de données', 'Continuité', 'Plan de sauvegarde adapté au besoin avec vérifications prévues.', 'Sources, fréquence et rétention à confirmer', 'Selon devis', TRUE, 20, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
('catalog-vpn', 'VPN privé', 'Accès', 'Accès VPN chiffré étudié selon les usages attendus.', 'Accès nominatifs et règles réseau à définir', 'Selon devis', TRUE, 30, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
('catalog-rds', 'Accès distant / RDS', 'Environnement', 'Solution adaptée après qualification du besoin.', 'Utilisateurs, applications et ressources à confirmer', 'Selon devis', TRUE, 40, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
('catalog-intervention', 'Intervention ponctuelle', 'Assistance', 'Diagnostic ou intervention ciblée.', 'Périmètre et délai convenus avant intervention', 'Selon devis', TRUE, 50, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
('catalog-network-advice', 'Conseil réseau et infrastructure', 'Conseil', 'Analyse et recommandations adaptées à l’existant.', 'Entretien, état des lieux et recommandations', 'Selon devis', TRUE, 60, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
('catalog-documentation', 'Documentation technique simplifiée', 'Documentation', 'Documentation lisible des procédures convenues.', 'Sujet et niveau de détail définis ensemble', 'Selon devis', TRUE, 70, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
('catalog-migration', 'Migration de données', 'Données', 'Préparation et accompagnement avec contrôles adaptés.', 'Sources, destination, volume et fenêtre à confirmer', 'Selon devis', TRUE, 80, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6));
-- statement-break

INSERT INTO invoices (
    id, customer_id, external_reference, invoice_number, status, issued_at,
    due_at, period_label, currency, subtotal_amount, tax_amount, total_amount,
    created_at, updated_at
) VALUES
('30000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', 'INV-DEMO-001', 'FACT-DEMO-2026-0042', 'paid', '2026-05-03 00:00:00', '2026-05-17 00:00:00', 'Mai 2026', 'EUR', 80.00, 16.00, 96.00, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
('30000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000001', 'INV-DEMO-002', 'FACT-DEMO-2026-0036', 'pending', '2026-06-03 00:00:00', '2026-06-17 00:00:00', 'Juin 2026', 'EUR', 80.00, 16.00, 96.00, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
('30000000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000001', 'INV-DEMO-003', 'FACT-DEMO-2026-0030', 'paid', '2026-04-03 00:00:00', '2026-04-17 00:00:00', 'Avril 2026', 'EUR', 80.00, 16.00, 96.00, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6));
-- statement-break

INSERT INTO support_requests (
    id, customer_id, service_id, reference, subject, description, priority,
    category, status, created_at, updated_at
) VALUES
(
    '40000000-0000-0000-0000-000000000001',
    '10000000-0000-0000-0000-000000000001',
    '20000000-0000-0000-0000-000000000002',
    'SUP-DEMO-2026-018',
    'Vérification d’une sauvegarde planifiée',
    'Demande fictive sans donnée sensible.',
    'normal',
    'support',
    'open',
    '2026-06-10 09:30:00',
    '2026-06-10 11:15:00'
);
