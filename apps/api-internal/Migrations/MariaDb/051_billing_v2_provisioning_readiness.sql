-- ============================================================================
-- Zachary IT - Billing V2
-- Migration 051 : readiness explicite du provisioning V2 par client
--
-- Objectif :
--   empêcher toute activation du provisioning V2 réel par simple flag global.
--   Un client doit être explicitement marqué prêt après shadow success et revue.
--
-- Cette table ne déclenche aucun provisioning. Elle ajoute seulement une gate
-- applicative fail-closed consultée avant toute action V2.
-- ============================================================================

CREATE TABLE IF NOT EXISTS billing_v2_provisioning_client_readiness (
    customer_id                         CHAR(36)      NOT NULL,

    ready_for_v2_provisioning           TINYINT(1)    NOT NULL DEFAULT 0,
    add_only_mode                       TINYINT(1)    NOT NULL DEFAULT 1,

    last_shadow_status                  VARCHAR(24)   NULL,
    last_shadow_matches_legacy          TINYINT(1)    NULL,
    unresolved_mismatch_count           INT           NOT NULL DEFAULT 0,

    reviewed_by_reference               VARCHAR(255)  NULL,
    reviewed_at                         DATETIME(6)   NULL,
    notes                               TEXT          NULL,

    created_at                          DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at                          DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
                                                    ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (customer_id),
    KEY idx_billing_v2_provisioning_readiness_ready
        (ready_for_v2_provisioning, last_shadow_status, last_shadow_matches_legacy)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
