-- VPS-CLOUD : caracteristiques commerciales autoritatives.
--
-- Les valeurs sont deja configurees dans le catalogue de production. Cette
-- migration les rend reproductibles pour les bases qui partent de migrations
-- versionnees, sans ecraser une modification ulterieure faite en administration
-- sur un attribut deja present.
SET NAMES utf8mb4;

-- statement-break

INSERT IGNORE INTO billing_v2_service_tier_attributes
    (id, tier_id, attribute_code, value_numeric, unit)
SELECT UUID(), tier.id, attribute.attribute_code, attribute.value_numeric,
       attribute.unit
FROM billing_v2_services AS service
INNER JOIN billing_v2_service_tiers AS tier
    ON tier.service_id = service.id
INNER JOIN (
    SELECT 'S' AS tier_code, 'vcpu_count' AS attribute_code, 2 AS value_numeric, 'count' AS unit
    UNION ALL SELECT 'S', 'ram_gib', 2, 'GiB'
    UNION ALL SELECT 'S', 'disk_gib', 60, 'GiB'
    UNION ALL SELECT 'M', 'vcpu_count', 4, 'count'
    UNION ALL SELECT 'M', 'ram_gib', 8, 'GiB'
    UNION ALL SELECT 'M', 'disk_gib', 160, 'GiB'
    UNION ALL SELECT 'L', 'vcpu_count', 8, 'count'
    UNION ALL SELECT 'L', 'ram_gib', 16, 'GiB'
    UNION ALL SELECT 'L', 'disk_gib', 320, 'GiB'
    UNION ALL SELECT 'XL', 'vcpu_count', 16, 'count'
    UNION ALL SELECT 'XL', 'ram_gib', 32, 'GiB'
    UNION ALL SELECT 'XL', 'disk_gib', 640, 'GiB'
) AS attribute
    ON attribute.tier_code = tier.code
WHERE service.code = 'VPS-CLOUD';

-- statement-break

-- Corrige uniquement la phrase de seed devenue fausse, sans remplacer le
-- document administrable entier ni ses autres adaptations locales.
UPDATE managed_content_entries
SET body_markdown = REPLACE(
    body_markdown,
    'VPS Cloud reste une prestation manuelle : aucune taille CPU, RAM ou disque n’est promise ici.',
    'Les caractéristiques CPU, RAM et stockage des paliers VPS Cloud sont publiées depuis le catalogue Billing V2.1 ; leur mise en service reste manuelle et cadrée.'
)
WHERE content_key = 'storefront:vps'
  AND body_markdown LIKE '%VPS Cloud reste une prestation manuelle : aucune taille CPU, RAM ou disque n’est promise ici.%';

-- statement-break

UPDATE managed_content_entries
SET body_markdown = REPLACE(
    body_markdown,
    'VPS Cloud reste une prestation manuelle, sans caractéristiques CPU, RAM ou disque promises à l’avance.',
    'Les caractéristiques CPU, RAM et stockage des paliers VPS Cloud sont publiées depuis le catalogue Billing V2.1 ; leur mise en service reste manuelle et cadrée.'
)
WHERE content_key = 'storefront:cloud-hebergement'
  AND body_markdown LIKE '%VPS Cloud reste une prestation manuelle, sans caractéristiques CPU, RAM ou disque promises à l’avance.%';
