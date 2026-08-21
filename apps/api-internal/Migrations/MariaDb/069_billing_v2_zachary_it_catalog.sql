-- ============================================================================
-- Billing V2.1 : catalogue Zachary IT dormant et additif.
-- Les services sont visibles mais non commandables par defaut. Aucun provider
-- ni backend technique n'est expose au client et DOMAIN-MANAGED reste sur devis.
-- ============================================================================

SET NAMES utf8mb4;

-- statement-break

INSERT IGNORE INTO billing_v2_services
    (id, code, name, category, billing_type, default_scope_type, pricing_model,
     discount_eligible, public_selectable, public_visible, self_service_orderable,
     status, display_order)
VALUES
    (UUID(), 'DOMAIN-MANAGED', 'Gestion de domaine et DNS', 'Domaines', 'recurring', 'subscription', 'fixed', 0, 0, 1, 0, 'active', 200),
    (UUID(), 'DNS-MANAGED', 'DNS managé', 'Domaines', 'recurring', 'subscription', 'fixed', 1, 0, 1, 0, 'active', 210),
    (UUID(), 'CLOUDFLARE-MANAGED', 'Cloudflare managé', 'Sécurité', 'recurring', 'subscription', 'fixed', 1, 0, 1, 0, 'active', 220),
    (UUID(), 'SSL-MANAGED', 'SSL managé', 'Sécurité', 'recurring', 'subscription', 'fixed', 1, 0, 1, 0, 'active', 230),
    (UUID(), 'MAIL-MANAGED', 'Messagerie managée', 'Messagerie', 'recurring', 'user', 'fixed', 1, 0, 1, 0, 'active', 240),
    (UUID(), 'MAIL-DMARC-MANAGED', 'DMARC managé', 'Messagerie', 'recurring', 'subscription', 'fixed', 1, 0, 1, 0, 'active', 250),
    (UUID(), 'M365-MANAGED', 'Microsoft 365 managé', 'Messagerie', 'recurring', 'user', 'fixed', 1, 0, 1, 0, 'active', 260),
    (UUID(), 'WEB-EXTERNAL-MANAGED', 'Hébergement Web géré', 'Web', 'recurring', 'subscription', 'fixed', 1, 0, 1, 0, 'active', 270),
    (UUID(), 'CMS-MAINT', 'Maintenance CMS', 'Web', 'recurring', 'subscription', 'tiered', 1, 0, 1, 0, 'active', 280),
    (UUID(), 'MONITORING-EXTERNAL', 'Supervision externe', 'Supervision', 'recurring', 'subscription', 'fixed', 1, 0, 1, 0, 'active', 290),
    (UUID(), 'NAS-MONITORING', 'Supervision NAS', 'Supervision', 'recurring', 'subscription', 'fixed', 1, 0, 1, 0, 'active', 300),
    (UUID(), 'FIREWALL-MANAGED', 'Firewall managé', 'Sécurité', 'recurring', 'subscription', 'fixed', 1, 0, 1, 0, 'active', 310),
    (UUID(), 'UNIFI-MANAGED', 'UniFi managé', 'Réseau', 'recurring', 'subscription', 'fixed', 1, 0, 1, 0, 'active', 320),
    (UUID(), 'IDENTITY-MANAGED', 'Gestion des identités', 'Identité', 'recurring', 'user', 'fixed', 1, 0, 1, 0, 'active', 330),
    (UUID(), 'BACKUP-EXTERNAL-MANAGED', 'Sauvegarde externe managée', 'Sauvegarde', 'recurring', 'subscription', 'fixed', 1, 0, 1, 0, 'active', 340),
    (UUID(), 'LINUX-PATCH-MANAGED', 'Maintenance Linux', 'Infogérance', 'recurring', 'subscription', 'fixed', 1, 0, 1, 0, 'active', 350),
    (UUID(), 'WAF-REVERSE-PROXY', 'WAF et reverse proxy', 'Sécurité', 'recurring', 'subscription', 'fixed', 1, 0, 1, 0, 'active', 360),
    (UUID(), 'NEXTCLOUD-EXTERNAL-MAINT', 'Maintenance Nextcloud externe', 'Infogérance', 'recurring', 'subscription', 'fixed', 1, 0, 1, 0, 'active', 370),
    (UUID(), 'VPS-EXTERNAL-MANAGED', 'Infogérance VPS externe', 'Infogérance', 'recurring', 'subscription', 'tiered', 1, 0, 1, 0, 'active', 380),
    (UUID(), 'VPS-LOCAL', 'VPS local', 'Cloud', 'recurring', 'subscription', 'tiered', 1, 0, 1, 0, 'active', 390),
    (UUID(), 'VPS-CLOUD', 'VPS cloud', 'Cloud', 'recurring', 'subscription', 'tiered', 1, 0, 1, 0, 'active', 400),
    (UUID(), 'VPS-MANAGED-ADDON', 'Infogérance VPS Zachary IT', 'Cloud', 'recurring', 'subscription', 'fixed', 1, 0, 1, 0, 'active', 410);

-- statement-break

INSERT IGNORE INTO billing_v2_service_tiers
    (id, service_id, code, name, public_label, public_selectable, status, display_order)
SELECT UUID(), s.id, x.code, x.name, x.name, 0, 'active', x.display_order
FROM billing_v2_services s
JOIN (
    SELECT 'CMS-MAINT' service_code, 'STANDARD' code, 'Standard' name, 10 display_order
    UNION ALL SELECT 'CMS-MAINT', 'PLUS', 'Plus', 20
    UNION ALL SELECT 'VPS-EXTERNAL-MANAGED', 'STANDARD', 'Standard', 10
    UNION ALL SELECT 'VPS-EXTERNAL-MANAGED', 'PLUS', 'Plus', 20
    UNION ALL SELECT 'VPS-LOCAL', 'NANO', 'Nano', 10
    UNION ALL SELECT 'VPS-LOCAL', 'MICRO', 'Micro', 20
    UNION ALL SELECT 'VPS-LOCAL', 'SMALL', 'Small', 30
    UNION ALL SELECT 'VPS-LOCAL', 'MEDIUM', 'Medium', 40
    UNION ALL SELECT 'VPS-CLOUD', 'S', 'S', 10
    UNION ALL SELECT 'VPS-CLOUD', 'M', 'M', 20
    UNION ALL SELECT 'VPS-CLOUD', 'L', 'L', 30
    UNION ALL SELECT 'VPS-CLOUD', 'XL', 'XL', 40
) x ON x.service_code = s.code;

-- statement-break

INSERT IGNORE INTO billing_v2_service_tier_attributes
    (id, tier_id, attribute_code, value_numeric, unit)
SELECT UUID(), t.id, x.attribute_code, x.value_numeric, x.unit
FROM billing_v2_services s
JOIN billing_v2_service_tiers t ON t.service_id = s.id
JOIN (
    SELECT 'VPS-LOCAL' service_code, 'NANO' tier_code, 'vcpu_count' attribute_code, 1 value_numeric, 'count' unit
    UNION ALL SELECT 'VPS-LOCAL', 'NANO', 'ram_gib', 1, 'GiB'
    UNION ALL SELECT 'VPS-LOCAL', 'NANO', 'disk_gib', 15, 'GiB'
    UNION ALL SELECT 'VPS-LOCAL', 'MICRO', 'vcpu_count', 1, 'count'
    UNION ALL SELECT 'VPS-LOCAL', 'MICRO', 'ram_gib', 2, 'GiB'
    UNION ALL SELECT 'VPS-LOCAL', 'MICRO', 'disk_gib', 25, 'GiB'
    UNION ALL SELECT 'VPS-LOCAL', 'SMALL', 'vcpu_count', 2, 'count'
    UNION ALL SELECT 'VPS-LOCAL', 'SMALL', 'ram_gib', 4, 'GiB'
    UNION ALL SELECT 'VPS-LOCAL', 'SMALL', 'disk_gib', 40, 'GiB'
    UNION ALL SELECT 'VPS-LOCAL', 'MEDIUM', 'vcpu_count', 4, 'count'
    UNION ALL SELECT 'VPS-LOCAL', 'MEDIUM', 'ram_gib', 8, 'GiB'
    UNION ALL SELECT 'VPS-LOCAL', 'MEDIUM', 'disk_gib', 80, 'GiB'
) x ON x.service_code = s.code AND x.tier_code = t.code;

-- statement-break

INSERT INTO billing_v2_service_prices
    (id, service_id, tier_id, price_code, price_version, amount_cents, currency,
     billing_cadence, charge_trigger, valid_from, status)
SELECT UUID(), s.id, t.id, x.price_code, 1, x.amount_cents, 'EUR',
       x.billing_cadence, x.charge_trigger, '2026-08-21 00:00:00.000000', 'active'
FROM (
    SELECT 'DNS-MANAGED' service_code, NULL tier_code, 'DNS-MANAGED-MONTHLY-EUR-V1' price_code, 290 amount_cents, 'monthly' billing_cadence, 'initial_subscription' charge_trigger
    UNION ALL SELECT 'CLOUDFLARE-MANAGED', NULL, 'CLOUDFLARE-MANAGED-MONTHLY-EUR-V1', 490, 'monthly', 'initial_subscription'
    UNION ALL SELECT 'SSL-MANAGED', NULL, 'SSL-MANAGED-MONTHLY-EUR-V1', 290, 'monthly', 'initial_subscription'
    UNION ALL SELECT 'MAIL-MANAGED', NULL, 'MAIL-MANAGED-MONTHLY-EUR-V1', 490, 'monthly', 'initial_subscription'
    UNION ALL SELECT 'MAIL-DMARC-MANAGED', NULL, 'MAIL-DMARC-MANAGED-MONTHLY-EUR-V1', 290, 'monthly', 'initial_subscription'
    UNION ALL SELECT 'M365-MANAGED', NULL, 'M365-MANAGED-MONTHLY-EUR-V1', 490, 'monthly', 'initial_subscription'
    UNION ALL SELECT 'WEB-EXTERNAL-MANAGED', NULL, 'WEB-EXTERNAL-MANAGED-MONTHLY-EUR-V1', 990, 'monthly', 'initial_subscription'
    UNION ALL SELECT 'CMS-MAINT', 'STANDARD', 'CMS-MAINT-STANDARD-MONTHLY-EUR-V1', 1490, 'monthly', 'initial_subscription'
    UNION ALL SELECT 'CMS-MAINT', 'PLUS', 'CMS-MAINT-PLUS-MONTHLY-EUR-V1', 2990, 'monthly', 'initial_subscription'
    UNION ALL SELECT 'MONITORING-EXTERNAL', NULL, 'MONITORING-EXTERNAL-MONTHLY-EUR-V1', 290, 'monthly', 'initial_subscription'
    UNION ALL SELECT 'NAS-MONITORING', NULL, 'NAS-MONITORING-MONTHLY-EUR-V1', 790, 'monthly', 'initial_subscription'
    UNION ALL SELECT 'FIREWALL-MANAGED', NULL, 'FIREWALL-MANAGED-MONTHLY-EUR-V1', 1990, 'monthly', 'initial_subscription'
    UNION ALL SELECT 'UNIFI-MANAGED', NULL, 'UNIFI-MANAGED-MONTHLY-EUR-V1', 990, 'monthly', 'initial_subscription'
    UNION ALL SELECT 'IDENTITY-MANAGED', NULL, 'IDENTITY-MANAGED-MONTHLY-EUR-V1', 290, 'monthly', 'initial_subscription'
    UNION ALL SELECT 'BACKUP-EXTERNAL-MANAGED', NULL, 'BACKUP-EXTERNAL-MANAGED-MONTHLY-EUR-V1', 790, 'monthly', 'initial_subscription'
    UNION ALL SELECT 'LINUX-PATCH-MANAGED', NULL, 'LINUX-PATCH-MANAGED-MONTHLY-EUR-V1', 1490, 'monthly', 'initial_subscription'
    UNION ALL SELECT 'WAF-REVERSE-PROXY', NULL, 'WAF-REVERSE-PROXY-MONTHLY-EUR-V1', 990, 'monthly', 'initial_subscription'
    UNION ALL SELECT 'NEXTCLOUD-EXTERNAL-MAINT', NULL, 'NEXTCLOUD-EXTERNAL-MAINT-MONTHLY-EUR-V1', 1990, 'monthly', 'initial_subscription'
    UNION ALL SELECT 'VPS-EXTERNAL-MANAGED', 'STANDARD', 'VPS-EXTERNAL-MANAGED-STANDARD-MONTHLY-EUR-V1', 2990, 'monthly', 'initial_subscription'
    UNION ALL SELECT 'VPS-EXTERNAL-MANAGED', 'PLUS', 'VPS-EXTERNAL-MANAGED-PLUS-MONTHLY-EUR-V1', 4990, 'monthly', 'initial_subscription'
    UNION ALL SELECT 'VPS-LOCAL', 'NANO', 'VPS-LOCAL-NANO-MONTHLY-EUR-V1', 590, 'monthly', 'initial_subscription'
    UNION ALL SELECT 'VPS-LOCAL', 'MICRO', 'VPS-LOCAL-MICRO-MONTHLY-EUR-V1', 890, 'monthly', 'initial_subscription'
    UNION ALL SELECT 'VPS-LOCAL', 'SMALL', 'VPS-LOCAL-SMALL-MONTHLY-EUR-V1', 1390, 'monthly', 'initial_subscription'
    UNION ALL SELECT 'VPS-LOCAL', 'MEDIUM', 'VPS-LOCAL-MEDIUM-MONTHLY-EUR-V1', 2290, 'monthly', 'initial_subscription'
    UNION ALL SELECT 'VPS-CLOUD', 'S', 'VPS-CLOUD-S-MONTHLY-EUR-V1', 1990, 'monthly', 'initial_subscription'
    UNION ALL SELECT 'VPS-CLOUD', 'M', 'VPS-CLOUD-M-MONTHLY-EUR-V1', 2990, 'monthly', 'initial_subscription'
    UNION ALL SELECT 'VPS-CLOUD', 'L', 'VPS-CLOUD-L-MONTHLY-EUR-V1', 4490, 'monthly', 'initial_subscription'
    UNION ALL SELECT 'VPS-CLOUD', 'XL', 'VPS-CLOUD-XL-MONTHLY-EUR-V1', 6990, 'monthly', 'initial_subscription'
    UNION ALL SELECT 'VPS-MANAGED-ADDON', NULL, 'VPS-MANAGED-ADDON-MONTHLY-EUR-V1', 2000, 'monthly', 'initial_subscription'
    UNION ALL SELECT 'VPS-LOCAL', 'NANO', 'VPS-LOCAL-NANO-SETUP-EUR-V1', 1990, 'one_time', 'initial_subscription'
    UNION ALL SELECT 'VPS-LOCAL', 'MICRO', 'VPS-LOCAL-MICRO-SETUP-EUR-V1', 1990, 'one_time', 'initial_subscription'
    UNION ALL SELECT 'VPS-LOCAL', 'SMALL', 'VPS-LOCAL-SMALL-SETUP-EUR-V1', 1990, 'one_time', 'initial_subscription'
    UNION ALL SELECT 'VPS-LOCAL', 'MEDIUM', 'VPS-LOCAL-MEDIUM-SETUP-EUR-V1', 1990, 'one_time', 'initial_subscription'
    UNION ALL SELECT 'VPS-CLOUD', 'S', 'VPS-CLOUD-S-SETUP-EUR-V1', 2990, 'one_time', 'initial_subscription'
    UNION ALL SELECT 'VPS-CLOUD', 'M', 'VPS-CLOUD-M-SETUP-EUR-V1', 2990, 'one_time', 'initial_subscription'
    UNION ALL SELECT 'VPS-CLOUD', 'L', 'VPS-CLOUD-L-SETUP-EUR-V1', 2990, 'one_time', 'initial_subscription'
    UNION ALL SELECT 'VPS-CLOUD', 'XL', 'VPS-CLOUD-XL-SETUP-EUR-V1', 2990, 'one_time', 'initial_subscription'
) x
JOIN billing_v2_services s ON s.code = x.service_code
LEFT JOIN billing_v2_service_tiers t ON t.service_id = s.id AND t.code = x.tier_code
WHERE (x.tier_code IS NULL OR t.id IS NOT NULL)
  AND NOT EXISTS (SELECT 1 FROM billing_v2_service_prices p WHERE p.price_code = x.price_code);

-- statement-break

INSERT IGNORE INTO billing_v2_service_fulfillment_profiles
    (id, service_id, tier_id, fulfillment_mode, default_backend, status)
SELECT UUID(), s.id, NULL, x.fulfillment_mode, x.backend, 'active'
FROM billing_v2_services s
JOIN (
    SELECT 'VPS-LOCAL' service_code, NULL tier_code, 'technical_provisioning' fulfillment_mode, 'LOCAL_HYPERV' backend
    UNION ALL SELECT 'VPS-CLOUD', NULL, 'manual_delivery', 'MANUAL'
    UNION ALL SELECT 'DOMAIN-MANAGED', NULL, 'manual_delivery', 'MANUAL'
    UNION ALL SELECT 'M365-MANAGED', NULL, 'manual_delivery', 'MANUAL'
    UNION ALL SELECT 'CMS-MAINT', NULL, 'manual_delivery', 'MANUAL'
    UNION ALL SELECT 'SUPPORT-PLUS', NULL, 'contractual_acknowledgement', 'MANUAL'
) x ON x.service_code = s.code AND x.tier_code IS NULL;
