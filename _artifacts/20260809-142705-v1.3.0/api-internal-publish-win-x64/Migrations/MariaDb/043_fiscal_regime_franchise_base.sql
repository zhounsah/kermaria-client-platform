-- Zachary IT applique actuellement la franchise en base de TVA.
-- `NULL` dans `tax_rate_basis_points` signifie : TVA non applicable.
UPDATE commercial_offers
SET tax_rate_basis_points = NULL,
    updated_at = UTC_TIMESTAMP(6)
WHERE tax_rate_basis_points IS NOT NULL;
