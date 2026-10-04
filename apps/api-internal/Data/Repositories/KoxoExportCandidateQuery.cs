namespace Kermaria.ApiInternal.Data.Repositories;

/// <summary>
/// Requete des candidats a l'export KoXo.
/// </summary>
/// <remarks>
/// <para>
/// Extraite du depot pour etre verifiable : c'est la seule regle du systeme
/// qui decide quelles identites reelles KoXo va creer, modifier ou
/// <b>desactiver</b> — le CSV faisant autorite, une ligne absente desactive le
/// compte correspondant. Une relaxation accidentelle de cette clause ne doit
/// pas pouvoir passer inapercue.
/// </para>
/// <para>
/// La regle de base reste <b>fail-closed</b> : un client payant ordinaire doit
/// deja porter un <c>customer_ad_links</c>. Trois exceptions seulement, toutes
/// motivees par la meme circularite — KoXo doit creer l'objet annuaire avant
/// que le lien puisse exister :
/// </para>
/// <list type="number">
/// <item>
/// l'essai de demonstration (<c>demo_kind = 'trial'</c>), regle preexistante,
/// inchangee ;
/// </item>
/// <item>
/// l'utilisateur additionnel Billing V2 <b>designe explicitement</b> par une
/// ligne de <c>billing_v2_user_identity_provisioning</c> en etat
/// <c>koxo_pending</c> ou <c>directory_ready</c> — les deux etats ou le lien
/// AD n'existe pas encore. Retenir le seul <c>koxo_pending</c> ouvrait une
/// fenetre de crash : le cycle passe par <c>directory_ready</c> avant
/// l'ecriture du lien, et une interruption a cet instant sortait l'identite du
/// CSV, donc la <b>desactivait</b>, sans retour possible.
/// </item>
/// <item>
/// le compte client <b>principal</b> designe explicitement par une ligne de
/// <c>portal_user_identity_bootstrap</c> (migration 096) dans ces deux memes
/// etats. Sans elle, la boucle etait fermee : pas de lien, donc pas d'export,
/// donc pas d'identite, donc pas de lien — et le set-password standard
/// repondait <c>AD_IDENTITY_NOT_READY</c> pour toujours. La branche exige en
/// plus l'e-mail verifie quand le parcours l'impose (self-service) et un
/// secret KoXo non expire : sans lui la ligne serait invalide, et un seul
/// invalide bloque l'export global.
/// </item>
/// </list>
/// <para>
/// La seconde exception ne se contente jamais d'un indice. Elle exige la
/// chaine complete : cycle de vie -&gt; place -&gt; abonnement -&gt; item actif
/// -&gt; service actif -&gt; regle <c>contractual_entitlement</c> /
/// <c>user_slot</c> active ; l'egalite stricte du <c>customer_id</c> sur les
/// trois niveaux ; la place non-primaire et active ; l'identifiant KoXo egal
/// des deux cotes ; et l'etat civil complet exige par le CSV.
/// </para>
/// <para>
/// Ce dernier point n'est pas cosmetique : un candidat incomplet fait echouer
/// la validation, et un seul invalide bloque l'export <b>global</b>. Le laisser
/// dehors le maintient en attente sans casser la synchronisation des autres
/// comptes.
/// </para>
/// <para>
/// Ce qui n'est <b>jamais</b> suffisant, seul ou combine : etre un client
/// payant, ne pas avoir de lien AD, avoir <c>password_hash IS NULL</c>, ou
/// porter un <c>identity_reference</c> non nul. Aucune de ces conditions ne
/// designe une identite que KoXo doit creer maintenant. En particulier, aucune
/// branche ne rend exportable « tout portal_user sans lien AD » : chaque
/// exception passe par une ligne de cycle de vie qui designe CE compte.
/// </para>
/// <para>
/// Une vitrine (<c>demo_kind = 'showcase'</c>) reste exclue en toutes
/// circonstances : elle est inerte par construction et n'a rien a faire dans le
/// pipeline d'identites reelles.
/// </para>
/// </remarks>
public static class KoxoExportCandidateQuery
{
    public const string Sql =
        """
        SELECT
            portal_user.id AS portal_user_id,
            customer.id AS customer_id,
            customer.external_reference AS customer_reference,
            portal_user.koxo_unique_identifier AS koxo_unique_identifier,
            portal_user.personal_title AS personal_title,
            portal_user.given_name AS given_name,
            portal_user.surname AS surname,
            portal_user.birth_date AS birth_date,
            portal_user.email AS email,
            customer.is_demo AS is_demo,
            customer.koxo_group_reference AS koxo_group_reference,
            -- Sans lien AD et hors demonstration, une ligne ne peut etre la
            -- que par l'exception Billing V2 : KoXo doit alors creer l'objet,
            -- et le mot de passe ne peut lui parvenir que par le CSV.
            (ad_link.portal_user_id IS NULL AND customer.is_demo = FALSE)
                AS requires_pending_password
        FROM portal_users portal_user
        INNER JOIN customers customer
            ON customer.id = portal_user.customer_id
        -- LEFT et non INNER : un essai de demonstration n'a pas encore
        -- d'identite AD, et c'est justement KoXo qui doit la creer. Avec une
        -- jointure stricte il serait exclu du CSV, donc jamais cree, donc
        -- toujours exclu — l'impasse qui laissait OU=CLI-DEMO vide.
        LEFT JOIN customer_ad_links ad_link
            ON ad_link.portal_user_id = portal_user.id
           AND ad_link.object_type = 'user'
        WHERE portal_user.status = 'active'
          AND customer.status = 'active'
          -- La regle stricte reste la norme : seuls les essais de demo et les
          -- utilisateurs additionnels Billing V2 explicitement autorises sont
          -- exportes sans identite prealable, pour qu'aucun vrai client dont
          -- le provisioning AD a echoue ne parte dans le CSV par accident.
          --
          -- L'etat civil complet est exige dans les deux cas : un compte
          -- incomplet serait rejete par la validation de l'export, or un seul
          -- invalide bloque l'export GLOBAL. Le laisser dehors le maintient en
          -- attente sans jamais casser la synchronisation des autres comptes.
          AND (
                ad_link.portal_user_id IS NOT NULL
             OR (
                    customer.is_demo = TRUE
                AND customer.demo_kind = 'trial'
                AND portal_user.personal_title IS NOT NULL
                AND portal_user.given_name IS NOT NULL
                AND portal_user.surname IS NOT NULL
                AND portal_user.birth_date IS NOT NULL
                AND portal_user.koxo_unique_identifier IS NOT NULL
             )
             OR (
                    customer.is_demo = FALSE
                AND portal_user.personal_title IS NOT NULL
                AND portal_user.given_name IS NOT NULL
                AND portal_user.surname IS NOT NULL
                AND portal_user.birth_date IS NOT NULL
                AND portal_user.koxo_unique_identifier IS NOT NULL
                -- Le cycle de vie doit designer CE portal_user, et lui seul.
                -- Aucun repli, aucune correspondance approchante : c'est la
                -- ligne de cycle de vie qui autorise, pas la ressemblance.
                AND EXISTS (
                    SELECT 1
                    FROM billing_v2_user_identity_provisioning lifecycle
                    INNER JOIN billing_v2_subscription_users slot
                        ON slot.id = lifecycle.subscription_user_id
                    INNER JOIN billing_v2_subscriptions sub
                        ON sub.id = lifecycle.subscription_id
                    INNER JOIN billing_v2_subscription_items item
                        ON item.subscription_user_id = slot.id
                       AND item.subscription_id = sub.id
                       AND item.status = 'active'
                       AND item.scope_type = 'user'
                       AND item.effective_from <= UTC_TIMESTAMP(6)
                       AND (item.effective_until IS NULL
                            OR item.effective_until > UTC_TIMESTAMP(6))
                    INNER JOIN billing_v2_services service
                        ON service.id = item.service_id
                       AND service.status = 'active'
                    INNER JOIN billing_v2_provisioning_rules rule
                        ON rule.service_id = service.id
                       AND rule.status = 'active'
                       AND rule.rule_type = 'contractual_entitlement'
                       AND rule.target_type = 'user_slot'
                       AND (rule.tier_id IS NULL
                            OR rule.tier_id = item.tier_id)
                    WHERE lifecycle.portal_user_id = portal_user.id
                      -- Deux etats seulement, et pour la meme raison : le lien
                      -- AD n'existe pas encore.
                      --
                      -- koxo_pending  : KoXo n'a pas encore cree l'objet.
                      -- directory_ready : l'objet est resolu mais le lien
                      --   n'est pas ecrit. Un arret entre les deux laisserait
                      --   sinon l'identite hors du CSV, donc DESACTIVEE, sans
                      --   aucun moyen de revenir — c'est la fenetre de crash.
                      --
                      -- awaiting_password ne part pas : le mot de passe n'est
                      -- pas pose et le compte naitrait sans secret maitrise.
                      -- ready n'en a pas besoin : le lien existe et la branche
                      -- normale suffit. failed et disabled n'autorisent rien.
                      AND lifecycle.status IN (
                          'koxo_pending', 'directory_ready')
                      AND lifecycle.koxo_unique_identifier =
                          portal_user.koxo_unique_identifier
                      -- Le meme client aux trois niveaux, sans exception :
                      -- une place d'un autre client ne doit jamais pouvoir
                      -- faire creer une identite ici.
                      AND lifecycle.customer_id = portal_user.customer_id
                      AND sub.customer_id = portal_user.customer_id
                      AND slot.subscription_id = lifecycle.subscription_id
                      -- La place doit reellement pointer cette personne.
                      AND slot.identity_reference = portal_user.id
                      AND slot.status = 'active'
                      AND slot.is_primary = 0
                      -- Etat contractuel de l'abonnement, aligne sur celui
                      -- qu'exige la projection de provisioning.
                      AND sub.status = 'active'
                )
             )
             -- Compte client PRINCIPAL, designe par son amorcage explicite
             -- (migration 096). Transcrit par PrimaryIdentityBootstrapPolicy,
             -- que le service reapplique avant toute action annuaire.
             OR (
                    customer.is_demo = FALSE
                AND ad_link.portal_user_id IS NULL
                AND portal_user.personal_title IS NOT NULL
                AND portal_user.given_name IS NOT NULL
                AND portal_user.surname IS NOT NULL
                AND portal_user.birth_date IS NOT NULL
                AND portal_user.koxo_unique_identifier IS NOT NULL
                AND EXISTS (
                    SELECT 1
                    FROM portal_user_identity_bootstrap bootstrap
                    WHERE bootstrap.portal_user_id = portal_user.id
                      AND bootstrap.customer_id = portal_user.customer_id
                      AND bootstrap.koxo_unique_identifier =
                          portal_user.koxo_unique_identifier
                      -- Les deux etats sans lien AD, pour la meme raison que
                      -- l'utilisateur additionnel : sortir du CSV entre la
                      -- resolution et le lien desactiverait l'objet.
                      AND bootstrap.status IN (
                          'koxo_pending', 'directory_ready')
                      -- Parcours self-service : le compte existe avant la
                      -- preuve de possession de l'adresse, KoXo ne doit rien
                      -- creer avant elle.
                      AND (bootstrap.email_verification_required = FALSE
                           OR portal_user.email_verified_at IS NOT NULL)
                )
                -- Le secret de la colonne 14 doit exister : KoXo va CREER
                -- l'objet, et une ligne sans mot de passe serait invalide,
                -- donc bloquerait l'export de tous les autres comptes.
                AND EXISTS (
                    SELECT 1
                    FROM koxo_pending_directory_passwords pending_secret
                    WHERE pending_secret.portal_user_id = portal_user.id
                      AND pending_secret.expires_at > UTC_TIMESTAMP(6)
                )
                -- Un utilisateur additionnel a son propre cycle : jamais
                -- les deux a la fois.
                AND NOT EXISTS (
                    SELECT 1
                    FROM billing_v2_user_identity_provisioning other_lifecycle
                    WHERE other_lifecycle.portal_user_id = portal_user.id
                )
             )
          )
          -- Une vitrine est inerte par construction : elle ne doit jamais
          -- atteindre le pipeline d'identites reelles. Seuls les essais
          -- (trial), qui ont besoin d'une identite AD, sont exportes.
          AND NOT (customer.is_demo = TRUE AND customer.demo_kind = 'showcase')
        ORDER BY
            customer.external_reference ASC,
            portal_user.koxo_unique_identifier ASC,
            portal_user.id ASC;
        """;
}
