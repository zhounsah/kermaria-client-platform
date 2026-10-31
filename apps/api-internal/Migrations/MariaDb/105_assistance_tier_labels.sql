-- Corrige les deux descriptions publiques introduites par 104.
-- Les codes, prix et droits sont inchanges ; les valeurs personnalisees
-- ulterieurement par l'administration sont preservees.
UPDATE billing_v2_service_tiers t
JOIN billing_v2_services s ON s.id = t.service_id
SET t.description = CASE t.code
    WHEN 'ASSIST-PERS-SERENITE' THEN 'Particuliers : jusqu''à 2 ordinateurs, 45 minutes par mois.'
    WHEN 'ASSIST-PERS-PREMIUM' THEN 'Particuliers : jusqu''à 4 ordinateurs, 90 minutes par mois.'
    ELSE t.description
END
WHERE s.code = 'ASSISTANCE-MAINTENANCE'
  AND ((t.code = 'ASSIST-PERS-SERENITE'
        AND t.description = 'Particuliers : jusqu a 2 ordinateurs, 45 minutes par mois.')
    OR (t.code = 'ASSIST-PERS-PREMIUM'
        AND t.description = 'Particuliers : jusqu a 4 ordinateurs, 90 minutes par mois.'));
