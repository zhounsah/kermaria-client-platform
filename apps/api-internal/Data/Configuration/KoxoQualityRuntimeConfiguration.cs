using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Configuration;

public sealed record KoxoQualityRuntimeConfiguration(bool Enabled, string? EmptyGroup = null)
{
    public const string Variable = "BILLING_V2_KOXO_QUALITIES_ENABLED";

    public static KoxoQualityRuntimeConfiguration Resolve(IConfiguration configuration,
        SqlRuntimeConfiguration sql, KoxoSyncWebhookRuntimeConfiguration webhook, bool provisioningEnabled,
        bool controlledAdWrites)
    {
        var value = configuration[Variable];
        if (string.IsNullOrWhiteSpace(value)) return new(false);
        if (!bool.TryParse(value, out var enabled)) throw new InvalidOperationException("KOXO_QUALITIES_ENABLED_INVALID");
        if (!enabled) return new(false);
        if (!string.Equals(configuration["APP_ENV"], "Development", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("KOXO_QUALITIES_DEV_ONLY");
        if (!sql.IsPersistent || string.IsNullOrWhiteSpace(sql.ConnectionString))
            throw new InvalidOperationException("KOXO_QUALITIES_MARIADB_REQUIRED");
        var database = new MySqlConnectionStringBuilder(sql.ConnectionString).Database;
        if (database != "kermaria_dev") throw new InvalidOperationException("KOXO_QUALITIES_WRONG_DATABASE");
        if (!provisioningEnabled) throw new InvalidOperationException("KOXO_QUALITIES_PROVISIONING_DISABLED");
        if (!controlledAdWrites) throw new InvalidOperationException("KOXO_QUALITIES_CONTROLLED_AD_REQUIRED");
        if (!webhook.Enabled || webhook.Url is null || webhook.Url.Port != 8043
            || webhook.Url.AbsolutePath.TrimEnd('/') != "/internal/koxo/sync")
            throw new InvalidOperationException("KOXO_QUALITIES_DEV_RECEIVER_REQUIRED");
        var emptyGroup = configuration["BILLING_V2_KOXO_EMPTY_QUALITY_GROUP"];
        if (string.IsNullOrWhiteSpace(emptyGroup)) emptyGroup = null;
        if (emptyGroup is not null && !System.Text.RegularExpressions.Regex.IsMatch(emptyGroup, @"\AGG_[A-Z0-9_]+_DEV\z"))
            throw new InvalidOperationException("KOXO_QUALITIES_EMPTY_GROUP_INVALID");
        return new(true, emptyGroup);
    }
}
