using System.Text;
using System.Text.Json;
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

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]{0,63}$")]
    private static partial Regex OptionValuePattern();

    [GeneratedRegex("^[A-Z][A-Z0-9-]{2,63}$")]
    private static partial Regex RuleIdPattern();

    public static JsonSerializerOptions SerializerOptions { get; } =
        new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Valide et canonicalise une configuration. La sortie est reserialisee a
    /// partir du modele : un champ inconnu envoye par un client ne peut pas se
    /// retrouver stocke en base.
    /// </summary>
    public static DiagnosticConfigurationValidation Validate(JsonElement payload)
    {
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
