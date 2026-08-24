using System.Globalization;
using System.Text;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Couples fournisseur / environnement qui existent reellement.
/// </summary>
/// <remarks>
/// Stripe ne connait pas « sandbox » ; PayPal ne connait pas « test ». Valider
/// le fournisseur et l'environnement comme deux listes independantes laisserait
/// enregistrer `stripe/sandbox` ou `paypal/test` : un rattachement accepte en
/// back-office, introuvable au moment du paiement, et donc une commande qui
/// echoue en production sans que rien ne l'ait signale avant.
///
/// Cette matrice est l'autorite unique. Le service d'administration s'en sert
/// pour valider une ecriture et pour construire la couverture affichee ; le
/// portail en tient une copie pour ne pas proposer un couple impossible, mais
/// c'est celle-ci qui tranche.
/// </remarks>
public static class BillingV2ProviderEnvironmentPolicy
{
    public static IReadOnlyDictionary<string, string[]> Matrix { get; } =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["stripe"] = ["test", "live"],
            ["paypal"] = ["sandbox", "live"]
        };

    public static IReadOnlyCollection<string> Providers => [.. Matrix.Keys];

    /// <summary>
    /// Environnements du fournisseur, ou <c>null</c> si le fournisseur lui-meme
    /// est inconnu. Les deux cas se distinguent volontairement : un fournisseur
    /// inconnu et un environnement invalide n'ont pas la meme cause.
    /// </summary>
    public static IReadOnlyList<string>? EnvironmentsFor(string? provider)
    {
        var normalized = provider?.Trim().ToLowerInvariant();
        return normalized is not null
               && Matrix.TryGetValue(normalized, out var environments)
            ? environments
            : null;
    }

    public static bool IsSupported(string? provider, string? environment)
    {
        var allowed = EnvironmentsFor(provider);
        if (allowed is null)
        {
            return false;
        }

        var normalized = environment?.Trim().ToLowerInvariant();
        return normalized is not null
               && allowed.Contains(normalized, StringComparer.Ordinal);
    }
}

public interface IBillingV2CatalogAdministrationService
{
    Task<BillingV2AdminCatalogSnapshot> GetCatalogAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<BillingV2AdminCatalogProviderCoverage>> GetProviderCoverageAsync(
        CancellationToken cancellationToken);

    Task<BillingV2AdminCatalogMutationResponse> UpdateServiceAsync(
        string serviceId,
        BillingV2AdminServicePayload payload,
        string actorReference,
        CancellationToken cancellationToken);

    Task<BillingV2AdminCatalogMutationResponse> UpdateTierAsync(
        string tierId,
        BillingV2AdminTierPayload payload,
        string actorReference,
        CancellationToken cancellationToken);

    Task<BillingV2AdminCatalogMutationResponse> PublishPriceRevisionAsync(
        BillingV2AdminPriceRevisionPayload payload,
        string actorReference,
        CancellationToken cancellationToken);

    Task<BillingV2AdminCatalogMutationResponse> DeactivatePriceAsync(
        string priceId,
        BillingV2AdminPriceDeactivationPayload payload,
        string actorReference,
        CancellationToken cancellationToken);

    Task<BillingV2AdminCatalogMutationResponse> CreatePresetAsync(
        BillingV2AdminPresetPayload payload,
        string actorReference,
        CancellationToken cancellationToken);

    Task<BillingV2AdminCatalogMutationResponse> UpdatePresetAsync(
        string presetId,
        BillingV2AdminPresetPayload payload,
        string actorReference,
        CancellationToken cancellationToken);

    Task<BillingV2AdminCatalogMutationResponse> AddPresetItemAsync(
        string presetId,
        BillingV2AdminPresetItemPayload payload,
        string actorReference,
        CancellationToken cancellationToken);

    Task<BillingV2AdminCatalogMutationResponse> UpdatePresetItemAsync(
        string presetId,
        string itemId,
        BillingV2AdminPresetItemPayload payload,
        string actorReference,
        CancellationToken cancellationToken);

    Task<BillingV2AdminCatalogMutationResponse> RemovePresetItemAsync(
        string presetId,
        string itemId,
        string actorReference,
        CancellationToken cancellationToken);

    Task<BillingV2AdminCatalogMutationResponse> CreateCommitmentAsync(
        BillingV2AdminCommitmentPayload payload,
        string actorReference,
        CancellationToken cancellationToken);

    Task<BillingV2AdminCatalogMutationResponse> UpdateCommitmentAsync(
        string commitmentId,
        BillingV2AdminCommitmentPayload payload,
        string actorReference,
        CancellationToken cancellationToken);

    Task<BillingV2AdminCatalogMutationResponse> UpsertCommitmentPaymentOptionAsync(
        string commitmentId,
        BillingV2AdminCommitmentPaymentOptionPayload payload,
        string actorReference,
        CancellationToken cancellationToken);

    Task<BillingV2AdminCatalogMutationResponse> UpsertProviderMappingAsync(
        string priceId,
        BillingV2AdminProviderMappingPayload payload,
        string actorReference,
        CancellationToken cancellationToken);
}

/// <summary>
/// Administration du catalogue Billing V2/V2.1.
/// </summary>
/// <remarks>
/// <para>
/// <b>Invariant central.</b> <c>billing_v2_service_prices</c> est versionnee et
/// immuable. Aucune methode de ce service ne fait un <c>UPDATE</c> sur
/// <c>amount_cents</c>. Une evolution tarifaire est une transaction unique qui
/// (1) ferme la fenetre courante par <c>valid_until = T</c> et (2) insere la
/// version N+1 avec <c>valid_from = T</c>. Les deux ecritures sont dans la
/// meme transaction : un instant ou le catalogue n'a pas de prix courant pour
/// une combinaison donnee est un instant ou un checkout se fait refuser.
/// </para>
/// <para>
/// <b>Recouvrement.</b> Deux fenetres actives ne peuvent pas se chevaucher pour
/// un meme <c>(service, palier, devise, cadence, declencheur)</c>. MariaDB ne
/// sait pas exprimer cette contrainte declarativement ; elle est verifiee ici,
/// dans la transaction, sur l'index pose par la migration 070. Sans ce
/// controle, le resolver tarifaire choisirait arbitrairement l'une des deux
/// lignes et deux clients identiques paieraient des montants differents.
/// </para>
/// <para>
/// <b>Pas de repli fictif.</b> Sans persistance, la lecture renvoie un
/// instantane vide marque non editable et toute mutation est refusee. Offrir
/// une administration en memoire donnerait a l'exploitant l'illusion d'avoir
/// enregistre un tarif.
/// </para>
/// </remarks>
public sealed class BillingV2CatalogAdministrationService
    : IBillingV2CatalogAdministrationService
{
    private const string DefaultCurrency = "EUR";
    private const string SourceDatabase = "mariadb";
    private const string SourceUnavailable = "unavailable";

    private static readonly string[] AllowedStatuses = ["active", "inactive"];
    private static readonly string[] AllowedCadences = ["monthly", "one_time"];
    private static readonly string[] AllowedChargeTriggers =
        ["initial_subscription", "subscription_change"];
    private static readonly string[] AllowedScopeTemplates =
        ["subscription", "primary_user", "additional_user"];
    private static readonly string[] AllowedPaymentModes = ["monthly", "upfront"];
    private static readonly IReadOnlyDictionary<string, string[]>
        ProviderEnvironments = BillingV2ProviderEnvironmentPolicy.Matrix;

    private static readonly string[] AllowedProviders =
        [.. BillingV2ProviderEnvironmentPolicy.Providers];

    // Le rail Stripe de Billing V2 construit ses lignes en `price_data` inline.
    // Aucun `price_id` externe n'est requis : exiger un mapping bloquerait des
    // offres parfaitement vendables. PayPal, lui, exige un plan preexistant.
    private static readonly Dictionary<string, bool> ProviderRequiresMapping =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["stripe"] = false,
            ["paypal"] = true
        };

    private readonly SqlRuntimeConfiguration _sql;
    private readonly ILogger<BillingV2CatalogAdministrationService> _logger;

    public BillingV2CatalogAdministrationService(
        SqlRuntimeConfiguration sql,
        ILogger<BillingV2CatalogAdministrationService> logger)
    {
        _sql = sql;
        _logger = logger;
    }

    private bool IsPersistent
        => _sql.IsPersistent && !string.IsNullOrWhiteSpace(_sql.ConnectionString);

    // ------------------------------------------------------------------
    // Lecture
    // ------------------------------------------------------------------

    public async Task<BillingV2AdminCatalogSnapshot> GetCatalogAsync(
        CancellationToken cancellationToken)
    {
        if (!IsPersistent)
        {
            return new BillingV2AdminCatalogSnapshot(
                SourceUnavailable,
                Editable: false,
                DefaultCurrency,
                Array.Empty<BillingV2AdminService>(),
                Array.Empty<BillingV2AdminPreset>(),
                Array.Empty<BillingV2AdminCommitment>());
        }

        await using var connection = await OpenAsync(cancellationToken);

        var mappings = await ReadProviderMappingsAsync(connection, cancellationToken);
        var prices = await ReadPricesAsync(connection, mappings, cancellationToken);
        var attributes = await ReadTierAttributesAsync(connection, cancellationToken);
        var tiers = await ReadTiersAsync(
            connection, prices, attributes, cancellationToken);
        var services = await ReadServicesAsync(
            connection, tiers, prices, cancellationToken);
        var presets = await ReadPresetsAsync(connection, cancellationToken);
        var commitments = await ReadCommitmentsAsync(connection, cancellationToken);

        return new BillingV2AdminCatalogSnapshot(
            SourceDatabase,
            Editable: true,
            DefaultCurrency,
            services,
            presets,
            commitments);
    }

    public async Task<IReadOnlyList<BillingV2AdminCatalogProviderCoverage>>
        GetProviderCoverageAsync(CancellationToken cancellationToken)
    {
        var snapshot = await GetCatalogAsync(cancellationToken);
        if (!snapshot.Editable)
        {
            return Array.Empty<BillingV2AdminCatalogProviderCoverage>();
        }

        var now = DateTime.UtcNow;
        var currentPrices = snapshot.Services
            .SelectMany(service => service.FlatPrices
                .Concat(service.Tiers.SelectMany(tier => tier.Prices)))
            .Where(price => price.IsCurrent(now))
            .ToList();

        var readiness = new List<BillingV2AdminCatalogProviderCoverage>();
        foreach (var (provider, environments) in ProviderEnvironments)
        {
            foreach (var environment in environments)
            {
                var mapped = currentPrices
                    .Where(price => price.ProviderMappings.Any(mapping =>
                        string.Equals(
                            mapping.Provider, provider,
                            StringComparison.OrdinalIgnoreCase)
                        && string.Equals(
                            mapping.Environment, environment,
                            StringComparison.OrdinalIgnoreCase)
                        && string.Equals(
                            mapping.Status, "active", StringComparison.Ordinal)))
                    .Select(price => price.PriceCode)
                    .ToHashSet(StringComparer.Ordinal);

                // Un environnement sans le moindre rattachement n'est pas
                // « incomplet » : il n'est simplement pas utilise. L'afficher
                // en rouge noierait le seul cas qui compte, un rail partiel.
                if (mapped.Count == 0)
                {
                    continue;
                }

                var unmapped = currentPrices
                    .Select(price => price.PriceCode)
                    .Where(code => !mapped.Contains(code))
                    .OrderBy(code => code, StringComparer.Ordinal)
                    .ToList();

                readiness.Add(new BillingV2AdminCatalogProviderCoverage(
                    provider,
                    environment,
                    ProviderRequiresMapping.TryGetValue(provider, out var required)
                        && required,
                    currentPrices.Count,
                    mapped.Count,
                    unmapped));
            }
        }

        return readiness;
    }

    // ------------------------------------------------------------------
    // Services et paliers
    // ------------------------------------------------------------------

    public async Task<BillingV2AdminCatalogMutationResponse> UpdateServiceAsync(
        string serviceId,
        BillingV2AdminServicePayload payload,
        string actorReference,
        CancellationToken cancellationToken)
    {
        RequirePersistence();
        var id = RequireIdentifier(serviceId);

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE billing_v2_services
            SET name = COALESCE(@name, name),
                description = CASE WHEN @description_set = 1
                                   THEN @description ELSE description END,
                category = CASE WHEN @category_set = 1
                                THEN @category ELSE category END,
                status = COALESCE(@status, status),
                display_order = COALESCE(@display_order, display_order),
                public_visible = COALESCE(@public_visible, public_visible),
                self_service_orderable =
                    COALESCE(@self_service_orderable, self_service_orderable),
                discount_eligible =
                    COALESCE(@discount_eligible, discount_eligible),
                mandatory_for_subscription =
                    COALESCE(@mandatory, mandatory_for_subscription),
                updated_by_reference = @actor,
                updated_at = UTC_TIMESTAMP(6)
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue(
            "@name", (object?)OptionalText(payload.Name, 160) ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@description_set", payload.Description is null ? 0 : 1);
        command.Parameters.AddWithValue(
            "@description",
            (object?)OptionalText(payload.Description, 4000) ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@category_set", payload.Category is null ? 0 : 1);
        command.Parameters.AddWithValue(
            "@category", (object?)OptionalText(payload.Category, 80) ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@status",
            (object?)OptionalEnum(payload.Status, AllowedStatuses) ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@display_order", (object?)payload.DisplayOrder ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@public_visible", (object?)ToFlag(payload.PublicVisible) ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@self_service_orderable",
            (object?)ToFlag(payload.SelfServiceOrderable) ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@discount_eligible",
            (object?)ToFlag(payload.DiscountEligible) ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@mandatory",
            (object?)ToFlag(payload.MandatoryForSubscription) ?? DBNull.Value);
        command.Parameters.AddWithValue("@actor", Truncate(actorReference, 255));

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
        {
            // MariaDB renvoie 0 aussi quand la ligne existe mais n'a pas
            // change. On distingue les deux : signaler « introuvable » sur une
            // mise a jour sans effet enverrait l'exploitant sur une fausse
            // piste.
            if (!await ExistsAsync(
                connection, "billing_v2_services", id, cancellationToken))
            {
                throw new PortalDataNotFoundException();
            }
        }

        return new BillingV2AdminCatalogMutationResponse(
            "BILLING_V2_CATALOG_SERVICE_UPDATED",
            "Service mis a jour.",
            id);
    }

    public async Task<BillingV2AdminCatalogMutationResponse> UpdateTierAsync(
        string tierId,
        BillingV2AdminTierPayload payload,
        string actorReference,
        CancellationToken cancellationToken)
    {
        RequirePersistence();
        var id = RequireIdentifier(tierId);

        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                UPDATE billing_v2_service_tiers
                SET name = COALESCE(@label, name),
                    public_label = CASE WHEN @public_label_set = 1
                                        THEN @public_label ELSE public_label END,
                    description = CASE WHEN @description_set = 1
                                       THEN @description ELSE description END,
                    numeric_value = CASE WHEN @numeric_set = 1
                                         THEN @numeric_value ELSE numeric_value END,
                    unit = CASE WHEN @unit_set = 1 THEN @unit ELSE unit END,
                    public_selectable =
                        COALESCE(@public_selectable, public_selectable),
                    status = COALESCE(@status, status),
                    display_order = COALESCE(@display_order, display_order),
                    updated_by_reference = @actor,
                    updated_at = UTC_TIMESTAMP(6)
                WHERE id = @id;
                """;
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue(
                "@label", (object?)OptionalText(payload.Label, 160) ?? DBNull.Value);
            command.Parameters.AddWithValue(
                "@public_label_set", payload.PublicLabel is null ? 0 : 1);
            command.Parameters.AddWithValue(
                "@public_label",
                (object?)OptionalText(payload.PublicLabel, 160) ?? DBNull.Value);
            command.Parameters.AddWithValue(
                "@description_set", payload.Description is null ? 0 : 1);
            command.Parameters.AddWithValue(
                "@description",
                (object?)OptionalText(payload.Description, 4000) ?? DBNull.Value);
            command.Parameters.AddWithValue(
                "@numeric_set", payload.NumericValue is null ? 0 : 1);
            command.Parameters.AddWithValue(
                "@numeric_value", (object?)payload.NumericValue ?? DBNull.Value);
            command.Parameters.AddWithValue(
                "@unit_set", payload.Unit is null ? 0 : 1);
            command.Parameters.AddWithValue(
                "@unit", (object?)OptionalText(payload.Unit, 32) ?? DBNull.Value);
            command.Parameters.AddWithValue(
                "@public_selectable",
                (object?)ToFlag(payload.PublicSelectable) ?? DBNull.Value);
            command.Parameters.AddWithValue(
                "@status",
                (object?)OptionalEnum(payload.Status, AllowedStatuses) ?? DBNull.Value);
            command.Parameters.AddWithValue(
                "@display_order", (object?)payload.DisplayOrder ?? DBNull.Value);
            command.Parameters.AddWithValue("@actor", Truncate(actorReference, 255));

            var affected = await command.ExecuteNonQueryAsync(cancellationToken);
            if (affected == 0
                && !await ExistsAsync(
                    connection, "billing_v2_service_tiers", id, cancellationToken,
                    transaction))
            {
                await transaction.RollbackAsync(cancellationToken);
                throw new PortalDataNotFoundException();
            }
        }

        // Attributs commerciaux (vCPU, RAM, stockage...). Ils ne portent aucun
        // montant : les reecrire est sans consequence financiere.
        if (payload.Attributes is { Count: > 0 })
        {
            foreach (var attribute in payload.Attributes)
            {
                var code = OptionalText(attribute.AttributeCode, 64);
                if (code is null)
                {
                    continue;
                }

                if (attribute.ValueNumeric is null
                    && OptionalText(attribute.ValueText, 255) is null)
                {
                    // La contrainte CHECK du schema refuse une valeur vide des
                    // deux cotes : on interprete cela comme une suppression.
                    await using var delete = connection.CreateCommand();
                    delete.Transaction = transaction;
                    delete.CommandText =
                        """
                        DELETE FROM billing_v2_service_tier_attributes
                        WHERE tier_id = @tier_id AND attribute_code = @code;
                        """;
                    delete.Parameters.AddWithValue("@tier_id", id);
                    delete.Parameters.AddWithValue("@code", code);
                    await delete.ExecuteNonQueryAsync(cancellationToken);
                    continue;
                }

                await using var upsert = connection.CreateCommand();
                upsert.Transaction = transaction;
                upsert.CommandText =
                    """
                    INSERT INTO billing_v2_service_tier_attributes
                        (id, tier_id, attribute_code, value_numeric, value_text, unit)
                    VALUES (@id, @tier_id, @code, @numeric, @text, @unit)
                    ON DUPLICATE KEY UPDATE
                        value_numeric = VALUES(value_numeric),
                        value_text = VALUES(value_text),
                        unit = VALUES(unit);
                    """;
                upsert.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                upsert.Parameters.AddWithValue("@tier_id", id);
                upsert.Parameters.AddWithValue("@code", code);
                upsert.Parameters.AddWithValue(
                    "@numeric", (object?)attribute.ValueNumeric ?? DBNull.Value);
                upsert.Parameters.AddWithValue(
                    "@text",
                    (object?)OptionalText(attribute.ValueText, 255) ?? DBNull.Value);
                upsert.Parameters.AddWithValue(
                    "@unit",
                    (object?)OptionalText(attribute.Unit, 32) ?? DBNull.Value);
                await upsert.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return new BillingV2AdminCatalogMutationResponse(
            "BILLING_V2_CATALOG_TIER_UPDATED",
            "Palier mis a jour.",
            id);
    }

    // ------------------------------------------------------------------
    // Revision tarifaire
    // ------------------------------------------------------------------

    public async Task<BillingV2AdminCatalogMutationResponse> PublishPriceRevisionAsync(
        BillingV2AdminPriceRevisionPayload payload,
        string actorReference,
        CancellationToken cancellationToken)
    {
        RequirePersistence();

        var serviceId = RequireIdentifier(payload.ServiceId);
        var tierId = OptionalIdentifier(payload.TierId);
        var amountCents = payload.AmountCents
            ?? throw new PortalValidationException();
        if (amountCents < 0 || amountCents > 100_000_000)
        {
            throw new PortalValidationException();
        }

        var currency = RequireCurrency(payload.Currency);
        var cadence = RequireEnum(payload.BillingCadence, AllowedCadences);
        var trigger = payload.ChargeTrigger is null
            ? "initial_subscription"
            : RequireEnum(payload.ChargeTrigger, AllowedChargeTriggers);

        if (payload.TaxRateBasisPoints is { } tax && (tax < 0 || tax > 10_000))
        {
            throw new PortalValidationException();
        }

        // `EffectiveAt` nul = maintenant. Une date passee est refusee : la
        // rendre effective retroactivement reecrirait le montant opposable a
        // des ventes deja conclues, ce que la versioning est justement la pour
        // empecher.
        var now = DateTime.UtcNow;
        var effectiveAt = payload.EffectiveAt is null
            ? now
            : DateTime.SpecifyKind(
                payload.EffectiveAt.Value.ToUniversalTime(), DateTimeKind.Utc);
        if (effectiveAt < now.AddMinutes(-1))
        {
            throw new PortalValidationException();
        }

        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);

        var (serviceCode, tierCode) = await ReadCodesAsync(
            connection, transaction, serviceId, tierId, cancellationToken);

        // Fenetre a fermer : la seule ligne active qui couvrira encore
        // `effectiveAt`. Verrouillee en ecriture pour que deux revisions
        // concurrentes ne se croisent pas.
        string? supersedesId = null;
        var nextVersion = 1;

        await using (var probe = connection.CreateCommand())
        {
            probe.Transaction = transaction;
            probe.CommandText =
                """
                SELECT id, price_version, valid_from, valid_until
                FROM billing_v2_service_prices
                WHERE service_id = @service_id
                  AND tier_id <=> @tier_id
                  AND currency = @currency
                  AND billing_cadence = @cadence
                  AND charge_trigger = @trigger
                ORDER BY price_version DESC, valid_from DESC
                FOR UPDATE;
                """;
            probe.Parameters.AddWithValue("@service_id", serviceId);
            probe.Parameters.AddWithValue(
                "@tier_id", (object?)tierId ?? DBNull.Value);
            probe.Parameters.AddWithValue("@currency", currency);
            probe.Parameters.AddWithValue("@cadence", cadence);
            probe.Parameters.AddWithValue("@trigger", trigger);

            await using var reader = await probe.ExecuteReaderAsync(
                cancellationToken);
            var first = true;
            while (await reader.ReadAsync(cancellationToken))
            {
                var version = reader.GetInt32("price_version");
                if (first)
                {
                    nextVersion = version + 1;
                    first = false;
                }

                if (supersedesId is not null)
                {
                    continue;
                }

                var validUntil = reader.IsDBNull(reader.GetOrdinal("valid_until"))
                    ? (DateTime?)null
                    : reader.GetDateTime("valid_until");
                if (validUntil is null || validUntil > effectiveAt)
                {
                    supersedesId = MariaDbIdentifierReader.ReadRequired(reader, "id");
                }
            }
        }

        if (supersedesId is not null)
        {
            await using var close = connection.CreateCommand();
            close.Transaction = transaction;
            // On ne ferme jamais une fenetre AVANT son ouverture : une revision
            // planifiee plus tot que la fenetre qu'elle remplace produirait un
            // intervalle negatif, invisible en base et fatal au resolver.
            close.CommandText =
                """
                UPDATE billing_v2_service_prices
                SET valid_until = @effective_at
                WHERE id = @id
                  AND valid_from <= @effective_at;
                """;
            close.Parameters.AddWithValue("@id", supersedesId);
            close.Parameters.AddWithValue("@effective_at", effectiveAt);
            var closed = await close.ExecuteNonQueryAsync(cancellationToken);
            if (closed == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw new PortalValidationException();
            }
        }

        var priceCode = await AllocatePriceCodeAsync(
            connection,
            transaction,
            serviceCode,
            tierCode,
            cadence,
            currency,
            trigger,
            nextVersion,
            cancellationToken);

        var priceId = Guid.NewGuid().ToString();
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT INTO billing_v2_service_prices
                    (id, service_id, tier_id, price_code, price_version,
                     amount_cents, currency, billing_cadence, charge_trigger,
                     tax_rate_basis_points, valid_from, valid_until, status,
                     created_by_reference, supersedes_price_id)
                VALUES
                    (@id, @service_id, @tier_id, @price_code, @price_version,
                     @amount_cents, @currency, @cadence, @trigger,
                     @tax, @valid_from, NULL, 'active',
                     @actor, @supersedes);
                """;
            insert.Parameters.AddWithValue("@id", priceId);
            insert.Parameters.AddWithValue("@service_id", serviceId);
            insert.Parameters.AddWithValue(
                "@tier_id", (object?)tierId ?? DBNull.Value);
            insert.Parameters.AddWithValue("@price_code", priceCode);
            insert.Parameters.AddWithValue("@price_version", nextVersion);
            insert.Parameters.AddWithValue("@amount_cents", amountCents);
            insert.Parameters.AddWithValue("@currency", currency);
            insert.Parameters.AddWithValue("@cadence", cadence);
            insert.Parameters.AddWithValue("@trigger", trigger);
            insert.Parameters.AddWithValue(
                "@tax", (object?)payload.TaxRateBasisPoints ?? DBNull.Value);
            insert.Parameters.AddWithValue("@valid_from", effectiveAt);
            insert.Parameters.AddWithValue("@actor", Truncate(actorReference, 255));
            insert.Parameters.AddWithValue(
                "@supersedes", (object?)supersedesId ?? DBNull.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        if (await HasOverlapAsync(
            connection, transaction, serviceId, tierId, currency, cadence,
            trigger, cancellationToken))
        {
            // Refus explicite plutot que correction silencieuse : un
            // recouvrement signale que le catalogue portait deja une anomalie,
            // et la resoudre a l'aveugle choisirait pour l'exploitant quel
            // tarif le client doit payer.
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogWarning(
                "Revision tarifaire refusee : recouvrement de fenetres actives "
                + "pour le service {ServiceId}.",
                serviceId);
            return new BillingV2AdminCatalogMutationResponse(
                "BILLING_V2_CATALOG_PRICE_OVERLAP",
                "Deux fenetres tarifaires actives se chevauchent pour cette "
                + "combinaison service / palier / devise / cadence.");
        }

        await transaction.CommitAsync(cancellationToken);
        return new BillingV2AdminCatalogMutationResponse(
            "BILLING_V2_CATALOG_PRICE_PUBLISHED",
            $"Version tarifaire {priceCode} publiee.",
            priceId);
    }

    public async Task<BillingV2AdminCatalogMutationResponse> DeactivatePriceAsync(
        string priceId,
        BillingV2AdminPriceDeactivationPayload payload,
        string actorReference,
        CancellationToken cancellationToken)
    {
        RequirePersistence();
        var id = RequireIdentifier(priceId);

        var now = DateTime.UtcNow;
        var effectiveAt = payload.EffectiveAt is null
            ? now
            : DateTime.SpecifyKind(
                payload.EffectiveAt.Value.ToUniversalTime(), DateTimeKind.Utc);
        if (effectiveAt < now.AddMinutes(-1))
        {
            throw new PortalValidationException();
        }

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        // Le montant n'est pas touche : une fenetre fermee reste lisible et
        // reste l'autorite des factures qu'elle a produites.
        command.CommandText =
            """
            UPDATE billing_v2_service_prices
            SET valid_until = @effective_at
            WHERE id = @id
              AND valid_from <= @effective_at
              AND (valid_until IS NULL OR valid_until > @effective_at);
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@effective_at", effectiveAt);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
        {
            if (!await ExistsAsync(
                connection, "billing_v2_service_prices", id, cancellationToken))
            {
                throw new PortalDataNotFoundException();
            }

            return new BillingV2AdminCatalogMutationResponse(
                "BILLING_V2_CATALOG_PRICE_NOT_CLOSABLE",
                "Cette version tarifaire est deja fermee ou n'a pas encore "
                + "commence.",
                id);
        }

        // L'auteur n'est pas re-persiste ici : la table ne porte pas de
        // colonne d'auteur pour cette mutation, et la tracabilite est
        // deja assuree par le journal d'audit applicatif — chaque route
        // `/internal/admin/billing-v2/catalog/*` enregistre l'action,
        // l'acteur, la cible et le code de refus. Ajouter une colonne ici
        // dupliquerait cette trace sans rien prouver de plus.
        _ = actorReference;
        return new BillingV2AdminCatalogMutationResponse(
            "BILLING_V2_CATALOG_PRICE_CLOSED",
            "Version tarifaire fermee.",
            id);
    }

    // ------------------------------------------------------------------
    // Formules
    // ------------------------------------------------------------------

    public async Task<BillingV2AdminCatalogMutationResponse> CreatePresetAsync(
        BillingV2AdminPresetPayload payload,
        string actorReference,
        CancellationToken cancellationToken)
    {
        RequirePersistence();
        var code = RequireCode(payload.Code, 96);
        var name = RequireText(payload.Name, 160);

        await using var connection = await OpenAsync(cancellationToken);
        var id = Guid.NewGuid().ToString();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO billing_v2_offer_presets
                (id, code, name, description, status, is_public, display_order)
            VALUES (@id, @code, @name, @description, @status, @is_public, @order);
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@code", code);
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue(
            "@description",
            (object?)OptionalText(payload.Description, 4000) ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@status", OptionalEnum(payload.Status, AllowedStatuses) ?? "active");
        command.Parameters.AddWithValue("@is_public", payload.IsPublic == true ? 1 : 0);
        command.Parameters.AddWithValue("@order", payload.DisplayOrder ?? 0);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (MySqlException exception) when (exception.Number == 1062)
        {
            return new BillingV2AdminCatalogMutationResponse(
                "BILLING_V2_CATALOG_PRESET_CODE_TAKEN",
                "Ce code de formule est deja utilise.");
        }

        // L'auteur n'est pas re-persiste ici : la table ne porte pas de
        // colonne d'auteur pour cette mutation, et la tracabilite est
        // deja assuree par le journal d'audit applicatif — chaque route
        // `/internal/admin/billing-v2/catalog/*` enregistre l'action,
        // l'acteur, la cible et le code de refus. Ajouter une colonne ici
        // dupliquerait cette trace sans rien prouver de plus.
        _ = actorReference;
        return new BillingV2AdminCatalogMutationResponse(
            "BILLING_V2_CATALOG_PRESET_CREATED",
            "Formule creee.",
            id);
    }

    public async Task<BillingV2AdminCatalogMutationResponse> UpdatePresetAsync(
        string presetId,
        BillingV2AdminPresetPayload payload,
        string actorReference,
        CancellationToken cancellationToken)
    {
        RequirePersistence();
        var id = RequireIdentifier(presetId);

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        // Le code n'est pas modifiable : il est l'identite publique de la
        // formule, present dans les URL `/formules/{code}` et dans les
        // souscriptions deja enregistrees.
        command.CommandText =
            """
            UPDATE billing_v2_offer_presets
            SET name = COALESCE(@name, name),
                description = CASE WHEN @description_set = 1
                                   THEN @description ELSE description END,
                status = COALESCE(@status, status),
                is_public = COALESCE(@is_public, is_public),
                display_order = COALESCE(@order, display_order),
                updated_at = UTC_TIMESTAMP(6)
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue(
            "@name", (object?)OptionalText(payload.Name, 160) ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@description_set", payload.Description is null ? 0 : 1);
        command.Parameters.AddWithValue(
            "@description",
            (object?)OptionalText(payload.Description, 4000) ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@status",
            (object?)OptionalEnum(payload.Status, AllowedStatuses) ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@is_public", (object?)ToFlag(payload.IsPublic) ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@order", (object?)payload.DisplayOrder ?? DBNull.Value);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0
            && !await ExistsAsync(
                connection, "billing_v2_offer_presets", id, cancellationToken))
        {
            throw new PortalDataNotFoundException();
        }

        // L'auteur n'est pas re-persiste ici : la table ne porte pas de
        // colonne d'auteur pour cette mutation, et la tracabilite est
        // deja assuree par le journal d'audit applicatif — chaque route
        // `/internal/admin/billing-v2/catalog/*` enregistre l'action,
        // l'acteur, la cible et le code de refus. Ajouter une colonne ici
        // dupliquerait cette trace sans rien prouver de plus.
        _ = actorReference;
        return new BillingV2AdminCatalogMutationResponse(
            "BILLING_V2_CATALOG_PRESET_UPDATED",
            "Formule mise a jour.",
            id);
    }

    public async Task<BillingV2AdminCatalogMutationResponse> AddPresetItemAsync(
        string presetId,
        BillingV2AdminPresetItemPayload payload,
        string actorReference,
        CancellationToken cancellationToken)
    {
        RequirePersistence();
        var preset = RequireIdentifier(presetId);
        var serviceId = RequireIdentifier(payload.ServiceId);
        var tierId = OptionalIdentifier(payload.TierId);
        var scope = RequireEnum(payload.ScopeTemplate, AllowedScopeTemplates);
        var quantity = payload.Quantity ?? 1;
        if (quantity is < 1 or > 1000)
        {
            throw new PortalValidationException();
        }

        await using var connection = await OpenAsync(cancellationToken);
        var id = Guid.NewGuid().ToString();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO billing_v2_preset_items
                (id, preset_id, service_id, tier_id, scope_template, quantity,
                 required_item, customer_editable, display_order)
            VALUES (@id, @preset_id, @service_id, @tier_id, @scope, @quantity,
                    @required, @editable, @order);
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@preset_id", preset);
        command.Parameters.AddWithValue("@service_id", serviceId);
        command.Parameters.AddWithValue("@tier_id", (object?)tierId ?? DBNull.Value);
        command.Parameters.AddWithValue("@scope", scope);
        command.Parameters.AddWithValue("@quantity", quantity);
        command.Parameters.AddWithValue(
            "@required", payload.RequiredItem == true ? 1 : 0);
        command.Parameters.AddWithValue(
            "@editable", payload.CustomerEditable == false ? 0 : 1);
        command.Parameters.AddWithValue("@order", payload.DisplayOrder ?? 0);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (MySqlException exception) when (exception.Number == 1452)
        {
            throw new PortalDataNotFoundException();
        }

        // L'auteur n'est pas re-persiste ici : la table ne porte pas de
        // colonne d'auteur pour cette mutation, et la tracabilite est
        // deja assuree par le journal d'audit applicatif — chaque route
        // `/internal/admin/billing-v2/catalog/*` enregistre l'action,
        // l'acteur, la cible et le code de refus. Ajouter une colonne ici
        // dupliquerait cette trace sans rien prouver de plus.
        _ = actorReference;
        return new BillingV2AdminCatalogMutationResponse(
            "BILLING_V2_CATALOG_PRESET_ITEM_ADDED",
            "Composant ajoute a la formule.",
            id);
    }

    public async Task<BillingV2AdminCatalogMutationResponse> UpdatePresetItemAsync(
        string presetId,
        string itemId,
        BillingV2AdminPresetItemPayload payload,
        string actorReference,
        CancellationToken cancellationToken)
    {
        RequirePersistence();
        var preset = RequireIdentifier(presetId);
        var item = RequireIdentifier(itemId);

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE billing_v2_preset_items
            SET service_id = COALESCE(@service_id, service_id),
                tier_id = CASE WHEN @tier_set = 1 THEN @tier_id ELSE tier_id END,
                scope_template = COALESCE(@scope, scope_template),
                quantity = COALESCE(@quantity, quantity),
                required_item = COALESCE(@required, required_item),
                customer_editable = COALESCE(@editable, customer_editable),
                display_order = COALESCE(@order, display_order)
            WHERE id = @id AND preset_id = @preset_id;
            """;
        command.Parameters.AddWithValue("@id", item);
        command.Parameters.AddWithValue("@preset_id", preset);
        command.Parameters.AddWithValue(
            "@service_id",
            (object?)OptionalIdentifier(payload.ServiceId) ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@tier_set", payload.TierId is null ? 0 : 1);
        command.Parameters.AddWithValue(
            "@tier_id",
            (object?)OptionalIdentifier(payload.TierId) ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@scope",
            (object?)OptionalEnum(payload.ScopeTemplate, AllowedScopeTemplates)
                ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@quantity", (object?)payload.Quantity ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@required", (object?)ToFlag(payload.RequiredItem) ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@editable", (object?)ToFlag(payload.CustomerEditable) ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@order", (object?)payload.DisplayOrder ?? DBNull.Value);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0
            && !await ExistsAsync(
                connection, "billing_v2_preset_items", item, cancellationToken))
        {
            throw new PortalDataNotFoundException();
        }

        // L'auteur n'est pas re-persiste ici : la table ne porte pas de
        // colonne d'auteur pour cette mutation, et la tracabilite est
        // deja assuree par le journal d'audit applicatif — chaque route
        // `/internal/admin/billing-v2/catalog/*` enregistre l'action,
        // l'acteur, la cible et le code de refus. Ajouter une colonne ici
        // dupliquerait cette trace sans rien prouver de plus.
        _ = actorReference;
        return new BillingV2AdminCatalogMutationResponse(
            "BILLING_V2_CATALOG_PRESET_ITEM_UPDATED",
            "Composant de formule mis a jour.",
            item);
    }

    public async Task<BillingV2AdminCatalogMutationResponse> RemovePresetItemAsync(
        string presetId,
        string itemId,
        string actorReference,
        CancellationToken cancellationToken)
    {
        RequirePersistence();
        var preset = RequireIdentifier(presetId);
        var item = RequireIdentifier(itemId);

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        // Un composant de formule est un modele, pas une vente : le supprimer
        // ne touche aucune souscription. Les abonnements deja crees portent
        // leurs propres items, copies a la souscription.
        command.CommandText =
            """
            DELETE FROM billing_v2_preset_items
            WHERE id = @id AND preset_id = @preset_id;
            """;
        command.Parameters.AddWithValue("@id", item);
        command.Parameters.AddWithValue("@preset_id", preset);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
        {
            throw new PortalDataNotFoundException();
        }

        // L'auteur n'est pas re-persiste ici : la table ne porte pas de
        // colonne d'auteur pour cette mutation, et la tracabilite est
        // deja assuree par le journal d'audit applicatif — chaque route
        // `/internal/admin/billing-v2/catalog/*` enregistre l'action,
        // l'acteur, la cible et le code de refus. Ajouter une colonne ici
        // dupliquerait cette trace sans rien prouver de plus.
        _ = actorReference;
        return new BillingV2AdminCatalogMutationResponse(
            "BILLING_V2_CATALOG_PRESET_ITEM_REMOVED",
            "Composant retire de la formule.",
            item);
    }

    // ------------------------------------------------------------------
    // Engagements et remises
    // ------------------------------------------------------------------

    public async Task<BillingV2AdminCatalogMutationResponse> CreateCommitmentAsync(
        BillingV2AdminCommitmentPayload payload,
        string actorReference,
        CancellationToken cancellationToken)
    {
        RequirePersistence();
        var code = RequireCode(payload.Code, 64);
        var name = RequireText(payload.Name, 160);
        var months = payload.CommitmentMonths ?? throw new PortalValidationException();
        if (months is < 1 or > 120)
        {
            throw new PortalValidationException();
        }

        ValidateDiscount(payload.DiscountBasisPoints);

        await using var connection = await OpenAsync(cancellationToken);
        var id = Guid.NewGuid().ToString();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO billing_v2_commitment_terms
                (id, code, name, commitment_months, discount_basis_points,
                 allow_monthly_payment, allow_upfront_payment, status, display_order)
            VALUES (@id, @code, @name, @months, @discount,
                    @allow_monthly, @allow_upfront, @status, @order);
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@code", code);
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@months", months);
        command.Parameters.AddWithValue(
            "@discount", (object?)payload.DiscountBasisPoints ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@allow_monthly", payload.AllowMonthlyPayment == false ? 0 : 1);
        command.Parameters.AddWithValue(
            "@allow_upfront", payload.AllowUpfrontPayment == false ? 0 : 1);
        command.Parameters.AddWithValue(
            "@status", OptionalEnum(payload.Status, AllowedStatuses) ?? "active");
        command.Parameters.AddWithValue("@order", payload.DisplayOrder ?? 0);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (MySqlException exception) when (exception.Number == 1062)
        {
            return new BillingV2AdminCatalogMutationResponse(
                "BILLING_V2_CATALOG_COMMITMENT_CODE_TAKEN",
                "Ce code d'engagement est deja utilise.");
        }

        // L'auteur n'est pas re-persiste ici : la table ne porte pas de
        // colonne d'auteur pour cette mutation, et la tracabilite est
        // deja assuree par le journal d'audit applicatif — chaque route
        // `/internal/admin/billing-v2/catalog/*` enregistre l'action,
        // l'acteur, la cible et le code de refus. Ajouter une colonne ici
        // dupliquerait cette trace sans rien prouver de plus.
        _ = actorReference;
        return new BillingV2AdminCatalogMutationResponse(
            "BILLING_V2_CATALOG_COMMITMENT_CREATED",
            "Engagement cree.",
            id);
    }

    public async Task<BillingV2AdminCatalogMutationResponse> UpdateCommitmentAsync(
        string commitmentId,
        BillingV2AdminCommitmentPayload payload,
        string actorReference,
        CancellationToken cancellationToken)
    {
        RequirePersistence();
        var id = RequireIdentifier(commitmentId);
        ValidateDiscount(payload.DiscountBasisPoints);

        if (payload.CommitmentMonths is { } months && months is < 1 or > 120)
        {
            throw new PortalValidationException();
        }

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        // `commitment_months` reste modifiable : il decrit l'offre proposee,
        // pas le contrat deja signe. Un abonnement en cours porte sa propre
        // duree, materialisee a l'activation.
        command.CommandText =
            """
            UPDATE billing_v2_commitment_terms
            SET name = COALESCE(@name, name),
                commitment_months = COALESCE(@months, commitment_months),
                discount_basis_points = CASE WHEN @discount_set = 1
                                             THEN @discount
                                             ELSE discount_basis_points END,
                allow_monthly_payment =
                    COALESCE(@allow_monthly, allow_monthly_payment),
                allow_upfront_payment =
                    COALESCE(@allow_upfront, allow_upfront_payment),
                status = COALESCE(@status, status),
                display_order = COALESCE(@order, display_order),
                updated_at = UTC_TIMESTAMP(6)
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue(
            "@name", (object?)OptionalText(payload.Name, 160) ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@months", (object?)payload.CommitmentMonths ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@discount_set", payload.DiscountBasisPoints is null ? 0 : 1);
        command.Parameters.AddWithValue(
            "@discount", (object?)payload.DiscountBasisPoints ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@allow_monthly",
            (object?)ToFlag(payload.AllowMonthlyPayment) ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@allow_upfront",
            (object?)ToFlag(payload.AllowUpfrontPayment) ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@status",
            (object?)OptionalEnum(payload.Status, AllowedStatuses) ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@order", (object?)payload.DisplayOrder ?? DBNull.Value);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0
            && !await ExistsAsync(
                connection, "billing_v2_commitment_terms", id, cancellationToken))
        {
            throw new PortalDataNotFoundException();
        }

        // L'auteur n'est pas re-persiste ici : la table ne porte pas de
        // colonne d'auteur pour cette mutation, et la tracabilite est
        // deja assuree par le journal d'audit applicatif — chaque route
        // `/internal/admin/billing-v2/catalog/*` enregistre l'action,
        // l'acteur, la cible et le code de refus. Ajouter une colonne ici
        // dupliquerait cette trace sans rien prouver de plus.
        _ = actorReference;
        return new BillingV2AdminCatalogMutationResponse(
            "BILLING_V2_CATALOG_COMMITMENT_UPDATED",
            "Engagement mis a jour.",
            id);
    }

    public async Task<BillingV2AdminCatalogMutationResponse>
        UpsertCommitmentPaymentOptionAsync(
            string commitmentId,
            BillingV2AdminCommitmentPaymentOptionPayload payload,
            string actorReference,
            CancellationToken cancellationToken)
    {
        RequirePersistence();
        var id = RequireIdentifier(commitmentId);
        var mode = RequireEnum(payload.PaymentMode, AllowedPaymentModes);
        var discount = payload.DiscountBasisPoints ?? 0;
        ValidateDiscount(discount);

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO billing_v2_commitment_payment_options
                (id, commitment_term_id, payment_mode, discount_basis_points,
                 status, display_order)
            VALUES (@id, @term_id, @mode, @discount, @status, @order)
            ON DUPLICATE KEY UPDATE
                discount_basis_points = VALUES(discount_basis_points),
                status = VALUES(status),
                display_order = VALUES(display_order),
                updated_at = UTC_TIMESTAMP(6);
            """;
        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("@term_id", id);
        command.Parameters.AddWithValue("@mode", mode);
        command.Parameters.AddWithValue("@discount", discount);
        command.Parameters.AddWithValue(
            "@status", OptionalEnum(payload.Status, AllowedStatuses) ?? "active");
        command.Parameters.AddWithValue("@order", payload.DisplayOrder ?? 0);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (MySqlException exception) when (exception.Number == 1452)
        {
            throw new PortalDataNotFoundException();
        }

        // L'auteur n'est pas re-persiste ici : la table ne porte pas de
        // colonne d'auteur pour cette mutation, et la tracabilite est
        // deja assuree par le journal d'audit applicatif — chaque route
        // `/internal/admin/billing-v2/catalog/*` enregistre l'action,
        // l'acteur, la cible et le code de refus. Ajouter une colonne ici
        // dupliquerait cette trace sans rien prouver de plus.
        _ = actorReference;
        return new BillingV2AdminCatalogMutationResponse(
            "BILLING_V2_CATALOG_PAYMENT_OPTION_SAVED",
            "Option de reglement enregistree.",
            id);
    }

    // ------------------------------------------------------------------
    // Providers
    // ------------------------------------------------------------------

    public async Task<BillingV2AdminCatalogMutationResponse> UpsertProviderMappingAsync(
        string priceId,
        BillingV2AdminProviderMappingPayload payload,
        string actorReference,
        CancellationToken cancellationToken)
    {
        RequirePersistence();
        var id = RequireIdentifier(priceId);
        var provider = RequireEnum(payload.Provider, AllowedProviders);
        var environment = RequireProviderEnvironment(
            provider, payload.Environment);

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO billing_v2_provider_price_mappings
                (id, service_price_id, provider, environment,
                 external_product_id, external_price_id, external_plan_id, status)
            VALUES (@id, @price_id, @provider, @environment,
                    @product, @price, @plan, @status)
            ON DUPLICATE KEY UPDATE
                external_product_id = VALUES(external_product_id),
                external_price_id = VALUES(external_price_id),
                external_plan_id = VALUES(external_plan_id),
                status = VALUES(status),
                updated_at = UTC_TIMESTAMP(6);
            """;
        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("@price_id", id);
        command.Parameters.AddWithValue("@provider", provider);
        command.Parameters.AddWithValue("@environment", environment);
        command.Parameters.AddWithValue(
            "@product",
            (object?)OptionalText(payload.ExternalProductId, 255) ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@price",
            (object?)OptionalText(payload.ExternalPriceId, 255) ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@plan",
            (object?)OptionalText(payload.ExternalPlanId, 255) ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@status", OptionalEnum(payload.Status, AllowedStatuses) ?? "active");

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (MySqlException exception) when (exception.Number == 1452)
        {
            throw new PortalDataNotFoundException();
        }

        // L'auteur n'est pas re-persiste ici : la table ne porte pas de
        // colonne d'auteur pour cette mutation, et la tracabilite est
        // deja assuree par le journal d'audit applicatif — chaque route
        // `/internal/admin/billing-v2/catalog/*` enregistre l'action,
        // l'acteur, la cible et le code de refus. Ajouter une colonne ici
        // dupliquerait cette trace sans rien prouver de plus.
        _ = actorReference;
        return new BillingV2AdminCatalogMutationResponse(
            "BILLING_V2_CATALOG_PROVIDER_MAPPING_SAVED",
            "Rattachement provider enregistre.",
            id);
    }

    // ------------------------------------------------------------------
    // Lecture SQL
    // ------------------------------------------------------------------

    private async Task<MySqlConnection> OpenAsync(
        CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task<Dictionary<string, List<BillingV2AdminProviderMapping>>>
        ReadProviderMappingsAsync(
            MySqlConnection connection,
            CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, List<BillingV2AdminProviderMapping>>(
            StringComparer.Ordinal);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, service_price_id, provider, environment,
                   external_product_id, external_price_id, external_plan_id, status
            FROM billing_v2_provider_price_mappings
            ORDER BY provider, environment;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var priceId = MariaDbIdentifierReader.ReadRequired(
                reader, "service_price_id");
            var mapping = new BillingV2AdminProviderMapping(
                MariaDbIdentifierReader.ReadRequired(reader, "id"),
                priceId,
                reader.GetString("provider"),
                reader.GetString("environment"),
                ReadNullableString(reader, "external_product_id"),
                ReadNullableString(reader, "external_price_id"),
                ReadNullableString(reader, "external_plan_id"),
                reader.GetString("status"));
            if (!result.TryGetValue(priceId, out var list))
            {
                list = [];
                result[priceId] = list;
            }

            list.Add(mapping);
        }

        return result;
    }

    private static async Task<List<BillingV2AdminPrice>> ReadPricesAsync(
        MySqlConnection connection,
        Dictionary<string, List<BillingV2AdminProviderMapping>> mappings,
        CancellationToken cancellationToken)
    {
        var prices = new List<BillingV2AdminPrice>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, service_id, tier_id, price_code, price_version,
                   amount_cents, currency, billing_cadence, charge_trigger,
                   tax_rate_basis_points, valid_from, valid_until, status,
                   created_by_reference, supersedes_price_id, created_at
            FROM billing_v2_service_prices
            ORDER BY service_id, tier_id, price_version DESC, valid_from DESC;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = MariaDbIdentifierReader.ReadRequired(reader, "id");
            prices.Add(new BillingV2AdminPrice(
                id,
                MariaDbIdentifierReader.ReadRequired(reader, "service_id"),
                MariaDbIdentifierReader.ReadNullable(reader, "tier_id"),
                reader.GetString("price_code"),
                reader.GetInt32("price_version"),
                reader.GetInt64("amount_cents"),
                reader.GetString("currency"),
                reader.GetString("billing_cadence"),
                reader.GetString("charge_trigger"),
                ReadNullableInt(reader, "tax_rate_basis_points"),
                ReadUtc(reader, "valid_from") ?? DateTime.UnixEpoch,
                ReadUtc(reader, "valid_until"),
                reader.GetString("status"),
                ReadNullableString(reader, "created_by_reference"),
                MariaDbIdentifierReader.ReadNullable(reader, "supersedes_price_id"),
                ReadUtc(reader, "created_at") ?? DateTime.UnixEpoch,
                mappings.TryGetValue(id, out var list)
                    ? list
                    : Array.Empty<BillingV2AdminProviderMapping>()));
        }

        return prices;
    }

    private static async Task<Dictionary<string, List<BillingV2AdminTierAttribute>>>
        ReadTierAttributesAsync(
            MySqlConnection connection,
            CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, List<BillingV2AdminTierAttribute>>(
            StringComparer.Ordinal);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT tier_id, attribute_code, value_numeric, value_text, unit
            FROM billing_v2_service_tier_attributes
            ORDER BY tier_id, attribute_code;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var tierId = MariaDbIdentifierReader.ReadRequired(reader, "tier_id");
            if (!result.TryGetValue(tierId, out var list))
            {
                list = [];
                result[tierId] = list;
            }

            list.Add(new BillingV2AdminTierAttribute(
                reader.GetString("attribute_code"),
                ReadNullableLong(reader, "value_numeric"),
                ReadNullableString(reader, "value_text"),
                ReadNullableString(reader, "unit")));
        }

        return result;
    }

    private static async Task<Dictionary<string, List<BillingV2AdminTier>>>
        ReadTiersAsync(
            MySqlConnection connection,
            List<BillingV2AdminPrice> prices,
            Dictionary<string, List<BillingV2AdminTierAttribute>> attributes,
            CancellationToken cancellationToken)
    {
        var pricesByTier = prices
            .Where(price => price.TierId is not null)
            .GroupBy(price => price.TierId!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<BillingV2AdminPrice>)group.ToList(),
                StringComparer.Ordinal);

        var result = new Dictionary<string, List<BillingV2AdminTier>>(
            StringComparer.Ordinal);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, service_id, code, name, public_label, description,
                   numeric_value, unit, public_selectable, status, display_order
            FROM billing_v2_service_tiers
            ORDER BY service_id, display_order, code;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = MariaDbIdentifierReader.ReadRequired(reader, "id");
            var serviceId = MariaDbIdentifierReader.ReadRequired(reader, "service_id");
            var tier = new BillingV2AdminTier(
                id,
                serviceId,
                reader.GetString("code"),
                reader.GetString("name"),
                ReadNullableString(reader, "public_label"),
                ReadNullableString(reader, "description"),
                ReadNullableLong(reader, "numeric_value"),
                ReadNullableString(reader, "unit"),
                reader.GetBoolean("public_selectable"),
                reader.GetString("status"),
                reader.GetInt32("display_order"),
                attributes.TryGetValue(id, out var tierAttributes)
                    ? tierAttributes
                    : Array.Empty<BillingV2AdminTierAttribute>(),
                pricesByTier.TryGetValue(id, out var tierPrices)
                    ? tierPrices
                    : Array.Empty<BillingV2AdminPrice>());

            if (!result.TryGetValue(serviceId, out var list))
            {
                list = [];
                result[serviceId] = list;
            }

            list.Add(tier);
        }

        return result;
    }

    private static async Task<IReadOnlyList<BillingV2AdminService>> ReadServicesAsync(
        MySqlConnection connection,
        Dictionary<string, List<BillingV2AdminTier>> tiers,
        List<BillingV2AdminPrice> prices,
        CancellationToken cancellationToken)
    {
        var flatByService = prices
            .Where(price => price.TierId is null)
            .GroupBy(price => price.ServiceId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<BillingV2AdminPrice>)group.ToList(),
                StringComparer.Ordinal);

        var services = new List<BillingV2AdminService>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, code, name, description, category, billing_type,
                   default_scope_type, pricing_model, mandatory_for_subscription,
                   discount_eligible, public_visible, self_service_orderable,
                   status, display_order, updated_by_reference
            FROM billing_v2_services
            ORDER BY display_order, code;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = MariaDbIdentifierReader.ReadRequired(reader, "id");
            services.Add(new BillingV2AdminService(
                id,
                reader.GetString("code"),
                reader.GetString("name"),
                ReadNullableString(reader, "description"),
                ReadNullableString(reader, "category"),
                reader.GetString("billing_type"),
                reader.GetString("default_scope_type"),
                reader.GetString("pricing_model"),
                reader.GetBoolean("mandatory_for_subscription"),
                reader.GetBoolean("discount_eligible"),
                reader.GetBoolean("public_visible"),
                reader.GetBoolean("self_service_orderable"),
                reader.GetString("status"),
                reader.GetInt32("display_order"),
                ReadNullableString(reader, "updated_by_reference"),
                tiers.TryGetValue(id, out var serviceTiers)
                    ? serviceTiers
                    : Array.Empty<BillingV2AdminTier>(),
                flatByService.TryGetValue(id, out var flat)
                    ? flat
                    : Array.Empty<BillingV2AdminPrice>()));
        }

        return services;
    }

    private static async Task<IReadOnlyList<BillingV2AdminPreset>> ReadPresetsAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        var itemsByPreset = new Dictionary<string, List<BillingV2AdminPresetItem>>(
            StringComparer.Ordinal);

        // Un seul lecteur ouvert par connexion : les items sont lus d'abord et
        // entierement materialises avant la requete des formules.
        await using (var itemCommand = connection.CreateCommand())
        {
            itemCommand.CommandText =
                """
                SELECT item.id, item.preset_id, item.service_id, service.code AS service_code,
                       item.tier_id, tier.code AS tier_code, item.scope_template,
                       item.quantity, item.required_item, item.customer_editable,
                       item.display_order
                FROM billing_v2_preset_items item
                INNER JOIN billing_v2_services service ON service.id = item.service_id
                LEFT JOIN billing_v2_service_tiers tier ON tier.id = item.tier_id
                ORDER BY item.preset_id, item.display_order, service.code;
                """;
            await using var reader = await itemCommand.ExecuteReaderAsync(
                cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var presetId = MariaDbIdentifierReader.ReadRequired(
                    reader, "preset_id");
                if (!itemsByPreset.TryGetValue(presetId, out var list))
                {
                    list = [];
                    itemsByPreset[presetId] = list;
                }

                list.Add(new BillingV2AdminPresetItem(
                    MariaDbIdentifierReader.ReadRequired(reader, "id"),
                    MariaDbIdentifierReader.ReadRequired(reader, "service_id"),
                    reader.GetString("service_code"),
                    MariaDbIdentifierReader.ReadNullable(reader, "tier_id"),
                    ReadNullableString(reader, "tier_code"),
                    reader.GetString("scope_template"),
                    reader.GetInt32("quantity"),
                    reader.GetBoolean("required_item"),
                    reader.GetBoolean("customer_editable"),
                    reader.GetInt32("display_order")));
            }
        }

        var presets = new List<BillingV2AdminPreset>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT id, code, name, description, status, is_public, display_order
                FROM billing_v2_offer_presets
                ORDER BY display_order, code;
                """;
            await using var reader = await command.ExecuteReaderAsync(
                cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = MariaDbIdentifierReader.ReadRequired(reader, "id");
                presets.Add(new BillingV2AdminPreset(
                    id,
                    reader.GetString("code"),
                    reader.GetString("name"),
                    ReadNullableString(reader, "description"),
                    reader.GetString("status"),
                    reader.GetBoolean("is_public"),
                    reader.GetInt32("display_order"),
                    itemsByPreset.TryGetValue(id, out var items)
                        ? items
                        : Array.Empty<BillingV2AdminPresetItem>()));
            }
        }

        return presets;
    }

    private static async Task<IReadOnlyList<BillingV2AdminCommitment>>
        ReadCommitmentsAsync(
            MySqlConnection connection,
            CancellationToken cancellationToken)
    {
        var optionsByTerm =
            new Dictionary<string, List<BillingV2AdminCommitmentPaymentOption>>(
                StringComparer.Ordinal);

        await using (var optionCommand = connection.CreateCommand())
        {
            optionCommand.CommandText =
                """
                SELECT id, commitment_term_id, payment_mode, discount_basis_points,
                       status, display_order
                FROM billing_v2_commitment_payment_options
                ORDER BY commitment_term_id, display_order, payment_mode;
                """;
            await using var reader = await optionCommand.ExecuteReaderAsync(
                cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var termId = MariaDbIdentifierReader.ReadRequired(
                    reader, "commitment_term_id");
                if (!optionsByTerm.TryGetValue(termId, out var list))
                {
                    list = [];
                    optionsByTerm[termId] = list;
                }

                list.Add(new BillingV2AdminCommitmentPaymentOption(
                    MariaDbIdentifierReader.ReadRequired(reader, "id"),
                    reader.GetString("payment_mode"),
                    reader.GetInt32("discount_basis_points"),
                    reader.GetString("status"),
                    reader.GetInt32("display_order")));
            }
        }

        var commitments = new List<BillingV2AdminCommitment>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT id, code, name, commitment_months, discount_basis_points,
                       allow_monthly_payment, allow_upfront_payment, status,
                       display_order
                FROM billing_v2_commitment_terms
                ORDER BY display_order, commitment_months;
                """;
            await using var reader = await command.ExecuteReaderAsync(
                cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = MariaDbIdentifierReader.ReadRequired(reader, "id");
                commitments.Add(new BillingV2AdminCommitment(
                    id,
                    reader.GetString("code"),
                    reader.GetString("name"),
                    reader.GetInt32("commitment_months"),
                    ReadNullableInt(reader, "discount_basis_points"),
                    reader.GetBoolean("allow_monthly_payment"),
                    reader.GetBoolean("allow_upfront_payment"),
                    reader.GetString("status"),
                    reader.GetInt32("display_order"),
                    optionsByTerm.TryGetValue(id, out var options)
                        ? options
                        : Array.Empty<BillingV2AdminCommitmentPaymentOption>()));
            }
        }

        return commitments;
    }

    // ------------------------------------------------------------------
    // Aides SQL
    // ------------------------------------------------------------------

    private static async Task<(string ServiceCode, string? TierCode)> ReadCodesAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string serviceId,
        string? tierId,
        CancellationToken cancellationToken)
    {
        string serviceCode;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                "SELECT code FROM billing_v2_services WHERE id = @id;";
            command.Parameters.AddWithValue("@id", serviceId);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            serviceCode = value as string ?? throw new PortalDataNotFoundException();
        }

        if (tierId is null)
        {
            return (serviceCode, null);
        }

        await using var tierCommand = connection.CreateCommand();
        tierCommand.Transaction = transaction;
        // Le palier doit appartenir au service : sans ce controle, un tarif
        // pourrait etre publie sur une combinaison que le resolver ne
        // rencontrera jamais, et le service resterait silencieusement invendable.
        tierCommand.CommandText =
            """
            SELECT code FROM billing_v2_service_tiers
            WHERE id = @id AND service_id = @service_id;
            """;
        tierCommand.Parameters.AddWithValue("@id", tierId);
        tierCommand.Parameters.AddWithValue("@service_id", serviceId);
        var tierValue = await tierCommand.ExecuteScalarAsync(cancellationToken);
        var tierCode = tierValue as string ?? throw new PortalValidationException();
        return (serviceCode, tierCode);
    }

    private static async Task<string> AllocatePriceCodeAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string serviceCode,
        string? tierCode,
        string cadence,
        string currency,
        string trigger,
        int version,
        CancellationToken cancellationToken)
    {
        // Convention reprise des migrations 048 et 069 :
        // SERVICE[-PALIER]-CADENCE-DEVISE[-CHANGE]-V{n}
        var builder = new StringBuilder(serviceCode.ToUpperInvariant());
        if (tierCode is not null)
        {
            builder.Append('-').Append(tierCode.ToUpperInvariant());
        }

        builder.Append('-').Append(cadence.ToUpperInvariant().Replace('_', '-'));
        builder.Append('-').Append(currency.ToUpperInvariant());
        if (!string.Equals(trigger, "initial_subscription", StringComparison.Ordinal))
        {
            builder.Append("-CHANGE");
        }

        var prefix = builder.ToString();
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var candidate = attempt == 0
                ? $"{prefix}-V{version.ToString(CultureInfo.InvariantCulture)}"
                : $"{prefix}-V{version.ToString(CultureInfo.InvariantCulture)}-{attempt}";
            if (candidate.Length > 96)
            {
                candidate = candidate[^96..];
            }

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                "SELECT 1 FROM billing_v2_service_prices WHERE price_code = @code;";
            command.Parameters.AddWithValue("@code", candidate);
            if (await command.ExecuteScalarAsync(cancellationToken) is null)
            {
                return candidate;
            }
        }

        throw new PortalValidationException();
    }

    private static async Task<bool> HasOverlapAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string serviceId,
        string? tierId,
        string currency,
        string cadence,
        string trigger,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM billing_v2_service_prices a
            JOIN billing_v2_service_prices b
              ON b.id <> a.id
             AND b.service_id = a.service_id
             AND b.tier_id <=> a.tier_id
             AND b.currency = a.currency
             AND b.billing_cadence = a.billing_cadence
             AND b.charge_trigger = a.charge_trigger
             AND b.status = 'active'
             AND a.valid_from < COALESCE(b.valid_until, '9999-12-31 23:59:59.999999')
             AND b.valid_from < COALESCE(a.valid_until, '9999-12-31 23:59:59.999999')
            WHERE a.status = 'active'
              AND a.service_id = @service_id
              AND a.tier_id <=> @tier_id
              AND a.currency = @currency
              AND a.billing_cadence = @cadence
              AND a.charge_trigger = @trigger;
            """;
        command.Parameters.AddWithValue("@service_id", serviceId);
        command.Parameters.AddWithValue("@tier_id", (object?)tierId ?? DBNull.Value);
        command.Parameters.AddWithValue("@currency", currency);
        command.Parameters.AddWithValue("@cadence", cadence);
        command.Parameters.AddWithValue("@trigger", trigger);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(value, CultureInfo.InvariantCulture) > 0;
    }

    private static async Task<bool> ExistsAsync(
        MySqlConnection connection,
        string table,
        string id,
        CancellationToken cancellationToken,
        MySqlTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        // `table` ne vient jamais d'une entree utilisateur : les seuls appels
        // passent une constante litterale de ce fichier.
        command.CommandText = $"SELECT 1 FROM {table} WHERE id = @id;";
        command.Parameters.AddWithValue("@id", id);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    // ------------------------------------------------------------------
    // Validation
    // ------------------------------------------------------------------

    private void RequirePersistence()
    {
        if (!IsPersistent)
        {
            // Refus explicite : sans base, il n'y a pas de catalogue a
            // administrer, et accepter la mutation ferait croire a un
            // enregistrement.
            throw new PortalValidationException();
        }
    }

    private static string RequireIdentifier(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)
            || normalized.Length > 64
            || !Guid.TryParse(normalized, out _))
        {
            throw new PortalValidationException();
        }

        return normalized;
    }

    private static string? OptionalIdentifier(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : RequireIdentifier(value);

    private static string RequireText(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maxLength)
        {
            throw new PortalValidationException();
        }

        return normalized;
    }

    private static string? OptionalText(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.Length > maxLength)
        {
            throw new PortalValidationException();
        }

        return normalized;
    }

    private static string RequireCode(string? value, int maxLength)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maxLength)
        {
            throw new PortalValidationException();
        }

        // Un code sert d'identite publique et se retrouve dans des URL : le
        // borner a un alphabet sur evite d'avoir a l'echapper partout ailleurs.
        foreach (var character in normalized)
        {
            var allowed = char.IsAsciiLetterOrDigit(character)
                || character is '-' or '_';
            if (!allowed)
            {
                throw new PortalValidationException();
            }
        }

        return normalized;
    }

    /// <summary>
    /// Un environnement n'a de sens que rapporte a son fournisseur.
    /// </summary>
    private static string RequireProviderEnvironment(
        string provider,
        string? environment)
    {
        var allowed = BillingV2ProviderEnvironmentPolicy.EnvironmentsFor(provider)
            ?? throw new PortalValidationException();

        return RequireEnum(environment, [.. allowed]);
    }

    private static string RequireEnum(string? value, string[] allowed)
        => OptionalEnum(value, allowed) ?? throw new PortalValidationException();

    private static string? OptionalEnum(string? value, string[] allowed)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return allowed.Contains(normalized, StringComparer.Ordinal)
            ? normalized
            : throw new PortalValidationException();
    }

    private static string RequireCurrency(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return DefaultCurrency;
        }

        if (normalized.Length != 3 || !normalized.All(char.IsAsciiLetterUpper))
        {
            throw new PortalValidationException();
        }

        return normalized;
    }

    private static void ValidateDiscount(int? basisPoints)
    {
        // 10 000 points de base = 100 %. Une remise superieure produirait un
        // montant negatif, que le moteur tarifaire refuserait plus loin sans
        // pouvoir expliquer pourquoi.
        if (basisPoints is { } value && value is < 0 or > 10_000)
        {
            throw new PortalValidationException();
        }
    }

    private static int? ToFlag(bool? value) => value is null ? null : value.Value ? 1 : 0;

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private static string? ReadNullableString(MySqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static int? ReadNullableInt(MySqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static long? ReadNullableLong(MySqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
    }

    private static DateTime? ReadUtc(MySqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        // La base stocke en UTC sans fuseau : sans SpecifyKind, la
        // serialisation ISO produirait un horodatage local et decalerait les
        // fenetres tarifaires de deux heures en ete.
        return DateTime.SpecifyKind(reader.GetDateTime(ordinal), DateTimeKind.Utc);
    }
}
