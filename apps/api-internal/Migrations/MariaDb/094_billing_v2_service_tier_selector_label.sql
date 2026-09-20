-- ============================================================================
-- Billing V2 : libellé public du choix de palier.
--
-- Le libellé d'une valeur de palier (ex. « 64 Go ») ne peut pas aussi nommer
-- le contrôle qui la sélectionne. Cette métadonnée de catalogue est purement
-- de présentation : elle ne touche ni aux prix, ni aux subscriptions, ni aux
-- providers, ni au provisioning.
--
-- Additive et récupérable après une application DDL partielle : l'ajout de
-- colonne et le backfill sont idempotents. Ne pas exécuter sans cible MariaDB
-- explicitement jetable et autorisée.
-- ============================================================================

ALTER TABLE billing_v2_services
    ADD COLUMN IF NOT EXISTS tier_selector_label VARCHAR(160) NULL
        AFTER description;

-- statement-break

-- Valeurs de présentation initiales observées dans le configurateur public.
-- Elles résident désormais dans le catalogue, jamais dans une condition React
-- sur le code de service. La clause IS NULL respecte toute personnalisation
-- ultérieure déjà saisie par un administrateur.
UPDATE billing_v2_services
SET tier_selector_label = CASE code
    WHEN 'STORAGE-PERSONAL' THEN 'Capacité de stockage'
    WHEN 'STORAGE-SHARED' THEN 'Capacité de stockage'
    WHEN 'BACKUP-PERSONAL' THEN 'Capacité protégée'
    WHEN 'BACKUP-SHARED' THEN 'Capacité protégée'
    WHEN 'VPN-ACCESS' THEN 'Niveau de service'
    WHEN 'VPS-LOCAL' THEN 'Configuration'
    WHEN 'VPS-CLOUD' THEN 'Configuration'
    ELSE tier_selector_label
END
WHERE tier_selector_label IS NULL
  AND code IN (
      'STORAGE-PERSONAL', 'STORAGE-SHARED',
      'BACKUP-PERSONAL', 'BACKUP-SHARED',
      'VPN-ACCESS', 'VPS-LOCAL', 'VPS-CLOUD'
  );
