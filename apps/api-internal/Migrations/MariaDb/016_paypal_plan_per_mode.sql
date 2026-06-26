ALTER TABLE commercial_offers
    ADD COLUMN IF NOT EXISTS paypal_plan_id_sandbox VARCHAR(64) NULL DEFAULT NULL;

-- statement-break

ALTER TABLE commercial_offers
    ADD COLUMN IF NOT EXISTS paypal_plan_id_live VARCHAR(64) NULL DEFAULT NULL;

-- statement-break

UPDATE commercial_offers
SET paypal_plan_id_sandbox = paypal_plan_id
WHERE paypal_plan_id IS NOT NULL
  AND paypal_plan_id_sandbox IS NULL;

-- statement-break

ALTER TABLE commercial_offers
    DROP COLUMN paypal_plan_id;
