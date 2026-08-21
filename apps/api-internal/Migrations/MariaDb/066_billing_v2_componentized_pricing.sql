-- ============================================================================
-- Zachary IT - Billing V2.1
-- Migration 066 : droits contractuels uniques et composantes tarifaires
--
-- Strictement additive. Les items V2 existants restent `legacy_single` : aucun
-- prix, item, lock ou historique n'est modifie ni reconstitue par cette
-- migration. Les nouvelles tables ne deviennent autoritatives que pour les
-- items explicitement crees avec `pricing_representation='componentized'`.
-- ============================================================================

SET NAMES utf8mb4;

-- statement-break

ALTER TABLE billing_v2_subscription_items
    ADD COLUMN IF NOT EXISTS pricing_representation VARCHAR(32) NOT NULL
        DEFAULT 'legacy_single' AFTER discount_eligible_snapshot;

ALTER TABLE billing_v2_subscriptions
    ADD COLUMN IF NOT EXISTS pricing_authority VARCHAR(32) NOT NULL
        DEFAULT 'legacy_global_lock' AFTER billing_model;

-- statement-break

ALTER TABLE billing_v2_services
    ADD COLUMN IF NOT EXISTS public_visible TINYINT(1) NOT NULL DEFAULT 0
        AFTER public_selectable,
    ADD COLUMN IF NOT EXISTS self_service_orderable TINYINT(1) NOT NULL DEFAULT 0
        AFTER public_visible;

-- Les lignes existantes conservent exactement leur semantique publique V2.0.
UPDATE billing_v2_services
SET public_visible = public_selectable,
    self_service_orderable = public_selectable
WHERE public_visible = 0
  AND self_service_orderable = 0;

-- statement-break

ALTER TABLE billing_v2_service_prices
    ADD COLUMN IF NOT EXISTS charge_trigger VARCHAR(32) NOT NULL
        DEFAULT 'initial_subscription' AFTER billing_cadence;

CREATE INDEX IF NOT EXISTS idx_billing_v2_service_prices_cadence_lookup
    ON billing_v2_service_prices
       (service_id, tier_id, currency, billing_cadence, status, valid_from, valid_until);

-- statement-break

CREATE TABLE IF NOT EXISTS billing_v2_service_tier_attributes (
    id                              CHAR(36)      NOT NULL,
    tier_id                         CHAR(36)      NOT NULL,
    attribute_code                  VARCHAR(64)   NOT NULL,
    value_numeric                   BIGINT        NULL,
    value_text                      VARCHAR(255)  NULL,
    unit                            VARCHAR(32)   NULL,
    created_at                      DATETIME(6)   NOT NULL DEFAULT UTC_TIMESTAMP(6),

    PRIMARY KEY (id),
    UNIQUE KEY uq_billing_v2_tier_attribute (tier_id, attribute_code),
    CONSTRAINT chk_billing_v2_tier_attribute_value CHECK (
        value_numeric IS NOT NULL OR value_text IS NOT NULL
    ),
    CONSTRAINT fk_billing_v2_tier_attribute_tier
        FOREIGN KEY (tier_id) REFERENCES billing_v2_service_tiers(id)
        ON UPDATE RESTRICT ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- statement-break

CREATE TABLE IF NOT EXISTS billing_v2_subscription_item_price_components (
    id                              CHAR(36)      NOT NULL,
    subscription_item_id            CHAR(36)      NOT NULL,
    service_price_id                CHAR(36)      NOT NULL,
    billing_cadence                 VARCHAR(24)   NOT NULL,
    charge_trigger                  VARCHAR(32)   NOT NULL,
    amount_cents_snapshot           BIGINT        NOT NULL,
    currency                        CHAR(3)       NOT NULL,
    discount_eligible_snapshot      TINYINT(1)    NOT NULL DEFAULT 1,
    effective_from                  DATETIME(6)   NOT NULL,
    effective_until                 DATETIME(6)   NULL,
    display_order                   INT           NOT NULL DEFAULT 0,
    status                          VARCHAR(24)   NOT NULL DEFAULT 'active',
    created_at                      DATETIME(6)   NOT NULL DEFAULT UTC_TIMESTAMP(6),

    PRIMARY KEY (id),
    UNIQUE KEY uq_billing_v2_item_price_component
        (subscription_item_id, service_price_id, effective_from),
    KEY idx_billing_v2_item_component_renewal
        (subscription_item_id, status, billing_cadence, effective_from, effective_until),
    CONSTRAINT chk_billing_v2_item_component_cadence CHECK (
        billing_cadence IN ('monthly', 'one_time')
    ),
    CONSTRAINT chk_billing_v2_item_component_trigger CHECK (
        charge_trigger IN ('initial_subscription', 'subscription_change')
    ),
    CONSTRAINT chk_billing_v2_item_component_amount CHECK (amount_cents_snapshot >= 0),
    CONSTRAINT fk_billing_v2_item_component_item
        FOREIGN KEY (subscription_item_id) REFERENCES billing_v2_subscription_items(id)
        ON UPDATE RESTRICT ON DELETE RESTRICT,
    CONSTRAINT fk_billing_v2_item_component_price
        FOREIGN KEY (service_price_id) REFERENCES billing_v2_service_prices(id)
        ON UPDATE RESTRICT ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- statement-break

ALTER TABLE billing_v2_billing_event_lines
    ADD COLUMN IF NOT EXISTS subscription_item_price_component_id CHAR(36) NULL
        AFTER subscription_item_id;

CREATE INDEX IF NOT EXISTS idx_billing_v2_billing_event_lines_component
    ON billing_v2_billing_event_lines (subscription_item_price_component_id);

ALTER TABLE billing_v2_billing_event_lines
    ADD CONSTRAINT fk_billing_v2_event_line_component
        FOREIGN KEY (subscription_item_price_component_id)
        REFERENCES billing_v2_subscription_item_price_components(id)
        ON UPDATE RESTRICT ON DELETE RESTRICT;

-- statement-break

CREATE TABLE IF NOT EXISTS billing_v2_one_time_component_consumptions (
    id                              CHAR(36)      NOT NULL,
    subscription_item_price_component_id CHAR(36) NOT NULL,
    billing_event_line_id           CHAR(36)      NOT NULL,
    consumption_kind                VARCHAR(32)   NOT NULL,
    status                          VARCHAR(24)   NOT NULL DEFAULT 'consumed',
    -- Cle conditionnelle materialisee par les triggers ci-dessous : MariaDB
    -- ne permet pas d'indexer une expression CASE dans une colonne generee.
    -- Les avoirs, adjustments et reconciliations gardent donc NULL.
    debit_charge_component_key CHAR(36) NULL,
    created_at                      DATETIME(6)   NOT NULL DEFAULT UTC_TIMESTAMP(6),
    finalized_at                    DATETIME(6)   NULL,

    PRIMARY KEY (id),
    UNIQUE KEY uq_billing_v2_one_time_debit_once (debit_charge_component_key),
    KEY idx_billing_v2_one_time_component (subscription_item_price_component_id),
    KEY idx_billing_v2_one_time_line (billing_event_line_id),
    CONSTRAINT chk_billing_v2_one_time_consumption_kind CHECK (
        consumption_kind IN ('debit_charge', 'credit_adjustment', 'reconciliation_reference')
    ),
    CONSTRAINT chk_billing_v2_one_time_consumption_status CHECK (
        status IN ('reserved', 'consumed', 'released')
    ),
    CONSTRAINT fk_billing_v2_one_time_component
        FOREIGN KEY (subscription_item_price_component_id)
        REFERENCES billing_v2_subscription_item_price_components(id)
        ON UPDATE RESTRICT ON DELETE RESTRICT,
    CONSTRAINT fk_billing_v2_one_time_line
        FOREIGN KEY (billing_event_line_id)
        REFERENCES billing_v2_billing_event_lines(id)
        ON UPDATE RESTRICT ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- statement-break

CREATE TRIGGER trg_billing_v2_one_time_consumption_before_insert
BEFORE INSERT ON billing_v2_one_time_component_consumptions
FOR EACH ROW
SET NEW.debit_charge_component_key = IF(
    NEW.consumption_kind = 'debit_charge'
    AND NEW.status IN ('reserved', 'consumed'),
    NEW.subscription_item_price_component_id,
    NULL
);

-- statement-break

CREATE TRIGGER trg_billing_v2_one_time_consumption_before_update
BEFORE UPDATE ON billing_v2_one_time_component_consumptions
FOR EACH ROW
SET NEW.debit_charge_component_key = IF(
    NEW.consumption_kind = 'debit_charge'
    AND NEW.status IN ('reserved', 'consumed'),
    NEW.subscription_item_price_component_id,
    NULL
);

-- statement-break

-- Unique point SQL de lecture du prix contractuel. Pour legacy_single, les
-- colonnes historiques sont projetees comme une composante virtuelle ; pour
-- componentized, elles ne sont jamais lues comme source financiere.
CREATE OR REPLACE VIEW billing_v2_subscription_item_effective_price_components AS
SELECT
    component.id AS component_id,
    component.subscription_item_id,
    component.service_price_id,
    component.billing_cadence,
    component.charge_trigger,
    component.amount_cents_snapshot,
    component.currency,
    component.discount_eligible_snapshot,
    component.effective_from,
    component.effective_until,
    component.display_order,
    component.status
FROM billing_v2_subscription_item_price_components component
INNER JOIN billing_v2_subscription_items item
    ON item.id = component.subscription_item_id
WHERE item.pricing_representation = 'componentized'
UNION ALL
SELECT
    NULL AS component_id,
    item.id AS subscription_item_id,
    item.service_price_id,
    price.billing_cadence,
    'initial_subscription' AS charge_trigger,
    item.amount_cents_snapshot,
    item.currency,
    item.discount_eligible_snapshot,
    item.effective_from,
    item.effective_until,
    0 AS display_order,
    item.status
FROM billing_v2_subscription_items item
INNER JOIN billing_v2_service_prices price
    ON price.id = item.service_price_id
WHERE item.pricing_representation = 'legacy_single';
