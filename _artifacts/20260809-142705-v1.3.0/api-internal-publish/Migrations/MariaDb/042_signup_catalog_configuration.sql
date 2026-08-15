ALTER TABLE signup_pending
    ADD COLUMN IF NOT EXISTS catalog_configuration_snapshot_json LONGTEXT NULL
    AFTER pack_selection_snapshot_json;
