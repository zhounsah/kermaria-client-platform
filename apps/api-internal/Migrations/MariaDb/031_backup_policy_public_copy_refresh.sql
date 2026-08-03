UPDATE commercial_offers
SET description = 'Sauvegarde automatique quotidienne des données couvertes par le service\nConservation des versions sauvegardées pendant 31 jours glissants\nLes données créées ou modifiées depuis la dernière sauvegarde réussie peuvent ne pas être récupérables\nLe nombre de points disponibles dépend de la réussite effective des sauvegardes\nFacturation mensuelle',
    updated_at = UTC_TIMESTAMP(6)
WHERE external_reference = 'SAVE-PERSO'
  AND (
    description IS NULL
    OR description <> 'Sauvegarde automatique quotidienne des données couvertes par le service\nConservation des versions sauvegardées pendant 31 jours glissants\nLes données créées ou modifiées depuis la dernière sauvegarde réussie peuvent ne pas être récupérables\nLe nombre de points disponibles dépend de la réussite effective des sauvegardes\nFacturation mensuelle'
  );
