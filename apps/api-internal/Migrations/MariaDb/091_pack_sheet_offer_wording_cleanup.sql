-- Nettoie les quatre fiches offres persistantes.
SET NAMES utf8mb4;

-- statement-break

UPDATE managed_content_entries
SET body_markdown = REPLACE(REPLACE(REPLACE(body_markdown, 'ce pack', 'cette offre'), 'du pack', 'de l''offre'), 'Une formule plus', 'Une offre plus'),
    updated_at = UTC_TIMESTAMP()
WHERE content_key IN (
    'pack-sheet:pack-dossier-securise',
    'pack-sheet:pack-acces-distance',
    'pack-sheet:pack-bureau-windows-distance',
    'pack-sheet:pack-pro-association'
);
