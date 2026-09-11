using System.Text;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Evaluation serveur du pré-diagnostic public. Le navigateur peut afficher une
/// estimation immédiate, mais ce calcul est l'autorité du récapitulatif e-mail.
/// Aucune réponse, score ou priorité fournie par le client n'est réutilisée.
/// </summary>
public static class PublicPreDiagnosticService
{
    private static readonly IReadOnlySet<string> IndividualKeys = new HashSet<string>(
        ["profile", "equipmentCount", "equipmentAge", "performance", "updates", "backup",
         "network", "wifiCoverage", "mfa", "sharedAccounts", "phishing"],
        StringComparer.Ordinal);
    private static readonly IReadOnlySet<string> OrganisationKeys = new HashSet<string>(
        ["profile", "equipmentCount", "equipmentAge", "performance", "updates", "backup",
         "network", "wifiCoverage", "guestWifi", "mfa", "sharedAccounts", "phishing",
         "continuity", "businessDependence"],
        StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, HashSet<string>> Allowed =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["profile"] = ["individual", "professional", "association"],
            ["equipmentCount"] = ["1-2", "3-5", "6+"],
            ["equipmentAge"] = ["under3", "3to5", "over5"],
            ["performance"] = ["none", "occasional", "frequent"],
            ["updates"] = ["automatic", "manual", "unknown"],
            ["backup"] = ["automatic_external_tested", "automatic_external", "automatic_same_site", "cloud_sync_only", "manual", "none"],
            ["network"] = ["stable", "occasional", "frequent"],
            ["wifiCoverage"] = ["good", "some_areas", "poor"],
            ["guestWifi"] = ["separate", "same"],
            ["mfa"] = ["all", "some", "none"],
            ["sharedAccounts"] = ["no", "some", "yes"],
            ["phishing"] = ["aware", "unsure", "no"],
            ["continuity"] = ["tested", "partial", "none"],
            ["businessDependence"] = ["low", "medium", "high"],
        };

    private static readonly IReadOnlyDictionary<string, string> Labels =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["profile"] = "Profil", ["equipmentCount"] = "Nombre d'appareils", ["equipmentAge"] = "Âge du matériel",
            ["performance"] = "Lenteurs ou coupures", ["updates"] = "Mises à jour", ["backup"] = "Sauvegardes",
            ["network"] = "Connexion / Wi-Fi", ["wifiCoverage"] = "Couverture Wi-Fi", ["guestWifi"] = "Wi-Fi visiteurs",
            ["mfa"] = "Validation en deux étapes", ["sharedAccounts"] = "Comptes partagés", ["phishing"] = "Vigilance phishing",
            ["continuity"] = "Reprise après panne", ["businessDependence"] = "Impact d'une panne",
        };

    public static bool TryEvaluate(
        IReadOnlyDictionary<string, string>? answers,
        out PublicPreDiagnosticResult? result)
    {
        result = null;
        if (answers is null || answers.Count is 0 or > 20
            || !answers.TryGetValue("profile", out var profile)
            || !Allowed["profile"].Contains(profile)) return false;

        var expectedKeys = profile == "individual" ? IndividualKeys : OrganisationKeys;
        // Le schéma est fermé par profil : une clé d'un autre profil, une clé
        // inconnue ou une valeur non listée ne doit jamais atteindre l'e-mail.
        if (answers.Count != expectedKeys.Count
            || answers.Keys.Any(key => !expectedKeys.Contains(key))
            || expectedKeys.Any(key => !answers.TryGetValue(key, out var value)
                || !Allowed[key].Contains(value))) return false;

        var equipment = Deduct(Deduct(Deduct(100, answers["equipmentAge"], new Dictionary<string, int> { ["3to5"] = 10, ["over5"] = 25 }), answers["performance"], new Dictionary<string, int> { ["occasional"] = 15, ["frequent"] = 30 }), answers["updates"], new Dictionary<string, int> { ["manual"] = 15, ["unknown"] = 20 });
        var backup = new Dictionary<string, int> { ["automatic_external_tested"] = 100, ["automatic_external"] = 80, ["automatic_same_site"] = 45, ["cloud_sync_only"] = 35, ["manual"] = 25, ["none"] = 0 }[answers["backup"]];
        var network = Deduct(Deduct(100, answers["network"], new Dictionary<string, int> { ["occasional"] = 15, ["frequent"] = 35 }), answers["wifiCoverage"], new Dictionary<string, int> { ["some_areas"] = 12, ["poor"] = 30 });
        if (profile is "professional" or "association") network = Deduct(network, answers["guestWifi"], new Dictionary<string, int> { ["same"] = 20 });
        var security = Deduct(Deduct(Deduct(100, answers["mfa"], new Dictionary<string, int> { ["some"] = 15, ["none"] = 35 }), answers["sharedAccounts"], new Dictionary<string, int> { ["some"] = 12, ["yes"] = 30 }), answers["phishing"], new Dictionary<string, int> { ["unsure"] = 8, ["no"] = 15 });
        var categories = new List<PublicPreDiagnosticCategory> { new("Équipements", equipment), new("Sauvegarde", backup), new("Réseau / Wi-Fi", network), new("Sécurité", security) };
        if (profile is "professional" or "association") {
            var continuity = new Dictionary<string, int> { ["tested"] = 100, ["partial"] = 55, ["none"] = 15 }[answers["continuity"]];
            if (answers["businessDependence"] == "high" && answers["continuity"] != "tested") continuity = Math.Max(0, continuity - 15);
            categories.Add(new("Continuité", continuity));
        }
        var weights = profile == "individual" ? new[] { .25m, .35m, .20m, .20m } : new[] { .20m, .30m, .18m, .20m, .12m };
        var score = (int)Math.Round(categories.Select((category, index) => category.Score * weights[index]).Sum(), MidpointRounding.AwayFromZero);
        var level = score >= 85 ? "Bon" : score >= 70 ? "À surveiller" : score >= 45 ? "Améliorations recommandées" : "Risque important";
        var priorities = categories.OrderBy(category => category.Score).Where(category => category.Score < 70).Take(3).Select(category => PriorityFor(category.Label, answers)).ToList();
        result = new PublicPreDiagnosticResult(score, level, categories, priorities, answers);
        return true;
    }

    public static string BuildEmailBody(PublicPreDiagnosticCallback callback, PublicPreDiagnosticResult result, string correlationId)
    {
        var builder = new StringBuilder();
        builder.AppendLine("NOUVELLE DEMANDE DE RAPPEL — DIAGNOSTIC ZACHARY IT").AppendLine();
        builder.AppendLine("CONTACT").AppendLine();
        builder.AppendLine($"Profil : {ProfileLabel(callback.Answers["profile"])}");
        builder.AppendLine($"Nom : {callback.Name}");
        if (!string.IsNullOrWhiteSpace(callback.Organisation)) builder.AppendLine($"Entreprise / organisation : {callback.Organisation}");
        builder.AppendLine($"Téléphone : {callback.Phone}");
        if (!string.IsNullOrWhiteSpace(callback.Email)) builder.AppendLine($"E-mail : {callback.Email}");
        if (!string.IsNullOrWhiteSpace(callback.PreferredTime)) builder.AppendLine($"Moment souhaité : {callback.PreferredTime}");
        if (!string.IsNullOrWhiteSpace(callback.Comment)) builder.AppendLine($"Commentaire : {callback.Comment}");
        builder.AppendLine().AppendLine("DIAGNOSTIC").AppendLine();
        builder.AppendLine($"Date : {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC");
        builder.AppendLine("Source : /diagnostic");
        builder.AppendLine($"Référence : {correlationId}").AppendLine();
        builder.AppendLine($"Score : {result.Score}/100");
        builder.AppendLine($"Niveau : {result.Level}").AppendLine();
        builder.AppendLine("PRIORITÉS").AppendLine();
        foreach (var (priority, index) in result.Priorities.Select((value, index) => (value, index + 1))) builder.AppendLine($"{index}. {priority}");
        builder.AppendLine().AppendLine("RÉSULTATS").AppendLine();
        foreach (var category in result.Categories) builder.AppendLine($"{category.Label} : {category.Score}/100");
        builder.AppendLine().AppendLine("RÉPONSES UTILES").AppendLine();
        foreach (var key in callback.Answers.Keys.OrderBy(key => key, StringComparer.Ordinal)) builder.AppendLine($"{Labels[key]} : {Describe(key, callback.Answers[key])}");
        return builder.ToString().Trim();
    }

    public static string NormalizeSingleLine(string value)
        => string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static string NormalizeMultiline(string value)
        => value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Trim();

    private static int Deduct(int score, string value, IReadOnlyDictionary<string, int> deductions) => Math.Max(0, score - (deductions.TryGetValue(value, out var deduction) ? deduction : 0));
    private static string PriorityFor(string category, IReadOnlyDictionary<string, string> answers) => category switch { "Sauvegarde" => "Mettre en place une sauvegarde externalisée et récupérable", "Sécurité" => "Renforcer les accès aux comptes importants", "Réseau / Wi-Fi" => "Fiabiliser le Wi-Fi et les accès réseau", "Équipements" => "Planifier les améliorations de matériel et de mises à jour", _ when answers["businessDependence"] == "high" => "Préparer la reprise après incident prioritaire", _ => "Préparer une procédure de reprise après incident" };
    private static string ProfileLabel(string profile) => profile switch { "individual" => "Particulier", "professional" => "Entreprise / activité professionnelle", _ => "Association" };
    private static string Describe(string key, string value) => (key, value) switch
    {
        ("profile", "individual") => "Informatique personnelle",
        ("profile", "professional") => "Entreprise / activité professionnelle",
        ("profile", "association") => "Association",
        ("equipmentCount", "1-2") => "1 à 2 appareils", ("equipmentCount", "3-5") => "3 à 5 appareils", ("equipmentCount", "6+") => "6 appareils ou plus",
        ("equipmentAge", "under3") => "Moins de 3 ans", ("equipmentAge", "3to5") => "3 à 5 ans", ("equipmentAge", "over5") => "Plus de 5 ans",
        ("performance", "none") => "Pas de problème notable", ("performance", "occasional") => "Occasionnellement", ("performance", "frequent") => "Souvent",
        ("updates", "automatic") => "Automatiques", ("updates", "manual") => "Manuelles", ("updates", "unknown") => "Je ne sais pas",
        ("backup", "automatic_external_tested") => "Automatique, externalisée et restauration testée", ("backup", "automatic_external") => "Automatique et externalisée", ("backup", "automatic_same_site") => "Automatique mais au même endroit", ("backup", "cloud_sync_only") => "Synchronisation OneDrive / Google Drive uniquement", ("backup", "manual") => "Manuelle", ("backup", "none") => "Aucune sauvegarde",
        ("network", "stable") => "Stable", ("network", "occasional") => "Quelques coupures", ("network", "frequent") => "Coupures fréquentes",
        ("wifiCoverage", "good") => "Bonne partout", ("wifiCoverage", "some_areas") => "Des zones moins bien couvertes", ("wifiCoverage", "poor") => "Insuffisante",
        ("guestWifi", "separate") => "Séparé des usages professionnels", ("guestWifi", "same") => "Partagé avec les usages professionnels",
        ("mfa", "all") => "Activée sur les comptes importants", ("mfa", "some") => "Activée sur certains comptes", ("mfa", "none") => "Non activée",
        ("sharedAccounts", "no") => "Non", ("sharedAccounts", "some") => "Quelques-uns", ("sharedAccounts", "yes") => "Oui",
        ("phishing", "aware") => "Sujets connus", ("phishing", "unsure") => "À confirmer", ("phishing", "no") => "Peu ou pas abordé",
        ("continuity", "tested") => "Procédure testée", ("continuity", "partial") => "Partiellement prévu", ("continuity", "none") => "Rien de prévu",
        ("businessDependence", "low") => "Faible", ("businessDependence", "medium") => "Moyen", ("businessDependence", "high") => "Fort",
        _ => value,
    };
}

public sealed record PublicPreDiagnosticCategory(string Label, int Score);
public sealed record PublicPreDiagnosticResult(int Score, string Level, IReadOnlyList<PublicPreDiagnosticCategory> Categories, IReadOnlyList<string> Priorities, IReadOnlyDictionary<string, string> Answers);
public sealed record PublicPreDiagnosticCallback(string Name, string Phone, string? Email, string? Organisation, string? PreferredTime, string? Comment, IReadOnlyDictionary<string, string> Answers);
