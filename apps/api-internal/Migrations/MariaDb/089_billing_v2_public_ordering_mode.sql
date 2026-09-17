-- ============================================================================
-- Zachary IT - Billing V2
-- Migration 089 : mode de commercialisation public administrable
--
-- Strictement additive : ce champ ne touche ni aux montants versionnés, ni
-- aux snapshots, ni aux abonnements, ni aux providers, ni au provisioning.
-- Il décrit seulement le parcours public à présenter pour un service.
-- ============================================================================

SET NAMES utf8mb4;

-- statement-break

ALTER TABLE billing_v2_services
    ADD COLUMN IF NOT EXISTS public_ordering_mode VARCHAR(32) NOT NULL
        DEFAULT 'quote' AFTER self_service_orderable;

-- statement-break

-- Reproduit le comportement commercial existant au plus près sans créer de
-- commande directe : un service déjà composé dans au moins une offre publique
-- active devient un composant d'offre ; tous les autres restent sur devis.
UPDATE billing_v2_services AS service
SET public_ordering_mode = 'offer_component'
WHERE service.public_ordering_mode = 'quote'
  AND EXISTS (
      SELECT 1
      FROM billing_v2_preset_items AS item
      INNER JOIN billing_v2_offer_presets AS preset
          ON preset.id = item.preset_id
      WHERE item.service_id = service.id
        AND preset.status = 'active'
        AND preset.is_public = 1
  );
