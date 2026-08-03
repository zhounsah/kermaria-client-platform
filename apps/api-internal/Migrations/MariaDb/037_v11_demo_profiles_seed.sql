-- V1.1 : profils de demo par defaut. Le registre demo_profiles reste
-- administrable ; ces lignes ne sont qu'un point de depart, editables/supprimables
-- ensuite via l'admin. Inserts idempotents (WHERE NOT EXISTS sur profile_key).
--
-- Rappel garde-fou : les profils 'showcase' restent totalement inertes
-- (ad_provisioning off, aucun groupe, pas de quota reel, rds off). Seul le
-- profil 'trial' porte un acces reel cadre (groupes GG_DEMO_*, quota, rds natif).

INSERT INTO demo_profiles (
    id, profile_key, label, kind, content_template_key,
    email_mode, bpce_mode, payment_mode, ad_provisioning_mode,
    ad_groups_json, storage_quota_go, rds_session_mode,
    lifetime_days, status, created_at, updated_at
)
SELECT
    '63000000-0000-0000-0000-000000000001', 'showcase-tpe', 'Vitrine TPE',
    'showcase', 'tpe', 'off', 'off', 'off', 'off',
    NULL, NULL, 'off', 14, 'active', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM demo_profiles WHERE profile_key = 'showcase-tpe'
);

-- statement-break

INSERT INTO demo_profiles (
    id, profile_key, label, kind, content_template_key,
    email_mode, bpce_mode, payment_mode, ad_provisioning_mode,
    ad_groups_json, storage_quota_go, rds_session_mode,
    lifetime_days, status, created_at, updated_at
)
SELECT
    '63000000-0000-0000-0000-000000000002', 'showcase-association',
    'Vitrine association', 'showcase', 'association', 'off', 'off', 'off', 'off',
    NULL, NULL, 'off', 14, 'active', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM demo_profiles WHERE profile_key = 'showcase-association'
);

-- statement-break

INSERT INTO demo_profiles (
    id, profile_key, label, kind, content_template_key,
    email_mode, bpce_mode, payment_mode, ad_provisioning_mode,
    ad_groups_json, storage_quota_go, rds_session_mode,
    lifetime_days, status, created_at, updated_at
)
SELECT
    '63000000-0000-0000-0000-000000000003', 'showcase-pme-multisite',
    'Vitrine PME multisite', 'showcase', 'pme-multisite',
    'off', 'off', 'off', 'off',
    NULL, NULL, 'off', 14, 'active', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM demo_profiles WHERE profile_key = 'showcase-pme-multisite'
);

-- statement-break

INSERT INTO demo_profiles (
    id, profile_key, label, kind, content_template_key,
    email_mode, bpce_mode, payment_mode, ad_provisioning_mode,
    ad_groups_json, storage_quota_go, rds_session_mode,
    lifetime_days, status, created_at, updated_at
)
SELECT
    '63000000-0000-0000-0000-000000000004', 'trial-ad-koxo',
    'Essai client AD/KoXo', 'trial', 'ad-koxo', 'off', 'off', 'off',
    'real_scoped', '["GG_DEMO_RDS","GG_DEMO_VPN"]', 5, 'native',
    14, 'active', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM demo_profiles WHERE profile_key = 'trial-ad-koxo'
);
