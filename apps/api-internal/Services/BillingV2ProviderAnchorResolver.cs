using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Une piste d'ancre fournisseur, avec la table qui l'a fournie.
/// </summary>
/// <param name="Source">
/// <c>payment_agreement</c>, <c>checkout_session</c> ou <c>payment_attempt</c>.
/// Conservee parce qu'elle change la confiance qu'on accorde a la valeur : un
/// <c>payment_attempt</c> REGLE a deja ete confronte au montant, a la devise et
/// au client lors de la verification de settlement.
/// </param>
public sealed record BillingV2ProviderAnchorCandidate(
    string Source,
    string Provider,
    string Environment,
    string ProviderSubscriptionId);

public enum BillingV2ProviderAnchorOutcome
{
    /// <summary>Une ancre unique et coherente a ete trouvee.</summary>
    Resolved,

    /// <summary>Aucune des trois sources autoritaires ne porte d'ancre.</summary>
    Missing,

    /// <summary>Les sources se contredisent : on refuse de choisir.</summary>
    Conflict
}

public sealed record BillingV2ProviderAnchorResolution(
    BillingV2ProviderAnchorOutcome Outcome,
    BillingV2ProviderAnchor? Anchor,
    string? Source,
    string ReasonCode)
{
    public bool IsResolved => Outcome is BillingV2ProviderAnchorOutcome.Resolved;
}

/// <summary>
/// Resolution de l'ancre fournisseur d'un abonnement Billing V2.
/// </summary>
/// <remarks>
/// <para>
/// Billing V2 ecrit l'identifiant d'abonnement fournisseur a trois endroits, et
/// aucun n'est present dans tous les scenarios. Un accord de paiement existe
/// quand le fournisseur a confirme un mandat ; une session de checkout existe
/// quand le parcours est passe par une redirection ; une tentative de paiement
/// REGLEE existe des qu'un encaissement a converge — y compris par
/// reconciliation, cas ou ni accord ni session ne portent l'identifiant.
/// </para>
/// <para>
/// Ne lire qu'une seule de ces sources, c'est conclure « pas d'abonnement
/// fournisseur » sur un abonnement qui en a un. Pour une resiliation, cette
/// conclusion est financierement fausse : elle cloturerait localement un
/// contrat que le fournisseur continue de prelever.
/// </para>
/// <para>
/// <b>En cas de desaccord entre sources, on ne choisit pas.</b> Deux
/// identifiants differents, ou deux fournisseurs, ou deux environnements pour
/// le meme abonnement signalent une donnee corrompue. Preferer arbitrairement
/// « la plus recente » reviendrait a agir sur un objet fournisseur possiblement
/// faux — resilier l'abonnement d'un autre client, ou en laisser un vivant. On
/// echoue en ferme et on laisse la main a un humain.
/// </para>
/// </remarks>
public static class BillingV2ProviderAnchorPolicy
{
    public const string ResolvedReasonCode =
        "BILLING_V2_PROVIDER_ANCHOR_RESOLVED";

    public const string MissingReasonCode =
        "BILLING_V2_CANCELLATION_PROVIDER_ANCHOR_MISSING";

    public const string ConflictReasonCode =
        "BILLING_V2_PROVIDER_ANCHOR_CONFLICT";

    /// <summary>
    /// Ordre de confiance, du plus sur au moins sur. N'intervient que pour
    /// nommer la source retenue : si les valeurs divergeaient, on aurait deja
    /// echoue.
    /// </summary>
    private static readonly string[] SourcePriority =
        ["payment_attempt", "payment_agreement", "checkout_session"];

    public static BillingV2ProviderAnchorResolution Resolve(
        IReadOnlyList<BillingV2ProviderAnchorCandidate> candidates)
    {
        var usable = candidates
            .Where(candidate =>
                !string.IsNullOrWhiteSpace(candidate.ProviderSubscriptionId))
            .ToList();

        if (usable.Count == 0)
        {
            return new BillingV2ProviderAnchorResolution(
                BillingV2ProviderAnchorOutcome.Missing,
                null,
                null,
                MissingReasonCode);
        }

        var distinct = usable
            .Select(candidate => (
                Provider: Normalize(candidate.Provider),
                Environment: Normalize(candidate.Environment),
                Subscription: candidate.ProviderSubscriptionId.Trim()))
            .Distinct()
            .ToList();

        if (distinct.Count > 1)
        {
            return new BillingV2ProviderAnchorResolution(
                BillingV2ProviderAnchorOutcome.Conflict,
                null,
                null,
                ConflictReasonCode);
        }

        var resolved = distinct[0];
        var source = usable
            .OrderBy(candidate => PriorityOf(candidate.Source))
            .First()
            .Source;

        return new BillingV2ProviderAnchorResolution(
            BillingV2ProviderAnchorOutcome.Resolved,
            new BillingV2ProviderAnchor(
                resolved.Provider,
                resolved.Environment,
                resolved.Subscription),
            source,
            ResolvedReasonCode);
    }

    private static int PriorityOf(string source)
    {
        var index = Array.IndexOf(SourcePriority, source);
        return index < 0 ? SourcePriority.Length : index;
    }

    private static string Normalize(string value)
        => value.Trim().ToLowerInvariant();
}

/// <summary>
/// Lecture des sources autoritaires de l'ancre fournisseur.
/// </summary>
/// <remarks>
/// Expose en statique pour que tous les appelants — resiliation,
/// renouvellement, mutation recurrente — partagent exactement cette resolution
/// au lieu d'en reecrire chacun une variante. Trois implementations divergentes
/// de « quel est l'abonnement fournisseur de ce contrat » finiraient par ne
/// plus repondre la meme chose, et la plus pauvre des trois deciderait d'une
/// resiliation.
/// </remarks>
public static class BillingV2ProviderAnchorReader
{
    /// <param name="provider">
    /// Restreint la recherche a un fournisseur. <c>null</c> cherche partout —
    /// c'est ce que fait la resiliation, qui ne presuppose pas le rail par
    /// lequel l'abonnement a ete souscrit.
    /// </param>
    public static async Task<IReadOnlyList<BillingV2ProviderAnchorCandidate>>
        ReadCandidatesAsync(
            MySqlConnection connection,
            MySqlTransaction? transaction,
            string subscriptionId,
            string? provider,
            CancellationToken cancellationToken)
    {
        var candidates = new List<BillingV2ProviderAnchorCandidate>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT 'payment_agreement' AS anchor_source,
                   provider,
                   environment,
                   provider_subscription_id
            FROM billing_v2_payment_agreements
            WHERE subscription_id = @subscription_id
              AND provider_subscription_id IS NOT NULL
              AND provider_subscription_id <> ''
              AND (@provider IS NULL OR provider = @provider)

            UNION ALL

            SELECT 'checkout_session',
                   provider,
                   environment,
                   provider_subscription_id
            FROM billing_v2_provider_checkout_sessions
            WHERE subscription_id = @subscription_id
              AND provider_subscription_id IS NOT NULL
              AND provider_subscription_id <> ''
              AND (@provider IS NULL OR provider = @provider)

            UNION ALL

            -- Source la plus sure : l'identifiant lie a une tentative REGLEE,
            -- donc deja confronte au montant, a la devise et au client lors de
            -- la verification de settlement. Un encaissement qui converge par
            -- reconciliation ne cree ni accord ni session portant cet
            -- identifiant : sans cette branche, l'ancre serait introuvable.
            SELECT 'payment_attempt',
                   attempt.provider,
                   attempt.environment,
                   attempt.provider_subscription_id
            FROM billing_v2_payment_attempts attempt
            INNER JOIN billing_v2_billing_events event_row
                ON event_row.id = attempt.billing_event_id
            WHERE event_row.subscription_id = @subscription_id
              AND attempt.status = 'succeeded'
              AND attempt.provider_subscription_id IS NOT NULL
              AND attempt.provider_subscription_id <> ''
              AND (@provider IS NULL OR attempt.provider = @provider);
            """;
        command.Parameters.AddWithValue("@subscription_id", subscriptionId);
        command.Parameters.AddWithValue(
            "@provider",
            provider is null ? DBNull.Value : provider);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            candidates.Add(new BillingV2ProviderAnchorCandidate(
                reader.GetString("anchor_source"),
                reader.GetString("provider"),
                reader.GetString("environment"),
                reader.GetString("provider_subscription_id")));
        }

        return candidates;
    }

    public static async Task<BillingV2ProviderAnchorResolution> ResolveAsync(
        MySqlConnection connection,
        MySqlTransaction? transaction,
        string subscriptionId,
        string? provider,
        CancellationToken cancellationToken)
        => BillingV2ProviderAnchorPolicy.Resolve(
            await ReadCandidatesAsync(
                connection,
                transaction,
                subscriptionId,
                provider,
                cancellationToken));

    /// <summary>
    /// Raccourci pour les appelants qui n'ont besoin que de l'identifiant
    /// d'abonnement Stripe.
    /// </summary>
    /// <remarks>
    /// Un conflit ressort ici en <c>null</c> : ces appelants echouent deja
    /// proprement sur l'absence d'ancre, et faire porter un identifiant
    /// contradictoire a un appel de facturation serait pire que ne rien faire.
    /// </remarks>
    public static async Task<string?> ReadStripeSubscriptionIdAsync(
        MySqlConnection connection,
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        var resolution = await ResolveAsync(
            connection,
            transaction: null,
            subscriptionId,
            "stripe",
            cancellationToken);
        return resolution.Anchor?.ProviderSubscriptionId;
    }

    /// <summary>
    /// L'abonnement porte-t-il au moins une composante reellement recurrente ?
    /// </summary>
    /// <remarks>
    /// <para>
    /// C'est la seule maniere honnete de decider qu'un contrat n'a legitimement
    /// aucun abonnement fournisseur. L'absence d'ancre, elle, ne prouve rien :
    /// elle peut aussi bien venir d'une ecriture manquee, d'une reconciliation
    /// incomplete ou d'un rail qui n'a pas persiste l'identifiant.
    /// </para>
    /// <para>
    /// On lit le snapshot de composantes effectif : les lignes actives dont la
    /// fenetre d'effet couvre l'instant courant. Une ligne retiree ou expiree
    /// ne cree plus d'obligation de prelevement.
    /// </para>
    /// <para>
    /// <b>La cadence se lit sur
    /// <c>billing_v2_subscription_item_effective_price_components</c>, jamais
    /// sur <c>item.service_price_id</c>.</b> Les colonnes historiques de l'item
    /// ne sont qu'un miroir de compatibilite : sur un item
    /// <c>componentized</c>, elles portent au mieux la premiere composante et
    /// ne decrivent pas le contrat. Un item dont la composante recurrente est
    /// mensuelle mais dont le prix miroir est ponctuel serait declare
    /// non-recurrent, et sa resiliation cloturerait localement un abonnement
    /// que le fournisseur continue de prelever. La vue est le point de lecture
    /// unique du prix contractuel ; elle projette aussi les items
    /// <c>legacy_single</c> en composante virtuelle, donc rien n'est perdu.
    /// </para>
    /// </remarks>
    public static async Task<bool> HasRecurringComponentAsync(
        MySqlConnection connection,
        MySqlTransaction? transaction,
        string subscriptionId,
        CancellationToken cancellationToken)
        => BillingV2RecurringComponentPolicy.HasRecurring(
            await ReadEffectivePriceComponentsAsync(
                connection,
                transaction,
                subscriptionId,
                cancellationToken),
            DateTime.UtcNow);

    /// <summary>
    /// Les composantes de prix effectives d'un abonnement, brutes.
    /// </summary>
    /// <remarks>
    /// Le filtrage — statuts, fenetres, cadence — est laisse a
    /// <see cref="BillingV2RecurringComponentPolicy"/> plutot qu'ecrit ici en
    /// SQL. La regle qui decide « cet abonnement preleve encore » est alors une
    /// fonction pure, exercee par des tests sur les cas qui comptent : un item
    /// componentized portant a la fois une composante mensuelle et une
    /// composante ponctuelle, une composante retiree, un item expire. Ecrite en
    /// SQL, elle ne serait verifiable qu'en base reelle — c'est-a-dire nulle
    /// part dans une suite qui tourne en persistance mock. Le volume lu est
    /// celui des composantes d'un seul abonnement.
    /// </remarks>
    private static async Task<IReadOnlyList<BillingV2EffectivePriceComponentRow>>
        ReadEffectivePriceComponentsAsync(
            MySqlConnection connection,
            MySqlTransaction? transaction,
            string subscriptionId,
            CancellationToken cancellationToken)
    {
        var rows = new List<BillingV2EffectivePriceComponentRow>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT
                item.status AS item_status,
                item.effective_from AS item_effective_from,
                item.effective_until AS item_effective_until,
                component.status AS component_status,
                component.effective_from AS component_effective_from,
                component.effective_until AS component_effective_until,
                component.billing_cadence AS billing_cadence
            FROM billing_v2_subscription_item_effective_price_components
                 component
            INNER JOIN billing_v2_subscription_items item
                ON item.id = component.subscription_item_id
            WHERE item.subscription_id = @subscription_id;
            """;
        command.Parameters.AddWithValue("@subscription_id", subscriptionId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new BillingV2EffectivePriceComponentRow(
                reader.GetString("item_status"),
                ReadUtc(reader, "item_effective_from")!.Value,
                ReadUtc(reader, "item_effective_until"),
                reader.GetString("component_status"),
                ReadUtc(reader, "component_effective_from")!.Value,
                ReadUtc(reader, "component_effective_until"),
                reader.GetString("billing_cadence")));
        }

        return rows;
    }

    /// <remarks>
    /// MariaDB rend un <c>DATETIME(6)</c> en
    /// <see cref="DateTimeKind.Unspecified"/> : sans requalification, la
    /// comparaison a <c>UtcNow</c> deviendrait une comparaison d'heure locale.
    /// </remarks>
    private static DateTime? ReadUtc(MySqlDataReader reader, string columnName)
        => reader.IsDBNull(reader.GetOrdinal(columnName))
            ? null
            : DateTime.SpecifyKind(
                reader.GetDateTime(columnName),
                DateTimeKind.Utc);
}

/// <summary>
/// Une ligne de
/// <c>billing_v2_subscription_item_effective_price_components</c>, avec la
/// fenetre de l'item qui la porte.
/// </summary>
public sealed record BillingV2EffectivePriceComponentRow(
    string ItemStatus,
    DateTime ItemEffectiveFrom,
    DateTime? ItemEffectiveUntil,
    string ComponentStatus,
    DateTime ComponentEffectiveFrom,
    DateTime? ComponentEffectiveUntil,
    string BillingCadence);

/// <summary>
/// « Cet abonnement porte-t-il encore une obligation de prelevement ? »
/// </summary>
/// <remarks>
/// <para>
/// Deux fenetres doivent etre ouvertes, pas une : celle de l'item et celle de
/// la composante. Sur un item <c>componentized</c> elles sont independantes —
/// une composante mensuelle peut etre retiree d'un item qui reste actif, et
/// c'est precisement le cas ou l'abonnement cesse d'etre recurrent sans que
/// rien ne bouge au niveau de l'item.
/// </para>
/// <para>
/// Un seul mensuel suffit. Un item qui melange une composante mensuelle et une
/// composante ponctuelle est recurrent : la ponctuelle ne rachete pas la
/// mensuelle.
/// </para>
/// </remarks>
public static class BillingV2RecurringComponentPolicy
{
    public const string ActiveStatus = "active";

    public static bool HasRecurring(
        IReadOnlyList<BillingV2EffectivePriceComponentRow> rows,
        DateTime nowUtc)
        => rows.Any(row => IsEffectiveRecurring(row, nowUtc));

    public static bool IsEffectiveRecurring(
        BillingV2EffectivePriceComponentRow row,
        DateTime nowUtc)
        => IsActive(row.ItemStatus)
           && IsWithin(row.ItemEffectiveFrom, row.ItemEffectiveUntil, nowUtc)
           && IsActive(row.ComponentStatus)
           && IsWithin(
               row.ComponentEffectiveFrom,
               row.ComponentEffectiveUntil,
               nowUtc)
           && string.Equals(
               row.BillingCadence,
               BillingV2BillingCadences.Monthly,
               StringComparison.OrdinalIgnoreCase);

    private static bool IsActive(string status)
        => string.Equals(
            status,
            ActiveStatus,
            StringComparison.OrdinalIgnoreCase);

    private static bool IsWithin(DateTime from, DateTime? until, DateTime nowUtc)
        => from <= nowUtc && (until is null || until > nowUtc);
}
