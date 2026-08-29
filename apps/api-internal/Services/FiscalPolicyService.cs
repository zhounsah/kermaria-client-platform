using System.Globalization;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;

namespace Kermaria.ApiInternal.Services;

public interface IFiscalPolicyService
{
    bool IsPersistent { get; }

    /// <summary>
    /// Charge les mentions administrees et les publie pour les projections
    /// synchrones. Appele au demarrage et apres chaque mutation.
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken);

    Task<FiscalPolicyAdminView> GetAdminViewAsync(CancellationToken cancellationToken);

    Task<FiscalPolicyMutationResponse> AddMentionAsync(
        FiscalMentionCreateRequest request,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken);

    Task<FiscalPolicyMutationResponse> DeleteScheduledMentionAsync(
        string id,
        string correlationId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Administration des mentions fiscales. Le service ne calcule aucune taxe : le
/// regime et le montant restent decides par <see cref="FiscalPolicy"/>, et seule
/// la formulation de la mention est administrable, pour un regime deja connu du
/// code.
/// </summary>
public sealed class FiscalPolicyService : IFiscalPolicyService
{
    private const int MentionMaxLength = 300;
    private const int MentionMinLength = 8;

    private sealed record RegimeDefinition(string Regime, string Label, string Description);

    // Registre ferme : un regime absent de cette liste ne peut pas etre
    // administre, quelle que soit la charge envoyee.
    private static readonly RegimeDefinition[] Regimes =
    [
        new(
            FiscalRegimes.FranchiseBase,
            "Franchise en base de TVA",
            "Regime applique tant qu'aucune TVA n'est facturee. La mention legale accompagne chaque ligne sans taxe."),
        new(
            FiscalRegimes.Standard,
            "TVA applicable",
            "Regime applique des qu'une ligne porte un taux de TVA. Le taux lui-meme vient du document, jamais de ce texte.")
    ];

    private readonly IFiscalPolicyRepository _repository;
    private readonly ILogger<FiscalPolicyService> _logger;

    public FiscalPolicyService(
        IFiscalPolicyRepository repository,
        ILogger<FiscalPolicyService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public bool IsPersistent => _repository.IsPersistent;

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            var stored = await _repository.ListAsync(cancellationToken);
            FiscalMentionDirectory.Apply(new FiscalMentionSnapshot(
                stored
                    .Where(item => Regimes.Any(regime =>
                        string.Equals(regime.Regime, item.Regime, StringComparison.Ordinal)))
                    .Select(item => new FiscalMentionVersion(
                        item.Regime,
                        item.Mention,
                        item.EffectiveFromUtc))
                    .ToArray()));
        }
        catch (Exception exception)
        {
            // Repli ferme : sans base lisible, les documents affichent la
            // mention integree au code. Jamais de texte invente, jamais de
            // mention vide.
            _logger.LogWarning(
                exception,
                "Fiscal mentions could not be loaded; falling back to built-in mentions.");
            FiscalMentionDirectory.Reset();
        }
    }

    public async Task<FiscalPolicyAdminView> GetAdminViewAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            // Une seule unite de lecture : assemblees separement, les mentions
            // et le numero de version peuvent decrire deux instants differents,
            // et l'administrateur repart avec une version qu'il n'a jamais vue.
            return BuildView(await _repository.GetSnapshotAsync(cancellationToken));
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Fiscal mentions unavailable for administration.");
            return BuildView(EmptySnapshot);
        }
    }

    private static readonly FiscalPolicyAdminSnapshot EmptySnapshot =
        new([], new Dictionary<string, int>(StringComparer.Ordinal));

    public async Task<FiscalPolicyMutationResponse> AddMentionAsync(
        FiscalMentionCreateRequest request,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var regime = Regimes.FirstOrDefault(item =>
            string.Equals(item.Regime, request.Regime, StringComparison.Ordinal));
        if (regime is null)
        {
            return Failure(
                "FISCAL_UNKNOWN_REGIME",
                "Ce regime fiscal n'appartient pas au registre autorise.",
                correlationId);
        }

        var mention = request.Mention?.Trim() ?? "";
        if (mention.Length is < MentionMinLength or > MentionMaxLength
            || mention.Any(char.IsControl)
            || mention.Contains('<')
            || mention.Contains('>'))
        {
            return Failure(
                "FISCAL_INVALID_MENTION",
                $"La mention doit faire entre {MentionMinLength} et {MentionMaxLength} caracteres, sans balise ni caractere de controle.",
                correlationId);
        }

        if (!TryParseUtc(request.EffectiveFrom, out var effectiveFrom))
        {
            return Failure(
                "FISCAL_INVALID_EFFECTIVE_DATE",
                "La date d'effet doit etre une date ISO 8601 valide.",
                correlationId);
        }

        var now = DateTime.UtcNow;
        if (effectiveFrom < now)
        {
            // Interdiction structurelle de l'antidatage : une mention ne doit
            // jamais changer ce qui a deja ete imprime sur un document emis.
            return Failure(
                "FISCAL_EFFECTIVE_DATE_IN_PAST",
                "Une mention ne peut pas prendre effet dans le passe : les documents deja emis ne doivent jamais changer.",
                correlationId);
        }

        // La version attendue est verifiee par le depot, sous le meme verrou que
        // l'insertion. La verifier ici, sur une lecture prealable, laissait deux
        // administrateurs partis du meme ecran ajouter chacun une mention sans
        // qu'aucun ne voie de conflit : la mention appliquee devenait celle a la
        // date d'effet la plus proche, et personne n'etait averti d'avoir ete
        // double sur un texte qui s'imprime sur des factures.
        FiscalMentionAddOutcome outcome;
        try
        {
            outcome = await _repository.TryAddAsync(
                new StoredFiscalMention(
                    Guid.NewGuid().ToString("D"),
                    regime.Regime,
                    mention,
                    effectiveFrom,
                    now,
                    actorUserId),
                request.ExpectedVersion,
                correlationId,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Ecriture impossible d'une mention fiscale.");
            return Failure(
                "FISCAL_STORAGE_UNAVAILABLE",
                "La mention n'a pas pu etre enregistree : rien n'a ete modifie.",
                correlationId);
        }

        if (outcome == FiscalMentionAddOutcome.VersionConflict)
        {
            return Failure(
                "FISCAL_VERSION_CONFLICT",
                "Ce regime a ete modifie par un autre administrateur. Rechargez la page.",
                correlationId);
        }

        if (outcome == FiscalMentionAddOutcome.EffectiveDateTaken)
        {
            return Failure(
                "FISCAL_EFFECTIVE_DATE_TAKEN",
                "Une version de ce regime prend deja effet a cette date exacte.",
                correlationId);
        }

        await RefreshAsync(cancellationToken);
        return new FiscalPolicyMutationResponse(
            "FISCAL_MENTION_SCHEDULED",
            "Mention enregistree. Elle s'appliquera aux documents emis a partir de sa date d'effet.",
            BuildView(await _repository.GetSnapshotAsync(cancellationToken)),
            correlationId);
    }

    public async Task<FiscalPolicyMutationResponse> DeleteScheduledMentionAsync(
        string id,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var deleted = await _repository.TryDeleteScheduledAsync(
            id,
            DateTime.UtcNow,
            cancellationToken);
        if (!deleted)
        {
            return Failure(
                "FISCAL_MENTION_NOT_CANCELLABLE",
                "Cette mention est introuvable ou a deja pris effet : elle ne peut plus etre annulee.",
                correlationId);
        }

        await RefreshAsync(cancellationToken);
        return new FiscalPolicyMutationResponse(
            "FISCAL_MENTION_CANCELLED",
            "Mention planifiee annulee.",
            BuildView(await _repository.GetSnapshotAsync(cancellationToken)),
            correlationId);
    }

    /// <param name="snapshot">
    /// Mentions et versions issues d'une meme lecture. Un regime absent de la
    /// table de versions retombe sur le nombre de mentions : cette valeur est
    /// toujours <b>inferieure ou egale</b> a la version reelle, donc un envoi
    /// fonde sur elle produit au pire un conflit, jamais une acceptation a tort.
    /// </param>
    private FiscalPolicyAdminView BuildView(FiscalPolicyAdminSnapshot snapshot)
    {
        var stored = snapshot.Mentions;
        var versions = snapshot.Versions;
        var now = DateTime.UtcNow;
        var regimes = Regimes.Select(definition =>
        {
            var versionItems = stored
                .Where(item => string.Equals(item.Regime, definition.Regime, StringComparison.Ordinal))
                .OrderBy(item => item.EffectiveFromUtc)
                .ToArray();
            var active = versionItems.LastOrDefault(item => item.EffectiveFromUtc <= now);
            var defaultMention = FiscalPolicy.DefaultMention(definition.Regime);
            return new FiscalPolicyRegimeView(
                definition.Regime,
                definition.Label,
                definition.Description,
                defaultMention,
                active?.Mention ?? defaultMention,
                active is null ? null : Iso(active.EffectiveFromUtc),
                active is null ? "code" : "database",
                versions.TryGetValue(definition.Regime, out var regimeVersion)
                    ? regimeVersion
                    : versionItems.Length,
                versionItems.Select(item => new FiscalMentionVersionItem(
                    item.Id,
                    item.Regime,
                    item.Mention,
                    Iso(item.EffectiveFromUtc),
                    Iso(item.CreatedAtUtc),
                    item.CreatedByUserId,
                    active is not null && string.Equals(item.Id, active.Id, StringComparison.Ordinal),
                    item.EffectiveFromUtc > now)).ToArray());
        }).ToArray();

        return new FiscalPolicyAdminView(regimes, IsPersistent);
    }

    private static FiscalPolicyMutationResponse Failure(
        string code,
        string message,
        string correlationId)
        => new(code, message, null, correlationId);

    private static bool TryParseUtc(string? value, out DateTime parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (!DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var candidate))
        {
            return false;
        }

        parsed = DateTime.SpecifyKind(candidate, DateTimeKind.Utc);
        return true;
    }

    private static string Iso(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            .ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
}
