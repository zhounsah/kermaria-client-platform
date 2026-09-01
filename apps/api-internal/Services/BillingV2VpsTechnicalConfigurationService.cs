using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public sealed record BillingV2VpsTechnicalConfigurationInput(
    string? ServiceCode,
    string? TierCode,
    string? Hostname,
    string? OperatingSystem,
    string? Usage,
    string? ManagementMode,
    string? InternetExposure,
    string? Comment,
    string? IdempotencyKey);

public sealed record BillingV2VpsTechnicalConfigurationResult(
    string ConfigurationId,
    string TechnicalStatus,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    BillingV2PublicQuote Quote,
    string CorrelationId);

public interface IBillingV2VpsTechnicalConfigurationService
{
    Task<BillingV2VpsTechnicalConfigurationResult> CreateAndQuoteAsync(
        PortalSessionContext session,
        BillingV2VpsTechnicalConfigurationInput input,
        string correlationId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Etape durable du tunnel VPS avant checkout. Elle conserve seulement une
/// configuration technique non secrete, puis appelle le devis Billing V2.
/// Aucune souscription, aucun BillingEvent, PaymentAttempt, provider outbox ou
/// action de provisioning ne peut etre cree par ce service.
/// </summary>
public sealed class BillingV2VpsTechnicalConfigurationService
    : IBillingV2VpsTechnicalConfigurationService
{
    private const string Draft = "draft";
    private static readonly HashSet<string> VpsServiceCodes =
        new(StringComparer.Ordinal) { "VPS-LOCAL", "VPS-CLOUD" };
    private static readonly HashSet<string> InternetExposureValues =
        new(StringComparer.Ordinal) { "yes", "no", "to_confirm" };
    private static readonly string[] RequiredTables =
    [
        "billing_v2_vps_technical_requests",
        "billing_v2_vps_technical_request_revisions"
    ];

    private readonly SqlRuntimeConfiguration _sql;
    private readonly IBillingV2PublicCatalogService _catalog;
    // Le service est scoped ; le repli Development doit donc etre partage pour
    // conserver une demande lors d'un POST rejoue dans une nouvelle requete.
    private static readonly ConcurrentDictionary<string, StoredRequest> Mock = new(
        StringComparer.Ordinal);

    public BillingV2VpsTechnicalConfigurationService(
        SqlRuntimeConfiguration sql,
        IBillingV2PublicCatalogService catalog)
    {
        _sql = sql;
        _catalog = catalog;
    }

    public async Task<BillingV2VpsTechnicalConfigurationResult> CreateAndQuoteAsync(
        PortalSessionContext session,
        BillingV2VpsTechnicalConfigurationInput input,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var normalized = Normalize(input);
        var selection = await RevalidateSelectionAsync(normalized, cancellationToken);
        var fingerprint = Hash(string.Join("|", selection.Canonical(), normalized.Canonical()));
        var now = DateTime.UtcNow;
        var stored = _sql.IsPersistent
            ? await PersistMariaDbAsync(session, normalized, selection, fingerprint, now, cancellationToken)
            : PersistMock(session, normalized, selection, fingerprint, now);

        // Le devis est deliberement demande apres la persistence. Il reste
        // une projection : il ne fige aucun prix et ne lance aucun checkout.
        var quote = await _catalog.QuoteAsync(selection, cancellationToken);
        return new BillingV2VpsTechnicalConfigurationResult(
            stored.Id,
            Draft,
            stored.CreatedAt,
            stored.UpdatedAt,
            quote,
            correlationId);
    }

    private async Task<BillingV2PublicSelection> RevalidateSelectionAsync(
        NormalizedInput input,
        CancellationToken cancellationToken)
    {
        var catalog = await _catalog.GetCatalogAsync(cancellationToken);
        var service = catalog.Services.FirstOrDefault(candidate =>
            string.Equals(candidate.Code, input.ServiceCode, StringComparison.Ordinal));
        var tier = service?.Tiers.FirstOrDefault(candidate =>
            string.Equals(candidate.Code, input.TierCode, StringComparison.Ordinal));

        if (service is null
            || tier is null
            || !VpsServiceCodes.Contains(service.Code)
            || !service.PublicVisible
            || !service.SelfServiceOrderable
            || !tier.PublicSelectable
            || !service.ComponentsFor(tier).Any(component =>
                component.AppliesToInitialSubscription))
        {
            throw new InvalidOperationException("BILLING_V2_VPS_SELECTION_UNAVAILABLE");
        }

        return new BillingV2PublicSelection(
            PresetCode: null,
            CommitmentCode: null,
            PaymentMode: BillingV2PaymentModes.Monthly,
            StoragePersonalTierCode: string.Empty,
            BackupPersonal: false,
            StorageSharedTierCode: null,
            BackupShared: false,
            VpnTierCode: null,
            RemoteDesktop: false,
            AdditionalUsers: 0,
            SupportPlus: false,
            Components: [new BillingV2PublicSelectionComponent(
                service.Code,
                tier.Code,
                1)]);
    }

    private async Task<StoredRequest> PersistMariaDbAsync(
        PortalSessionContext session,
        NormalizedInput input,
        BillingV2PublicSelection selection,
        string fingerprint,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_sql.ConnectionString))
        {
            throw new InvalidOperationException("BILLING_V2_VPS_CONFIGURATION_STORAGE_UNAVAILABLE");
        }

        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        if (!await SchemaIsReadyAsync(connection, cancellationToken))
        {
            throw new InvalidOperationException("BILLING_V2_VPS_CONFIGURATION_SCHEMA_UNAVAILABLE");
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var existing = await ReadByIdempotencyKeyAsync(
            connection, transaction, session.CustomerId, input.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(existing.Fingerprint),
                    Encoding.UTF8.GetBytes(fingerprint)))
            {
                throw new InvalidOperationException("BILLING_V2_VPS_CONFIGURATION_IDEMPOTENCY_CONFLICT");
            }

            await transaction.CommitAsync(cancellationToken);
            return existing;
        }

        var id = Guid.NewGuid().ToString("D");
        var configurationHash = Hash(input.Canonical());
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO billing_v2_vps_technical_requests
                    (id, customer_id, requested_by_user_id, service_code, tier_code,
                     selection_canonical, selection_fingerprint, technical_status,
                     current_revision, configuration_hash, idempotency_key,
                     request_fingerprint_hash, created_at, updated_at)
                VALUES
                    (@id, @customer_id, @user_id, @service_code, @tier_code,
                     @selection_canonical, @selection_fingerprint, 'draft',
                     1, @configuration_hash, @idempotency_key,
                     @request_fingerprint_hash, @now, @now);
                """;
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@customer_id", session.CustomerId);
            command.Parameters.AddWithValue("@user_id", session.UserId);
            command.Parameters.AddWithValue("@service_code", input.ServiceCode);
            command.Parameters.AddWithValue("@tier_code", input.TierCode);
            command.Parameters.AddWithValue("@selection_canonical", selection.Canonical());
            command.Parameters.AddWithValue("@selection_fingerprint", BillingV2CheckoutSelectionFingerprint.ForSelection(selection.Canonical()));
            command.Parameters.AddWithValue("@configuration_hash", configurationHash);
            command.Parameters.AddWithValue("@idempotency_key", input.IdempotencyKey);
            command.Parameters.AddWithValue("@request_fingerprint_hash", fingerprint);
            command.Parameters.AddWithValue("@now", now);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO billing_v2_vps_technical_request_revisions
                    (id, technical_request_id, revision_number, hostname,
                     operating_system, usage_description, management_mode,
                     internet_exposure, comment_text, configuration_hash,
                     selection_fingerprint, created_by_user_id, created_at)
                VALUES
                    (@id, @technical_request_id, 1, @hostname,
                     @operating_system, @usage_description, @management_mode,
                     @internet_exposure, @comment_text, @configuration_hash,
                     @selection_fingerprint, @created_by_user_id, @created_at);
                """;
            command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
            command.Parameters.AddWithValue("@technical_request_id", id);
            command.Parameters.AddWithValue("@hostname", input.Hostname);
            command.Parameters.AddWithValue("@operating_system", input.OperatingSystem);
            command.Parameters.AddWithValue("@usage_description", input.Usage);
            command.Parameters.AddWithValue("@management_mode", input.ManagementMode);
            command.Parameters.AddWithValue("@internet_exposure", input.InternetExposure);
            command.Parameters.AddWithValue("@comment_text", input.Comment);
            command.Parameters.AddWithValue("@configuration_hash", configurationHash);
            command.Parameters.AddWithValue("@selection_fingerprint", BillingV2CheckoutSelectionFingerprint.ForSelection(selection.Canonical()));
            command.Parameters.AddWithValue("@created_by_user_id", session.UserId);
            command.Parameters.AddWithValue("@created_at", now);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new StoredRequest(id, fingerprint, now, now);
    }

    private StoredRequest PersistMock(
        PortalSessionContext session,
        NormalizedInput input,
        BillingV2PublicSelection selection,
        string fingerprint,
        DateTime now)
    {
        var key = $"{session.CustomerId}|{input.IdempotencyKey}";
        var candidate = new StoredRequest(Guid.NewGuid().ToString("D"), fingerprint, now, now);
        var stored = Mock.GetOrAdd(key, candidate);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(stored.Fingerprint),
                Encoding.UTF8.GetBytes(fingerprint)))
        {
            throw new InvalidOperationException("BILLING_V2_VPS_CONFIGURATION_IDEMPOTENCY_CONFLICT");
        }

        return stored;
    }

    private static async Task<StoredRequest?> ReadByIdempotencyKeyAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string customerId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT id, request_fingerprint_hash, created_at, updated_at
            FROM billing_v2_vps_technical_requests
            WHERE customer_id = @customer_id
              AND idempotency_key = @idempotency_key
            FOR UPDATE;
            """;
        command.Parameters.AddWithValue("@customer_id", customerId);
        command.Parameters.AddWithValue("@idempotency_key", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new StoredRequest(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            reader.GetString("request_fingerprint_hash"),
            reader.GetDateTime("created_at"),
            reader.GetDateTime("updated_at"));
    }

    private static async Task<bool> SchemaIsReadyAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_schema = DATABASE()
              AND table_name IN (
                  'billing_v2_vps_technical_requests',
                  'billing_v2_vps_technical_request_revisions');
            """;
        var count = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(count) == RequiredTables.Length;
    }

    private static NormalizedInput Normalize(BillingV2VpsTechnicalConfigurationInput input)
    {
        var normalized = new NormalizedInput(
            Trim(input.ServiceCode), Trim(input.TierCode), Trim(input.Hostname),
            Trim(input.OperatingSystem), Trim(input.Usage), Trim(input.ManagementMode),
            Trim(input.InternetExposure).ToLowerInvariant(), Trim(input.Comment),
            Trim(input.IdempotencyKey));
        if (string.IsNullOrWhiteSpace(normalized.ServiceCode)
            || string.IsNullOrWhiteSpace(normalized.TierCode)
            || normalized.ServiceCode.Length > 64
            || normalized.TierCode.Length > 64
            || normalized.Hostname.Length is < 1 or > 253
            || normalized.OperatingSystem.Length is < 1 or > 120
            || normalized.Usage.Length is < 1 or > 1000
            || normalized.ManagementMode.Length is < 1 or > 120
            || normalized.Comment.Length > 1000
            || normalized.IdempotencyKey.Length is < 1 or > 128
            || !InternetExposureValues.Contains(normalized.InternetExposure)
            || ContainsSecret(normalized))
        {
            throw new PortalValidationException();
        }
        return normalized;
    }

    private static bool ContainsSecret(NormalizedInput input)
        => new[] { input.Hostname, input.OperatingSystem, input.Usage, input.ManagementMode, input.Comment }
            .Any(value => value.Contains("-----BEGIN", StringComparison.OrdinalIgnoreCase)
                || value.Contains("mot de passe", StringComparison.OrdinalIgnoreCase)
                || value.Contains("password", StringComparison.OrdinalIgnoreCase)
                || value.Contains("private key", StringComparison.OrdinalIgnoreCase)
                || value.Contains("clé privée", StringComparison.OrdinalIgnoreCase)
                || value.Contains("api key", StringComparison.OrdinalIgnoreCase)
                || value.Contains("api_key", StringComparison.OrdinalIgnoreCase)
                || value.Contains("token=", StringComparison.OrdinalIgnoreCase));

    private static string Trim(string? value) => value?.Trim() ?? string.Empty;
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed record StoredRequest(string Id, string Fingerprint, DateTime CreatedAt, DateTime UpdatedAt);
    private sealed record NormalizedInput(
        string ServiceCode, string TierCode, string Hostname, string OperatingSystem,
        string Usage, string ManagementMode, string InternetExposure, string Comment,
        string IdempotencyKey)
    {
        public string Canonical() => string.Join("|", Hostname, OperatingSystem, Usage, ManagementMode, InternetExposure, Comment);
    }
}
