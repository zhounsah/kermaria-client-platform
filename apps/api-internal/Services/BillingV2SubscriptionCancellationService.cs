using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public sealed record BillingV2CancellationOutcome(
    BillingV2CancellationMode Mode,
    string LocalStatus,
    string ReasonCode,
    bool ProviderActionQueued)
{
    public bool RequiresManualReview =>
        Mode is BillingV2CancellationMode.ManualReviewRequired;
}

public interface IBillingV2SubscriptionCancellationService
{
    /// <summary>
    /// Enregistre la demande de resiliation et, si un abonnement fournisseur
    /// existe, met en file le ou les gestes qui la rendront vraie.
    /// </summary>
    /// <param name="fallbackStatus">
    /// Utilise uniquement quand le snapshot contractuel n'est pas lisible
    /// (persistance mock). En persistance reelle, c'est la base qui fait foi :
    /// le statut vu par l'appelant a pu changer entre sa lecture et celle-ci.
    /// </param>
    Task<BillingV2CancellationOutcome> RequestCancellationAsync(
        string subscriptionId,
        string fallbackStatus,
        bool forceImmediate,
        string actorReference,
        CancellationToken cancellationToken);
}

/// <summary>
/// Resiliation Billing V2 : ecriture locale et demande fournisseur, dans la
/// meme transaction.
/// </summary>
/// <remarks>
/// <para>
/// L'etat local et les evenements d'outbox sont ecrits ensemble ou pas du tout.
/// C'est ce qui empeche les deux derives symetriques : un abonnement affiche
/// « en cours de resiliation » qu'aucun appel fournisseur ne suivra jamais, ou
/// un appel fournisseur parti sans trace locale.
/// </para>
/// <para>
/// Le passage a <c>cancelled</c> n'est PAS fait ici quand un fournisseur est
/// implique : il appartient au dispatcher, apres acceptation du fournisseur.
/// Voir <see cref="BillingV2CancellationPolicy"/>.
/// </para>
/// </remarks>
public sealed class BillingV2SubscriptionCancellationService
    : IBillingV2SubscriptionCancellationService
{
    private readonly SqlRuntimeConfiguration _sql;
    private readonly ILogger<BillingV2SubscriptionCancellationService> _logger;

    public BillingV2SubscriptionCancellationService(
        SqlRuntimeConfiguration sql,
        ILogger<BillingV2SubscriptionCancellationService> logger)
    {
        _sql = sql;
        _logger = logger;
    }

    public async Task<BillingV2CancellationOutcome> RequestCancellationAsync(
        string subscriptionId,
        string fallbackStatus,
        bool forceImmediate,
        string actorReference,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;

        if (!_sql.IsPersistent
            || string.IsNullOrWhiteSpace(_sql.ConnectionString))
        {
            // Persistance mock : aucune table Billing V2, donc ni composante
            // recurrente ni ancre a lire. La decision reste calculee et
            // retournee, mais rien n'est ecrit et surtout aucun appel
            // fournisseur n'est promis.
            var mockPlan = BillingV2CancellationPolicy.Resolve(
                new BillingV2CancellationContext(
                    fallbackStatus,
                    HasRecurringComponent: false,
                    StartedAtUtc: null,
                    CurrentPeriodEndsAtUtc: null,
                    RenewsAtUtc: null),
                MissingAnchor,
                forceImmediate,
                nowUtc);
            return new BillingV2CancellationOutcome(
                mockPlan.Mode,
                mockPlan.LocalStatus,
                mockPlan.ReasonCode,
                ProviderActionQueued: false);
        }

        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var snapshot = await ReadSnapshotAsync(
            connection,
            subscriptionId,
            cancellationToken);
        if (snapshot is null)
        {
            // La ligne a disparu entre la lecture de l'appelant et celle-ci.
            // On n'invente pas d'etat contractuel.
            return new BillingV2CancellationOutcome(
                BillingV2CancellationMode.ManualReviewRequired,
                fallbackStatus,
                "BILLING_V2_CANCELLATION_SUBSCRIPTION_NOT_FOUND",
                ProviderActionQueued: false);
        }

        var context = snapshot with
        {
            HasRecurringComponent =
                await BillingV2ProviderAnchorReader.HasRecurringComponentAsync(
                    connection,
                    transaction: null,
                    subscriptionId,
                    cancellationToken)
        };

        var anchor = await BillingV2ProviderAnchorReader.ResolveAsync(
            connection,
            transaction: null,
            subscriptionId,
            provider: null,
            cancellationToken);

        var plan = BillingV2CancellationPolicy.Resolve(
            context,
            anchor,
            forceImmediate,
            nowUtc);

        if (string.Equals(
                plan.ReasonCode,
                BillingV2CancellationPolicy.AlreadyTerminalReasonCode,
                StringComparison.Ordinal))
        {
            return new BillingV2CancellationOutcome(
                plan.Mode,
                plan.LocalStatus,
                plan.ReasonCode,
                ProviderActionQueued: false);
        }

        if (plan.RequiresManualReview)
        {
            // On refuse de conclure : aucune ecriture de statut, aucun appel.
            // La trace d'audit est le seul effet — c'est elle qui rend la
            // situation visible a un exploitant.
            await InsertAuditAsync(
                connection,
                transaction: null,
                subscriptionId,
                plan,
                actorReference,
                cancellationToken);

            _logger.LogError(
                "Billing V2 cancellation refused for subscription {SubscriptionId}: {ReasonCode}. Local status left unchanged ({Status}); the provider may still bill and manual review is required.",
                subscriptionId,
                plan.ReasonCode,
                context.Status);

            return new BillingV2CancellationOutcome(
                plan.Mode,
                context.Status,
                plan.ReasonCode,
                ProviderActionQueued: false);
        }

        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);

        await ApplyLocalStatusAsync(
            connection,
            transaction,
            subscriptionId,
            plan,
            cancellationToken);

        var queued = false;
        foreach (var action in plan.ProviderActions)
        {
            var payload = new BillingV2CancellationOutboxPayload(
                subscriptionId,
                anchor.Anchor!.Provider,
                anchor.Anchor.Environment,
                anchor.Anchor.ProviderSubscriptionId!,
                action.Operation,
                plan.ReasonCode);
            queued |= await EnqueueAsync(
                connection,
                transaction,
                payload,
                action.AvailableAtUtc,
                cancellationToken);
        }

        await InsertAuditAsync(
            connection,
            transaction,
            subscriptionId,
            plan,
            actorReference,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Billing V2 cancellation requested for subscription {SubscriptionId}: mode={Mode}, local_status={LocalStatus}, actions={Actions}, provider_action_queued={Queued}.",
            subscriptionId,
            plan.Mode,
            plan.LocalStatus,
            string.Join(
                ",",
                plan.ProviderActions.Select(action => action.Operation)),
            queued);

        return new BillingV2CancellationOutcome(
            plan.Mode,
            plan.LocalStatus,
            plan.ReasonCode,
            queued);
    }

    private static readonly BillingV2ProviderAnchorResolution MissingAnchor =
        new(
            BillingV2ProviderAnchorOutcome.Missing,
            null,
            null,
            BillingV2ProviderAnchorPolicy.MissingReasonCode);

    /// <remarks>
    /// Les dates du contrat sont lues ici et pas deduites du statut : c'est la
    /// seule facon de savoir si une periode payee court encore. Elles arrivent
    /// de MariaDB en <c>Unspecified</c> alors qu'elles sont stockees en UTC ;
    /// sans <c>SpecifyKind</c>, la comparaison avec <c>UtcNow</c> deriverait de
    /// deux heures en ete.
    /// </remarks>
    private static async Task<BillingV2CancellationContext?> ReadSnapshotAsync(
        MySqlConnection connection,
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT status, started_at, current_period_ends_at, renews_at
            FROM billing_v2_subscriptions
            WHERE id = @subscription_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@subscription_id", subscriptionId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new BillingV2CancellationContext(
            reader.GetString("status"),
            HasRecurringComponent: false,
            ReadUtc(reader, "started_at"),
            ReadUtc(reader, "current_period_ends_at"),
            ReadUtc(reader, "renews_at"));
    }

    private static DateTime? ReadUtc(MySqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal)
            ? null
            : DateTime.SpecifyKind(
                reader.GetDateTime(ordinal),
                DateTimeKind.Utc);
    }

    private static async Task ApplyLocalStatusAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string subscriptionId,
        BillingV2CancellationPlan plan,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        // `cancellation_requested_at` n'est pose qu'une fois : la premiere
        // demande fait foi. La reecrire a chaque appel effacerait la date que
        // le contrat oppose au client.
        command.CommandText =
            """
            UPDATE billing_v2_subscriptions
            SET status = @status,
                cancel_at_period_end = @cancel_at_period_end,
                cancellation_requested_at =
                    COALESCE(cancellation_requested_at, UTC_TIMESTAMP(6)),
                updated_at = UTC_TIMESTAMP(6)
            WHERE id = @id
              AND status NOT IN ('cancelled', 'expired');
            """;
        command.Parameters.AddWithValue("@id", subscriptionId);
        command.Parameters.AddWithValue("@status", plan.LocalStatus);
        command.Parameters.AddWithValue(
            "@cancel_at_period_end",
            plan.CancelAtPeriodEnd ? 1 : 0);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <param name="availableAtUtc">
    /// <c>null</c> = executable des maintenant. Une date future laisse
    /// l'evenement dormant dans l'outbox jusqu'au terme : le dispatcher ne lit
    /// que les evenements dont <c>available_at</c> est echu. C'est ce qui rend
    /// la resiliation PayPal au terme resistante a un redemarrage, la ou un
    /// minuteur en memoire serait perdu.
    /// </param>
    /// <returns>
    /// <c>true</c> si un NOUVEL evenement a ete cree. <c>false</c> si le meme
    /// geste etait deja en file : c'est le cas « deja en cours », qui ne doit
    /// produire aucun second appel fournisseur.
    /// </returns>
    private static async Task<bool> EnqueueAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        BillingV2CancellationOutboxPayload payload,
        DateTime? availableAtUtc,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO billing_v2_outbox_events (
                id,
                aggregate_type,
                aggregate_id,
                event_type,
                payload_text,
                idempotency_key_hash,
                status,
                retry_count,
                available_at,
                created_at
            ) VALUES (
                @id,
                @aggregate_type,
                @aggregate_id,
                @event_type,
                @payload_text,
                @idempotency_key_hash,
                'pending',
                0,
                COALESCE(@available_at, UTC_TIMESTAMP(6)),
                UTC_TIMESTAMP(6)
            )
            ON DUPLICATE KEY UPDATE
                id = id;
            """;
        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue(
            "@aggregate_type",
            BillingV2CancellationOutbox.AggregateType);
        command.Parameters.AddWithValue(
            "@aggregate_id",
            payload.SubscriptionId);
        command.Parameters.AddWithValue(
            "@event_type",
            BillingV2CancellationOutbox.EventType);
        command.Parameters.AddWithValue(
            "@payload_text",
            BillingV2CancellationOutbox.Serialize(payload));
        command.Parameters.AddWithValue(
            "@idempotency_key_hash",
            BillingV2CancellationOutbox.ComputeIdempotencyHash(payload));
        command.Parameters.AddWithValue(
            "@available_at",
            availableAtUtc is null ? DBNull.Value : availableAtUtc.Value);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static async Task InsertAuditAsync(
        MySqlConnection connection,
        MySqlTransaction? transaction,
        string subscriptionId,
        BillingV2CancellationPlan plan,
        string actorReference,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO billing_v2_audit_log (
                id, entity_type, entity_id, action,
                actor_reference, details_text, created_at
            ) VALUES (
                @id, 'billing_v2_subscription', @entity_id, @action,
                @actor_reference, @details, UTC_TIMESTAMP(6)
            );
            """;
        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("@entity_id", subscriptionId);
        command.Parameters.AddWithValue(
            "@action",
            plan.RequiresManualReview
                ? "billing_v2.subscription.cancellation_refused"
                : "billing_v2.subscription.cancellation_requested");
        command.Parameters.AddWithValue("@actor_reference", actorReference);
        command.Parameters.AddWithValue(
            "@details",
            $"mode={plan.Mode};local_status={plan.LocalStatus};"
                + $"reason={plan.ReasonCode};actions="
                + string.Join(
                    "+",
                    plan.ProviderActions.Select(action => action.Operation)));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
