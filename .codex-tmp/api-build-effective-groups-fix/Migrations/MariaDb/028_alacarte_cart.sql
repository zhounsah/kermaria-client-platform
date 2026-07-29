-- V0.35 — Panier / commande groupee a la carte.
-- Panier client self-service d'options a la carte (offres one-shot).
-- Le panier est implicitement rattache au client : une ligne par offre
-- retenue, quantite cumulee. Materialise a la confirmation en un unique
-- commercial_document multi-lignes (colonne origin = 'client_cart').

CREATE TABLE IF NOT EXISTS cart_items (
    id CHAR(36) NOT NULL,
    customer_id CHAR(36) NOT NULL,
    offer_id CHAR(36) NOT NULL,
    quantity INT NOT NULL DEFAULT 1,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_cart_items_customer_offer (customer_id, offer_id),
    KEY ix_cart_items_customer (customer_id),
    CONSTRAINT fk_cart_items_customer
        FOREIGN KEY (customer_id) REFERENCES customers (id)
        ON DELETE CASCADE,
    CONSTRAINT fk_cart_items_offer
        FOREIGN KEY (offer_id) REFERENCES commercial_offers (id)
        ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Tag d'origine sur les documents commerciaux : distingue les documents
-- issus d'un panier client des documents crees par l'admin ou generes par
-- un abonnement. Utilise pour cibler le provisioning « le cas echeant »
-- au reglement sans changer le comportement des autres documents.
ALTER TABLE commercial_documents
    ADD COLUMN IF NOT EXISTS origin VARCHAR(32) NULL AFTER subscription_id;
