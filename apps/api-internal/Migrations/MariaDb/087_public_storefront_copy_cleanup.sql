-- Mise a jour editoriale ciblee des contenus storefront deja persistants.
--
-- Le seed ne complete que les entrees manquantes : ces remplacements exacts
-- sont donc necessaires pour corriger les anciennes phrases sans ecraser les
-- autres adaptations effectuees depuis l'administration.
SET NAMES utf8mb4;

-- statement-break

UPDATE managed_content_entries
SET body_markdown = REPLACE(
    body_markdown,
    'Les prix publiés viennent du catalogue Billing lorsqu’ils sont disponibles. Les services de mise en place, migration, réseau ou infogérance restent qualifiés avant devis.',
    'Les prix affichés correspondent aux services proposés. Les services de mise en place, migration, réseau ou infogérance restent qualifiés avant devis.'
)
WHERE content_key = 'storefront:tarifs'
  AND body_markdown LIKE '%Les prix publiés viennent du catalogue Billing lorsqu’ils sont disponibles.%';

-- statement-break

UPDATE managed_content_entries
SET body_markdown = REPLACE(
    body_markdown,
    'Selon le service, la facturation peut être exprimée par domaine, utilisateur, site, serveur ou instance et par mois. Le montant affiché, lorsqu’il existe, est une projection du catalogue Billing ; il n’est jamais recopié dans ce contenu.',
    'Selon le service, la facturation peut être exprimée par domaine, utilisateur, site, serveur ou instance et par mois. Le montant affiché correspond au service et à son unité de facturation.'
)
WHERE content_key = 'storefront:tarifs'
  AND body_markdown LIKE '%est une projection du catalogue Billing%';

-- statement-break

UPDATE managed_content_entries
SET body_markdown = REPLACE(body_markdown, 'Formules et catalogue', 'Formules et services')
WHERE content_key = 'storefront:tarifs'
  AND body_markdown LIKE '%Formules et catalogue%';

-- statement-break

UPDATE managed_content_entries
SET body_markdown = REPLACE(
    body_markdown,
    'Les caractéristiques CPU, RAM et stockage des paliers VPS Cloud sont publiées depuis le catalogue Billing V2.1 ; leur mise en service reste manuelle et cadrée.',
    'Les caractéristiques CPU, RAM et stockage sont celles affichées sur chaque offre. Lorsque le parcours le permet, la commande peut être payée en ligne, puis la mise en service intervient après validation technique.'
)
WHERE content_key IN ('storefront:vps', 'storefront:cloud-hebergement')
  AND body_markdown LIKE '%catalogue Billing V2.1%';

-- statement-break

UPDATE managed_content_entries
SET body_markdown = REPLACE(
    body_markdown,
    'Le provisioning réel peut rester désactivé même si le service est présenté publiquement.',
    'La mise en service est organisée après la validation technique requise.'
)
WHERE content_key = 'storefront:vps'
  AND body_markdown LIKE '%Le provisioning réel peut rester désactivé%';

-- statement-break

UPDATE managed_content_entries
SET body_markdown = REPLACE(
    body_markdown,
    'Non. Sa mise en service est manuelle et cadrée par devis.',
    'Non. Sa mise en service est organisée après validation technique.'
)
WHERE content_key = 'storefront:vps'
  AND body_markdown LIKE '%Sa mise en service est manuelle et cadrée par devis.%';

-- statement-break

UPDATE managed_content_entries
SET body_markdown = REPLACE(
    body_markdown,
    'Lorsqu’aucune projection Billing autoritative n’est disponible, Zachary IT propose un devis plutôt qu’un prix inventé.',
    'Lorsque le prix ne peut pas être déterminé immédiatement, nous vous proposons un devis adapté à votre besoin.'
)
WHERE body_markdown LIKE '%Lorsqu’aucune projection Billing autoritative n’est disponible%';
