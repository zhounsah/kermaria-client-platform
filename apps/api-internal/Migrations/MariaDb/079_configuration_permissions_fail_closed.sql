-- Permissions du Centre de configuration : fail-closed.
-- Les administrateurs internes actifs presents lors de la migration recoivent
-- une attribution explicite. Les futurs comptes n'heritent d'aucune permission
-- du Centre sans grant explicite.
INSERT INTO admin_permission_grants (
    id, user_id, permission_code, created_at, created_by_user_id)
SELECT
    UUID(), users.id, permissions.permission_code, UTC_TIMESTAMP(6), NULL
FROM portal_users AS users
CROSS JOIN (
    SELECT 'settings.read' AS permission_code
    UNION ALL SELECT 'settings.write'
    UNION ALL SELECT 'settings.templates.write'
    UNION ALL SELECT 'settings.diagnostic.write'
    UNION ALL SELECT 'settings.billing.write'
    UNION ALL SELECT 'settings.demo.write'
    UNION ALL SELECT 'settings.integrations.test'
) AS permissions
WHERE users.role = 'internal_admin'
  AND users.status = 'active'
  AND NOT EXISTS (
      SELECT 1
      FROM admin_permission_grants AS existing_grant
      WHERE existing_grant.user_id = users.id
        AND existing_grant.permission_code = permissions.permission_code
  );
