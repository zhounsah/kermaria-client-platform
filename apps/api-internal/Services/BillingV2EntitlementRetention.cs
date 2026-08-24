namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Qui garde ses droits, et jusqu'a quand.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="BillingV2CancellationPolicy"/> promet au client que la periode
/// qu'il a deja payee sera servie jusqu'a son terme : c'est la raison meme pour
/// laquelle une resiliation a fin de terme pose <c>pending_cancellation</c> et
/// non <c>cancelled</c>. Cette promesse n'a de valeur que si les portes d'acces
/// la respectent. Filtrer sur <c>status = 'active'</c> la brise a la seconde ou
/// le client clique sur « resilier » : il perd ses telechargements alors qu'il
/// a paye jusqu'au terme.
/// </para>
/// <para>
/// <b>Conserver n'est pas ouvrir.</b> Deux questions distinctes vivent ici :
/// <see cref="GrantsAcquiredRights"/> — le client garde-t-il ce qu'il a deja
/// acquis ? — et <see cref="AllowsNewMutations"/> — a-t-il encore le droit
/// d'agrandir son contrat ? Un abonnement en cours de resiliation repond oui a
/// la premiere et non a la seconde. Les confondre transformerait cette
/// correction en regression symetrique : des places supplementaires, du
/// provisioning et des changements d'offre demandes sur un contrat qu'on est en
/// train de fermer.
/// </para>
/// <para>
/// La borne de conservation est <c>current_period_ends_at</c>, pas
/// <c>renews_at</c> : c'est la fin de ce qui a ete encaisse. Passe ce terme, un
/// <c>pending_cancellation</c> n'ouvre plus rien, meme si l'appel fournisseur
/// n'a pas encore converge — on ne sert pas gratuitement en attendant une
/// confirmation.
/// </para>
/// </remarks>
public static class BillingV2EntitlementRetentionPolicy
{
    public const string Active = "active";
    public const string PendingCancellation = "pending_cancellation";

    /// <summary>
    /// La fenetre contractuelle globale, miroir exact de
    /// <see cref="BillingV2ContractWindowSql.SubscriptionStillInForce"/>.
    /// </summary>
    /// <remarks>
    /// Un contrat comptant reste <c>active</c> en base une fois arrive a terme :
    /// aucun renouvellement automatique ne le bascule. Sans cette borne, ses
    /// droits survivraient indefiniment a la periode payee.
    /// </remarks>
    public static bool StillInForce(
        DateTime? renewsAtUtc,
        DateTime? commitmentEndsAtUtc,
        DateTime nowUtc)
        => renewsAtUtc is not null
           || commitmentEndsAtUtc is null
           || commitmentEndsAtUtc > nowUtc;

    /// <summary>
    /// Le client conserve-t-il les droits deja acquis sur cet abonnement ?
    /// </summary>
    public static bool GrantsAcquiredRights(
        string status,
        DateTime? currentPeriodEndsAtUtc,
        DateTime? renewsAtUtc,
        DateTime? commitmentEndsAtUtc,
        DateTime nowUtc)
    {
        if (!StillInForce(renewsAtUtc, commitmentEndsAtUtc, nowUtc))
        {
            return false;
        }

        return status switch
        {
            Active => true,

            // La periode encaissee, et elle seule. `cancelled` et `expired`
            // n'apparaissent pas : ils affirment que plus rien n'est du, donc
            // que plus rien n'est servi.
            PendingCancellation =>
                currentPeriodEndsAtUtc is not null
                && currentPeriodEndsAtUtc > nowUtc,

            _ => false
        };
    }

    /// <summary>
    /// L'abonnement peut-il encore etre agrandi : nouvelles places, nouveau
    /// provisioning, changement d'offre ?
    /// </summary>
    /// <remarks>
    /// Volontairement plus strict que <see cref="GrantsAcquiredRights"/>. Un
    /// contrat qu'on ferme n'accueille pas de nouvel utilisateur : le geste
    /// serait facture sur une periode qui ne sera pas renouvelee, et devrait
    /// etre deprovisionne dans la foulee.
    /// </remarks>
    public static bool AllowsNewMutations(string status)
        => string.Equals(status, Active, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Traduction SQL de <see cref="BillingV2EntitlementRetentionPolicy"/>.
/// </summary>
/// <remarks>
/// Les fragments sont composes a partir des memes constantes de statut que la
/// politique C#, de sorte qu'ajouter un statut a l'une sans l'autre soit
/// impossible par simple oubli d'edition. Alias impose a tout consommateur :
/// <c>subscription</c> pour l'abonnement.
/// </remarks>
public static class BillingV2EntitlementRetentionSql
{
    /// <summary>
    /// Predicat unique des portes d'acces : statut ET fenetre contractuelle.
    /// </summary>
    public static readonly string SubscriptionGrantsAcquiredRights =
        $"""
        (
            (
                subscription.status
                    = '{BillingV2EntitlementRetentionPolicy.Active}'
                OR (
                    subscription.status
                        = '{BillingV2EntitlementRetentionPolicy.PendingCancellation}'
                    AND subscription.current_period_ends_at IS NOT NULL
                    AND subscription.current_period_ends_at > UTC_TIMESTAMP(6)
                )
            )
            AND {BillingV2ContractWindowSql.SubscriptionStillInForce}
        )
        """;
}
