-- ============================================================================
-- Billing V2 : provenance explicite des items de Cart.
--
-- Phase 3 utilise cette metadonnee uniquement pour presenter et compter les
-- selections commerciales sans confondre une intention client avec un socle
-- structurel ou une dependance ajoutee par le serveur. Les lignes existantes
-- restent intactes : leur provenance legacy est derivee en lecture.
--
-- Ne pas executer sans cible MariaDB explicitement jetable et autorisee.
-- Aucune subscription, aucun prix, aucun evenement financier ni provider n'est
-- touche par cette migration.
-- ============================================================================

ALTER TABLE billing_v2_cart_items
    ADD COLUMN IF NOT EXISTS origin VARCHAR(24) NULL
        AFTER source_preset_item_id;
