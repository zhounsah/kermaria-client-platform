-- Historique operationnel des memberships effectivement ajoutes par Billing V2.
-- Ce n'est pas une configuration de groupes : seul un AD_GROUP_MEMBER_ADDED
-- reel peut creer une ligne active. Elle rend le retrait inverse possible sans
-- prendre possession d'un groupe ajoute manuellement.
CREATE TABLE IF NOT EXISTS billing_v2_provisioning_managed_memberships (
    customer_id                 CHAR(36)     NOT NULL,
    identity_reference          CHAR(36)     NOT NULL,
    group_sam_account_name      VARCHAR(255) NOT NULL,
    status                      VARCHAR(24)  NOT NULL DEFAULT 'active',
    created_at                  DATETIME(6)  NOT NULL DEFAULT UTC_TIMESTAMP(6),
    updated_at                  DATETIME(6)  NOT NULL DEFAULT UTC_TIMESTAMP(6),
    PRIMARY KEY (customer_id, identity_reference, group_sam_account_name),
    KEY idx_billing_v2_provisioning_managed_memberships_active
        (customer_id, status, identity_reference),
    CONSTRAINT fk_billing_v2_provisioning_managed_memberships_customer
        FOREIGN KEY (customer_id) REFERENCES customers(id)
        ON UPDATE RESTRICT ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
