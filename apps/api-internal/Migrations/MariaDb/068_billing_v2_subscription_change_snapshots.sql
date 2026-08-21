-- ============================================================================
-- Billing V2.1 : snapshots de composantes proposes par un changement.
-- Aucune ligne contractuelle historique n'est modifiee par ce schema.
-- ============================================================================

CREATE TABLE IF NOT EXISTS billing_v2_subscription_change_item_components (
    id                              CHAR(36)      NOT NULL,
    subscription_change_item_id     CHAR(36)      NOT NULL,
    service_price_id                CHAR(36)      NOT NULL,
    billing_cadence                 VARCHAR(24)   NOT NULL,
    charge_trigger                  VARCHAR(32)   NOT NULL,
    amount_cents_snapshot           BIGINT        NOT NULL,
    currency                        CHAR(3)       NOT NULL,
    discount_eligible_snapshot      TINYINT(1)    NOT NULL DEFAULT 1,
    display_order                   INT           NOT NULL DEFAULT 0,
    created_at                      DATETIME(6)   NOT NULL DEFAULT UTC_TIMESTAMP(6),
    PRIMARY KEY (id),
    UNIQUE KEY uq_billing_v2_change_component
        (subscription_change_item_id, service_price_id, billing_cadence),
    CONSTRAINT fk_billing_v2_change_component_item
        FOREIGN KEY (subscription_change_item_id)
        REFERENCES billing_v2_subscription_change_items(id)
        ON UPDATE RESTRICT ON DELETE CASCADE,
    CONSTRAINT fk_billing_v2_change_component_price
        FOREIGN KEY (service_price_id) REFERENCES billing_v2_service_prices(id)
        ON UPDATE RESTRICT ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
