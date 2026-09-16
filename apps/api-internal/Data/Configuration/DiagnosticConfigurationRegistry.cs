using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Data.Configuration;

/// <summary>
/// Registre ferme du diagnostic administrable. Les contextes, les operateurs
/// et les natures de donnees sont definis ici : une configuration qui sort de
/// ces ensembles est refusee, jamais tronquee silencieusement.
/// </summary>
public static partial class DiagnosticConfigurationRegistry
{
    public const int SchemaVersion = 1;
    public const int PreDiagnosticSchemaVersion = 2;
    public const int MaxPayloadBytes = 512_000;

    /// <summary>
    /// Contextes du parcours public. Ils correspondent exactement a
    /// <c>DIAGNOSTIC_CONTEXT_IDS</c> cote WebPortal : le contrat de
    /// verification web echoue si les deux listes divergent.
    /// </summary>
    public static readonly IReadOnlyList<string> ContextIds =
    [
        "backup",
        "remote-access",
        "network",
        "messaging",
        "domain-dns",
        "server",
        "web-hosting",
        "general",
    ];

    public static readonly IReadOnlySet<string> Operators =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "equals", "not_equals", "one_of", "includes", "only", "answered",
        };

    public static readonly IReadOnlySet<string> DataKinds =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "personal_documents",
            "business_documents",
            "photos",
            "association_data",
            "work_files",
            "other_important_files",
        };

    [GeneratedRegex("^[a-z][a-z0-9-]{1,63}$")]
    private static partial Regex IdentifierPattern();

    // Les IDs v2 reprennent les clés stables déjà envoyées par le
    // pré-diagnostic public (equipmentAge, businessDependence). Le schéma v1
    // conserve volontairement sa syntaxe historique, plus restrictive.
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9-]{1,63}$")]
    private static partial Regex PreDiagnosticIdentifierPattern();

    // Les valeurs restent des identifiants inertes. Le signe + est toutefois
    // nécessaire pour les paliers historiques tels que "6+" et "12-plus" ;
    // il ne possède aucune interprétation de code dans ce document JSON.
    [GeneratedRegex("^[a-z0-9][a-z0-9_+\\-]{0,63}$")]
    private static partial Regex OptionValuePattern();

    [GeneratedRegex("^[A-Z][A-Z0-9-]{2,63}$")]
    private static partial Regex RuleIdPattern();

    public static JsonSerializerOptions SerializerOptions { get; } =
        new(JsonSerializerDefaults.Web);

    // v2 is an admin-controlled, closed document.  Silently ignoring an
    // unknown property would make a typo look saved even though it has no
    // effect.  Keep v1 compatibility untouched, but reject every unmapped v2
    // property before it can be persisted or published.
    private static JsonSerializerOptions PreDiagnosticSerializerOptions { get; } =
        new(JsonSerializerDefaults.Web)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };

    private static readonly IReadOnlySet<string> CommercialProfileIds =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "simple_backup", "vpn_access", "windows_desktop",
            "team_or_structure", "team_windows_desktop",
        };

    /// <summary>
    /// Valide et canonicalise une configuration. La sortie est reserialisee a
    /// partir du modele : un champ inconnu envoye par un client ne peut pas se
    /// retrouver stocke en base.
    /// </summary>
    public static DiagnosticConfigurationValidation Validate(JsonElement payload)
    {
        if (payload.ValueKind == JsonValueKind.Object
            && payload.TryGetProperty("schemaVersion", out var schemaVersion)
            && schemaVersion.ValueKind == JsonValueKind.Number
            && schemaVersion.TryGetInt32(out var version)
            && version == PreDiagnosticSchemaVersion)
        {
            return ValidatePreDiagnosticV2(payload);
        }

        var errors = new List<string>();
        DiagnosticConfigurationModel? model;
        try
        {
            model = payload.Deserialize<DiagnosticConfigurationModel>(SerializerOptions);
        }
        catch (JsonException)
        {
            return new DiagnosticConfigurationValidation(
                null,
                ["Structure JSON invalide."]);
        }

        if (model is null)
        {
            return new DiagnosticConfigurationValidation(null, ["Configuration absente."]);
        }

        if (model.SchemaVersion != SchemaVersion)
        {
            errors.Add($"schemaVersion doit valoir {SchemaVersion}.");
        }

        var contexts = model.Contexts ?? [];
        var seenContexts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var context in contexts)
        {
            ValidateContext(context, seenContexts, errors);
        }

        foreach (var expected in ContextIds)
        {
            if (!seenContexts.Contains(expected))
            {
                errors.Add($"Contexte manquant : {expected}.");
            }
        }

        if (errors.Count > 0)
        {
            return new DiagnosticConfigurationValidation(null, errors);
        }

        var canonical = JsonSerializer.Serialize(model, SerializerOptions);
        if (Encoding.UTF8.GetByteCount(canonical) > MaxPayloadBytes)
        {
            return new DiagnosticConfigurationValidation(
                null,
                [$"Configuration trop volumineuse (max {MaxPayloadBytes} octets)."]);
        }

        return new DiagnosticConfigurationValidation(canonical, []);
    }

    /// <summary>
    /// Validation du moteur actuel. Cette seconde branche utilise les memes
    /// lignes brouillon/publie et le meme versioning que v1, mais ferme le
    /// schema v2 avant toute persistance. Les decisions metier restent dans le
    /// document publie, jamais dans le navigateur.
    /// </summary>
    private static DiagnosticConfigurationValidation ValidatePreDiagnosticV2(JsonElement payload)
    {
        PreDiagnosticConfigurationModel? model;
        try { model = payload.Deserialize<PreDiagnosticConfigurationModel>(PreDiagnosticSerializerOptions); }
        catch (JsonException) { return new DiagnosticConfigurationValidation(null, ["Structure JSON invalide."]); }
        if (model is null || model.SchemaVersion != PreDiagnosticSchemaVersion)
            return new DiagnosticConfigurationValidation(null, [$"schemaVersion doit valoir {PreDiagnosticSchemaVersion}."]);

        var errors = new List<string>();
        var profileIds = new HashSet<string>(StringComparer.Ordinal) { "individual", "professional", "association" };
        var profiles = model.Profiles ?? [];
        var configuredProfileIds = profiles
            .Where(profile => profile?.Id is not null)
            .Select(profile => profile!.Id!)
            .ToHashSet(StringComparer.Ordinal);
        if (profiles.Count != 3 || configuredProfileIds.Count != 3 || !configuredProfileIds.SetEquals(profileIds))
            errors.Add("profiles : les trois profils stables individual, professional et association sont requis.");
        foreach (var profile in profiles)
        {
            if (profile is null) continue;
            RequireText(profile.Label, 2, 120, $"profiles.{profile.Id}.label", errors);
            RequireText(profile.Description, 2, 300, $"profiles.{profile.Id}.description", errors);
            if (profile.Order is < 0 or > 10_000) errors.Add($"profiles.{profile.Id}.order : valeur attendue entre 0 et 10000.");
        }

        var categories = model.Categories ?? [];
        var categoryIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var category in categories)
        {
            var id = category?.Id ?? string.Empty;
            if (!IdentifierPattern().IsMatch(id) || !categoryIds.Add(id)) { errors.Add("categories : identifiant invalide ou en double."); continue; }
            RequireText(category?.Label, 2, 120, $"categories.{id}.label", errors);
            var weights = category?.Weights;
            foreach (var profile in profileIds)
                if (weights is null || !weights.TryGetValue(profile, out var weight) || weight is < 0 or > 100)
                    errors.Add($"categories.{id}.weights.{profile} : poids attendu entre 0 et 100.");
        }
        foreach (var profile in profileIds)
        {
            var total = categories.Sum(category => category?.Active == true && category.Weights is not null && category.Weights.TryGetValue(profile, out var weight) ? weight : 0);
            if (total != 100) errors.Add($"profiles.{profile}.categoryWeights : la somme des ponderations doit etre egale a 100 %.");
        }

        var knownQuestions = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var question in model.Questions ?? [])
        {
            var safeQuestion = question!;
            var id = safeQuestion.Id ?? string.Empty;
            if (!PreDiagnosticIdentifierPattern().IsMatch(id) || !knownQuestions.TryAdd(id, new HashSet<string>(StringComparer.Ordinal))) { errors.Add("questions : identifiant invalide ou en double."); continue; }
            if (question?.CategoryId is not null && !categoryIds.Contains(question.CategoryId)) errors.Add($"questions.{id}.categoryId : categorie inconnue.");
            if (question?.Profiles is null || question.Profiles.Count == 0 || question.Profiles.Any(profile => !profileIds.Contains(profile))) errors.Add($"questions.{id}.profiles : profil invalide.");
            RequireText(question?.Label, 5, 300, $"questions.{id}.label", errors);
            if (question?.Hint is not null) RequireText(question.Hint, 3, 400, $"questions.{id}.hint", errors);
            // Une visibilite ne peut dependre que d'une reponse deja definie :
            // pas de cycle, de cle fantome ni de DSL executable.
            ValidateV2Conditions($"questions.{id}.when", question?.When ?? [], knownQuestions, errors);
            var values = knownQuestions[id];
            var options = question?.Options ?? [];
            if (options.Count is < 2 or > 20) errors.Add($"questions.{id}.options : entre 2 et 20 reponses.");
            if (question?.Required == true && !options.Any(option => option?.Active == true))
                errors.Add($"questions.{id}.options : une question obligatoire doit conserver au moins une reponse active.");
            foreach (var option in options)
            {
                var value = option?.Value ?? string.Empty;
                if (!OptionValuePattern().IsMatch(value) || !values.Add(value)) { errors.Add($"questions.{id}.options : valeur invalide ou en double."); continue; }
                RequireText(option?.Label, 1, 160, $"questions.{id}.options.{value}.label", errors);
                foreach (var scoreEffect in option?.Effects ?? [])
                {
                    if (scoreEffect is null || scoreEffect.Mode is not ("absolute" or "deduction") || scoreEffect.Value is < 0 or > 100)
                        errors.Add($"questions.{id}.options.{value}.effects : effet de score invalide.");
                    else ValidateV2Conditions($"questions.{id}.options.{value}.effects", scoreEffect.When ?? [], knownQuestions, errors);
                }
            }
        }
        if (!knownQuestions.TryGetValue("profile", out var profileValues)
            || !profileValues.SetEquals(profileIds)
            || !(model.Questions ?? []).Any(question => question?.Id == "profile" && question.Active && question.Required))
            errors.Add("questions.profile : la question de profil active et ses trois reponses stables sont requises.");

        var levels = model.Levels ?? [];
        if (levels.Count == 0 || !levels.Any(level => level?.MinimumScore == 0)) errors.Add("levels : un niveau commencant a 0 est requis.");
        var levelIds = new HashSet<string>(StringComparer.Ordinal);
        var levelThresholds = new HashSet<int>();
        foreach (var level in levels)
        {
            if (level is null || level.MinimumScore is < 0 or > 100) errors.Add("levels : score minimum attendu entre 0 et 100.");
            else
            {
                if (string.IsNullOrWhiteSpace(level.Id) || !PreDiagnosticIdentifierPattern().IsMatch(level.Id) || !levelIds.Add(level.Id)) errors.Add("levels : identifiant invalide ou en double.");
                if (!levelThresholds.Add(level.MinimumScore)) errors.Add("levels : deux niveaux ne peuvent pas avoir le meme seuil.");
                RequireText(level.Label, 2, 120, $"levels.{level.Id}.label", errors);
            }
        }
        if (model.MaximumPriorities is < 1 or > 5) errors.Add("maximumPriorities : valeur attendue entre 1 et 5.");
        foreach (var priority in model.Priorities ?? [])
        {
            if (priority is null || priority.CategoryId is null || !categoryIds.Contains(priority.CategoryId) || priority.Threshold is < 0 or > 100) errors.Add("priorities : categorie ou seuil invalide.");
            else { RequireText(priority.Title, 5, 300, $"priorities.{priority.CategoryId}.title", errors); RequireText(priority.Body, 10, 1500, $"priorities.{priority.CategoryId}.body", errors); }
        }
        foreach (var positive in model.Positives ?? [])
        {
            if (positive is null || positive.CategoryId is null || !categoryIds.Contains(positive.CategoryId) || positive.Threshold is < 0 or > 100) errors.Add("positives : categorie ou seuil invalide.");
            else RequireText(positive.Text, 5, 500, $"positives.{positive.CategoryId}.text", errors);
        }

        var contexts = model.Contexts ?? [];
        var contextIds = new HashSet<string>(contexts.Where(context => context?.Id is not null).Select(context => context!.Id!), StringComparer.Ordinal);
        if (contexts.Count != ContextIds.Count || contextIds.Count != ContextIds.Count)
            errors.Add("contexts : les huit identifiants de contexte stables, sans doublon, sont requis.");
        foreach (var expected in ContextIds) if (!contextIds.Contains(expected)) errors.Add($"Contexte manquant : {expected}.");
        foreach (var context in contexts) { if (context is null || !ContextIds.Contains(context.Id ?? string.Empty, StringComparer.Ordinal)) errors.Add("contexts : contexte inconnu."); else { RequireText(context.Label, 2, 120, $"contexts.{context.Id}.label", errors); RequireText(context.Text, 2, 500, $"contexts.{context.Id}.text", errors); } }

        var commerce = model.Commerce;
        if (commerce is null || commerce.MinimumStorageGb < 1 || commerce.MaximumStorageGb < commerce.MinimumStorageGb || commerce.MaximumStorageGb > 10_000 || commerce.MinimumUsers < 1 || commerce.MaximumUsers < commerce.MinimumUsers || commerce.MaximumUsers > 10_000 || commerce.MaximumSites < 1 || commerce.MaximumSites > 100)
            errors.Add("commerce : limites self-service invalides.");
        else if (commerce.CatalogBindings is null || commerce.CatalogBindings.Count != 5)
            errors.Add("commerce.catalogBindings : les cinq correspondances catalogue sont requises.");
        else
        {
            if (commerce.CompatibleScopes is null || commerce.CompatibleScopes.Count == 0
                || commerce.CompatibleScopes.Distinct(StringComparer.Ordinal).Count() != commerce.CompatibleScopes.Count
                || commerce.CompatibleScopes.Any(scope => scope is not ("files" or "windows")))
                errors.Add("commerce.compatibleScopes : scopes files/windows uniques requis.");
            if (commerce.HumanReviewIntents is null
                || commerce.HumanReviewIntents.Distinct(StringComparer.Ordinal).Count() != commerce.HumanReviewIntents.Count
                || commerce.HumanReviewIntents.Any(intent => !OptionValuePattern().IsMatch(intent)))
                errors.Add("commerce.humanReviewIntents : intentions uniques et valides requises.");
            ValidateCommercialQuestions(commerce.Questions ?? [], profileIds, errors);
            ValidateActiveContextsHaveCommercialQuestions(contexts, commerce.Questions ?? [], errors);
            ValidateCommercialProfilesAndBindings(commerce, errors);
        }

        if (errors.Count > 0) return new DiagnosticConfigurationValidation(null, errors);
        var canonical = JsonSerializer.Serialize(model, SerializerOptions);
        return Encoding.UTF8.GetByteCount(canonical) > MaxPayloadBytes
            ? new DiagnosticConfigurationValidation(null, [$"Configuration trop volumineuse (max {MaxPayloadBytes} octets)."])
            : new DiagnosticConfigurationValidation(canonical, []);
    }

    private static void ValidateCommercialQuestions(
        IReadOnlyList<PreDiagnosticCommercialQuestionModel> questions,
        IReadOnlySet<string> profileIds,
        List<string> errors)
    {
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var question in questions)
        {
            if (question is null)
            {
                errors.Add("commerce.questions : question vide.");
                continue;
            }
            var safeQuestion = question!;
            var id = safeQuestion.Id ?? string.Empty;
            if (!PreDiagnosticIdentifierPattern().IsMatch(id) || !known.Add(id))
            {
                errors.Add("commerce.questions : identifiant invalide ou en double.");
                continue;
            }
            RequireText(safeQuestion.Label, 5, 300, $"commerce.questions.{id}.label", errors);
            if (safeQuestion.Hint is not null) RequireText(safeQuestion.Hint, 3, 400, $"commerce.questions.{id}.hint", errors);
            if (safeQuestion.Profiles is null || safeQuestion.Profiles.Count == 0 || safeQuestion.Profiles.Any(profile => !profileIds.Contains(profile))) errors.Add($"commerce.questions.{id}.profiles : profil invalide.");
            if (safeQuestion.Contexts is null || safeQuestion.Contexts.Count == 0 || safeQuestion.Contexts.Any(context => !ContextIds.Contains(context, StringComparer.Ordinal))) errors.Add($"commerce.questions.{id}.contexts : contexte invalide.");
            if (safeQuestion.DynamicOptions is not ("none" or "storage" or "users")) errors.Add($"commerce.questions.{id}.dynamicOptions : valeur invalide.");
            var values = new HashSet<string>(StringComparer.Ordinal);
            var options = safeQuestion.Options ?? [];
            if (options.Count is < 2 or > 30) errors.Add($"commerce.questions.{id}.options : entre 2 et 30 reponses.");
            foreach (var option in options)
            {
                if (option is null)
                {
                    errors.Add($"commerce.questions.{id}.options : reponse vide.");
                    continue;
                }
                var safeOption = option!;
                var value = safeOption.Value ?? string.Empty;
                if (!OptionValuePattern().IsMatch(value) || !values.Add(value)) { errors.Add($"commerce.questions.{id}.options : valeur invalide ou en double."); continue; }
                RequireText(safeOption.Label, 1, 160, $"commerce.questions.{id}.options.{value}.label", errors);
                if (safeOption.Profiles is null || safeOption.Profiles.Count == 0 || safeOption.Profiles.Any(profile => !profileIds.Contains(profile))) errors.Add($"commerce.questions.{id}.options.{value}.profiles : profil invalide.");
                if (safeOption.Contexts is null || safeOption.Contexts.Count == 0 || safeOption.Contexts.Any(context => !ContextIds.Contains(context, StringComparer.Ordinal))) errors.Add($"commerce.questions.{id}.options.{value}.contexts : contexte invalide.");
                if ((safeOption.Effects?.Count ?? 0) != 0) errors.Add($"commerce.questions.{id}.options.{value}.effects : les questions commerciales ne peuvent pas modifier le score de sante.");
            }
        }
        if (questions.Count == 0) errors.Add("commerce.questions : au moins une question de qualification est requise.");
    }

    private static void ValidateCommercialProfilesAndBindings(
        PreDiagnosticCommerceModel commerce,
        List<string> errors)
    {
        var configuredProfiles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var profile in commerce.Profiles ?? [])
        {
            var id = profile?.Id ?? string.Empty;
            if (!CommercialProfileIds.Contains(id) || !configuredProfiles.Add(id))
            {
                errors.Add("commerce.profiles : profil commercial invalide ou en double.");
                continue;
            }
            RequireText(profile!.Label, 2, 120, $"commerce.profiles.{id}.label", errors);
            if (profile.Intents is null || profile.Intents.Count == 0 || profile.Intents.Any(intent => !OptionValuePattern().IsMatch(intent)))
                errors.Add($"commerce.profiles.{id}.intents : intention invalide.");
            if (profile.Scopes is null || profile.Scopes.Count == 0 || profile.Scopes.Any(scope => scope is not ("files" or "windows")))
                errors.Add($"commerce.profiles.{id}.scopes : scope invalide.");
        }
        if (!configuredProfiles.SetEquals(CommercialProfileIds))
            errors.Add("commerce.profiles : les cinq profils commerciaux stables sont requis.");

        var boundProfiles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var binding in commerce.CatalogBindings ?? [])
        {
            var profileId = binding?.ProfileId ?? string.Empty;
            if (!CommercialProfileIds.Contains(profileId) || !boundProfiles.Add(profileId))
                errors.Add("commerce.catalogBindings : profil commercial invalide ou en double.");
            var required = binding?.RequiredServiceCodes ?? [];
            if (required.Count == 0 || required.Distinct(StringComparer.Ordinal).Count() != required.Count
                || required.Any(code => !RuleIdPattern().IsMatch(code)))
                errors.Add($"commerce.catalogBindings.{profileId}.requiredServiceCodes : services uniques valides requis.");
            if (string.IsNullOrWhiteSpace(binding?.StorageServiceCode) || !RuleIdPattern().IsMatch(binding.StorageServiceCode)
                || !required.Contains(binding.StorageServiceCode, StringComparer.Ordinal))
                errors.Add($"commerce.catalogBindings.{profileId}.storageServiceCode : doit faire partie des services requis.");
        }
        if (!boundProfiles.SetEquals(CommercialProfileIds))
            errors.Add("commerce.catalogBindings : les cinq profils commerciaux stables doivent etre relies au catalogue.");
    }

    private static void ValidateActiveContextsHaveCommercialQuestions(
        IReadOnlyList<PreDiagnosticContextModel> contexts,
        IReadOnlyList<PreDiagnosticCommercialQuestionModel> questions,
        List<string> errors)
    {
        foreach (var context in contexts.Where(context => context?.Active == true && context.Id is not null))
        {
            var applicable = questions.Where(question => question is not null
                && question.Active
                && (question.Contexts ?? []).Contains(context.Id!, StringComparer.Ordinal)
                && (question.Options ?? []).Any(option => option is not null
                    && option.Active
                    && (option.Contexts ?? []).Contains(context.Id!, StringComparer.Ordinal)))
                .ToArray();
            if (applicable.Length == 0)
                errors.Add($"contexts.{context.Id} : un contexte actif doit conserver au moins une question commerciale exploitable.");
        }
    }

    private static void ValidateV2Conditions(string field, IReadOnlyList<DiagnosticConditionModel> conditions, Dictionary<string, HashSet<string>> known, List<string> errors)
    {
        foreach (var condition in conditions)
        {
            if (condition is null || condition.QuestionId is null || !known.TryGetValue(condition.QuestionId, out var values) || condition.Operator is not ("equals" or "not_equals" or "one_of" or "answered")) { errors.Add($"{field} : condition invalide."); continue; }
            var conditionValues = condition.Values ?? [];
            if (condition.Operator == "answered") { if (conditionValues.Count != 0) errors.Add($"{field} : answered n'accepte aucune valeur."); continue; }
            if (conditionValues.Count == 0 || conditionValues.Any(value => !values.Contains(value))) errors.Add($"{field} : valeur de condition inconnue.");
        }
    }

    private static void ValidateContext(
        DiagnosticContextModel? context,
        HashSet<string> seenContexts,
        List<string> errors)
    {
        if (context is null)
        {
            errors.Add("Contexte vide.");
            return;
        }

        var id = context.Id ?? string.Empty;
        if (!ContextIds.Contains(id, StringComparer.Ordinal))
        {
            errors.Add($"Contexte inconnu : {Describe(id)}.");
            return;
        }

        if (!seenContexts.Add(id))
        {
            errors.Add($"Contexte en double : {id}.");
            return;
        }

        RequireText(context.Label, 2, 80, $"{id}.label", errors);
        RequireText(context.Eyebrow, 2, 120, $"{id}.eyebrow", errors);
        RequireText(context.Title, 5, 200, $"{id}.title", errors);
        RequireText(context.Intro, 10, 1_000, $"{id}.intro", errors);
        RequireText(context.ContactSubject, 5, 200, $"{id}.contactSubject", errors);

        var questions = context.Questions ?? [];
        if (questions.Count > 30)
        {
            errors.Add($"{id} : 30 questions au maximum.");
        }

        // Les options connues sont accumulees question par question : une
        // condition ne peut viser qu'une question declaree avant elle.
        var known = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var question in questions)
        {
            ValidateQuestion(id, question, known, errors);
        }

        ValidateGuidance(id, context.Guidance ?? [], known, errors);

        if (context.BillingMapping is not null && !context.FormulaEligible)
        {
            errors.Add(
                $"{id} : une correspondance Billing V2 exige formulaEligible = true.");
        }

        if (context.BillingMapping is not null)
        {
            ValidateBillingMapping(id, context.BillingMapping, known, errors);
        }
    }

    private static void ValidateQuestion(
        string contextId,
        DiagnosticQuestionModel? question,
        Dictionary<string, HashSet<string>> known,
        List<string> errors)
    {
        if (question is null)
        {
            errors.Add($"{contextId} : question vide.");
            return;
        }

        var questionId = question.Id ?? string.Empty;
        if (!IdentifierPattern().IsMatch(questionId))
        {
            errors.Add(
                $"{contextId} : identifiant de question invalide {Describe(questionId)}.");
            return;
        }

        if (known.ContainsKey(questionId))
        {
            errors.Add($"{contextId} : question en double {questionId}.");
            return;
        }

        RequireText(question.Legend, 5, 300, $"{contextId}.{questionId}.legend", errors);
        RequireText(
            question.SummaryLabel,
            2,
            120,
            $"{contextId}.{questionId}.summaryLabel",
            errors);
        if (question.Hint is not null)
        {
            RequireText(question.Hint, 3, 400, $"{contextId}.{questionId}.hint", errors);
        }

        if (question.Mode is not ("single" or "multi"))
        {
            errors.Add($"{contextId}.{questionId} : mode doit valoir single ou multi.");
        }

        var options = question.Options ?? [];
        if (options.Count is < 2 or > 20)
        {
            errors.Add($"{contextId}.{questionId} : entre 2 et 20 options.");
        }

        var values = new HashSet<string>(StringComparer.Ordinal);
        foreach (var option in options)
        {
            var value = option?.Value ?? string.Empty;
            if (!OptionValuePattern().IsMatch(value))
            {
                errors.Add(
                    $"{contextId}.{questionId} : valeur d'option invalide {Describe(value)}.");
                continue;
            }

            if (!values.Add(value))
            {
                errors.Add($"{contextId}.{questionId} : option en double {value}.");
                continue;
            }

            RequireText(
                option?.Label,
                1,
                160,
                $"{contextId}.{questionId}.{value}.label",
                errors);
            if (option?.Exclusive == true && question.Mode != "multi")
            {
                errors.Add(
                    $"{contextId}.{questionId} : une option exclusive n'a de sens qu'en mode multi.");
            }
        }

        if (question.When is not null)
        {
            var target = question.When.QuestionId ?? string.Empty;
            if (!known.TryGetValue(target, out var targetValues))
            {
                errors.Add(
                    $"{contextId}.{questionId} : condition d'affichage vers une question inconnue ou posterieure {Describe(target)}.");
            }
            else
            {
                var whenValues = question.When.Values ?? [];
                if (whenValues.Count == 0)
                {
                    errors.Add(
                        $"{contextId}.{questionId} : condition d'affichage sans valeur.");
                }

                foreach (var value in whenValues)
                {
                    if (!targetValues.Contains(value))
                    {
                        errors.Add(
                            $"{contextId}.{questionId} : la valeur {Describe(value)} n'existe pas dans {target}.");
                    }
                }
            }
        }

        known[questionId] = values;
    }

    private static void ValidateGuidance(
        string contextId,
        IReadOnlyList<DiagnosticGuidanceRuleModel> guidance,
        Dictionary<string, HashSet<string>> known,
        List<string> errors)
    {
        if (guidance.Count == 0)
        {
            errors.Add($"{contextId} : au moins une regle de resultat est requise.");
            return;
        }

        if (guidance.Count > 40)
        {
            errors.Add($"{contextId} : 40 regles de resultat au maximum.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < guidance.Count; index++)
        {
            var rule = guidance[index];
            if (rule is null)
            {
                errors.Add($"{contextId} : regle de resultat vide.");
                continue;
            }

            var ruleId = rule.Id ?? string.Empty;
            if (!RuleIdPattern().IsMatch(ruleId))
            {
                errors.Add($"{contextId} : identifiant de regle invalide {Describe(ruleId)}.");
            }
            else if (!ids.Add(ruleId))
            {
                errors.Add($"{contextId} : regle en double {ruleId}.");
            }

            RequireText(rule.Title, 5, 300, $"{contextId}.{ruleId}.title", errors);
            RequireText(rule.Body, 10, 1_500, $"{contextId}.{ruleId}.body", errors);

            var points = rule.Points ?? [];
            if (points.Count > 10)
            {
                errors.Add($"{contextId}.{ruleId} : 10 points au maximum.");
            }

            foreach (var point in points)
            {
                RequireText(point, 3, 300, $"{contextId}.{ruleId}.points", errors);
            }

            var conditions = rule.When ?? [];
            ValidateConditions(contextId, $"{ruleId}.when", conditions, known, errors);

            // Sans regle inconditionnelle finale, une combinaison de reponses
            // pourrait ne produire aucun texte : le parcours public afficherait
            // un resultat vide. La configuration est donc refusee.
            if (index == guidance.Count - 1 && conditions.Count != 0)
            {
                errors.Add(
                    $"{contextId} : la derniere regle de resultat doit etre inconditionnelle.");
            }
        }
    }

    private static void ValidateBillingMapping(
        string contextId,
        DiagnosticBillingMappingModel mapping,
        Dictionary<string, HashSet<string>> known,
        List<string> errors)
    {
        ValidateConditions(
            contextId,
            "billingMapping.requireAll",
            mapping.RequireAll ?? [],
            known,
            errors);
        ValidateOptionalConditions(
            contextId,
            "billingMapping.needsRemoteFilesWhen",
            mapping.NeedsRemoteFilesWhen,
            known,
            errors);
        ValidateOptionalConditions(
            contextId,
            "billingMapping.needsVpnWhen",
            mapping.NeedsVpnWhen,
            known,
            errors);
        ValidateOptionalConditions(
            contextId,
            "billingMapping.needsWindowsDesktopWhen",
            mapping.NeedsWindowsDesktopWhen,
            known,
            errors);

        RequireKnownQuestion(contextId, "usersQuestionId", mapping.UsersQuestionId, known, errors);
        RequireKnownQuestion(
            contextId,
            "structureQuestionId",
            mapping.StructureQuestionId,
            known,
            errors);
        RequireKnownQuestion(
            contextId,
            "storageQuestionId",
            mapping.StorageQuestionId,
            known,
            errors);
        RequireKnownQuestion(
            contextId,
            "restoreTestQuestionId",
            mapping.RestoreTestQuestionId,
            known,
            errors);

        // La structure decide de la nature des donnees transmise a Billing V2 :
        // les deux valeurs doivent exister dans le contrat partage.
        if (mapping.IndividualDataKind is null
            || !DataKinds.Contains(mapping.IndividualDataKind))
        {
            errors.Add(
                $"{contextId} : individualDataKind inconnu {Describe(mapping.IndividualDataKind ?? string.Empty)}.");
        }

        if (mapping.OrganisationDataKind is null
            || !DataKinds.Contains(mapping.OrganisationDataKind))
        {
            errors.Add(
                $"{contextId} : organisationDataKind inconnu {Describe(mapping.OrganisationDataKind ?? string.Empty)}.");
        }

        // Sans type de structure, aucune formule ne peut etre construite : la
        // correspondance serait morte et le parcours sortirait toujours en
        // devis sans que l'administrateur comprenne pourquoi.
        if (mapping.StructureQuestionId is null)
        {
            errors.Add(
                $"{contextId} : structureQuestionId est obligatoire pour une correspondance Billing V2.");
        }
    }

    private static void RequireKnownQuestion(
        string contextId,
        string field,
        string? questionId,
        Dictionary<string, HashSet<string>> known,
        List<string> errors)
    {
        if (questionId is not null && !known.ContainsKey(questionId))
        {
            errors.Add($"{contextId}.{field} : question inconnue {Describe(questionId)}.");
        }
    }

    private static void ValidateOptionalConditions(
        string contextId,
        string field,
        IReadOnlyList<DiagnosticConditionModel>? conditions,
        Dictionary<string, HashSet<string>> known,
        List<string> errors)
    {
        if (conditions is null)
        {
            return;
        }

        if (conditions.Count == 0)
        {
            errors.Add($"{contextId}.{field} : utiliser null plutot qu'une liste vide.");
            return;
        }

        ValidateConditions(contextId, field, conditions, known, errors);
    }

    private static void ValidateConditions(
        string contextId,
        string field,
        IReadOnlyList<DiagnosticConditionModel> conditions,
        Dictionary<string, HashSet<string>> known,
        List<string> errors)
    {
        if (conditions.Count > 10)
        {
            errors.Add($"{contextId}.{field} : 10 conditions au maximum.");
        }

        foreach (var condition in conditions)
        {
            if (condition is null)
            {
                errors.Add($"{contextId}.{field} : condition vide.");
                continue;
            }

            var target = condition.QuestionId ?? string.Empty;
            if (!known.TryGetValue(target, out var values))
            {
                errors.Add($"{contextId}.{field} : question inconnue {Describe(target)}.");
                continue;
            }

            var op = condition.Operator ?? string.Empty;
            if (!Operators.Contains(op))
            {
                errors.Add($"{contextId}.{field} : operateur inconnu {Describe(op)}.");
                continue;
            }

            var conditionValues = condition.Values ?? [];
            if (op == "answered")
            {
                if (conditionValues.Count != 0)
                {
                    errors.Add($"{contextId}.{field} : answered n'accepte aucune valeur.");
                }

                continue;
            }

            if (conditionValues.Count == 0)
            {
                errors.Add($"{contextId}.{field} : l'operateur {op} exige au moins une valeur.");
                continue;
            }

            if (op == "equals" && conditionValues.Count != 1)
            {
                errors.Add($"{contextId}.{field} : equals n'accepte qu'une valeur.");
            }

            foreach (var value in conditionValues)
            {
                if (!values.Contains(value))
                {
                    errors.Add(
                        $"{contextId}.{field} : la valeur {Describe(value)} n'existe pas dans {target}.");
                }
            }
        }
    }

    private static void RequireText(
        string? value,
        int minimum,
        int maximum,
        string field,
        List<string> errors)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length < minimum || trimmed.Length > maximum)
        {
            errors.Add($"{field} : texte requis entre {minimum} et {maximum} caracteres.");
        }
    }

    /// <summary>
    /// Tronque une valeur refusee : un message d'erreur ne rejoue jamais une
    /// charge entiere.
    /// </summary>
    private static string Describe(string value)
        => value.Length <= 40 ? $"\"{value}\"" : $"\"{value[..40]}…\"";
}

public sealed record DiagnosticConfigurationValidation(
    string? CanonicalJson,
    IReadOnlyList<string> Errors)
{
    public bool IsValid => CanonicalJson is not null;
}
