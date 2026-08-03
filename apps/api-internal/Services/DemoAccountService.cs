using System.Globalization;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;

namespace Kermaria.ApiInternal.Services;

public interface IDemoAccountService
{
    bool IsPersistent { get; }

    Task<IReadOnlyList<DemoProfileSummary>> ListProfilesAsync(
        CancellationToken cancellationToken);

    Task<DemoProfileSummary> UpsertProfileAsync(
        DemoProfilePayload payload,
        CancellationToken cancellationToken);

    Task<bool> DeleteProfileAsync(
        string key,
        CancellationToken cancellationToken);

    IReadOnlyList<DemoContentTemplateSummary> GetContentTemplates();

    Task<IReadOnlyList<DemoAccountSummary>> ListAccountsAsync(
        CancellationToken cancellationToken);

    Task<DemoAccountCreatedResponse> CreateAccountAsync(
        DemoAccountCreateRequest request,
        string? createdByUserId,
        CancellationToken cancellationToken);

    Task<DemoPurgeResult> PurgeExpiredAccountsAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Balayage complet du cycle de vie (Lot 3) : revoque l'acces reel des
    /// essais echus (retrait GG_DEMO_* + desactivation AD) puis purge les
    /// comptes de demo echus. Idempotent : un essai deja revoque est ignore.
    /// </summary>
    Task<DemoLifecycleSweepResult> RunExpirationSweepAsync(
        CancellationToken cancellationToken);
}

public sealed class DemoAccountService : IDemoAccountService
{
    private const string RealScopedProvisioningMode = "real_scoped";
    private static readonly string[] ProvisioningModes =
        ["off", "mock", RealScopedProvisioningMode];
    private static readonly string[] StatusValues = ["active", "inactive"];

    private readonly IDemoProfileRepository _profiles;
    private readonly IDemoAccountRepository _accounts;
    private readonly IPortalPasswordService _passwordService;
    private readonly IDemoProvisioningService _provisioning;
    private readonly ILogger<DemoAccountService> _logger;

    public DemoAccountService(
        IDemoProfileRepository profiles,
        IDemoAccountRepository accounts,
        IPortalPasswordService passwordService,
        IDemoProvisioningService provisioning,
        ILogger<DemoAccountService> logger)
    {
        _profiles = profiles;
        _accounts = accounts;
        _passwordService = passwordService;
        _provisioning = provisioning;
        _logger = logger;
    }

    public bool IsPersistent => _profiles.IsPersistent;

    public async Task<IReadOnlyList<DemoProfileSummary>> ListProfilesAsync(
        CancellationToken cancellationToken)
    {
        var profiles = await _profiles.ListAsync(cancellationToken);
        return profiles.Select(ToSummary).ToArray();
    }

    public async Task<DemoProfileSummary> UpsertProfileAsync(
        DemoProfilePayload payload,
        CancellationToken cancellationToken)
    {
        var profile = BuildProfile(payload);
        var stored = await _profiles.UpsertAsync(profile, cancellationToken);
        return ToSummary(stored);
    }

    public async Task<bool> DeleteProfileAsync(
        string key,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeKey(key);
        return await _profiles.DeleteByKeyAsync(normalized, cancellationToken);
    }

    public IReadOnlyList<DemoContentTemplateSummary> GetContentTemplates()
        => DemoContentTemplateRegistry.Summaries();

    public Task<IReadOnlyList<DemoAccountSummary>> ListAccountsAsync(
        CancellationToken cancellationToken)
        => _accounts.ListDemoAccountsAsync(cancellationToken);

    public Task<DemoPurgeResult> PurgeExpiredAccountsAsync(
        CancellationToken cancellationToken)
        => _accounts.PurgeExpiredDemoCustomersAsync(
            DateTime.UtcNow,
            cancellationToken);

    public async Task<DemoAccountCreatedResponse> CreateAccountAsync(
        DemoAccountCreateRequest request,
        string? createdByUserId,
        CancellationToken cancellationToken)
    {
        var profileKey = NormalizeKey(request.ProfileKey);
        var displayName = RequireText(request.DisplayName, 200);
        var email = NormalizeEmail(request.Email);
        var password = request.InitialPassword;
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new PortalValidationException();
        }

        var profile = await _profiles.GetByKeyAsync(profileKey, cancellationToken)
            ?? throw new PortalDataNotFoundException();
        if (!string.Equals(profile.Status, "active", StringComparison.Ordinal))
        {
            throw new PortalValidationException();
        }

        if (await _accounts.EmailExistsAsync(email, cancellationToken))
        {
            throw new DemoConflictException(
                "DEMO_EMAIL_IN_USE",
                "Cette adresse e-mail est déjà utilisée par un compte du portail.");
        }

        var lifetimeDays = request.LifetimeDaysOverride ?? profile.LifetimeDays;
        if (lifetimeDays < 0)
        {
            throw new PortalValidationException();
        }

        var expiresAt = lifetimeDays > 0
            ? DateTime.UtcNow.AddDays(lifetimeDays)
            : (DateTime?)null;

        var customerId = Guid.NewGuid().ToString("D");
        var portalUserId = Guid.NewGuid().ToString("D");
        var externalReference = $"DEMO-{Guid.NewGuid():N}"[..24];
        // Code de groupe definitif, alloue des maintenant mais RETENU : tant que
        // le compte est en demonstration, l'export KoXo publie « CLI-DEMO » et
        // l'identite reste dans l'OU commune. La conversion se contente de
        // publier ce code, et KoXo cree alors l'OU cible. Reserver ici evite de
        // renommer la reference client a la conversion (cascade factures /
        // documents / abonnements).
        var koxoGroupReference = await ReserveGroupReferenceAsync(
            cancellationToken);
        var userDisplayName = string.IsNullOrWhiteSpace(request.UserDisplayName)
            ? displayName
            : RequireText(request.UserDisplayName, 200);

        var template = DemoContentTemplateRegistry.Find(profile.ContentTemplateKey);
        var templateServices = template?.Services ?? [];
        // Composition a la carte : si une selection est fournie, on ne retient
        // que les services du template dont le nom est coche.
        var chosenServices = request.SelectedServiceNames is { } selection
            ? templateServices
                .Where(service => selection.Contains(
                    service.Name,
                    StringComparer.OrdinalIgnoreCase))
                .ToArray()
            : templateServices;
        var services = chosenServices
            .Select(service => new DemoServiceSeed(
                service.ServiceType,
                service.Name,
                service.Description,
                service.Scope,
                DemoContentTemplateRegistry.CommercialTermsLabel))
            .ToArray();

        var passwordHash = _passwordService.HashPassword(portalUserId, password);

        var spec = new DemoAccountCreationSpec(
            customerId,
            externalReference,
            displayName,
            "professional",
            profile.Id,
            profile.Kind,
            expiresAt,
            createdByUserId,
            portalUserId,
            email,
            passwordHash,
            userDisplayName,
            services,
            koxoGroupReference);

        await _accounts.CreateDemoAccountAsync(spec, cancellationToken);

        // Essai reel (usage 2) : declenche l'acces reel cadre. Best-effort — un
        // echec de provisioning ne doit pas invalider le compte deja cree (il
        // sera rejoue). La vitrine reste totalement inerte (garde-fou du service
        // de provisioning : showcase = no-op dur).
        if (string.Equals(profile.Kind, DemoKinds.Trial, StringComparison.Ordinal))
        {
            try
            {
                var outcome = await _provisioning.ProvisionTrialAsync(
                    externalReference,
                    portalUserId,
                    profile.Kind,
                    profile.AdProvisioningMode,
                    profile.AdGroups,
                    cancellationToken);
                if (!string.Equals(
                        outcome.ResultCode,
                        "DEMO_PROVISIONING_MODE_NOT_REAL",
                        StringComparison.Ordinal))
                {
                    await _accounts.MarkTrialProvisionedAsync(
                        customerId,
                        DateTime.UtcNow,
                        cancellationToken);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(
                    exception,
                    "Demo trial provisioning failed for {CustomerReference}; account kept, provisioning will be retried.",
                    externalReference);
            }
        }

        return new DemoAccountCreatedResponse(
            externalReference,
            email,
            profile.Kind,
            expiresAt is null ? null : ToUtcIso(expiresAt.Value));
    }

    public async Task<DemoLifecycleSweepResult> RunExpirationSweepAsync(
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;

        // 0) Reprise du provisioning des essais encore vivants. A la creation,
        //    l'identite AD n'existe generalement pas encore (chaine KoXo
        //    semi-manuelle) : le provisioning ressort en PENDING_IDENTITY sans
        //    aucun groupe. Sans cette reprise, l'essai n'obtiendrait jamais son
        //    acces reel. L'operation est idempotente (une appartenance deja
        //    presente renvoie ALREADY_PRESENT) et ne redeclenche pas KoXo.
        var reprovisionedCount = 0;
        var pendingTrials = await _accounts.ListTrialsForProvisioningRetryAsync(
            nowUtc,
            cancellationToken);
        foreach (var trial in pendingTrials)
        {
            var outcome = await _provisioning.ProvisionTrialAsync(
                trial.CustomerReference,
                trial.PortalUserId,
                DemoKinds.Trial,
                RealScopedProvisioningMode,
                trial.AdGroups,
                cancellationToken,
                triggerKoxo: false);
            if (outcome.RealAccessApplied)
            {
                await _accounts.MarkTrialProvisionedAsync(
                    trial.CustomerId,
                    nowUtc,
                    cancellationToken);
                reprovisionedCount++;
                _logger.LogInformation(
                    "Demo trial {CustomerReference}: real access applied on retry ({GroupCount} group(s)).",
                    trial.CustomerReference,
                    outcome.GroupsApplied.Count);
            }
        }

        // 1) Revocation de l'acces reel des essais echus (avant purge).
        var expiredTrials = await _accounts.ListExpiredTrialsToRevokeAsync(
            nowUtc,
            cancellationToken);
        var revokedCount = 0;
        var revokeFailures = new List<string>();
        foreach (var trial in expiredTrials)
        {
            var outcome = await _provisioning.RevokeTrialAsync(
                trial.CustomerReference,
                trial.PortalUserId,
                trial.AdGroups,
                cancellationToken);
            if (outcome.Succeeded)
            {
                await _accounts.MarkTrialRevokedAsync(
                    trial.CustomerId,
                    nowUtc,
                    cancellationToken);
                revokedCount++;
            }
            else
            {
                // Non marque revoque -> retente au prochain passage.
                revokeFailures.Add(trial.CustomerReference);
            }
        }

        // 2) Purge par lot des comptes echus (identite + contenu couvert), avec
        //    garde-fou « saute si contenu metier hors cascade ».
        var purge = await _accounts.PurgeExpiredDemoCustomersAsync(
            nowUtc,
            cancellationToken);

        return new DemoLifecycleSweepResult(
            revokedCount,
            purge.PurgedCustomerCount,
            purge.SkippedCustomerReferences,
            revokeFailures,
            reprovisionedCount);
    }

    private DemoProfile BuildProfile(DemoProfilePayload payload)
    {
        var key = NormalizeKey(payload.Key);
        var label = RequireText(payload.Label, 200);
        var kind = payload.Kind?.Trim().ToLowerInvariant();
        if (!DemoKinds.IsValid(kind))
        {
            throw new PortalValidationException();
        }

        var adProvisioningMode = NormalizeMode(
            payload.AdProvisioningMode,
            ProvisioningModes,
            "off");
        var emailMode = NormalizeText(payload.EmailMode, "off");
        var bpceMode = NormalizeText(payload.BpceMode, "off");
        var paymentMode = NormalizeText(payload.PaymentMode, "off");
        var rdsSessionMode = NormalizeText(payload.RdsSessionMode, "off");
        var status = NormalizeMode(payload.Status, StatusValues, "active");
        var lifetimeDays = payload.LifetimeDays ?? 14;
        if (lifetimeDays < 0)
        {
            throw new PortalValidationException();
        }

        var adGroups = (payload.AdGroups ?? [])
            .Select(group => group?.Trim() ?? string.Empty)
            .Where(group => group.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Garde-fou : un profil vitrine reste totalement inerte cote acces reel.
        if (string.Equals(kind, DemoKinds.Showcase, StringComparison.Ordinal))
        {
            if (!string.Equals(adProvisioningMode, "off", StringComparison.Ordinal)
                || adGroups.Length > 0
                || payload.StorageQuotaGo is > 0
                || !string.Equals(rdsSessionMode, "off", StringComparison.Ordinal))
            {
                throw new PortalValidationException();
            }
        }

        return new DemoProfile(
            string.Empty,
            key,
            label,
            kind!,
            NormalizeOptionalKey(payload.ContentTemplateKey),
            emailMode,
            bpceMode,
            paymentMode,
            adProvisioningMode,
            adGroups,
            payload.StorageQuotaGo,
            rdsSessionMode,
            lifetimeDays,
            status);
    }

    private static DemoProfileSummary ToSummary(DemoProfile profile)
        => new(
            profile.Key,
            profile.Label,
            profile.Kind,
            profile.ContentTemplateKey,
            profile.LifetimeDays,
            profile.Status,
            new DemoCapabilities(
                profile.EmailMode,
                profile.BpceMode,
                profile.PaymentMode,
                profile.AdProvisioningMode,
                profile.AdGroups,
                profile.StorageQuotaGo,
                profile.RdsSessionMode));

    private static string NormalizeKey(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 64)
        {
            throw new PortalValidationException();
        }

        return normalized;
    }

    private static string? NormalizeOptionalKey(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private async Task<string> ReserveGroupReferenceAsync(
        CancellationToken cancellationToken)
        => await _accounts.TryReserveGroupReferenceAsync(cancellationToken)
            ?? throw new DemoConflictException(
                "DEMO_GROUP_REFERENCE_UNAVAILABLE",
                "Impossible de réserver un code de groupe unique pour ce compte.");

    private static string RequireText(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maxLength)
        {
            throw new PortalValidationException();
        }

        return normalized;
    }

    private static string NormalizeText(string? value, string fallback)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static string NormalizeMode(
        string? value,
        string[] allowed,
        string fallback)
    {
        var normalized = NormalizeText(value, fallback);
        if (!allowed.Contains(normalized, StringComparer.Ordinal))
        {
            throw new PortalValidationException();
        }

        return normalized;
    }

    private static string NormalizeEmail(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized)
            || normalized.Length > 254
            || !normalized.Contains('@')
            || normalized.StartsWith('@')
            || normalized.EndsWith('@'))
        {
            throw new PortalValidationException();
        }

        return normalized;
    }

    private static string ToUtcIso(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            .ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ", CultureInfo.InvariantCulture);
}
