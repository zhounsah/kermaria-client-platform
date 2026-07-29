-- V0.35 - Backfill des offres recurrentes historiques.
-- Les offres inserees par 009_catalog_articles precedaient l'ajout de
-- billing_cadence en 011_subscription_offers. Sur une base existante,
-- elles ont donc herite du defaut one_time alors que leur description et
-- leur tunnel metier sont mensuels.

UPDATE commercial_offers
SET billing_cadence = 'monthly'
WHERE external_reference IN (
    'ACCES-VPN',
    'ACCES-RDS',
    'SAVE-PERSO',
    'STOCK-PERSO-32',
    'STOCK-SUP-32',
    'SUPERV-SERVICE',
    'SUPPORT-LV1',
    'USER-ADD'
)
  AND billing_cadence <> 'monthly';

-- L'offre de demonstration seedee avant le tunnel abonnement reste un
-- achat ponctuel. On normalise simplement son libelle d'unite pour eviter
-- qu'elle apparaisse comme une offre mensuelle dans /souscrire.
UPDATE commercial_offers
SET unit_label = 'Forfait'
WHERE external_reference IS NULL
  AND name = 'Sauvegarde dossier personnel'
  AND category = 'Sauvegarde'
  AND billing_cadence = 'one_time'
  AND LOWER(unit_label) = 'mois';
