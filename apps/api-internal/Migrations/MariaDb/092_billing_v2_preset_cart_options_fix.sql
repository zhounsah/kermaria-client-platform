-- ============================================================================
-- Billing V2 : correction additive des definitions multi-tier des presets Cart.
--
-- Post-091, les tiers backup ne sont pas public_selectable : ils sont resolus
-- serveur par same_numeric_value depuis leur stockage couvert. Les options
-- BACKUP-SHARED ont donc ete omises par 091. Le configurateur legacy expose
-- egalement tous les tiers publics de stockage personnel, pas seulement le
-- tier par defaut conserve par chaque preset historique.
--
-- Ne pas executer sans cible MariaDB explicitement jetable et autorisee.
-- Aucune subscription, aucun Cart existant, aucun prix ni evenement financier
-- n'est modifie par cette migration.
-- ============================================================================

-- Les 15 BACKUP-SHARED absents : 4 tiers pour les trois premiers presets et
-- 32/64/256 pour Pro (128 est deja sa selection historique par defaut).
-- Les backups ne sont volontairement pas filtres sur public_selectable : leur
-- palier est derive cote serveur du STORAGE-SHARED de meme valeur numerique.
INSERT INTO billing_v2_preset_items
    (id, preset_id, service_id, tier_id, scope_template, quantity,
     required_item, customer_editable, selected_by_default, minimum_quantity, maximum_quantity, display_order)
SELECT UUID(), preset.id, service.id, tier.id, 'subscription', 1, 0, 1, 0, 1, 1, 60
FROM billing_v2_offer_presets preset
JOIN billing_v2_services service ON service.code = 'BACKUP-SHARED'
JOIN billing_v2_service_tiers tier ON tier.service_id = service.id
    AND tier.status = 'active' AND tier.code IN ('32', '64', '128', '256')
WHERE preset.code IN ('pack-dossier-securise', 'pack-acces-distance',
                      'pack-bureau-windows-distance', 'pack-pro-association')
  AND NOT EXISTS (
      SELECT 1 FROM billing_v2_preset_items existing
      WHERE existing.preset_id = preset.id
        AND existing.service_id = service.id
        AND existing.tier_id = tier.id
        AND existing.scope_template = 'subscription'
  );

-- statement-break

-- Le legacy affiche tous les tiers publics de STORAGE-PERSONAL. Chaque preset
-- conserve son tier historique deja ON (32 pour Dossier/Acces, 64 pour
-- Bureau/Pro) ; les 16 definitions soeurs ajoutees ici restent des options OFF.
INSERT INTO billing_v2_preset_items
    (id, preset_id, service_id, tier_id, scope_template, quantity,
     required_item, customer_editable, selected_by_default, minimum_quantity, maximum_quantity, display_order)
SELECT UUID(), preset.id, service.id, tier.id, 'primary_user', 1, 0, 1, 0, 1, 1, 20
FROM billing_v2_offer_presets preset
JOIN billing_v2_services service ON service.code = 'STORAGE-PERSONAL'
JOIN billing_v2_service_tiers tier ON tier.service_id = service.id
    AND tier.status = 'active' AND tier.public_selectable = 1
    AND tier.code IN ('16', '32', '64', '128', '256')
WHERE preset.code IN ('pack-dossier-securise', 'pack-acces-distance',
                      'pack-bureau-windows-distance', 'pack-pro-association')
  AND NOT EXISTS (
      SELECT 1 FROM billing_v2_preset_items existing
      WHERE existing.preset_id = preset.id
        AND existing.service_id = service.id
        AND existing.tier_id = tier.id
        AND existing.scope_template = 'primary_user'
  );

-- statement-break

-- Les 16 BACKUP-PERSONAL correspondants. Comme pour le partage, le backup
-- n'est pas directement public : Billing V2 le cale sur le numeric_value du
-- stockage personnel choisi, via la dependance same_numeric_value.
INSERT INTO billing_v2_preset_items
    (id, preset_id, service_id, tier_id, scope_template, quantity,
     required_item, customer_editable, selected_by_default, minimum_quantity, maximum_quantity, display_order)
SELECT UUID(), preset.id, service.id, tier.id, 'primary_user', 1, 0, 1, 0, 1, 1, 30
FROM billing_v2_offer_presets preset
JOIN billing_v2_services service ON service.code = 'BACKUP-PERSONAL'
JOIN billing_v2_service_tiers tier ON tier.service_id = service.id
    AND tier.status = 'active' AND tier.code IN ('16', '32', '64', '128', '256')
WHERE preset.code IN ('pack-dossier-securise', 'pack-acces-distance',
                      'pack-bureau-windows-distance', 'pack-pro-association')
  AND NOT EXISTS (
      SELECT 1 FROM billing_v2_preset_items existing
      WHERE existing.preset_id = preset.id
        AND existing.service_id = service.id
        AND existing.tier_id = tier.id
        AND existing.scope_template = 'primary_user'
  );
