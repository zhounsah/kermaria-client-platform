-- Align persisted public legal contact identity.
SET NAMES utf8mb4;

-- statement-break

UPDATE managed_content_entries
SET body_markdown = REPLACE(body_markdown, 'zhounsah@home.bzh', 'contact@zachary-it.fr'),
    updated_at = UTC_TIMESTAMP()
WHERE content_key IN ('legal:cgv','legal:politique-confidentialite','legal:mentions-legales')
  AND body_markdown LIKE '%zhounsah@home.bzh%';

-- statement-break

UPDATE managed_content_entries
SET body_markdown = REPLACE(body_markdown, 'zacharyhounsa.ovh', 'zachary-it.fr'),
    updated_at = UTC_TIMESTAMP()
WHERE content_key IN ('legal:cgv','legal:politique-confidentialite','legal:mentions-legales')
  AND body_markdown LIKE '%zacharyhounsa.ovh%';
