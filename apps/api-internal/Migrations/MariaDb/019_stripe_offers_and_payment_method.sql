ALTER TABLE commercial_offers
    ADD COLUMN IF NOT EXISTS stripe_price_id_test VARCHAR(64) NULL
        DEFAULT NULL;

-- statement-break

ALTER TABLE commercial_offers
    ADD COLUMN IF NOT EXISTS stripe_price_id_live VARCHAR(64) NULL
        DEFAULT NULL;

-- statement-break

ALTER TABLE commercial_documents
    ADD COLUMN IF NOT EXISTS payment_method ENUM('paypal','stripe','manual')
        NULL DEFAULT NULL;
