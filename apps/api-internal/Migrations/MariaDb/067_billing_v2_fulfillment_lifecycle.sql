-- ============================================================================
-- Billing V2.1 : fulfillment commercial distinct du provisioning technique.
-- ============================================================================

CREATE TABLE IF NOT EXISTS billing_v2_service_fulfillment_profiles (
    id                              CHAR(36)      NOT NULL,
    service_id                      CHAR(36)      NOT NULL,
    tier_id                         CHAR(36)      NULL,
    fulfillment_mode                VARCHAR(32)   NOT NULL,
    default_backend                 VARCHAR(64)   NOT NULL DEFAULT 'MANUAL',
    status                          VARCHAR(24)   NOT NULL DEFAULT 'active',
    created_at                      DATETIME(6)   NOT NULL DEFAULT UTC_TIMESTAMP(6),
    updated_at                      DATETIME(6)   NOT NULL DEFAULT UTC_TIMESTAMP(6),
    PRIMARY KEY (id),
    UNIQUE KEY uq_billing_v2_fulfillment_profile (service_id, tier_id),
    CONSTRAINT chk_billing_v2_fulfillment_mode CHECK (
        fulfillment_mode IN ('contractual_acknowledgement', 'manual_delivery', 'technical_provisioning')
    ),
    CONSTRAINT fk_billing_v2_fulfillment_profile_service
        FOREIGN KEY (service_id) REFERENCES billing_v2_services(id)
        ON UPDATE RESTRICT ON DELETE RESTRICT,
    CONSTRAINT fk_billing_v2_fulfillment_profile_tier
        FOREIGN KEY (tier_id) REFERENCES billing_v2_service_tiers(id)
        ON UPDATE RESTRICT ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- statement-break

CREATE TABLE IF NOT EXISTS billing_v2_subscription_item_fulfillment (
    subscription_item_id            CHAR(36)      NOT NULL,
    fulfillment_profile_id          CHAR(36)      NULL,
    backend                         VARCHAR(64)   NOT NULL DEFAULT 'MANUAL',
    provider_resource_id            VARCHAR(255)  NULL,
    region                          VARCHAR(96)   NULL,
    fulfillment_status              VARCHAR(32)   NOT NULL DEFAULT 'pending',
    last_error                      TEXT          NULL,
    requested_at                    DATETIME(6)   NOT NULL DEFAULT UTC_TIMESTAMP(6),
    started_at                      DATETIME(6)   NULL,
    fulfilled_at                    DATETIME(6)   NULL,
    updated_at                      DATETIME(6)   NOT NULL DEFAULT UTC_TIMESTAMP(6),
    PRIMARY KEY (subscription_item_id),
    CONSTRAINT chk_billing_v2_item_fulfillment_status CHECK (
        fulfillment_status IN ('pending', 'in_progress', 'fulfilled', 'failed')
    ),
    CONSTRAINT fk_billing_v2_item_fulfillment_item
        FOREIGN KEY (subscription_item_id) REFERENCES billing_v2_subscription_items(id)
        ON UPDATE RESTRICT ON DELETE RESTRICT,
    CONSTRAINT fk_billing_v2_item_fulfillment_profile
        FOREIGN KEY (fulfillment_profile_id) REFERENCES billing_v2_service_fulfillment_profiles(id)
        ON UPDATE RESTRICT ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
