-- ============================================================================
-- Editorial: preserve legacy SEO URLs moved to the public Wiki.
-- ============================================================================

SET NAMES utf8mb4;

-- statement-break

INSERT IGNORE INTO editorial_redirects (
    id, content_id, content_type, old_path, new_path, created_at, created_by_user_id
)
SELECT UUID(), c.id, c.content_type, '/securite-des-donnees',
       'https://wiki.zacharyhounsa.ovh/article/securite-des-donnees',
       UTC_TIMESTAMP(6), NULL
FROM editorial_contents c
WHERE c.content_type = 'seo_page' AND c.slug = 'securite-des-donnees'
ORDER BY c.updated_at DESC
LIMIT 1;

-- statement-break

INSERT IGNORE INTO editorial_redirects (
    id, content_id, content_type, old_path, new_path, created_at, created_by_user_id
)
SELECT UUID(), c.id, c.content_type, '/fonctionnement-sauvegarde-zachary-it',
       'https://wiki.zacharyhounsa.ovh/article/fonctionnement-sauvegarde-zachary-it',
       UTC_TIMESTAMP(6), NULL
FROM editorial_contents c
WHERE c.content_type = 'seo_page' AND c.slug = 'fonctionnement-sauvegarde-zachary-it'
ORDER BY c.updated_at DESC
LIMIT 1;

-- statement-break

INSERT IGNORE INTO editorial_redirects (
    id, content_id, content_type, old_path, new_path, created_at, created_by_user_id
)
SELECT UUID(), c.id, c.content_type, '/ou-sont-stockees-les-donnees-zachary-it',
       'https://wiki.zacharyhounsa.ovh/article/ou-sont-stockees-les-donnees-zachary-it',
       UTC_TIMESTAMP(6), NULL
FROM editorial_contents c
WHERE c.content_type = 'seo_page' AND c.slug = 'ou-sont-stockees-les-donnees-zachary-it'
ORDER BY c.updated_at DESC
LIMIT 1;
