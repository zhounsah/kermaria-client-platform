using System.Text.RegularExpressions;
using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Services;

public static class BackupProtectionStatuses
{
    public const string Protected = "protected";
    public const string Warning = "warning";
    public const string Critical = "critical";
    public const string Unknown = "unknown";
}

public interface IBackupProtectionService
{
    string NormalizeProvider(string? provider);
    string NormalizeResult(string? result);
    string PublicResultLabel(string? result);
    string PublicProtectionLabel(string protectionStatus);
    string ComputeProtectionStatus(
        DateTime nowUtc,
        DateTime? lastSuccessAtUtc,
        string? lastResult,
        DateTime? collectedAtUtc,
        int expectedIntervalMinutes,
        int criticalAfterMinutes,
        int staleAfterMinutes);
    BackupReportPayload NormalizeReport(BackupReportPayload payload);
    BackupIntegrationPayload NormalizeIntegration(
        BackupIntegrationPayload payload);
    BackupRestoreRequestPayload NormalizeRestoreRequest(
        BackupRestoreRequestPayload payload);
}

public sealed class BackupProtectionService : IBackupProtectionService
{
    private static readonly Regex TechnicalTokenPattern = new(
        @"(\\\\|(?:\d{1,3}\.){3}\d{1,3}|[A-Z0-9-]{3,}\$?|repository|srv-\d+|veeam)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public string NormalizeProvider(string? provider)
    {
        var value = provider?.Trim().ToLowerInvariant();
        return value is "veeam" ? value : throw new PortalValidationException();
    }

    public string NormalizeResult(string? result)
    {
        var value = result?.Trim().ToLowerInvariant();
        return value switch
        {
            "success" or "succeeded" => "success",
            "warning" or "warnings" => "warning",
            "failed" or "failure" or "error" => "failed",
            "running" or "working" => "running",
            "stopped" or "cancelled" or "canceled" => "failed",
            _ => "unknown"
        };
    }

    public string PublicResultLabel(string? result)
        => NormalizeResult(result) switch
        {
            "success" => "Sauvegarde reussie",
            "warning" => "Sauvegarde terminee avec avertissement",
            "failed" => "Sauvegarde en echec",
            "running" => "Sauvegarde en cours",
            _ => "Etat de sauvegarde inconnu"
        };

    public string PublicProtectionLabel(string protectionStatus)
        => protectionStatus switch
        {
            BackupProtectionStatuses.Protected => "Protege",
            BackupProtectionStatuses.Warning => "Attention",
            BackupProtectionStatuses.Critical => "Protection interrompue",
            _ => "Etat inconnu"
        };

    public string ComputeProtectionStatus(
        DateTime nowUtc,
        DateTime? lastSuccessAtUtc,
        string? lastResult,
        DateTime? collectedAtUtc,
        int expectedIntervalMinutes,
        int criticalAfterMinutes,
        int staleAfterMinutes)
    {
        if (collectedAtUtc is null)
        {
            return BackupProtectionStatuses.Unknown;
        }

        if (nowUtc - collectedAtUtc.Value
            > TimeSpan.FromMinutes(Math.Max(1, staleAfterMinutes)))
        {
            return BackupProtectionStatuses.Unknown;
        }

        var normalizedResult = NormalizeResult(lastResult);
        if (lastSuccessAtUtc is null)
        {
            return normalizedResult == "warning"
                ? BackupProtectionStatuses.Warning
                : BackupProtectionStatuses.Unknown;
        }

        if (nowUtc - lastSuccessAtUtc.Value
            > TimeSpan.FromMinutes(Math.Max(1, criticalAfterMinutes)))
        {
            return BackupProtectionStatuses.Critical;
        }

        if (normalizedResult == "warning")
        {
            return BackupProtectionStatuses.Warning;
        }

        if (normalizedResult == "failed")
        {
            var expectedWindow = TimeSpan.FromMinutes(
                Math.Max(1, expectedIntervalMinutes));
            return nowUtc - lastSuccessAtUtc.Value > expectedWindow
                ? BackupProtectionStatuses.Warning
                : BackupProtectionStatuses.Protected;
        }

        return BackupProtectionStatuses.Protected;
    }

    public BackupReportPayload NormalizeReport(BackupReportPayload payload)
    {
        var provider = NormalizeProvider(payload.Provider);
        var externalJobId = NormalizeIdentifier(payload.ExternalJobId, 160);
        var externalSessionId = NormalizeIdentifier(
            payload.ExternalSessionId,
            160);
        var result = NormalizeResult(payload.Result);

        if (payload.StartedAt is null)
        {
            throw new PortalValidationException();
        }

        return payload with
        {
            Provider = provider,
            ExternalJobId = externalJobId,
            ExternalSessionId = externalSessionId,
            Result = result,
            PublicMessage = SanitizePublicMessage(payload.PublicMessage)
        };
    }

    public BackupIntegrationPayload NormalizeIntegration(
        BackupIntegrationPayload payload)
    {
        return payload with
        {
            Id = string.IsNullOrWhiteSpace(payload.Id)
                ? null
                : NormalizeIdentifier(payload.Id, 100),
            Provider = NormalizeProvider(payload.Provider),
            ExternalJobId = NormalizeIdentifier(payload.ExternalJobId, 160),
            CustomerId = NormalizeIdentifier(payload.CustomerId, 100),
            ServiceId = NormalizeIdentifier(payload.ServiceId, 100),
            ExpectedIntervalMinutes = NormalizeThreshold(
                payload.ExpectedIntervalMinutes,
                60,
                7 * 24 * 60),
            CriticalAfterMinutes = NormalizeThreshold(
                payload.CriticalAfterMinutes,
                60,
                14 * 24 * 60),
            StaleAfterMinutes = NormalizeThreshold(
                payload.StaleAfterMinutes,
                15,
                7 * 24 * 60)
        };
    }

    public BackupRestoreRequestPayload NormalizeRestoreRequest(
        BackupRestoreRequestPayload payload)
    {
        var itemPath = NormalizeOptionalText(payload.ItemPath, 300);
        var description = NormalizeOptionalText(payload.Description, 2000);
        var priority = payload.Priority?.Trim().ToLowerInvariant();

        if (priority is not ("low" or "normal" or "high"))
        {
            throw new PortalValidationException();
        }

        if (string.IsNullOrWhiteSpace(itemPath)
            && string.IsNullOrWhiteSpace(description))
        {
            throw new PortalValidationException();
        }

        return payload with
        {
            ItemPath = itemPath,
            Description = description,
            Priority = priority
        };
    }

    public static string SanitizePublicMessage(string? value)
    {
        var normalized = NormalizeOptionalText(value, 280);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return TechnicalTokenPattern.IsMatch(normalized)
            ? "Un avertissement technique a ete detecte. Contactez le support si besoin."
            : normalized;
    }

    private static string NormalizeIdentifier(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)
            || normalized.Length > maxLength)
        {
            throw new PortalValidationException();
        }

        foreach (var character in normalized)
        {
            if (!char.IsAsciiLetterOrDigit(character)
                && character is not '-' and not '_' and not '.' and not ':')
            {
                throw new PortalValidationException();
            }
        }

        return normalized;
    }

    private static string NormalizeOptionalText(string? value, int maxLength)
    {
        var normalized = (value ?? string.Empty)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }

    private static int NormalizeThreshold(
        int? value,
        int minimum,
        int maximum)
    {
        if (value is null or < 1 || value < minimum || value > maximum)
        {
            throw new PortalValidationException();
        }

        return value.Value;
    }
}
