SELECT 'ad_active_idempotency_duplicate_rows' AS metric, COUNT(*) AS value FROM (SELECT idempotency_key_hash, COUNT(*) c FROM ad_actions WHERE idempotency_key_hash IS NOT NULL AND status IN ('requested','running') GROUP BY idempotency_key_hash HAVING c > 1) dup;
SELECT 'outbox_duplicate_precondition_rows' AS metric, COUNT(*) AS value FROM (SELECT idempotency_key_hash, COUNT(*) c FROM billing_v2_outbox_events WHERE idempotency_key_hash IS NOT NULL GROUP BY idempotency_key_hash HAVING c > 1) dup;
SHOW CREATE TABLE ad_actions;
SHOW CREATE TABLE billing_v2_subscription_documents;
SHOW CREATE TABLE billing_v2_document_line_snapshots;
SHOW INDEX FROM ad_actions WHERE Key_name IN ('ux_ad_actions_active_idempotency','ix_ad_actions_idempotency','ix_ad_actions_subscription');
SHOW INDEX FROM billing_v2_subscription_documents;
SHOW INDEX FROM billing_v2_document_line_snapshots;
SELECT table_name, column_name, referenced_table_name, referenced_column_name, constraint_name FROM information_schema.key_column_usage WHERE table_schema = DATABASE() AND table_name IN ('billing_v2_subscription_documents','billing_v2_document_line_snapshots','ad_actions') AND referenced_table_name IS NOT NULL ORDER BY table_name, constraint_name, ordinal_position;
