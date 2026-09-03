-- Finalise les libelles imbriques du catalogue public apres la migration 088.
SET NAMES utf8mb4;

-- statement-break

UPDATE public_pack_catalog_content SET content_json = JSON_REPLACE(content_json, '$.packs[0].label', 'Offre Dossier Sécurisé'), updated_at = UTC_TIMESTAMP() WHERE content_key = 'public-pack-catalog' AND JSON_VALID(content_json);

-- statement-break

UPDATE public_pack_catalog_content SET content_json = JSON_REPLACE(content_json, '$.packs[1].label', 'Offre Accès à Distance', '$.packs[1].highlights[0]', 'Tout ce que comprend l''offre Dossier Sécurisé'), updated_at = UTC_TIMESTAMP() WHERE content_key = 'public-pack-catalog' AND JSON_VALID(content_json);

-- statement-break

UPDATE public_pack_catalog_content SET content_json = JSON_REPLACE(content_json, '$.packs[2].label', 'Offre Bureau Windows à Distance'), updated_at = UTC_TIMESTAMP() WHERE content_key = 'public-pack-catalog' AND JSON_VALID(content_json);

-- statement-break

UPDATE public_pack_catalog_content SET content_json = JSON_REPLACE(content_json, '$.packs[3].label', 'Offre Pro / Association', '$.packs[3].description', 'Une offre plus complète pour une petite structure, avec plus de capacité.'), updated_at = UTC_TIMESTAMP() WHERE content_key = 'public-pack-catalog' AND JSON_VALID(content_json);
