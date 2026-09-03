-- Synchronise les contenus publics persistants avec la copie validee apres audit v2.0.2.5.
SET NAMES utf8mb4;

-- statement-break

UPDATE managed_content_entries SET body_markdown = JSON_SET(body_markdown, '$.seoTitle', 'Tarifs des services IT : unités et devis', '$.sections[2].heading', 'Offres et services', '$.sections[2].bodyMarkdown', 'Les offres sont des configurations prêtes à l''emploi et configurables. Elles ne remplacent pas l''ensemble des services Zachary IT.', '$.faq[2].answer', 'Non par défaut : les licences de fournisseurs sont précisées séparément dans le devis quand elles sont nécessaires.', '$.relatedLinks[0].label', 'Voir les offres'), updated_at = UTC_TIMESTAMP() WHERE content_key = 'storefront:tarifs' AND JSON_VALID(body_markdown);

-- statement-break

UPDATE managed_content_entries SET body_markdown = JSON_SET(body_markdown, '$.sections[0].bodyMarkdown', 'Un VPS Zachary IT ou un VPS Cloud peut être préparé et géré ; les caractéristiques CPU, RAM et stockage sont celles affichées sur chaque offre. Lorsque le parcours le permet, la commande peut être payée en ligne, puis la mise en service intervient après validation technique.'), updated_at = UTC_TIMESTAMP() WHERE content_key = 'storefront:cloud-hebergement' AND JSON_VALID(body_markdown);

-- statement-break

UPDATE managed_content_entries SET body_markdown = JSON_SET(body_markdown, '$.sections[0].bodyMarkdown', 'Le VPS Zachary IT et le VPS Cloud ne recouvrent pas la même mise en œuvre. Les caractéristiques CPU, RAM et stockage sont celles affichées sur chaque offre. Lorsque le parcours le permet, la commande peut être payée en ligne, puis la mise en service intervient après validation technique. L''infogérance peut aussi porter sur un VPS chez un autre fournisseur.', '$.sections[1].bodyMarkdown', 'Usages, données, accès, sauvegarde, maintenance, dépendances et responsabilité du fournisseur sont qualifiés avant la mise en service. L''infogérance peut aussi porter sur un VPS chez un autre fournisseur.', '$.sections[2].bodyMarkdown', 'Le service est qualifié avant mise en œuvre. Lorsque le prix ne peut pas être déterminé immédiatement, nous vous proposons un devis adapté à votre besoin.', '$.faq[1].answer', 'Non. Sa mise en service est organisée après validation technique.'), updated_at = UTC_TIMESTAMP() WHERE content_key = 'storefront:vps' AND JSON_VALID(body_markdown);

-- statement-break

UPDATE managed_content_entries SET body_markdown = JSON_SET(body_markdown, '$.sections[2].bodyMarkdown', 'Le service est qualifié avant mise en œuvre. Lorsque le prix ne peut pas être déterminé immédiatement, nous vous proposons un devis adapté à votre besoin.'), updated_at = UTC_TIMESTAMP() WHERE content_key IN ('storefront:infogerance-vps','storefront:hebergement-web','storefront:maintenance-linux','storefront:maintenance-wordpress','storefront:sauvegarde-externalisee','storefront:supervision-nas','storefront:vpn-entreprise','storefront:bureau-windows-distance','storefront:unifi','storefront:firewall','storefront:cloudflare-waf','storefront:gestion-dns-domaines','storefront:messagerie-professionnelle') AND JSON_VALID(body_markdown);

-- statement-break

UPDATE managed_content_entries SET body_markdown = JSON_SET(body_markdown, '$.sections[2].bodyMarkdown', 'Le tarif dépend du nombre d''équipements ou de services à superviser, des accès nécessaires et du niveau de suivi attendu. Lorsque le prix ne peut pas être déterminé immédiatement, nous vous proposons un devis adapté à votre besoin.'), updated_at = UTC_TIMESTAMP() WHERE content_key = 'storefront:supervision-informatique' AND JSON_VALID(body_markdown);

-- statement-break

UPDATE managed_content_entries SET body_markdown = JSON_SET(body_markdown, '$.faq[2].question', 'Est-ce un configurateur d''offre ?', '$.faq[2].answer', 'Non. Cette page explique le service ; les offres restent sur leur parcours dédié.'), updated_at = UTC_TIMESTAMP() WHERE content_key = 'storefront:bureau-windows-distance' AND JSON_VALID(body_markdown);

-- statement-break

UPDATE managed_content_entries SET body_markdown = JSON_SET(body_markdown, '$.faq[0].answer', 'Pas automatiquement : les licences nécessaires sont indiquées séparément lorsqu''elles s''appliquent.'), updated_at = UTC_TIMESTAMP() WHERE content_key = 'storefront:messagerie-professionnelle' AND JSON_VALID(body_markdown);

-- statement-break

UPDATE managed_content_entries SET title = CASE content_key WHEN 'pack-sheet:pack-dossier-securise' THEN 'Fiche technique - Offre Dossier Sécurisé' WHEN 'pack-sheet:pack-acces-distance' THEN 'Fiche technique - Offre Accès à Distance' WHEN 'pack-sheet:pack-bureau-windows-distance' THEN 'Fiche technique - Offre Bureau Windows à Distance' WHEN 'pack-sheet:pack-pro-association' THEN 'Fiche technique - Offre Pro / Association' ELSE title END, body_markdown = REPLACE(REPLACE(REPLACE(body_markdown, 'Une formule plus complète pour une petite structure, avec plus de capacité et une documentation simplifiée.', 'Une offre plus complète pour une petite structure, avec plus de capacité et une documentation simplifiée.'), 'La composition technique liée à ce pack', 'La composition technique liée à cette offre'), 'Cette fiche décrit le périmètre standard du pack', 'Cette fiche décrit le périmètre standard de l''offre'), updated_at = UTC_TIMESTAMP() WHERE content_key IN ('pack-sheet:pack-dossier-securise','pack-sheet:pack-acces-distance','pack-sheet:pack-bureau-windows-distance','pack-sheet:pack-pro-association');

-- statement-break

UPDATE public_pack_catalog_content SET content_json = JSON_SET(content_json, '$.pageEyebrow', 'Catalogue des offres', '$.pageTitle', 'Des offres simples, lisibles et prêtes à activer', '$.pageDescription', 'Comparez les offres, choisissez votre durée d''engagement, puis lancez votre demande à partir d''un périmètre clair.', '$.footnotePrimary', 'Les tarifs affichés sont hors taxes et correspondent au catalogue public actuel. La mise en service et le support sont organisés selon l''offre retenue.', '$.packs[0].label', 'Offre Dossier Sécurisé', '$.packs[1].label', 'Offre Accès à Distance', '$.packs[1].highlights[0]', 'Tout ce que comprend l''offre Dossier Sécurisé', '$.packs[2].label', 'Offre Bureau Windows à Distance', '$.packs[3].label', 'Offre Pro / Association', '$.packs[3].description', 'Une offre plus complète pour une petite structure, avec plus de capacité.'), updated_at = UTC_TIMESTAMP() WHERE content_key = 'public-pack-catalog' AND JSON_VALID(content_json);
