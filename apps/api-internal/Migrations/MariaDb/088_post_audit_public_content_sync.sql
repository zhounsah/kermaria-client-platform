-- Synchronise les contenus publics persistants avec la copie validee apres audit v2.0.2.5.
SET NAMES utf8mb4;

-- statement-break

UPDATE managed_content_entries SET body_markdown = JSON_SET(body_markdown, '$.seoTitle', 'Tarifs des services IT : unit├®s et devis', '$.sections[2].heading', 'Offres et services', '$.sections[2].bodyMarkdown', 'Les offres sont des configurations pr├¬tes ├á l''emploi et configurables. Elles ne remplacent pas l''ensemble des services Zachary IT.', '$.faq[2].answer', 'Non par d├®faut : les licences de fournisseurs sont pr├®cis├®es s├®par├®ment dans le devis quand elles sont n├®cessaires.', '$.relatedLinks[0].label', 'Voir les offres'), updated_at = UTC_TIMESTAMP() WHERE content_key = 'storefront:tarifs' AND JSON_VALID(body_markdown);

-- statement-break

UPDATE managed_content_entries SET body_markdown = JSON_SET(body_markdown, '$.sections[0].bodyMarkdown', 'Un VPS Zachary IT ou un VPS Cloud peut ├¬tre pr├®par├® et g├®r├® ; les caract├®ristiques CPU, RAM et stockage sont celles affich├®es sur chaque offre. Lorsque le parcours le permet, la commande peut ├¬tre pay├®e en ligne, puis la mise en service intervient apr├¿s validation technique.'), updated_at = UTC_TIMESTAMP() WHERE content_key = 'storefront:cloud-hebergement' AND JSON_VALID(body_markdown);

-- statement-break

UPDATE managed_content_entries SET body_markdown = JSON_SET(body_markdown, '$.sections[0].bodyMarkdown', 'Le VPS Zachary IT et le VPS Cloud ne recouvrent pas la m├¬me mise en ┼ôuvre. Les caract├®ristiques CPU, RAM et stockage sont celles affich├®es sur chaque offre. Lorsque le parcours le permet, la commande peut ├¬tre pay├®e en ligne, puis la mise en service intervient apr├¿s validation technique. L''infog├®rance peut aussi porter sur un VPS chez un autre fournisseur.', '$.sections[1].bodyMarkdown', 'Usages, donn├®es, acc├¿s, sauvegarde, maintenance, d├®pendances et responsabilit├® du fournisseur sont qualifi├®s avant la mise en service. L''infog├®rance peut aussi porter sur un VPS chez un autre fournisseur.', '$.sections[2].bodyMarkdown', 'Le service est qualifi├® avant mise en ┼ôuvre. Lorsque le prix ne peut pas ├¬tre d├®termin├® imm├®diatement, nous vous proposons un devis adapt├® ├á votre besoin.', '$.faq[1].answer', 'Non. Sa mise en service est organis├®e apr├¿s validation technique.'), updated_at = UTC_TIMESTAMP() WHERE content_key = 'storefront:vps' AND JSON_VALID(body_markdown);

-- statement-break

UPDATE managed_content_entries SET body_markdown = JSON_SET(body_markdown, '$.sections[2].bodyMarkdown', 'Le service est qualifi├® avant mise en ┼ôuvre. Lorsque le prix ne peut pas ├¬tre d├®termin├® imm├®diatement, nous vous proposons un devis adapt├® ├á votre besoin.'), updated_at = UTC_TIMESTAMP() WHERE content_key IN ('storefront:infogerance-vps','storefront:hebergement-web','storefront:maintenance-linux','storefront:maintenance-wordpress','storefront:sauvegarde-externalisee','storefront:supervision-nas','storefront:vpn-entreprise','storefront:bureau-windows-distance','storefront:unifi','storefront:firewall','storefront:cloudflare-waf','storefront:gestion-dns-domaines','storefront:messagerie-professionnelle') AND JSON_VALID(body_markdown);

-- statement-break

UPDATE managed_content_entries SET body_markdown = JSON_SET(body_markdown, '$.sections[2].bodyMarkdown', 'Le tarif d├®pend du nombre d''├®quipements ou de services ├á superviser, des acc├¿s n├®cessaires et du niveau de suivi attendu. Lorsque le prix ne peut pas ├¬tre d├®termin├® imm├®diatement, nous vous proposons un devis adapt├® ├á votre besoin.'), updated_at = UTC_TIMESTAMP() WHERE content_key = 'storefront:supervision-informatique' AND JSON_VALID(body_markdown);

-- statement-break

UPDATE managed_content_entries SET body_markdown = JSON_SET(body_markdown, '$.faq[2].question', 'Est-ce un configurateur d''offre ?', '$.faq[2].answer', 'Non. Cette page explique le service ; les offres restent sur leur parcours d├®di├®.'), updated_at = UTC_TIMESTAMP() WHERE content_key = 'storefront:bureau-windows-distance' AND JSON_VALID(body_markdown);

-- statement-break

UPDATE managed_content_entries SET body_markdown = JSON_SET(body_markdown, '$.faq[0].answer', 'Pas automatiquement : les licences n├®cessaires sont indiqu├®es s├®par├®ment lorsqu''elles s''appliquent.'), updated_at = UTC_TIMESTAMP() WHERE content_key = 'storefront:messagerie-professionnelle' AND JSON_VALID(body_markdown);

-- statement-break

UPDATE managed_content_entries SET title = CASE content_key WHEN 'pack-sheet:pack-dossier-securise' THEN 'Fiche technique - Offre Dossier S├®curis├®' WHEN 'pack-sheet:pack-acces-distance' THEN 'Fiche technique - Offre Acc├¿s ├á Distance' WHEN 'pack-sheet:pack-bureau-windows-distance' THEN 'Fiche technique - Offre Bureau Windows ├á Distance' WHEN 'pack-sheet:pack-pro-association' THEN 'Fiche technique - Offre Pro / Association' ELSE title END, body_markdown = REPLACE(REPLACE(REPLACE(body_markdown, 'Une formule plus compl├¿te pour une petite structure, avec plus de capacit├® et une documentation simplifi├®e.', 'Une offre plus compl├¿te pour une petite structure, avec plus de capacit├® et une documentation simplifi├®e.'), 'La composition technique li├®e ├á ce pack', 'La composition technique li├®e ├á cette offre'), 'Cette fiche d├®crit le p├®rim├¿tre standard du pack', 'Cette fiche d├®crit le p├®rim├¿tre standard de l''offre'), updated_at = UTC_TIMESTAMP() WHERE content_key IN ('pack-sheet:pack-dossier-securise','pack-sheet:pack-acces-distance','pack-sheet:pack-bureau-windows-distance','pack-sheet:pack-pro-association');

-- statement-break

UPDATE public_pack_catalog_content SET content_json = JSON_SET(content_json, '$.pageEyebrow', 'Catalogue des offres', '$.pageTitle', 'Des offres simples, lisibles et pr├¬tes ├á activer', '$.pageDescription', 'Comparez les offres, choisissez votre dur├®e d''engagement, puis lancez votre demande ├á partir d''un p├®rim├¿tre clair.', '$.footnotePrimary', 'Les tarifs affich├®s sont hors taxes et correspondent au catalogue public actuel. La mise en service et le support sont organis├®s selon l''offre retenue.', '$.packs[0].label', 'Offre Dossier S├®curis├®', '$.packs[1].label', 'Offre Acc├¿s ├á Distance', '$.packs[1].highlights[0]', 'Tout ce que comprend l''offre Dossier S├®curis├®', '$.packs[2].label', 'Offre Bureau Windows ├á Distance', '$.packs[3].label', 'Offre Pro / Association', '$.packs[3].description', 'Une offre plus compl├¿te pour une petite structure, avec plus de capacit├®.'), updated_at = UTC_TIMESTAMP() WHERE content_key = 'public-pack-catalog' AND JSON_VALID(content_json);
