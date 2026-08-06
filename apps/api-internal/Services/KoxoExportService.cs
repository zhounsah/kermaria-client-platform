using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;

namespace Kermaria.ApiInternal.Services;

public sealed class KoxoValidationException : Exception
{
    public KoxoValidationException(IReadOnlyList<KoxoInvalidUser> invalidUsers)
        : base("Un ou plusieurs utilisateurs KoXo sont invalides.")
    {
        InvalidUsers = invalidUsers;
    }

    public IReadOnlyList<KoxoInvalidUser> InvalidUsers { get; }
}

public interface IKoxoExportService
{
    bool IsPersistent { get; }

    Task<KoxoExportPayload> ExportAsync(
        string source,
        string correlationId,
        string? sourceAddress,
        CancellationToken cancellationToken);

    Task<KoxoAdminDashboard> GetDashboardAsync(
        CancellationToken cancellationToken);

    Task<KoxoAdminDashboard> ValidateAndRecordAsync(
        string correlationId,
        string? sourceAddress,
        CancellationToken cancellationToken);
}

public sealed class KoxoExportService : IKoxoExportService
{
    private const int SchemaVersion = 1;
    private const int PreviewLimit = 5;

    /// <summary>OU commune hebergeant les identites de demonstration.</summary>
    public const string DemoGroupReference = "CLI-DEMO";

    private static readonly Regex EmailPattern =
        new("^[^\\s@]+@[^\\s@]+\\.[^\\s@]+$", RegexOptions.Compiled);
    private static readonly Regex IdentifierPattern =
        new("^CLI-\\d{6}$", RegexOptions.Compiled);

    private readonly IKoxoRepository _repository;
    private readonly IKoxoPendingPasswordStore _pendingPasswords;

    public KoxoExportService(
        IKoxoRepository repository,
        IKoxoPendingPasswordStore pendingPasswords)
    {
        _repository = repository;
        _pendingPasswords = pendingPasswords;
    }

    public bool IsPersistent => _repository.IsPersistent;

    public async Task<KoxoExportPayload> ExportAsync(
        string source,
        string correlationId,
        string? sourceAddress,
        CancellationToken cancellationToken)
    {
        var prepared = await PrepareAsync(
            consumePendingPasswords: true,
            cancellationToken);
        await PersistRunAsync(
            source,
            correlationId,
            sourceAddress,
            prepared,
            cancellationToken);

        if (prepared.InvalidUsers.Count > 0 || prepared.Payload is null)
        {
            throw new KoxoValidationException(prepared.InvalidUsers);
        }

        return prepared.Payload;
    }

    public async Task<KoxoAdminDashboard> GetDashboardAsync(
        CancellationToken cancellationToken)
    {
        var prepared = await PrepareAsync(
            consumePendingPasswords: false,
            cancellationToken);
        return await BuildDashboardAsync(prepared, cancellationToken);
    }

    public async Task<KoxoAdminDashboard> ValidateAndRecordAsync(
        string correlationId,
        string? sourceAddress,
        CancellationToken cancellationToken)
    {
        var prepared = await PrepareAsync(
            consumePendingPasswords: false,
            cancellationToken);
        await PersistRunAsync(
            "admin_validation",
            correlationId,
            sourceAddress,
            prepared,
            cancellationToken);
        return await BuildDashboardAsync(prepared, cancellationToken);
    }

    /// <summary>
    /// Determine l'OU cible cote KoXo, qui la cree si elle n'existe pas.
    /// </summary>
    /// <remarks>
    /// Trois cas :
    /// <list type="bullet">
    /// <item>essai en cours : <see cref="DemoGroupReference"/>, l'identite reste
    /// dans l'OU de demonstration commune et le code reserve n'est pas publie ;</item>
    /// <item>compte converti : le code reserve a la creation, ce qui fait creer
    /// l'OU definitive a KoXo sans renommer la reference client ;</item>
    /// <item>client reel ordinaire : sa reference, qui nomme deja son OU.</item>
    /// </list>
    /// C'est le seul levier de la conversion cote annuaire : l'application ne
    /// deplace aucune identite elle-meme.
    /// </remarks>
    private static string ResolveGroupeSecondaire(KoxoExportCandidate candidate)
        => candidate.IsDemo
            ? DemoGroupReference
            : candidate.KoxoGroupReference ?? candidate.CustomerReference;

    /// <param name="consumePendingPasswords">
    /// Vrai pour le seul export reel. Le tableau de bord et la validation
    /// admin passent faux : ils rejouent <see cref="PrepareAsync"/> a la
    /// demande, et consommer la un mot de passe a usage unique le ferait
    /// disparaitre avant d'atteindre KoXo — en plus de l'exposer dans
    /// l'apercu montre a l'administrateur.
    /// </param>
    private async Task<KoxoPreparedExport> PrepareAsync(
        bool consumePendingPasswords,
        CancellationToken cancellationToken)
    {
        var candidates = (await _repository.ListExportCandidatesAsync(cancellationToken))
            .OrderBy(candidate => candidate.CustomerReference, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.KoxoUniqueIdentifier, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.PortalUserId, StringComparer.Ordinal)
            .ToList();

        var invalidUsers = new List<KoxoInvalidUser>();
        var validUsers = new List<KoxoExportUser>();

        foreach (var candidate in candidates)
        {
            var fields = new List<string>();
            var mappedTitle = MapCivilite(candidate.PersonalTitle);
            if (mappedTitle is null)
            {
                fields.Add("civilite");
            }

            var surname = NormalizeRequired(candidate.Surname);
            if (surname is null)
            {
                fields.Add("nom");
            }

            var givenName = NormalizeRequired(candidate.GivenName);
            if (givenName is null)
            {
                fields.Add("prenom");
            }

            var birthDate = NormalizeBirthDate(candidate.BirthDate);
            if (birthDate is null)
            {
                fields.Add("dateNaissance");
            }

            var identifiantUnique = NormalizeRequired(candidate.KoxoUniqueIdentifier);
            if (identifiantUnique is null || !IdentifierPattern.IsMatch(identifiantUnique))
            {
                fields.Add("identifiantUnique");
            }

            var groupeSecondaire = NormalizeRequired(
                ResolveGroupeSecondaire(candidate));
            if (groupeSecondaire is null)
            {
                fields.Add("groupeSecondaire");
            }

            var email = NormalizeRequired(candidate.Email)?.ToLowerInvariant();
            if (email is null || !EmailPattern.IsMatch(email))
            {
                fields.Add("email");
            }

            if (fields.Count > 0)
            {
                invalidUsers.Add(CreateInvalidUser(candidate, fields));
                continue;
            }

            validUsers.Add(new KoxoExportUser(
                mappedTitle!,
                surname!,
                givenName!,
                birthDate!,
                identifiantUnique!,
                groupeSecondaire!,
                email!,
                consumePendingPasswords
                    ? _pendingPasswords.Consume(candidate.PortalUserId)
                    : null));
        }

        foreach (var duplicate in candidates
            .Where(candidate =>
                NormalizeRequired(candidate.KoxoUniqueIdentifier) is string identifier
                && IdentifierPattern.IsMatch(identifier))
            .GroupBy(
                candidate => NormalizeRequired(candidate.KoxoUniqueIdentifier)!,
                StringComparer.Ordinal)
            .Where(group => group.Count() > 1))
        {
            foreach (var candidate in duplicate)
            {
                invalidUsers.Add(CreateInvalidUser(candidate, ["identifiantUnique"]));
            }
        }

        invalidUsers = invalidUsers
            .GroupBy(
                item => $"{item.PortalUserId}|{item.IdentifiantUnique ?? string.Empty}",
                StringComparer.Ordinal)
            .Select(group => new KoxoInvalidUser(
                group.First().IdentifiantUnique,
                group.First().PortalUserId,
                group.SelectMany(item => item.Fields)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(field => field, StringComparer.Ordinal)
                    .ToArray()))
            .OrderBy(item => item.IdentifiantUnique ?? item.PortalUserId, StringComparer.Ordinal)
            .ToList();

        var exportUsers = validUsers
            .Where(user =>
                invalidUsers.All(invalid =>
                    !string.Equals(
                        invalid.IdentifiantUnique,
                        user.IdentifiantUnique,
                        StringComparison.Ordinal)))
            .ToList();

        var generatedAtUtc = DateTime.UtcNow;
        var payload = invalidUsers.Count == 0
            ? new KoxoExportPayload(
                SchemaVersion,
                generatedAtUtc.ToString("O"),
                exportUsers.Count,
                exportUsers)
            : null;

        return new KoxoPreparedExport(
            candidates.Count,
            invalidUsers,
            payload,
            generatedAtUtc);
    }

    private async Task<KoxoAdminDashboard> BuildDashboardAsync(
        KoxoPreparedExport prepared,
        CancellationToken cancellationToken)
    {
        var lastRun = await _repository.GetLatestRunAsync(cancellationToken);
        var lastApiRun = await _repository.GetLatestRunBySourceAsync(
            "api",
            cancellationToken);
        var preview = prepared.Payload is null
            ? null
            : prepared.Payload with
            {
                Users = prepared.Payload.Users.Take(PreviewLimit).ToArray(),
                UserCount = prepared.Payload.UserCount
            };

        return new KoxoAdminDashboard(
            prepared.Payload?.UserCount ?? Math.Max(0, prepared.CandidateCount - prepared.InvalidUsers.Count),
            prepared.InvalidUsers.Count,
            lastApiRun?.CreatedAt,
            lastRun?.Status,
            SchemaVersion,
            preview,
            prepared.InvalidUsers,
            lastRun);
    }

    private async Task PersistRunAsync(
        string source,
        string correlationId,
        string? sourceAddress,
        KoxoPreparedExport prepared,
        CancellationToken cancellationToken)
    {
        var status = prepared.InvalidUsers.Count == 0 ? "validated" : "validation_failed";
        var summaryMessage = prepared.InvalidUsers.Count == 0
            ? $"{prepared.Payload?.UserCount ?? 0} utilisateur(s) KoXo validé(s)."
            : $"{prepared.InvalidUsers.Count} utilisateur(s) KoXo invalide(s).";
        var previewPayload = prepared.Payload is null
            ? null
            : prepared.Payload with
            {
                Users = prepared.Payload.Users.Take(PreviewLimit).ToArray()
            };

        await _repository.InsertRunAsync(
            new KoxoRunInsert(
                Guid.NewGuid().ToString("D"),
                source,
                status,
                prepared.Payload is null ? null : SchemaVersion,
                prepared.Payload?.UserCount ?? 0,
                prepared.InvalidUsers.Count,
                correlationId,
                sourceAddress,
                summaryMessage,
                prepared.Payload is null ? null : prepared.GeneratedAtUtc,
                previewPayload is null ? null : JsonSerializer.Serialize(previewPayload),
                prepared.InvalidUsers.Count == 0
                    ? null
                    : JsonSerializer.Serialize(prepared.InvalidUsers)),
            cancellationToken);
    }

    private static KoxoInvalidUser CreateInvalidUser(
        KoxoExportCandidate candidate,
        IReadOnlyList<string> fields)
        => new(
            NormalizeRequired(candidate.KoxoUniqueIdentifier),
            candidate.PortalUserId,
            fields);

    private static string? MapCivilite(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            "madame" => "Mme",
            "monsieur" => "M.",
            _ => null
        };

    private static string? NormalizeRequired(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string? NormalizeBirthDate(string? value)
    {
        var normalized = NormalizeRequired(value);
        if (normalized is null)
        {
            return null;
        }

        return DateOnly.TryParseExact(
                normalized,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var birthDate)
            ? birthDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;
    }

    private sealed record KoxoPreparedExport(
        int CandidateCount,
        IReadOnlyList<KoxoInvalidUser> InvalidUsers,
        KoxoExportPayload? Payload,
        DateTime GeneratedAtUtc);
}
