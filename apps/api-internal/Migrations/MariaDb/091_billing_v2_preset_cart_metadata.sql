-- ============================================================================
-- Billing V2 : definition complete des presets pour le configurateur Cart.
-- Additif : aucune subscription, aucun prix, aucun evenement financier.
-- Ne pas executer sans cible MariaDB explicitement jetable et autorisee.
-- ============================================================================

ALTER TABLE billing_v2_preset_items
    ADD COLUMN IF NOT EXISTS selected_by_default TINYINT(1) NOT NULL DEFAULT 1 AFTER customer_editable,
    ADD COLUMN IF NOT EXISTS minimum_quantity INT NOT NULL DEFAULT 1 AFTER selected_by_default,
    ADD COLUMN IF NOT EXISTS maximum_quantity INT NOT NULL DEFAULT 1 AFTER minimum_quantity;

-- statement-break

-- Toutes les lignes historiques restent dans leur composition initiale. Les
-- bornes non explicites sont fixes a 1 ; la seule borne legacy connue est le
-- compteur d'utilisateurs supplementaires (1..10).
UPDATE billing_v2_preset_items item
JOIN billing_v2_services service ON service.id = item.service_id
SET item.selected_by_default = 1,
    item.minimum_quantity = 1,
    item.maximum_quantity = CASE WHEN service.code = 'USER-ADDITIONAL' THEN 10 ELSE 1 END;

-- statement-break

-- Sur les quatre formules portees par le configurateur historique, seuls le
-- socle et le stockage personnel sont fixes. Les toggles initialement ON
-- restent selectionnes, mais deviennent retirables par le client.
UPDATE billing_v2_preset_items item
JOIN billing_v2_offer_presets preset ON preset.id = item.preset_id
JOIN billing_v2_services service ON service.id = item.service_id
SET item.required_item = CASE
        WHEN service.code IN ('BASE-SERVICE', 'STORAGE-PERSONAL') THEN 1
        ELSE 0
    END
WHERE preset.code IN ('pack-dossier-securise', 'pack-acces-distance',
                      'pack-bureau-windows-distance', 'pack-pro-association');

-- statement-break

-- Options plates explicites du configurateur historique. Les lignes deja
-- selectionnees par defaut (RDS Bureau, utilisateur/support Pro) restent les
-- lignes 048 ; seules les definitions absentes sont ajoutees en OFF.
INSERT INTO billing_v2_preset_items
    (id, preset_id, service_id, tier_id, scope_template, quantity,
     required_item, customer_editable, selected_by_default, minimum_quantity, maximum_quantity, display_order)
SELECT UUID(), preset.id, service.id, NULL,
       CASE
           WHEN service.code = 'USER-ADDITIONAL' THEN 'additional_user'
           WHEN service.code = 'RDS-ACCESS' THEN 'primary_user'
           ELSE 'subscription'
       END,
       1, 0, 1, 0, 1, CASE WHEN service.code = 'USER-ADDITIONAL' THEN 10 ELSE 1 END,
       CASE service.code WHEN 'RDS-ACCESS' THEN 70 WHEN 'USER-ADDITIONAL' THEN 80 ELSE 90 END
FROM billing_v2_offer_presets preset
JOIN billing_v2_services service ON service.code IN ('RDS-ACCESS', 'USER-ADDITIONAL', 'SUPPORT-PLUS')
WHERE preset.code IN ('pack-dossier-securise', 'pack-acces-distance',
                      'pack-bureau-windows-distance', 'pack-pro-association')
  AND NOT EXISTS (
      SELECT 1 FROM billing_v2_preset_items existing
      WHERE existing.preset_id = preset.id AND existing.service_id = service.id
        AND existing.scope_template = CASE
            WHEN service.code = 'USER-ADDITIONAL' THEN 'additional_user'
            WHEN service.code = 'RDS-ACCESS' THEN 'primary_user'
            ELSE 'subscription'
        END
  );

-- statement-break

-- Le legacy affiche tous les paliers publics de stockage partage et VPN. Une
-- ligne par palier rend ce choix explicite, sans inventer de palier initial :
-- seuls les composants deja selectionnes en 048 restent ON par defaut.
INSERT INTO billing_v2_preset_items
    (id, preset_id, service_id, tier_id, scope_template, quantity,
     required_item, customer_editable, selected_by_default, minimum_quantity, maximum_quantity, display_order)
SELECT UUID(), preset.id, service.id, tier.id, 'subscription', 1, 0, 1, 0, 1, 1, 50
FROM billing_v2_offer_presets preset
JOIN billing_v2_services service ON service.code = 'STORAGE-SHARED'
JOIN billing_v2_service_tiers tier ON tier.service_id = service.id
  AND tier.status = 'active' AND tier.public_selectable = 1
WHERE preset.code IN ('pack-dossier-securise', 'pack-acces-distance',
                      'pack-bureau-windows-distance', 'pack-pro-association')
  AND NOT EXISTS (
      SELECT 1 FROM billing_v2_preset_items existing
      WHERE existing.preset_id = preset.id AND existing.service_id = service.id
        AND existing.tier_id = tier.id AND existing.scope_template = 'subscription'
  );

-- statement-break

INSERT INTO billing_v2_preset_items
    (id, preset_id, service_id, tier_id, scope_template, quantity,
     required_item, customer_editable, selected_by_default, minimum_quantity, maximum_quantity, display_order)
SELECT UUID(), preset.id, service.id, tier.id, 'subscription', 1, 0, 1, 0, 1, 1, 60
FROM billing_v2_offer_presets preset
JOIN billing_v2_services service ON service.code = 'BACKUP-SHARED'
JOIN billing_v2_service_tiers tier ON tier.service_id = service.id
  AND tier.status = 'active' AND tier.public_selectable = 1
WHERE preset.code IN ('pack-dossier-securise', 'pack-acces-distance',
                      'pack-bureau-windows-distance', 'pack-pro-association')
  AND NOT EXISTS (
      SELECT 1 FROM billing_v2_preset_items existing
      WHERE existing.preset_id = preset.id AND existing.service_id = service.id
        AND existing.tier_id = tier.id AND existing.scope_template = 'subscription'
  );

-- statement-break

INSERT INTO billing_v2_preset_items
    (id, preset_id, service_id, tier_id, scope_template, quantity,
     required_item, customer_editable, selected_by_default, minimum_quantity, maximum_quantity, display_order)
SELECT UUID(), preset.id, service.id, tier.id, 'primary_user', 1, 0, 1, 0, 1, 1, 40
FROM billing_v2_offer_presets preset
JOIN billing_v2_services service ON service.code = 'VPN-ACCESS'
JOIN billing_v2_service_tiers tier ON tier.service_id = service.id
  AND tier.status = 'active' AND tier.public_selectable = 1
WHERE preset.code IN ('pack-dossier-securise', 'pack-acces-distance',
                      'pack-bureau-windows-distance', 'pack-pro-association')
  AND NOT EXISTS (
      SELECT 1 FROM billing_v2_preset_items existing
      WHERE existing.preset_id = preset.id AND existing.service_id = service.id
        AND existing.tier_id = tier.id AND existing.scope_template = 'primary_user'
  );

-- statement-break

-- MariaDB CHECK est une defense additionnelle ; les services gardent la
-- validation transactionnelle pour les versions qui ne l'appliquent pas.
ALTER TABLE billing_v2_preset_items
    ADD CONSTRAINT IF NOT EXISTS chk_billing_v2_preset_item_required_selected
    CHECK (required_item = 0 OR selected_by_default = 1);

-- statement-break

ALTER TABLE billing_v2_preset_items
    ADD CONSTRAINT IF NOT EXISTS chk_billing_v2_preset_item_quantity_bounds
    CHECK (minimum_quantity >= 1 AND maximum_quantity >= minimum_quantity AND quantity BETWEEN minimum_quantity AND maximum_quantity);
