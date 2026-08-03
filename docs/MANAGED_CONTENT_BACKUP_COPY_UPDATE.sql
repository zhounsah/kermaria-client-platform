-- Mise à jour manuelle et prudente du contenu administré public.
-- Objectif : aligner uniquement les pages juridiques si elles contiennent encore
-- l'ancienne formulation contradictoire (30 jours / restauration mensuelle).
-- Cette procédure n'écrase pas aveuglément des modifications admin plus récentes.

SELECT content_key,
       title,
       version_label,
       updated_at,
       CASE
           WHEN body_markdown LIKE '%30 jours glissants%'
             OR body_markdown LIKE '%restauration par mois est incluse%'
             OR body_markdown LIKE '%demande de restauration par mois est incluse%'
             OR body_markdown LIKE '%30 jours après la fin du service%'
           THEN 'review_required'
           ELSE 'leave_unchanged'
       END AS suggested_action
FROM managed_content_entries
WHERE content_key IN ('legal:cgv', 'legal:politique-confidentialite');

-- Après revue humaine du SELECT ci-dessus, exécuter l'UPDATE uniquement si
-- l'entrée ciblée contient encore l'ancien texte à corriger.
--
-- Exemple : mise à jour de `legal:cgv`
-- 1. Ouvrir le contenu actuel en admin ou l'exporter.
-- 2. Remplacer seulement les passages contradictoires par le nouveau texte validé.
-- 3. Réinjecter le markdown complet validé via `/admin/content/legal:cgv`
--    ou via un UPDATE SQL ciblé avec garde sur `updated_at` ou sur le contenu courant.
--
-- Exemple de garde SQL minimale :
-- UPDATE managed_content_entries
-- SET body_markdown = :new_body_markdown,
--     version_label = 'Version du : 03 août 2026',
--     updated_at = UTC_TIMESTAMP(6)
-- WHERE content_key = 'legal:cgv'
--   AND updated_at = :reviewed_updated_at
--   AND (
--     body_markdown LIKE '%30 jours glissants%'
--     OR body_markdown LIKE '%restauration par mois est incluse%'
--     OR body_markdown LIKE '%demande de restauration par mois est incluse%'
--     OR body_markdown LIKE '%30 jours après la fin du service%'
--   );
--
-- Répéter la même approche pour `legal:politique-confidentialite` seulement si
-- le contenu administré diverge encore du texte validé.
