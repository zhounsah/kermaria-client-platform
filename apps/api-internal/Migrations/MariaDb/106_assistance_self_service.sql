-- Préparation additive des bornes directes par palier.
-- L'activation commerciale distincte est en 107. Le runner peut appliquer
-- les deux fichiers dans le même passage : garder le Checkout global fermé.
ALTER TABLE billing_v2_service_tiers
    ADD COLUMN IF NOT EXISTS direct_minimum_quantity INT NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS direct_maximum_quantity INT NOT NULL DEFAULT 1;
