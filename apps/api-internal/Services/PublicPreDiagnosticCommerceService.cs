using System.Text.Json;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Reconstruit une selection Billing V2 depuis les reponses, jamais depuis une
/// selection envoyee par le navigateur. Les regles metier viennent uniquement
/// de la revision v2 publiee et le catalogue reste l'autorite des paliers.
/// </summary>
public static class PublicPreDiagnosticCommerceService
{
    public static bool TryRecommend(JsonElement? configuration, string? context,
        IReadOnlyDictionary<string, string>? healthAnswers,
        IReadOnlyDictionary<string, string>? commercialAnswers,
        BillingV2PublicCatalogSnapshot catalog, out DiagnosticCommerceResult result)
    {
        result = DiagnosticCommerceResult.HumanReview("Aucune formule standard ne represente ce besoin.");
        if (configuration is null) return TryRecommendLegacy(context, healthAnswers, commercialAnswers, catalog, out result);
        if (configuration is not { ValueKind: JsonValueKind.Object } json
            || !json.TryGetProperty("schemaVersion", out var version) || version.GetInt32() != 2
            || !PublicPreDiagnosticService.TryEvaluate(healthAnswers, configuration, out _)) return false;
        PreDiagnosticConfigurationModel? model;
        try { model = json.Deserialize<PreDiagnosticConfigurationModel>(DiagnosticConfigurationRegistry.SerializerOptions); }
        catch (JsonException) { return false; }
        if (model?.Commerce is null || healthAnswers is null || commercialAnswers is null
            || !healthAnswers.TryGetValue("profile", out var profile) || string.IsNullOrWhiteSpace(context)) return false;
        var configuredContext = (model.Contexts ?? []).FirstOrDefault(item => item?.Id == context && item.Active);
        if (configuredContext is null || !configuredContext.AllowsSelfService)
            return true;
        var commerce = model.Commerce;
        var questions = (commerce.Questions ?? []).Where(question => question is not null && question.Active
            && (question.Profiles ?? []).Contains(profile) && (question.Contexts ?? []).Contains(context)).ToArray();
        if (questions.Any(question => !commercialAnswers.TryGetValue(question.Id!, out var answer)
            || !(question.Options ?? []).Any(option => option is not null && option.Active && option.Value == answer
                && (option.Profiles ?? []).Contains(profile) && (option.Contexts ?? []).Contains(context)))) return false;
        var intent = commercialAnswers.GetValueOrDefault("commercialIntent");
        var scope = commercialAnswers.GetValueOrDefault("commercialScope");
        if (string.IsNullOrWhiteSpace(intent) || string.IsNullOrWhiteSpace(scope)
            || (commerce.HumanReviewIntents ?? []).Contains(intent)
            || !(commerce.CompatibleScopes ?? []).Contains(scope)) return true;
        var storage = ReadBounded(commercialAnswers.GetValueOrDefault("commercialStorage"), commerce.MinimumStorageGb, commerce.MaximumStorageGb);
        var organisation = profile != "individual";
        var users = organisation ? ReadBounded(commercialAnswers.GetValueOrDefault("commercialUsers"), commerce.MinimumUsers, commerce.MaximumUsers) : 1;
        if (storage is null || users is null || users - 1 > BillingV2PublicCatalogCodes.MaxAdditionalUsers
            || (organisation && commerce.MaximumSites <= 1 && commercialAnswers.GetValueOrDefault("commercialSites") != "one")) return true;
        var commercialProfile = (commerce.Profiles ?? []).FirstOrDefault(item => item is not null && item.Active
            && (item.Intents ?? []).Contains(intent) && (item.Scopes ?? []).Contains(scope));
        var binding = commercialProfile is null ? null : (commerce.CatalogBindings ?? []).FirstOrDefault(item => item?.ProfileId == commercialProfile.Id);
        if (binding is null) return true;
        var storageService = catalog.Services.FirstOrDefault(item => item.Code == binding.StorageServiceCode && item.PublicVisible && item.SelfServiceOrderable);
        var tier = storageService?.Tiers.Where(item => item.PublicSelectable && item.NumericValue is not null && item.NumericValue >= storage)
            .OrderBy(item => item.NumericValue).FirstOrDefault();
        if (tier?.NumericValue is null) return true;
        var required = (binding.RequiredServiceCodes ?? []).ToHashSet(StringComparer.Ordinal);
        var preset = catalog.Presets.Where(item => required.All(code => item.Items.Any(component => component.ServiceCode == code))).OrderBy(item => item.DisplayOrder).FirstOrDefault();
        var commitment = catalog.Commitments.FirstOrDefault(item => item.Code == "FLEX" && item.Option(BillingV2PaymentModes.Monthly) is not null)
            ?? catalog.Commitments.FirstOrDefault(item => item.Months <= 1 && item.Option(BillingV2PaymentModes.Monthly) is not null);
        if (preset is null || commitment is null) return true;
        var selection = new BillingV2PublicSelection(preset.Code, commitment.Code, BillingV2PaymentModes.Monthly,
            binding.StorageServiceCode == BillingV2PublicCatalogCodes.StoragePersonal ? tier.Code : "",
            required.Contains(BillingV2PublicCatalogCodes.BackupPersonal),
            binding.StorageServiceCode == BillingV2PublicCatalogCodes.StorageShared ? tier.Code : null,
            required.Contains(BillingV2PublicCatalogCodes.BackupShared),
            required.Contains(BillingV2PublicCatalogCodes.VpnAccess) ? catalog.Services.FirstOrDefault(item => item.Code == BillingV2PublicCatalogCodes.VpnAccess)?.Tiers.FirstOrDefault(item => item.PublicSelectable)?.Code : null,
            required.Contains(BillingV2PublicCatalogCodes.RemoteDesktop), users.Value - 1,
            required.Contains(BillingV2PublicCatalogCodes.SupportPlus));
        result = new DiagnosticCommerceResult("standard", "Une formule correspond a votre besoin", selection, tier.NumericValue.Value, preset.Name);
        return true;
    }

    // Compatibilite explicite avant la premiere publication v2. Ce chemin ne
    // lit jamais une selection navigateur : il reconstruit toujours le palier
    // depuis le catalogue serveur et disparaitra avec le fallback v0.
    private static bool TryRecommendLegacy(string? context, IReadOnlyDictionary<string, string>? health,
        IReadOnlyDictionary<string, string>? commercial, BillingV2PublicCatalogSnapshot catalog, out DiagnosticCommerceResult result)
    {
        result = DiagnosticCommerceResult.HumanReview("Ce besoin merite un echange avant de choisir une offre.");
        if (!PublicPreDiagnosticService.TryEvaluate(health, out _) || commercial is null || health is null
            || context is not ("general" or "backup" or "remote-access")
            || !health.TryGetValue("profile", out var profile)) return false;
        var intent = commercial.GetValueOrDefault("commercialIntent");
        var scope = commercial.GetValueOrDefault("commercialScope");
        var storage = ReadBounded(commercial.GetValueOrDefault("commercialStorage"), 1, 256);
        if (storage is null || scope is not ("files" or "windows") || intent is "access_complex" or "backup_complex" or "unknown") return true;
        var required = intent switch
        {
            "backup_simple" when profile == "individual" && scope == "files" => new[] { "BASE-SERVICE", "STORAGE-PERSONAL", "BACKUP-PERSONAL" },
            "remote_files" when scope == "files" => new[] { "BASE-SERVICE", "STORAGE-PERSONAL", "BACKUP-PERSONAL", "VPN-ACCESS" },
            "windows_desktop" when scope == "windows" => new[] { "BASE-SERVICE", "STORAGE-PERSONAL", "BACKUP-PERSONAL", "VPN-ACCESS", "RDS-ACCESS" },
            _ => null,
        };
        if (required is null) return true;
        var storageService = catalog.Services.FirstOrDefault(item => item.Code == "STORAGE-PERSONAL" && item.PublicVisible && item.SelfServiceOrderable);
        var tier = storageService?.Tiers.Where(item => item.PublicSelectable && item.NumericValue >= storage).OrderBy(item => item.NumericValue).FirstOrDefault();
        var preset = catalog.Presets.Where(item => required.All(code => item.Items.Any(component => component.ServiceCode == code))).OrderBy(item => item.DisplayOrder).FirstOrDefault();
        var commitment = catalog.Commitments.FirstOrDefault(item => item.Code == "FLEX" && item.Option(BillingV2PaymentModes.Monthly) is not null);
        if (tier?.NumericValue is null || preset is null || commitment is null) return true;
        result = new DiagnosticCommerceResult("standard", "Une formule correspond a votre besoin", new BillingV2PublicSelection(preset.Code, commitment.Code, BillingV2PaymentModes.Monthly, tier.Code, true, null, false,
            required.Contains("VPN-ACCESS") ? catalog.Services.First(item => item.Code == "VPN-ACCESS").Tiers.First(item => item.PublicSelectable).Code : null,
            required.Contains("RDS-ACCESS"), 0, false), tier.NumericValue, preset.Name);
        return true;
    }

    private static int? ReadBounded(string? value, int minimum, int maximum)
        => int.TryParse(value, out var parsed) && parsed >= minimum && parsed <= maximum ? parsed : null;
}

public sealed record DiagnosticCommerceResult(string Kind, string Reason, BillingV2PublicSelection? Selection, int? SelectedCapacityGb, string? OfferName)
{
    public static DiagnosticCommerceResult HumanReview(string reason) => new("human_review", reason, null, null, null);
}
