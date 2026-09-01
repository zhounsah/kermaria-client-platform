-- Liaison immuable entre la revision technique VPS achetee et le checkout
-- Billing V2 authoritative. 083 est deja appliquee : cette table additive est
-- le minimum necessaire pour prouver le BillingEvent achete, sans dupliquer
-- PaymentAttempt (qui depend deja du BillingEvent).
SET NAMES utf8mb4;

-- statement-break

CREATE TABLE IF NOT EXISTS billing_v2_vps_technical_request_checkouts (
    id                                      CHAR(36) NOT NULL PRIMARY KEY,
    technical_request_id                    CHAR(36) NOT NULL,
    technical_request_revision_number       INT UNSIGNED NOT NULL,
    authoritative_checkout_request_id       CHAR(36) NOT NULL,
    billing_event_id                        CHAR(36) NOT NULL,
    subscription_id                         CHAR(36) NOT NULL,
    created_at                              DATETIME(6) NOT NULL,

    UNIQUE KEY ux_billing_v2_vps_checkout_revision
        (technical_request_id, technical_request_revision_number),
    UNIQUE KEY ux_billing_v2_vps_checkout_authoritative_request
        (authoritative_checkout_request_id),
    UNIQUE KEY ux_billing_v2_vps_checkout_billing_event
        (billing_event_id),
    KEY ix_billing_v2_vps_checkout_subscription (subscription_id),

    CONSTRAINT fk_billing_v2_vps_checkout_request
        FOREIGN KEY (technical_request_id)
        REFERENCES billing_v2_vps_technical_requests(id)
        ON UPDATE RESTRICT ON DELETE RESTRICT,
    CONSTRAINT fk_billing_v2_vps_checkout_authoritative_request
        FOREIGN KEY (authoritative_checkout_request_id)
        REFERENCES billing_v2_authoritative_checkout_requests(id)
        ON UPDATE RESTRICT ON DELETE RESTRICT,
    CONSTRAINT fk_billing_v2_vps_checkout_billing_event
        FOREIGN KEY (billing_event_id)
        REFERENCES billing_v2_billing_events(id)
        ON UPDATE RESTRICT ON DELETE RESTRICT,
    CONSTRAINT fk_billing_v2_vps_checkout_subscription
        FOREIGN KEY (subscription_id)
        REFERENCES billing_v2_subscriptions(id)
        ON UPDATE RESTRICT ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
