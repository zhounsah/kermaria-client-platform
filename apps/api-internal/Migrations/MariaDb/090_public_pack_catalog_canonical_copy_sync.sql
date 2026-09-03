-- Corrige la copie du catalogue persistant sur ses noms de proprietes .NET (PascalCase).
SET NAMES utf8mb4;

-- statement-break

UPDATE public_pack_catalog_content SET content_json = JSON_SET(content_json, '$.PageEyebrow', 'Catalogue des offres', '$.PageTitle', 'Des offres simples, lisibles et prêtes à activer', '$.PageDescription', 'Comparez les offres, choisissez votre durée d''engagement, puis lancez votre demande à partir d''un périmètre clair.', '$.FootnotePrimary', 'Les tarifs affichés sont hors taxes et correspondent au catalogue public actuel. La mise en service et le support sont organisés selon l''offre retenue.', '$.Packs[0].Label', 'Offre Dossier Sécurisé', '$.Packs[1].Label', 'Offre Accès à Distance', '$.Packs[1].Highlights[0]', 'Tout ce que comprend l''offre Dossier Sécurisé', '$.Packs[2].Label', 'Offre Bureau Windows à Distance', '$.Packs[3].Label', 'Offre Pro / Association', '$.Packs[3].Description', 'Une offre plus complète pour une petite structure, avec plus de capacité.'), updated_at = UTC_TIMESTAMP() WHERE content_key = 'public-pack-catalog' AND JSON_VALID(content_json);

-- statement-break

UPDATE public_pack_catalog_content SET content_json = JSON_REMOVE(content_json, '$.pageEyebrow', '$.pageTitle', '$.pageDescription', '$.footnotePrimary') WHERE content_key = 'public-pack-catalog' AND JSON_VALID(content_json);
