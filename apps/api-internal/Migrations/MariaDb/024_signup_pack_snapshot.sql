ALTER TABLE signup_pending
    ADD COLUMN IF NOT EXISTS pack_selection_snapshot_json LONGTEXT NULL;

-- statement-break

ALTER TABLE signup_pending
    ADD KEY IF NOT EXISTS idx_signup_approved_customer_status
        (approved_customer_id, status, approved_at);
