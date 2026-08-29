-- 078_settings_audit_index.sql
-- Index de lecture pour l'audit du Centre de configuration.
--
-- La page d'audit filtre `audit_logs` sur un petit ensemble ferme d'actions,
-- puis trie par date decroissante. Sans index sur (action, occurred_at), cette
-- lecture parcourt tout le journal : il grossit indefiniment, alors que la
-- fenetre consultee reste petite.
--
-- Purement additif : aucune donnee n'est modifiee, aucune contrainte ajoutee.
-- Le rollback consiste a supprimer l'index.

CREATE INDEX IF NOT EXISTS ix_audit_logs_action_occurred_at
    ON audit_logs (action, occurred_at);
