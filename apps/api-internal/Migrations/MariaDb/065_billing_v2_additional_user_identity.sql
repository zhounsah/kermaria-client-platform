-- ============================================================================
-- Zachary IT - Billing V2
-- Migration 065 : cycle de vie d'identite des utilisateurs additionnels
--
-- Migration ADDITIVE et DORMANTE. Deux tables nouvelles, aucun ALTER, aucun
-- DROP, aucune donnee existante touchee. Tant qu'aucune place USER-ADDITIONAL
-- n'est attribuee, les deux tables restent vides et l'application se comporte
-- exactement comme avant.
--
-- Pourquoi deux tables et pas une :
--
--   `billing_v2_user_identity_provisioning` porte l'etat de MATERIALISATION
--   d'une place d'abonnement : ou en est la chaine portail -> KoXo -> AD.
--   C'est un sujet Billing V2 / annuaire.
--
--   `portal_user_password_setups` porte un jeton de DEFINITION DE MOT DE PASSE
--   rattache a un utilisateur portail. C'est un sujet d'authentification, qui
--   ne connait ni abonnement ni KoXo. Les melanger reproduirait exactement le
--   defaut de `signup_pending`, ou le jeton, l'etat AD et l'etat commercial
--   vivent dans la meme ligne et ne peuvent plus etre reutilises separement.
--
-- Ce que la migration NE fait PAS :
--   - elle ne touche pas `signup_pending` ;
--   - elle n'ajoute aucun statut a `portal_users` (un `pending` casserait a la
--     fois l'authentification et l'export KoXo, qui exigent tous deux
--     `status='active'`) ;
--   - elle n'ajoute aucun statut a `billing_v2_subscription_users`, dont le
--     `status` reste strictement contractuel.
-- ============================================================================

-- ============================================================================
-- 1. CYCLE DE VIE D'IDENTITE D'UNE PLACE D'ABONNEMENT
--
-- Relation 1:1 avec `billing_v2_subscription_users` : une place attribuee a
-- exactement une trajectoire de materialisation, et une seule.
--
-- Etats :
--   awaiting_password  la personne est creee cote portail, elle n'a pas encore
--                      choisi son mot de passe. Rien n'est publie vers KoXo :
--                      le CSV porte le mot de passe en colonne 14, donc
--                      exporter maintenant creerait un compte annuaire dont
--                      l'application ne maitrise pas le mot de passe.
--   koxo_pending       le mot de passe est defini. C'est le SEUL etat qui
--                      autorise l'export KoXo sans `customer_ad_links`, parce
--                      que c'est exactement la fenetre ou l'objet AD n'existe
--                      pas encore et ou seul KoXo peut le creer.
--   directory_ready    l'objet AD a ete retrouve par son `employeeNumber`,
--                      mais le lien `customer_ad_links` n'est pas encore
--                      confirme. Etat reel et non decoratif : la resolution
--                      annuaire et l'ecriture du lien sont deux operations
--                      distinctes, et une interruption entre les deux doit
--                      etre reprise sans relancer la resolution.
--   ready              le lien `customer_ad_links` existe et a ete relu. Le
--                      cycle est termine ; le stockage, le VPN et le RDS de
--                      cet utilisateur peuvent se debloquer par les mecanismes
--                      normaux de provisioning.
--   failed             une etape a echoue de facon explicite. Conserve le code
--                      d'echec pour diagnostic ; reprenable.
--   disabled           place desactivee. Prevu pour une desactivation future :
--                      ce lot ne construit AUCUNE suppression annuaire ou KoXo
--                      automatique, faute de contrat suffisamment etabli.
--
-- `assigned` n'existe pas : il serait entierement redondant avec
-- `billing_v2_subscription_users.identity_reference IS NOT NULL`, et deux
-- representations du meme fait finissent toujours par diverger.
-- ============================================================================

CREATE TABLE IF NOT EXISTS billing_v2_user_identity_provisioning (
    id                              CHAR(36)      NOT NULL,

    -- 1:1 avec la place d'abonnement.
    subscription_user_id            CHAR(36)      NOT NULL,

    -- Denormalises volontairement : l'export KoXo doit pouvoir prouver la
    -- coherence client/abonnement sans dependre d'une jointure supplementaire
    -- que quelqu'un pourrait un jour relacher.
    subscription_id                 CHAR(36)      NOT NULL,
    customer_id                     CHAR(36)      NOT NULL,

    portal_user_id                  CHAR(36)      NOT NULL,

    -- CLI-NNNNNN alloue par KoxoIdentifierAllocator dans la meme transaction
    -- que la creation de l'utilisateur portail. Recopie ici pour que la regle
    -- d'export puisse exiger l'egalite avec portal_users plutot que de la
    -- supposer.
    koxo_unique_identifier          VARCHAR(32)   NOT NULL,

    status                          VARCHAR(32)   NOT NULL
                                    DEFAULT 'awaiting_password',

    failure_code                    VARCHAR(96)   NULL,
    failure_detail                  TEXT          NULL,

    -- objectGUID de l'identite adoptee, forme canonique minuscule sans
    -- accolades. Preuve d'adoption, jamais cle de recherche.
    directory_object_guid           VARCHAR(64)   NULL,

    password_set_at                 DATETIME(6)   NULL,
    koxo_triggered_at               DATETIME(6)   NULL,
    directory_resolved_at           DATETIME(6)   NULL,
    directory_linked_at             DATETIME(6)   NULL,
    disabled_at                     DATETIME(6)   NULL,

    created_at                      DATETIME(6)   NOT NULL DEFAULT UTC_TIMESTAMP(6),
    updated_at                      DATETIME(6)   NOT NULL DEFAULT UTC_TIMESTAMP(6),

    PRIMARY KEY (id),

    -- Une place n'a qu'un cycle de vie : garde de base contre une double
    -- attribution concurrente, meme si le verrou applicatif tombait.
    UNIQUE KEY uq_billing_v2_user_identity_slot (subscription_user_id),

    -- Un utilisateur portail n'est la materialisation que d'une seule place :
    -- sans cette unicite, deux places pourraient revendiquer la meme personne
    -- et l'export KoXo la verrait deux fois.
    UNIQUE KEY uq_billing_v2_user_identity_portal_user (portal_user_id),

    KEY idx_billing_v2_user_identity_status (status, updated_at),
    KEY idx_billing_v2_user_identity_customer (customer_id, status),

    CONSTRAINT chk_billing_v2_user_identity_status CHECK (
        status IN (
            'awaiting_password',
            'koxo_pending',
            'directory_ready',
            'ready',
            'failed',
            'disabled'
        )
    ),

    CONSTRAINT fk_billing_v2_user_identity_slot
        FOREIGN KEY (subscription_user_id)
        REFERENCES billing_v2_subscription_users(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_user_identity_subscription
        FOREIGN KEY (subscription_id)
        REFERENCES billing_v2_subscriptions(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_user_identity_customer
        FOREIGN KEY (customer_id)
        REFERENCES customers(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    CONSTRAINT fk_billing_v2_user_identity_portal_user
        FOREIGN KEY (portal_user_id)
        REFERENCES portal_users(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- statement-break

-- ============================================================================
-- 2. JETON DE DEFINITION DE MOT DE PASSE, PAR UTILISATEUR PORTAIL
--
-- Generique et reutilisable : la table ne connait que `portal_users`. Le
-- `purpose` sert a tracer l'origine, pas a changer la semantique.
--
-- Invariants :
--   - seul SHA-256(jeton) est stocke, en hexadecimal minuscule ; le jeton en
--     clair n'existe que dans le lien envoye par e-mail ;
--   - `token_hash` est unique : une collision de jeton ne pourrait etre
--     tranchee en faveur de personne, donc elle doit etre impossible ;
--   - usage unique par `consumed_at`, la consommation etant un UPDATE
--     conditionnel dont on exige exactement une ligne affectee ;
--   - renouvellement par `superseded_at` : le lien precedent cesse
--     immediatement d'etre valable, sans etre efface (tracabilite).
-- ============================================================================

CREATE TABLE IF NOT EXISTS portal_user_password_setups (
    id                              CHAR(36)      NOT NULL,
    portal_user_id                  CHAR(36)      NOT NULL,

    purpose                         VARCHAR(48)   NOT NULL,

    -- SHA-256 hexadecimal minuscule. Jamais le jeton en clair.
    token_hash                      CHAR(64)      NOT NULL,

    expires_at                      DATETIME(6)   NOT NULL,
    consumed_at                     DATETIME(6)   NULL,
    superseded_at                   DATETIME(6)   NULL,

    created_at                      DATETIME(6)   NOT NULL DEFAULT UTC_TIMESTAMP(6),
    updated_at                      DATETIME(6)   NOT NULL DEFAULT UTC_TIMESTAMP(6),

    PRIMARY KEY (id),
    UNIQUE KEY uq_portal_user_password_setups_token (token_hash),
    KEY idx_portal_user_password_setups_user (portal_user_id, purpose, created_at),

    CONSTRAINT fk_portal_user_password_setups_user
        FOREIGN KEY (portal_user_id)
        REFERENCES portal_users(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
