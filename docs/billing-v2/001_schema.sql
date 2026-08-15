-- ============================================================================
-- Zachary IT - Billing V2
-- Schéma additif et rétrocompatible pour MariaDB
--
-- Objectifs :
--   * ne supprimer / modifier aucune table legacy
--   * séparer catalogue, prix, contrat, paiement et provisioning
--   * permettre des abonnements modulaires
--   * supporter paiement mensuel ou comptant
--   * distinguer droit acheté et état réellement provisionné
--   * conserver l'historique des tarifs et changements
--   * préparer une migration progressive depuis `commercial_offers`
--
-- Convention :
--   * UUID applicatifs : CHAR(36)
--   * monnaie : centimes entiers (BIGINT), jamais FLOAT/DOUBLE
--   * taux : basis points (10000 = 100 %, 1000 = 10 %)
--   * dates : DATETIME(6), stockées en UTC par l'application
--   * moteur : InnoDB
--   * charset : utf8mb4
--
-- IMPORTANT :
--   Ce fichier ne modifie PAS la table legacy `commercial_offers`.
--   Les tables sont volontairement préfixées billing_v2_ pendant la migration.
-- ============================================================================

SET NAMES utf8mb4;

-- ============================================================================
-- 1. CATALOGUE : définition des services
-- ============================================================================

CREATE TABLE IF NOT EXISTS billing_v2_services (
    id                              CHAR(36)      NOT NULL,
    code                            VARCHAR(64)   NOT NULL,
    name                            VARCHAR(160)  NOT NULL,
    description                     TEXT          NULL,
    category                        VARCHAR(80)   NULL,

    -- recurring : service récurrent facturable
    -- one_time  : prestation ponctuelle
    -- included  : fonctionnalité incluse dans un autre service
    billing_type                    VARCHAR(24)   NOT NULL,

    -- subscription : service porté par l'abonnement
    -- user         : service porté par un utilisateur de l'abonnement
    default_scope_type              VARCHAR(24)   NOT NULL,

    -- fixed  : un prix unique
    -- tiered : le prix dépend d'un tier explicite
    pricing_model                   VARCHAR(24)   NOT NULL DEFAULT 'fixed',

    mandatory_for_subscription      TINYINT(1)    NOT NULL DEFAULT 0,
    discount_eligible               TINYINT(1)    NOT NULL DEFAULT 1,
    public_selectable               TINYINT(1)    NOT NULL DEFAULT 1,

    status                          VARCHAR(24)   NOT NULL DEFAULT 'active',
    display_order                   INT           NOT NULL DEFAULT 0,

    created_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
                                                ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),
    UNIQUE KEY uq_billing_v2_services_code (code),
    KEY idx_billing_v2_services_status (status),
    KEY idx_billing_v2_services_category (category)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- ============================================================================
-- 2. TIERS : 16/32/64/... GiB, VPN Essentiel/Plus/Performance...
-- ============================================================================

CREATE TABLE IF NOT EXISTS billing_v2_service_tiers (
    id                              CHAR(36)      NOT NULL,
    service_id                      CHAR(36)      NOT NULL,

    code                            VARCHAR(64)   NOT NULL,
    name                            VARCHAR(160)  NOT NULL,
    public_label                    VARCHAR(160)  NULL,
    description                     TEXT          NULL,

    -- Valeur normalisée utile au moteur.
    -- Exemples :
    --   STORAGE : 64 + unit='GiB'
    --   VPN     : 250 + unit='Mbps'
    numeric_value                   BIGINT        NULL,
    unit                            VARCHAR(32)   NULL,

    public_selectable               TINYINT(1)    NOT NULL DEFAULT 1,
    status                          VARCHAR(24)   NOT NULL DEFAULT 'active',
    display_order                   INT           NOT NULL DEFAULT 0,

    created_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
                                                ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),
    UNIQUE KEY uq_billing_v2_service_tiers_code (service_id, code),
    KEY idx_billing_v2_service_tiers_service (service_id, status),

    CONSTRAINT fk_billing_v2_service_tiers_service
        FOREIGN KEY (service_id)
        REFERENCES billing_v2_services(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- ============================================================================
-- 3. PRIX : versionnés et immutables
--
-- Règle applicative :
--   ne jamais UPDATE amount_cents d'un prix déjà utilisé.
--   Créer une nouvelle ligne avec une nouvelle version/valid_from.
-- ============================================================================

CREATE TABLE IF NOT EXISTS billing_v2_service_prices (
    id                              CHAR(36)      NOT NULL,
    service_id                      CHAR(36)      NOT NULL,
    tier_id                         CHAR(36)      NULL,

    price_code                      VARCHAR(96)   NOT NULL,
    price_version                   INT           NOT NULL DEFAULT 1,

    amount_cents                    BIGINT        NOT NULL,
    currency                        CHAR(3)       NOT NULL DEFAULT 'EUR',

    -- monthly / one_time
    billing_cadence                 VARCHAR(24)   NOT NULL,

    tax_rate_basis_points           INT           NULL,

    valid_from                      DATETIME(6)   NOT NULL,
    valid_until                     DATETIME(6)   NULL,
    status                          VARCHAR(24)   NOT NULL DEFAULT 'active',

    created_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),
    UNIQUE KEY uq_billing_v2_service_prices_code (price_code),
    -- price_code est l'identifiant immuable/versionné.
    -- On n'utilise pas de UNIQUE(service_id, tier_id, ...) ici car MariaDB
    -- autorise plusieurs NULL dans un index UNIQUE pour les services sans tier.
    KEY idx_billing_v2_service_prices_version
        (service_id, tier_id, currency, price_version),
    KEY idx_billing_v2_service_prices_lookup
        (service_id, tier_id, currency, status, valid_from, valid_until),

    CONSTRAINT fk_billing_v2_service_prices_service
        FOREIGN KEY (service_id)
        REFERENCES billing_v2_services(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_service_prices_tier
        FOREIGN KEY (tier_id)
        REFERENCES billing_v2_service_tiers(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- ============================================================================
-- 4. DÉPENDANCES ENTRE SERVICES
--
-- Exemples :
--   BACKUP-PERSONAL -> nécessite STORAGE-PERSONAL dans le même scope utilisateur
--   BACKUP-SHARED   -> nécessite STORAGE-SHARED au niveau abonnement
--
-- tier_relation :
--   any
--   same_numeric_value
--   dependent_gte_required
-- ============================================================================

CREATE TABLE IF NOT EXISTS billing_v2_service_dependencies (
    id                              CHAR(36)      NOT NULL,
    service_id                      CHAR(36)      NOT NULL,
    required_service_id             CHAR(36)      NOT NULL,

    scope_relation                  VARCHAR(32)   NOT NULL DEFAULT 'same_scope',
    tier_relation                   VARCHAR(32)   NOT NULL DEFAULT 'any',

    status                          VARCHAR(24)   NOT NULL DEFAULT 'active',
    created_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),
    UNIQUE KEY uq_billing_v2_service_dependencies
        (service_id, required_service_id, scope_relation),
    KEY idx_billing_v2_service_dependencies_service (service_id),

    CONSTRAINT fk_billing_v2_service_dependencies_service
        FOREIGN KEY (service_id)
        REFERENCES billing_v2_services(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_service_dependencies_required
        FOREIGN KEY (required_service_id)
        REFERENCES billing_v2_services(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- ============================================================================
-- 5. CONDITIONS D'ENGAGEMENT
--
-- discount_basis_points peut rester NULL tant que le tarif commercial V2
-- n'est pas définitivement arrêté.
-- ============================================================================

CREATE TABLE IF NOT EXISTS billing_v2_commitment_terms (
    id                              CHAR(36)      NOT NULL,
    code                            VARCHAR(64)   NOT NULL,
    name                            VARCHAR(160)  NOT NULL,

    commitment_months               INT           NOT NULL,
    discount_basis_points           INT           NULL,

    allow_monthly_payment           TINYINT(1)    NOT NULL DEFAULT 1,
    allow_upfront_payment           TINYINT(1)    NOT NULL DEFAULT 1,

    status                          VARCHAR(24)   NOT NULL DEFAULT 'active',
    display_order                   INT           NOT NULL DEFAULT 0,

    created_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
                                                ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),
    UNIQUE KEY uq_billing_v2_commitment_terms_code (code),
    KEY idx_billing_v2_commitment_terms_status (status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- ============================================================================
-- 6. OPTIONS DE PAIEMENT PAR ENGAGEMENT
--
-- Une durée d'engagement ne suffit pas à déterminer la remise :
--   6 mois mensuel  !=  6 mois comptant
--   12 mois mensuel != 12 mois comptant
-- ============================================================================

CREATE TABLE IF NOT EXISTS billing_v2_commitment_payment_options (
    id                              CHAR(36)      NOT NULL,
    commitment_term_id              CHAR(36)      NOT NULL,

    -- monthly / upfront
    payment_mode                    VARCHAR(24)   NOT NULL,
    discount_basis_points           INT           NOT NULL DEFAULT 0,

    status                          VARCHAR(24)   NOT NULL DEFAULT 'active',
    display_order                   INT           NOT NULL DEFAULT 0,

    created_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
                                                ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),
    UNIQUE KEY uq_billing_v2_commitment_payment_options
        (commitment_term_id, payment_mode),
    KEY idx_billing_v2_commitment_payment_options_status
        (status, display_order),

    CONSTRAINT fk_billing_v2_commitment_payment_options_term
        FOREIGN KEY (commitment_term_id)
        REFERENCES billing_v2_commitment_terms(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- ============================================================================
-- 7. PRESETS COMMERCIAUX
--
-- Ce ne sont PAS des contrats ni des produits de paiement.
-- Ils servent uniquement de configurations initiales / commerciales.
-- ============================================================================

CREATE TABLE IF NOT EXISTS billing_v2_offer_presets (
    id                              CHAR(36)      NOT NULL,
    code                            VARCHAR(96)   NOT NULL,
    name                            VARCHAR(160)  NOT NULL,
    description                     TEXT          NULL,

    status                          VARCHAR(24)   NOT NULL DEFAULT 'active',
    is_public                       TINYINT(1)    NOT NULL DEFAULT 1,
    display_order                   INT           NOT NULL DEFAULT 0,

    created_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
                                                ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),
    UNIQUE KEY uq_billing_v2_offer_presets_code (code),
    KEY idx_billing_v2_offer_presets_public (is_public, status, display_order)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


CREATE TABLE IF NOT EXISTS billing_v2_preset_items (
    id                              CHAR(36)      NOT NULL,
    preset_id                       CHAR(36)      NOT NULL,
    service_id                      CHAR(36)      NOT NULL,
    tier_id                         CHAR(36)      NULL,

    -- subscription / primary_user / additional_user
    scope_template                  VARCHAR(32)   NOT NULL,
    quantity                        INT           NOT NULL DEFAULT 1,

    required_item                   TINYINT(1)    NOT NULL DEFAULT 0,
    customer_editable               TINYINT(1)    NOT NULL DEFAULT 1,
    display_order                   INT           NOT NULL DEFAULT 0,

    created_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),
    KEY idx_billing_v2_preset_items_preset (preset_id, display_order),

    CONSTRAINT fk_billing_v2_preset_items_preset
        FOREIGN KEY (preset_id)
        REFERENCES billing_v2_offer_presets(id)
        ON UPDATE RESTRICT
        ON DELETE CASCADE,

    CONSTRAINT fk_billing_v2_preset_items_service
        FOREIGN KEY (service_id)
        REFERENCES billing_v2_services(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_preset_items_tier
        FOREIGN KEY (tier_id)
        REFERENCES billing_v2_service_tiers(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- ============================================================================
-- 8. ABONNEMENTS
--
-- customer_id n'a volontairement pas encore de FK :
-- la table exacte des clients du site legacy doit être raccordée après audit.
--
-- discount_basis_points_snapshot :
--   remise contractuelle figée au moment de la souscription / renouvellement.
--
-- minimum_commitment_amount_cents :
--   plancher MRR après remise pour les contrats mensuels engagés.
--   NULL = pas de plancher.
-- ============================================================================

CREATE TABLE IF NOT EXISTS billing_v2_subscriptions (
    id                                  CHAR(36)      NOT NULL,
    customer_id                         CHAR(36)      NOT NULL,

    originating_preset_id               CHAR(36)      NULL,
    commitment_term_id                  CHAR(36)      NULL,

    status                              VARCHAR(32)   NOT NULL DEFAULT 'draft',

    -- monthly / upfront
    payment_mode                        VARCHAR(24)   NOT NULL,

    currency                            CHAR(3)       NOT NULL DEFAULT 'EUR',

    started_at                          DATETIME(6)   NULL,
    commitment_started_at               DATETIME(6)   NULL,
    commitment_ends_at                  DATETIME(6)   NULL,

    current_period_started_at           DATETIME(6)   NULL,
    current_period_ends_at              DATETIME(6)   NULL,
    renews_at                           DATETIME(6)   NULL,

    cancel_at_period_end                TINYINT(1)    NOT NULL DEFAULT 0,
    cancellation_requested_at           DATETIME(6)   NULL,

    discount_basis_points_snapshot      INT           NOT NULL DEFAULT 0,

    -- S'applique après la réduction globale.
    minimum_commitment_amount_cents     BIGINT        NULL,

    -- legacy / v2 ; utile pendant la migration.
    billing_model                       VARCHAR(16)   NOT NULL DEFAULT 'v2',

    created_at                          DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at                          DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
                                                    ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),
    KEY idx_billing_v2_subscriptions_customer (customer_id, status),
    KEY idx_billing_v2_subscriptions_renewal (renews_at, status),

    CONSTRAINT fk_billing_v2_subscriptions_preset
        FOREIGN KEY (originating_preset_id)
        REFERENCES billing_v2_offer_presets(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_subscriptions_commitment
        FOREIGN KEY (commitment_term_id)
        REFERENCES billing_v2_commitment_terms(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- ============================================================================
-- 9. UTILISATEURS RATTACHÉS À UN ABONNEMENT
--
-- Un client/organisation peut avoir plusieurs utilisateurs avec des ressources
-- différentes : stockage personnel, VPN, RDS, etc.
-- ============================================================================

CREATE TABLE IF NOT EXISTS billing_v2_subscription_users (
    id                              CHAR(36)      NOT NULL,
    subscription_id                 CHAR(36)      NOT NULL,

    -- Référence stable vers l'identité existante de l'application / AD.
    identity_reference              VARCHAR(255)  NULL,

    display_name                    VARCHAR(160)  NOT NULL,
    email                           VARCHAR(255)  NULL,

    is_primary                      TINYINT(1)    NOT NULL DEFAULT 0,
    status                          VARCHAR(24)   NOT NULL DEFAULT 'active',

    created_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
                                                ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),
    UNIQUE KEY uq_billing_v2_subscription_users_identity
        (subscription_id, identity_reference),
    KEY idx_billing_v2_subscription_users_subscription
        (subscription_id, status),

    CONSTRAINT fk_billing_v2_subscription_users_subscription
        FOREIGN KEY (subscription_id)
        REFERENCES billing_v2_subscriptions(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- ============================================================================
-- 10. ITEMS D'ABONNEMENT = DROITS CONTRACTUELS ACHETÉS
--
-- C'est la table centrale du Billing V2.
--
-- Pour scope_type='subscription' :
--   subscription_user_id doit être NULL.
--
-- Pour scope_type='user' :
--   subscription_user_id doit viser un utilisateur du même abonnement.
--
-- La règle de cohérence de scope est contrôlée par le service applicatif
-- (et pourra être renforcée par trigger après audit de la version MariaDB).
--
-- amount_cents_snapshot :
--   prix catalogue de CET ITEM avant remise globale d'engagement.
--   La réduction globale n'est pas enregistrée ligne par ligne.
-- ============================================================================

CREATE TABLE IF NOT EXISTS billing_v2_subscription_items (
    id                              CHAR(36)      NOT NULL,
    subscription_id                 CHAR(36)      NOT NULL,
    subscription_user_id            CHAR(36)      NULL,

    service_id                      CHAR(36)      NOT NULL,
    tier_id                         CHAR(36)      NULL,
    service_price_id                CHAR(36)      NOT NULL,

    scope_type                      VARCHAR(24)   NOT NULL,
    quantity                        INT           NOT NULL DEFAULT 1,

    amount_cents_snapshot           BIGINT        NOT NULL,
    currency                        CHAR(3)       NOT NULL DEFAULT 'EUR',
    discount_eligible_snapshot      TINYINT(1)    NOT NULL DEFAULT 1,

    -- preset / manual / migration / system
    source                          VARCHAR(24)   NOT NULL DEFAULT 'manual',

    effective_from                  DATETIME(6)   NOT NULL,
    effective_until                 DATETIME(6)   NULL,

    status                          VARCHAR(24)   NOT NULL DEFAULT 'active',

    created_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
                                                ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),
    KEY idx_billing_v2_subscription_items_active
        (subscription_id, status, effective_from, effective_until),
    KEY idx_billing_v2_subscription_items_user
        (subscription_user_id, status),
    KEY idx_billing_v2_subscription_items_service
        (service_id, tier_id),

    CONSTRAINT fk_billing_v2_subscription_items_subscription
        FOREIGN KEY (subscription_id)
        REFERENCES billing_v2_subscriptions(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_subscription_items_user
        FOREIGN KEY (subscription_user_id)
        REFERENCES billing_v2_subscription_users(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_subscription_items_service
        FOREIGN KEY (service_id)
        REFERENCES billing_v2_services(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_subscription_items_tier
        FOREIGN KEY (tier_id)
        REFERENCES billing_v2_service_tiers(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_subscription_items_price
        FOREIGN KEY (service_price_id)
        REFERENCES billing_v2_service_prices(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- ============================================================================
-- 11. ÉTAT PROVISIONNÉ
--
-- Sépare ce qui a été ACHETÉ de ce qui est techniquement PROVISIONNÉ.
--
-- Exemple prépayé :
--   item acheté       = STORAGE-PERSONAL 128 GiB
--   provisioned_tier  = STORAGE-PERSONAL 64 GiB
--
-- Le client peut remonter jusqu'à son droit acheté sans nouvel achat,
-- pendant la période contractuelle concernée.
-- ============================================================================

CREATE TABLE IF NOT EXISTS billing_v2_subscription_item_provisioning (
    subscription_item_id            CHAR(36)      NOT NULL,

    provisioned_tier_id             CHAR(36)      NULL,
    provisioned_quantity            INT           NOT NULL DEFAULT 1,

    provisioning_status             VARCHAR(32)   NOT NULL DEFAULT 'pending',
    last_provisioned_at             DATETIME(6)   NULL,
    last_error                      TEXT          NULL,

    created_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
                                                ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (subscription_item_id),

    CONSTRAINT fk_billing_v2_item_provisioning_item
        FOREIGN KEY (subscription_item_id)
        REFERENCES billing_v2_subscription_items(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_item_provisioning_tier
        FOREIGN KEY (provisioned_tier_id)
        REFERENCES billing_v2_service_tiers(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- ============================================================================
-- 12. MODIFICATIONS D'ABONNEMENT
--
-- Supporte :
--   mensuel : upgrade/downgrade immédiat + prorata
--   upfront  : upgrade immédiat avec complément
--              downgrade technique possible sans remboursement
--              downgrade contractuel programmé au renouvellement
-- ============================================================================

CREATE TABLE IF NOT EXISTS billing_v2_subscription_changes (
    id                              CHAR(36)      NOT NULL,
    subscription_id                 CHAR(36)      NOT NULL,

    change_kind                     VARCHAR(24)   NOT NULL,
    billing_effect                  VARCHAR(40)   NOT NULL,

    requested_at                    DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    effective_at                    DATETIME(6)   NOT NULL,

    status                          VARCHAR(24)   NOT NULL DEFAULT 'pending',
    requested_by_reference          VARCHAR(255)  NULL,
    reason                          TEXT          NULL,

    applied_at                      DATETIME(6)   NULL,
    cancelled_at                    DATETIME(6)   NULL,

    created_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),
    KEY idx_billing_v2_subscription_changes_pending
        (subscription_id, status, effective_at),

    CONSTRAINT fk_billing_v2_subscription_changes_subscription
        FOREIGN KEY (subscription_id)
        REFERENCES billing_v2_subscriptions(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


CREATE TABLE IF NOT EXISTS billing_v2_subscription_change_items (
    id                              CHAR(36)      NOT NULL,
    change_id                       CHAR(36)      NOT NULL,

    -- NULL pour un ajout d'item qui n'existe pas encore.
    subscription_item_id            CHAR(36)      NULL,

    action_type                     VARCHAR(24)   NOT NULL,

    service_id                      CHAR(36)      NOT NULL,
    subscription_user_id            CHAR(36)      NULL,

    old_tier_id                     CHAR(36)      NULL,
    new_tier_id                     CHAR(36)      NULL,

    old_quantity                    INT           NULL,
    new_quantity                    INT           NULL,

    -- Permet les changements purement techniques sans modifier le droit acheté.
    old_provisioned_tier_id         CHAR(36)      NULL,
    new_provisioned_tier_id         CHAR(36)      NULL,

    created_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),
    KEY idx_billing_v2_subscription_change_items_change (change_id),

    CONSTRAINT fk_billing_v2_subscription_change_items_change
        FOREIGN KEY (change_id)
        REFERENCES billing_v2_subscription_changes(id)
        ON UPDATE RESTRICT
        ON DELETE CASCADE,

    CONSTRAINT fk_billing_v2_subscription_change_items_item
        FOREIGN KEY (subscription_item_id)
        REFERENCES billing_v2_subscription_items(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_subscription_change_items_service
        FOREIGN KEY (service_id)
        REFERENCES billing_v2_services(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_subscription_change_items_user
        FOREIGN KEY (subscription_user_id)
        REFERENCES billing_v2_subscription_users(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_subscription_change_items_old_tier
        FOREIGN KEY (old_tier_id)
        REFERENCES billing_v2_service_tiers(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_subscription_change_items_new_tier
        FOREIGN KEY (new_tier_id)
        REFERENCES billing_v2_service_tiers(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_subscription_change_items_old_provisioned_tier
        FOREIGN KEY (old_provisioned_tier_id)
        REFERENCES billing_v2_service_tiers(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_subscription_change_items_new_provisioned_tier
        FOREIGN KEY (new_provisioned_tier_id)
        REFERENCES billing_v2_service_tiers(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- ============================================================================
-- 13. PROVIDERS DE PAIEMENT
--
-- Le moteur Zachary IT reste source de vérité.
-- Stripe / PayPal sont des adaptateurs externes.
-- ============================================================================

CREATE TABLE IF NOT EXISTS billing_v2_provider_price_mappings (
    id                              CHAR(36)      NOT NULL,
    service_price_id                CHAR(36)      NOT NULL,

    provider                        VARCHAR(32)   NOT NULL,
    environment                     VARCHAR(16)   NOT NULL,

    external_product_id             VARCHAR(255)  NULL,
    external_price_id               VARCHAR(255)  NULL,
    external_plan_id                VARCHAR(255)  NULL,

    status                          VARCHAR(24)   NOT NULL DEFAULT 'active',
    created_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
                                                ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),
    UNIQUE KEY uq_billing_v2_provider_price_mapping
        (service_price_id, provider, environment),

    CONSTRAINT fk_billing_v2_provider_price_mappings_price
        FOREIGN KEY (service_price_id)
        REFERENCES billing_v2_service_prices(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


CREATE TABLE IF NOT EXISTS billing_v2_payment_agreements (
    id                              CHAR(36)      NOT NULL,
    subscription_id                 CHAR(36)      NOT NULL,

    provider                        VARCHAR(32)   NOT NULL,
    environment                     VARCHAR(16)   NOT NULL,

    provider_customer_id            VARCHAR(255)  NULL,
    provider_subscription_id        VARCHAR(255)  NULL,
    provider_agreement_id           VARCHAR(255)  NULL,

    status                          VARCHAR(32)   NOT NULL DEFAULT 'pending',

    created_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
                                                ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),
    UNIQUE KEY uq_billing_v2_payment_agreements_subscription
        (subscription_id, provider, environment),
    UNIQUE KEY uq_billing_v2_payment_agreements_provider_subscription
        (provider, environment, provider_subscription_id),
    KEY idx_billing_v2_payment_agreements_external_subscription
        (provider, environment, provider_subscription_id),

    CONSTRAINT fk_billing_v2_payment_agreements_subscription
        FOREIGN KEY (subscription_id)
        REFERENCES billing_v2_subscriptions(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- ============================================================================
-- 14. RÈGLES DE PROVISIONING
--
-- Les règles sont normalisées : plus de listes JSON de groupes AD dans commercial_offers.
--
-- Exemples :
--   ACCES VPN      -> ad_group_membership / GG_VPN
--   RDS            -> ad_group_membership / GG_RDS
--   STORAGE        -> nextcloud_quota / source = tier.numeric_value
-- ============================================================================

CREATE TABLE IF NOT EXISTS billing_v2_provisioning_rules (
    id                              CHAR(36)      NOT NULL,
    service_id                      CHAR(36)      NOT NULL,
    tier_id                         CHAR(36)      NULL,

    rule_type                       VARCHAR(64)   NOT NULL,
    target_type                     VARCHAR(64)   NOT NULL,
    target_reference                VARCHAR(255)  NULL,

    -- Exemples : none / tier_numeric_value / static
    value_source                    VARCHAR(64)   NOT NULL DEFAULT 'none',
    static_value                    VARCHAR(255)  NULL,

    enable_action                   VARCHAR(64)   NULL,
    disable_action                  VARCHAR(64)   NULL,

    status                          VARCHAR(24)   NOT NULL DEFAULT 'active',
    display_order                   INT           NOT NULL DEFAULT 0,

    created_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
                                                ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),
    KEY idx_billing_v2_provisioning_rules_lookup
        (service_id, tier_id, status),

    CONSTRAINT fk_billing_v2_provisioning_rules_service
        FOREIGN KEY (service_id)
        REFERENCES billing_v2_services(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_provisioning_rules_tier
        FOREIGN KEY (tier_id)
        REFERENCES billing_v2_service_tiers(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- ============================================================================
-- 15. MAPPING LEGACY -> BILLING V2
--
-- Ne dépend volontairement PAS d'une FK vers commercial_offers afin que le nouveau schéma
-- puisse être créé sans connaître les contraintes exactes de la table legacy.
-- legacy_offer_id doit contenir commercial_offers.id.
-- ============================================================================

CREATE TABLE IF NOT EXISTS billing_v2_legacy_offer_mappings (
    legacy_offer_id                 CHAR(36)      NOT NULL,

    preset_id                       CHAR(36)      NULL,
    commitment_term_id              CHAR(36)      NULL,
    payment_mode                    VARCHAR(24)   NULL,

    legacy_external_reference       VARCHAR(255)  NULL,

    status                          VARCHAR(24)   NOT NULL DEFAULT 'active',
    created_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    PRIMARY KEY (legacy_offer_id),
    KEY idx_billing_v2_legacy_mapping_preset (preset_id),

    CONSTRAINT fk_billing_v2_legacy_offer_mappings_preset
        FOREIGN KEY (preset_id)
        REFERENCES billing_v2_offer_presets(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_legacy_offer_mappings_commitment
        FOREIGN KEY (commitment_term_id)
        REFERENCES billing_v2_commitment_terms(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- ============================================================================
-- 16. SHADOW PRICING / NON-RÉGRESSION
--
-- Pendant la migration, l'ancien moteur reste autoritaire.
-- Le nouveau calcule en parallèle et on journalise les différences.
-- ============================================================================

CREATE TABLE IF NOT EXISTS billing_v2_shadow_price_checks (
    id                              CHAR(36)      NOT NULL,

    legacy_offer_id                 CHAR(36)      NULL,
    subscription_id                 CHAR(36)      NULL,

    legacy_amount_cents             BIGINT        NOT NULL,
    v2_amount_cents                 BIGINT        NOT NULL,
    difference_cents                BIGINT        NOT NULL,

    currency                        CHAR(3)       NOT NULL DEFAULT 'EUR',
    context_reference               VARCHAR(255)  NULL,

    checked_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),
    KEY idx_billing_v2_shadow_price_checks_difference
        (difference_cents, checked_at),
    KEY idx_billing_v2_shadow_price_checks_legacy
        (legacy_offer_id, checked_at),

    CONSTRAINT fk_billing_v2_shadow_price_checks_subscription
        FOREIGN KEY (subscription_id)
        REFERENCES billing_v2_subscriptions(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- ============================================================================
-- 17. OUTBOX : synchronisation fiable vers Stripe/PayPal/provisioning
--
-- Un changement SQL et la création de l'événement doivent être faits dans la
-- même transaction applicative. Un worker traite ensuite l'événement.
--
-- payload_text peut contenir du JSON sérialisé sans que le coeur relationnel
-- dépende du type JSON MariaDB.
-- ============================================================================

CREATE TABLE IF NOT EXISTS billing_v2_outbox_events (
    id                              CHAR(36)      NOT NULL,

    aggregate_type                  VARCHAR(64)   NOT NULL,
    aggregate_id                    CHAR(36)      NOT NULL,
    event_type                      VARCHAR(96)   NOT NULL,

    payload_text                    LONGTEXT      NULL,

    status                          VARCHAR(24)   NOT NULL DEFAULT 'pending',
    retry_count                     INT           NOT NULL DEFAULT 0,

    available_at                    DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    processed_at                    DATETIME(6)   NULL,
    last_error                      TEXT          NULL,

    created_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),
    KEY idx_billing_v2_outbox_pending
        (status, available_at, created_at),
    KEY idx_billing_v2_outbox_aggregate
        (aggregate_type, aggregate_id, created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- ============================================================================
-- 18. AUDIT MÉTIER
-- ============================================================================

CREATE TABLE IF NOT EXISTS billing_v2_audit_log (
    id                              CHAR(36)      NOT NULL,

    entity_type                     VARCHAR(64)   NOT NULL,
    entity_id                       CHAR(36)      NOT NULL,
    action                          VARCHAR(96)   NOT NULL,

    actor_reference                 VARCHAR(255)  NULL,
    details_text                    LONGTEXT      NULL,

    created_at                      DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),
    KEY idx_billing_v2_audit_entity
        (entity_type, entity_id, created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- ============================================================================
-- 19. SEED DU CATALOGUE FONCTIONNEL
--
-- Aucun prix n'est seedé ici : ils doivent être décidés séparément.
-- Les INSERT sont idempotents grâce aux codes uniques.
-- ============================================================================

INSERT IGNORE INTO billing_v2_services
    (id, code, name, description, category, billing_type, default_scope_type,
     pricing_model, mandatory_for_subscription, discount_eligible,
     public_selectable, status, display_order)
VALUES
    (UUID(), 'BASE-SERVICE',
     'Socle de service',
     'Socle récurrent obligatoire : compte client, exploitation de la plateforme, sécurité générale, supervision de l''infrastructure et support lié au fonctionnement normal des services.',
     'Socle', 'recurring', 'subscription', 'fixed', 1, 1, 0, 'active', 10),

    (UUID(), 'STORAGE-PERSONAL',
     'Stockage personnel',
     'Quota de stockage personnel attribué à un utilisateur.',
     'Stockage', 'recurring', 'user', 'tiered', 0, 1, 1, 'active', 20),

    (UUID(), 'STORAGE-SHARED',
     'Stockage partagé',
     'Quota de stockage partagé attribué à l''abonnement ou à l''organisation.',
     'Stockage', 'recurring', 'subscription', 'tiered', 0, 1, 1, 'active', 30),

    (UUID(), 'BACKUP-PERSONAL',
     'Sauvegarde du stockage personnel',
     'Sauvegarde du stockage personnel d''un utilisateur. Le tier doit suivre la capacité de stockage personnel couverte.',
     'Sauvegarde', 'recurring', 'user', 'tiered', 0, 1, 1, 'active', 40),

    (UUID(), 'BACKUP-SHARED',
     'Sauvegarde du stockage partagé',
     'Sauvegarde du stockage partagé. Le tier doit suivre la capacité de stockage partagé couverte.',
     'Sauvegarde', 'recurring', 'subscription', 'tiered', 0, 1, 1, 'active', 50),

    (UUID(), 'VPN-ACCESS',
     'Accès VPN',
     'Accès VPN sécurisé avec niveau de performance commercial.',
     'Accès', 'recurring', 'user', 'tiered', 0, 1, 1, 'active', 60),

    (UUID(), 'RDS-ACCESS',
     'Accès bureau distant RDS',
     'Accès utilisateur à l''environnement Windows distant.',
     'Accès', 'recurring', 'user', 'fixed', 0, 1, 1, 'active', 70),

    (UUID(), 'USER-ADDITIONAL',
     'Utilisateur supplémentaire',
     'Compte utilisateur supplémentaire rattaché à l''abonnement.',
     'Utilisateurs', 'recurring', 'user', 'fixed', 0, 1, 1, 'active', 80),

    (UUID(), 'SUPPORT-STANDARD',
     'Support standard',
     'Support relatif au fonctionnement normal des services Zachary IT. Inclus dans le socle.',
     'Support', 'included', 'subscription', 'fixed', 0, 0, 0, 'active', 90),

    (UUID(), 'SUPPORT-PLUS',
     'Support Plus',
     'Option d''assistance renforcée pour les services souscrits.',
     'Support', 'recurring', 'subscription', 'fixed', 0, 1, 1, 'active', 100),

    (UUID(), 'INIT-SERVICE',
     'Mise en service',
     'Prestation ponctuelle de mise en service, contrôles et activation initiale.',
     'Mise en service', 'one_time', 'subscription', 'fixed', 0, 0, 0, 'active', 105),

    (UUID(), 'MONITORING-INTERNAL',
     'Supervision de l''infrastructure',
     'Supervision interne des services et de l''infrastructure Zachary IT. Incluse dans le socle.',
     'Supervision', 'included', 'subscription', 'fixed', 0, 0, 0, 'active', 110);


-- --------------------------------------------------------------------------
-- Tiers stockage personnel : 16 / 32 / 64 / 128 / 256 / 512 GiB
-- --------------------------------------------------------------------------

INSERT IGNORE INTO billing_v2_service_tiers
    (id, service_id, code, name, public_label, numeric_value, unit,
     public_selectable, status, display_order)
SELECT UUID(), s.id, x.code, x.name, x.name, x.numeric_value, 'GiB',
       x.public_selectable, 'active', x.display_order
FROM billing_v2_services s
CROSS JOIN (
    SELECT '16'  AS code, '16 Go'  AS name, 16  AS numeric_value, 1 AS public_selectable, 10 AS display_order
    UNION ALL SELECT '32',  '32 Go',  32,  1, 20
    UNION ALL SELECT '64',  '64 Go',  64,  1, 30
    UNION ALL SELECT '128', '128 Go', 128, 1, 40
    UNION ALL SELECT '256', '256 Go', 256, 1, 50
    UNION ALL SELECT '512', '512 Go', 512, 0, 60
) x
WHERE s.code = 'STORAGE-PERSONAL';


-- --------------------------------------------------------------------------
-- Tiers stockage partagé : 32 / 64 / 128 / 256 / 512 GiB
-- --------------------------------------------------------------------------

INSERT IGNORE INTO billing_v2_service_tiers
    (id, service_id, code, name, public_label, numeric_value, unit,
     public_selectable, status, display_order)
SELECT UUID(), s.id, x.code, x.name, x.name, x.numeric_value, 'GiB',
       x.public_selectable, 'active', x.display_order
FROM billing_v2_services s
CROSS JOIN (
    SELECT '32'  AS code, '32 Go'  AS name, 32  AS numeric_value, 1 AS public_selectable, 10 AS display_order
    UNION ALL SELECT '64',  '64 Go',  64,  1, 20
    UNION ALL SELECT '128', '128 Go', 128, 1, 30
    UNION ALL SELECT '256', '256 Go', 256, 1, 40
    UNION ALL SELECT '512', '512 Go', 512, 0, 50
) x
WHERE s.code = 'STORAGE-SHARED';


-- --------------------------------------------------------------------------
-- Tiers backup : même capacité logique que le stockage couvert.
-- --------------------------------------------------------------------------

INSERT IGNORE INTO billing_v2_service_tiers
    (id, service_id, code, name, public_label, numeric_value, unit,
     public_selectable, status, display_order)
SELECT UUID(), s.id, x.code, x.name, x.name, x.numeric_value, 'GiB',
       0, 'active', x.display_order
FROM billing_v2_services s
CROSS JOIN (
    SELECT '16'  AS code, '16 Go protégés'  AS name, 16  AS numeric_value, 10 AS display_order
    UNION ALL SELECT '32',  '32 Go protégés',  32,  20
    UNION ALL SELECT '64',  '64 Go protégés',  64,  30
    UNION ALL SELECT '128', '128 Go protégés', 128, 40
    UNION ALL SELECT '256', '256 Go protégés', 256, 50
    UNION ALL SELECT '512', '512 Go protégés', 512, 60
) x
WHERE s.code = 'BACKUP-PERSONAL';


INSERT IGNORE INTO billing_v2_service_tiers
    (id, service_id, code, name, public_label, numeric_value, unit,
     public_selectable, status, display_order)
SELECT UUID(), s.id, x.code, x.name, x.name, x.numeric_value, 'GiB',
       0, 'active', x.display_order
FROM billing_v2_services s
CROSS JOIN (
    SELECT '32'  AS code, '32 Go protégés'  AS name, 32  AS numeric_value, 10 AS display_order
    UNION ALL SELECT '64',  '64 Go protégés',  64,  20
    UNION ALL SELECT '128', '128 Go protégés', 128, 30
    UNION ALL SELECT '256', '256 Go protégés', 256, 40
    UNION ALL SELECT '512', '512 Go protégés', 512, 50
) x
WHERE s.code = 'BACKUP-SHARED';


-- --------------------------------------------------------------------------
-- Tiers VPN : nom commercial + plafond technique interne.
-- Les chiffres correspondent à des limites techniques, pas à un débit garanti.
-- --------------------------------------------------------------------------

INSERT IGNORE INTO billing_v2_service_tiers
    (id, service_id, code, name, public_label, description,
     numeric_value, unit, public_selectable, status, display_order)
SELECT UUID(), s.id, x.code, x.name, x.name, x.description,
       x.numeric_value, 'Mbps', x.public_selectable, 'active', x.display_order
FROM billing_v2_services s
CROSS JOIN (
    SELECT 'ESSENTIAL' AS code,
           'VPN Essentiel' AS name,
           'Pour l''accès sécurisé aux fichiers et les usages courants.' AS description,
           100 AS numeric_value, 1 AS public_selectable, 10 AS display_order
    UNION ALL
    SELECT 'PLUS',
           'VPN Plus',
           'Pour une utilisation régulière et des transferts plus importants.',
           250, 1, 20
    UNION ALL
    SELECT 'PERFORMANCE',
           'VPN Performance',
           'Pour les usages intensifs et les transferts volumineux.',
           500, 1, 30
    UNION ALL
    SELECT 'PRO',
           'VPN Pro',
           'Pour les structures ayant des besoins réseau importants.',
           1000, 0, 40
) x
WHERE s.code = 'VPN-ACCESS';


-- ============================================================================
-- 20. SEED DES DÉPENDANCES
-- ============================================================================

INSERT IGNORE INTO billing_v2_service_dependencies
    (id, service_id, required_service_id, scope_relation, tier_relation, status)
SELECT
    UUID(),
    backup.id,
    storage.id,
    'same_scope',
    'same_numeric_value',
    'active'
FROM billing_v2_services backup
CROSS JOIN billing_v2_services storage
WHERE backup.code = 'BACKUP-PERSONAL'
  AND storage.code = 'STORAGE-PERSONAL';


INSERT IGNORE INTO billing_v2_service_dependencies
    (id, service_id, required_service_id, scope_relation, tier_relation, status)
SELECT
    UUID(),
    backup.id,
    storage.id,
    'same_scope',
    'same_numeric_value',
    'active'
FROM billing_v2_services backup
CROSS JOIN billing_v2_services storage
WHERE backup.code = 'BACKUP-SHARED'
  AND storage.code = 'STORAGE-SHARED';


-- ============================================================================
-- 21. SEED DES DURÉES D'ENGAGEMENT
--
-- Les remises 6/12 mois restent NULL volontairement tant que leur niveau
-- commercial n'est pas définitivement arrêté.
-- ============================================================================

INSERT IGNORE INTO billing_v2_commitment_terms
    (id, code, name, commitment_months, discount_basis_points,
     allow_monthly_payment, allow_upfront_payment, status, display_order)
VALUES
    (UUID(), 'FLEX',    'Sans engagement', 1,  0,    1, 0, 'active', 10),
    (UUID(), 'TERM-6',  'Engagement 6 mois', 6, NULL, 1, 1, 'active', 20),
    (UUID(), 'TERM-12', 'Engagement 12 mois', 12, NULL, 1, 1, 'active', 30);


-- ============================================================================
-- 22. SEED DES OPTIONS DE PAIEMENT PAR ENGAGEMENT
-- ============================================================================

INSERT IGNORE INTO billing_v2_commitment_payment_options
    (id, commitment_term_id, payment_mode, discount_basis_points, status, display_order)
SELECT UUID(), t.id, x.payment_mode, x.discount_basis_points, 'active', x.display_order
FROM billing_v2_commitment_terms t
JOIN (
    SELECT 'FLEX' AS term_code, 'monthly' AS payment_mode, 0 AS discount_basis_points, 10 AS display_order
    UNION ALL SELECT 'TERM-6', 'monthly', 1000, 20
    UNION ALL SELECT 'TERM-12', 'monthly', 1500, 30
    UNION ALL SELECT 'TERM-6', 'upfront', 1500, 40
    UNION ALL SELECT 'TERM-12', 'upfront', 2000, 50
) x ON x.term_code = t.code;


-- ============================================================================
-- 23. VUES D'AIDE AU MOTEUR DE PRIX
-- ============================================================================

CREATE OR REPLACE VIEW billing_v2_active_subscription_items AS
SELECT
    si.*
FROM billing_v2_subscription_items si
WHERE si.status = 'active'
  AND si.effective_from <= UTC_TIMESTAMP(6)
  AND (si.effective_until IS NULL OR si.effective_until > UTC_TIMESTAMP(6));


-- Sous-total récurrent AVANT remise globale.
CREATE OR REPLACE VIEW billing_v2_subscription_recurring_subtotals AS
SELECT
    s.id AS subscription_id,
    COALESCE(SUM(
        CASE
            WHEN sv.billing_type = 'recurring'
             AND si.discount_eligible_snapshot = 1
            THEN si.amount_cents_snapshot * si.quantity
            ELSE 0
        END
    ), 0) AS discount_eligible_subtotal_cents,

    COALESCE(SUM(
        CASE
            WHEN sv.billing_type = 'recurring'
             AND si.discount_eligible_snapshot = 0
            THEN si.amount_cents_snapshot * si.quantity
            ELSE 0
        END
    ), 0) AS non_discountable_subtotal_cents,

    COALESCE(SUM(
        CASE
            WHEN sv.billing_type = 'recurring'
            THEN si.amount_cents_snapshot * si.quantity
            ELSE 0
        END
    ), 0) AS recurring_subtotal_cents

FROM billing_v2_subscriptions s
LEFT JOIN billing_v2_active_subscription_items si
       ON si.subscription_id = s.id
LEFT JOIN billing_v2_services sv
       ON sv.id = si.service_id
GROUP BY s.id;


-- Montant récurrent contractuel après remise globale puis plancher.
--
-- Formule :
--   discounted =
--       eligible_subtotal * (1 - discount)
--       + non_discountable_subtotal
--
--   payable =
--       MAX(discounted, minimum_commitment_amount_cents)
--
-- Remarque :
--   + 5000 puis division entière garantit le même arrondi en centimes que
--   les totaux candidats documentés dans PRESETS.md.
CREATE OR REPLACE VIEW billing_v2_subscription_monthly_amounts AS
SELECT
    s.id AS subscription_id,
    x.recurring_subtotal_cents,
    s.discount_basis_points_snapshot,

    (
        (
            x.discount_eligible_subtotal_cents
            * (10000 - s.discount_basis_points_snapshot)
        )
        + 5000
        ) DIV 10000
        + x.non_discountable_subtotal_cents
    ) AS discounted_recurring_amount_cents,

    CASE
        WHEN s.minimum_commitment_amount_cents IS NULL THEN
            (
                (
                    x.discount_eligible_subtotal_cents
                    * (10000 - s.discount_basis_points_snapshot)
                )
                + 5000
                ) DIV 10000
                + x.non_discountable_subtotal_cents
            )
        ELSE
            GREATEST(
                (
                    (
                        x.discount_eligible_subtotal_cents
                        * (10000 - s.discount_basis_points_snapshot)
                    )
                    + 5000
                    ) DIV 10000
                    + x.non_discountable_subtotal_cents
                ),
                s.minimum_commitment_amount_cents
            )
    END AS payable_recurring_amount_cents

FROM billing_v2_subscriptions s
JOIN billing_v2_subscription_recurring_subtotals x
  ON x.subscription_id = s.id;


-- ============================================================================
-- FIN DU SCHÉMA BILLING V2
--
-- Étapes suivantes recommandées :
--   1. sauvegarde + application en DEV
--   2. tests de création/rollback
--   3. audit du code qui dépend de `commercial_offers`
--   4. création des prix V2
--   5. création des presets V2
--   6. mapping des 20 PACK-* legacy vers les presets/engagements V2
--   7. shadow pricing
--   8. bascule des nouveaux abonnements uniquement
-- ============================================================================
