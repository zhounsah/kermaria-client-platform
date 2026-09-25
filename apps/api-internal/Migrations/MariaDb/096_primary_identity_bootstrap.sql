-- ============================================================================
-- Zachary IT - Identite annuaire du compte client principal
-- Migration 096 : cycle de vie explicite de l'amorcage de l'identite AD
--
-- Migration ADDITIVE. Une table nouvelle, aucun ALTER, aucun DROP, aucune
-- donnee existante touchee, aucun remplissage retroactif : un compte deja
-- cree n'entre dans ce cycle que par une action explicite (definition ou
-- reprise du mot de passe), jamais par la migration.
--
-- Pourquoi une table dediee et pas `billing_v2_user_identity_provisioning` :
--
--   ce cycle-la materialise une PLACE d'abonnement non primaire. Le compte
--   principal existe avant tout abonnement (inscription, panier, VPS) et n'a
--   aucune place a designer. Y inscrire le principal obligerait a inventer une
--   place, un abonnement et un droit contractuel qui n'existent pas.
--
-- Invariant metier : tout compte client principal possede une identite AD
-- dans CLIENTS.HOME.BZH. Quand KoXo fait autorite, c'est lui qui la cree a
-- partir du CSV ; or un compte sans `customer_ad_links` n'etait jamais
-- exporte, donc jamais cree, donc jamais lie. Cette ligne est l'autorisation
-- EXPLICITE qui rompt cette boucle, et rien d'autre ne la remplace.
--
-- Etats :
--   awaiting_password  le compte existe, aucun secret destine a KoXo n'est
--                      disponible (inscription standard avant set-password,
--                      secret expire, reprise demandee). Jamais exporte.
--   koxo_pending       le secret chiffre est depose. Seul etat, avec le
--                      suivant, qui autorise l'export sans lien AD — et
--                      seulement si l'e-mail est verifie lorsque le parcours
--                      l'exige.
--   directory_ready    l'objet AD a ete retrouve par employeeNumber et son
--                      objectGUID fige ici, le lien n'est pas encore relu.
--                      Reste exporte : le sortir du CSV desactiverait l'objet.
--   completed          `customer_ad_links(user)` existe et a ete relu. La
--                      contrainte CHECK interdit cet etat sans objectGUID ni
--                      date de liaison.
--   failed             conflit d'identite (objet d'un autre client, objet deja
--                      lie a un autre utilisateur portail...). Arbitrage humain.
-- ============================================================================

CREATE TABLE IF NOT EXISTS portal_user_identity_bootstrap (
    id                              CHAR(36)      NOT NULL,

    -- 1:1 avec le compte principal.
    portal_user_id                  CHAR(36)      NOT NULL,
    customer_id                     CHAR(36)      NOT NULL,

    -- Demande d'inscription d'origine : porte l'etat civil et le parcours.
    signup_id                       CHAR(36)      NOT NULL,

    -- Recopie de portal_users.koxo_unique_identifier : l'export exige
    -- l'egalite des deux plutot que de la supposer.
    koxo_unique_identifier          VARCHAR(32)   NOT NULL,

    origin                          VARCHAR(32)   NOT NULL,

    -- Vrai pour les parcours self-service, qui creent le compte AVANT la
    -- preuve de possession de l'adresse : l'export attend alors
    -- portal_users.email_verified_at.
    email_verification_required     BOOLEAN       NOT NULL DEFAULT TRUE,

    status                          VARCHAR(32)   NOT NULL
                                    DEFAULT 'awaiting_password',

    failure_code                    VARCHAR(96)   NULL,
    failure_detail                  TEXT          NULL,

    -- objectGUID adopte, forme canonique. Preuve d'adoption, jamais cle de
    -- recherche.
    directory_object_guid           VARCHAR(64)   NULL,

    password_set_at                 DATETIME(6)   NULL,
    koxo_triggered_at               DATETIME(6)   NULL,
    directory_resolved_at           DATETIME(6)   NULL,
    directory_linked_at             DATETIME(6)   NULL,
    recovery_requested_at           DATETIME(6)   NULL,
    last_attempt_at                 DATETIME(6)   NULL,
    attempt_count                   INT           NOT NULL DEFAULT 0,

    created_at                      DATETIME(6)   NOT NULL DEFAULT UTC_TIMESTAMP(6),
    updated_at                      DATETIME(6)   NOT NULL DEFAULT UTC_TIMESTAMP(6),

    PRIMARY KEY (id),

    -- Un seul cycle par compte : deux amorcages concurrents ne peuvent pas
    -- produire deux trajectoires, meme si le verrou applicatif tombait.
    UNIQUE KEY uq_portal_user_identity_bootstrap_user (portal_user_id),

    KEY idx_portal_user_identity_bootstrap_status (status, last_attempt_at),
    KEY idx_portal_user_identity_bootstrap_customer (customer_id, status),
    KEY idx_portal_user_identity_bootstrap_signup (signup_id),

    CONSTRAINT chk_portal_user_identity_bootstrap_status CHECK (
        status IN (
            'awaiting_password',
            'koxo_pending',
            'directory_ready',
            'completed',
            'failed'
        )
    ),

    CONSTRAINT chk_portal_user_identity_bootstrap_origin CHECK (
        origin IN ('signup', 'self_service_cart', 'self_service_vps')
    ),

    -- Jamais `completed` sans preuve d'adoption : la base refuse l'etat final
    -- tant que l'objectGUID et la date de liaison ne sont pas poses.
    CONSTRAINT chk_portal_user_identity_bootstrap_completed CHECK (
        status <> 'completed'
        OR (directory_object_guid IS NOT NULL
            AND directory_linked_at IS NOT NULL)
    ),

    CONSTRAINT fk_portal_user_identity_bootstrap_user
        FOREIGN KEY (portal_user_id)
        REFERENCES portal_users(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_portal_user_identity_bootstrap_customer
        FOREIGN KEY (customer_id)
        REFERENCES customers(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_portal_user_identity_bootstrap_signup
        FOREIGN KEY (signup_id)
        REFERENCES signup_pending(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
