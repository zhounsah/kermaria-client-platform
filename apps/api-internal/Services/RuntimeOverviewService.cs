using System.Reflection;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Vue runtime consolidee (specification, section 17).
///
/// Le but est de rendre l'exploitation comprehensible, pas d'exposer le contenu
/// brut du fichier de configuration : chaque ligne porte sa **source**, sa
/// classification et le fait qu'un redemarrage soit necessaire. Aucun secret
/// n'y figure, et la chaine de connexion n'est jamais renvoyee — ses composants
/// non sensibles le sont, un a un.
///
/// Tout y est en lecture seule. Ces reglages vivent avant le graphe DI : les
/// rendre mutables depuis une page web supposerait de recharger le processus,
/// ce que l'API ne sait pas faire sans redemarrage.
/// </summary>
public interface IRuntimeOverviewService
{
    Task<RuntimeOverview> GetAsync(CancellationToken cancellationToken);
}

public sealed class RuntimeOverviewService : IRuntimeOverviewService
{
    private static readonly DateTime StartedAtUtc = DateTime.UtcNow;

    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly SqlRuntimeConfiguration _sql;
    private readonly AdRuntimeConfiguration _ad;
    private readonly DownloadStorageRuntimeConfiguration _downloads;
    private readonly AuthRuntimeConfiguration _auth;
    private readonly ILogger<RuntimeOverviewService> _logger;

    public RuntimeOverviewService(
        IHostEnvironment environment,
        IConfiguration configuration,
        SqlRuntimeConfiguration sql,
        AdRuntimeConfiguration ad,
        DownloadStorageRuntimeConfiguration downloads,
        AuthRuntimeConfiguration auth,
        ILogger<RuntimeOverviewService> logger)
    {
        _environment = environment;
        _configuration = configuration;
        _sql = sql;
        _ad = ad;
        _downloads = downloads;
        _auth = auth;
        _logger = logger;
    }

    public async Task<RuntimeOverview> GetAsync(CancellationToken cancellationToken)
    {
        var configPath = Environment.GetEnvironmentVariable("KERMARIA_CONFIG_PATH")
            ?? @"C:\ProgramData\Kermaria\api-internal.config.json";
        var configPresent = FileExists(configPath);

        return new RuntimeOverview(
            _environment.EnvironmentName,
            Assembly.GetEntryAssembly()
                ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
                ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
                ?? "inconnue",
            configPath,
            configPresent,
            Iso(StartedAtUtc),
            (long)(DateTime.UtcNow - StartedAtUtc).TotalSeconds,
            [
                BuildApi(configPath, configPresent),
                await BuildDatabaseAsync(cancellationToken),
                BuildStorage(),
                BuildLogging(),
            ]);
    }

    private RuntimeSectionView BuildApi(string configPath, bool configPresent)
    {
        var parameters = new List<RuntimeParameterItem>
        {
            Item("ASPNETCORE_ENVIRONMENT", "Environnement", _environment.EnvironmentName, "code_invariant"),
            Item("KERMARIA_CONFIG_PATH", "Fichier de configuration", configPath, "restart_required"),
            Item(
                "configuration_file_present",
                "Fichier present",
                configPresent ? "Oui" : "Non",
                "code_invariant",
                source: "default"),
            Item("AD_INTEGRATION_MODE", "Mode annuaire", _ad.ModeName, "restart_required"),
            Item(
                "ad_configuration_valid",
                "Configuration annuaire",
                _ad.ConfigurationValid ? "Valide" : "Invalide",
                "code_invariant",
                source: "default"),
            Item(
                "AD_ALLOWED_ROOTS",
                "Racines annuaire autorisees",
                _ad.AllowedRoots.Count == 0
                    ? "Aucune"
                    : string.Join(" · ", _ad.AllowedRoots),
                "restart_required"),
            // Section 11.2 : les proprietes du cookie de session sont montrees,
            // pas editees. Les rendre modifiables permettrait de desactiver
            // `Secure` en production depuis une page web.
            Item(
                "SESSION_COOKIE_SECURE",
                "Cookie de session securise",
                string.Equals(
                    _configuration["SESSION_COOKIE_SECURE"]?.Trim(),
                    "false",
                    StringComparison.OrdinalIgnoreCase)
                    ? "Non"
                    : "Oui",
                "restart_required"),
            Item(
                "SESSION_DURATION_MINUTES",
                "Duree de session",
                $"{(int)_auth.SessionDuration.TotalMinutes} minutes",
                "restart_required"),
            Item(
                "LOGIN_MAX_FAILURES",
                "Echecs avant verrouillage",
                _auth.LoginMaxFailures.ToString(),
                "restart_required"),
        };

        var warning = configPresent
            ? _ad.ConfigurationValid
                ? null
                : "La configuration d'annuaire est invalide : les ecritures restent refusees."
            : "Aucun fichier de configuration a l'emplacement attendu : seules les variables d'environnement s'appliquent.";

        return new RuntimeSectionView(
            "api",
            "API-INTERNAL",
            warning is null ? "healthy" : "warning",
            warning,
            parameters);
    }

    private async Task<RuntimeSectionView> BuildDatabaseAsync(
        CancellationToken cancellationToken)
    {
        var parameters = new List<RuntimeParameterItem>
        {
            Item("SQL_PROVIDER", "Fournisseur", _sql.Provider, "restart_required"),
            Item(
                "persistence_mode",
                "Persistance",
                _sql.IsPersistent ? "MariaDB" : "Mock (memoire de processus)",
                "code_invariant",
                source: "default"),
            Item(
                "sql_status",
                "Etat declare",
                _sql.StatusReason,
                "code_invariant",
                source: "default"),
        };

        // La chaine de connexion n'est jamais renvoyee : ses composants non
        // sensibles sont extraits un a un, et le mot de passe reste une simple
        // presence.
        if (_sql.IsPersistent && !string.IsNullOrWhiteSpace(_sql.ConnectionString))
        {
            try
            {
                var builder = new MySqlConnectionStringBuilder(_sql.ConnectionString);
                parameters.Add(Item("SQL_HOST", "Hote", builder.Server, "restart_required"));
                parameters.Add(Item("SQL_PORT", "Port", builder.Port.ToString(), "restart_required"));
                parameters.Add(Item("SQL_DATABASE", "Base", builder.Database, "restart_required"));
                parameters.Add(Item("SQL_USERNAME", "Compte", builder.UserID, "restart_required"));
                parameters.Add(Item(
                    "SQL_PASSWORD",
                    "Mot de passe",
                    string.IsNullOrWhiteSpace(builder.Password) ? "Non configure" : "Configure",
                    "secret",
                    sensitive: true));
            }
            catch (ArgumentException exception)
            {
                _logger.LogError(exception, "Chaine de connexion SQL illisible.");
            }
        }

        var state = "info";
        string? warning = null;
        if (_sql.IsPersistent)
        {
            var probe = await ProbeDatabaseAsync(cancellationToken);
            parameters.Add(Item(
                "sql_connectivity",
                "Connectivite",
                probe.Reachable ? "Etablie" : "Indisponible",
                "code_invariant",
                source: "database"));
            parameters.Add(Item(
                "schema_version",
                "Derniere migration appliquee",
                probe.LastMigration ?? "Inconnue",
                "code_invariant",
                source: "database"));
            state = probe.Reachable ? "healthy" : "warning";
            warning = probe.Reachable ? null : probe.Message;
        }
        else if (!_environment.IsDevelopment())
        {
            state = "critical";
            warning = "Persistance mock hors developpement : aucune donnee n'est conservee.";
        }
        else
        {
            warning = "Persistance mock : l'etat disparait au redemarrage.";
        }

        return new RuntimeSectionView("database", "MariaDB", state, warning, parameters);
    }

    private RuntimeSectionView BuildStorage()
    {
        var accessible = false;
        string? warning = null;
        try
        {
            accessible = Directory.Exists(_downloads.RootPath);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Acces impossible a la racine de stockage.");
        }

        if (!accessible)
        {
            warning = "La racine de stockage n'est pas accessible : les telechargements echoueront.";
        }
        else if (!_downloads.IsExplicitlyConfigured)
        {
            warning = "Racine implicite : le stockage suit le repertoire de l'application, qui change a chaque deploiement.";
        }

        return new RuntimeSectionView(
            "storage",
            "Stockage des telechargements",
            accessible ? _downloads.IsExplicitlyConfigured ? "healthy" : "warning" : "warning",
            warning,
            [
                Item("DOWNLOAD_STORAGE_ROOT", "Racine", _downloads.RootPath, "restart_required",
                    source: _downloads.IsExplicitlyConfigured ? "environment" : "default"),
                Item(
                    "storage_access",
                    "Acces",
                    accessible ? "Disponible" : "Indisponible",
                    "code_invariant",
                    source: "default"),
            ]);
    }

    private RuntimeSectionView BuildLogging()
    {
        var directory = _configuration["LOG_FILE_DIRECTORY"]?.Trim();
        var fileEnabled = !string.IsNullOrWhiteSpace(directory);
        var level = _configuration["LOG_LEVEL"]?.Trim();

        var parameters = new List<RuntimeParameterItem>
        {
            Item("LOG_LEVEL", "Niveau global", string.IsNullOrWhiteSpace(level) ? "Information (defaut)" : level, "restart_required",
                source: string.IsNullOrWhiteSpace(level) ? "default" : Source("LOG_LEVEL")),
            Item(
                "log_file_enabled",
                "Journal fichier",
                fileEnabled ? "Active" : "Desactive",
                "restart_required",
                source: "default"),
        };

        if (fileEnabled)
        {
            var fileLevel = _configuration["LOG_FILE_LEVEL"]?.Trim();
            var retention = _configuration["LOG_FILE_RETENTION_DAYS"]?.Trim();
            parameters.Add(Item("LOG_FILE_DIRECTORY", "Repertoire", directory!, "restart_required"));
            parameters.Add(Item(
                "LOG_FILE_LEVEL",
                "Niveau fichier",
                string.IsNullOrWhiteSpace(fileLevel) ? "Suit le niveau global" : fileLevel,
                "restart_required",
                source: string.IsNullOrWhiteSpace(fileLevel) ? "default" : Source("LOG_FILE_LEVEL")));
            parameters.Add(Item(
                "LOG_FILE_RETENTION_DAYS",
                "Retention",
                string.IsNullOrWhiteSpace(retention) ? "30 jours (defaut)" : $"{retention} jours",
                "restart_required",
                source: string.IsNullOrWhiteSpace(retention) ? "default" : Source("LOG_FILE_RETENTION_DAYS")));
        }

        return new RuntimeSectionView(
            "logging",
            "Journalisation",
            "info",
            fileEnabled
                ? null
                : "Aucun journal fichier : seule la sortie console du service conserve une trace.",
            parameters);
    }

    private async Task<DatabaseProbe> ProbeDatabaseAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new MySqlConnection(_sql.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            // Lecture seule et tolerante, en deux temps : `schema_migrations`
            // peut ne pas exister sur une base jamais migree, ce qui n'est pas
            // une panne de connectivite. Interroger la table directement
            // echouerait alors et masquerait une base pourtant joignable. Le
            // compte applicatif n'a de toute facon aucun droit de schema.
            bool tablePresent;
            await using (var probe = connection.CreateCommand())
            {
                probe.CommandText =
                    """
                    SELECT COUNT(*)
                    FROM information_schema.tables
                    WHERE table_schema = DATABASE()
                      AND table_name = 'schema_migrations';
                    """;
                tablePresent = Convert.ToInt32(
                    await probe.ExecuteScalarAsync(cancellationToken)) > 0;
            }

            if (!tablePresent)
            {
                return new DatabaseProbe(true, null, null);
            }

            await using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    """
                    SELECT migration_id
                    FROM schema_migrations
                    ORDER BY migration_id DESC
                    LIMIT 1;
                    """;
                var scalar = await command.ExecuteScalarAsync(cancellationToken);
                return new DatabaseProbe(
                    true,
                    scalar is null or DBNull ? null : scalar.ToString(),
                    null);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Sonde de connectivite MariaDB en echec.");
            return new DatabaseProbe(
                false,
                null,
                "La base n'a pas repondu. Les operations persistantes echoueront.");
        }
    }

    private static bool FileExists(string path)
    {
        try
        {
            return File.Exists(path);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private RuntimeParameterItem Item(
        string key,
        string label,
        string value,
        string classification,
        string? source = null,
        bool sensitive = false)
        => new(
            key,
            label,
            value,
            source ?? Source(key),
            classification,
            classification is "restart_required" or "secret",
            sensitive,
            // Ces valeurs sont resolues au demarrage : leur derniere
            // modification connue est celle du processus courant.
            classification == "code_invariant" ? null : Iso(StartedAtUtc));

    // Distinguer une variable d'environnement d'une entree du fichier JSON
    // evite le piege documente : corriger un reglage a un seul des deux
    // endroits, et le voir revenir a la regeneration du fichier.
    private string Source(string key)
        => Environment.GetEnvironmentVariable(key) is not null
            ? "environment"
            : _configuration[key] is not null
                ? "json"
                : "default";

    private static string Iso(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ");

    private sealed record DatabaseProbe(
        bool Reachable,
        string? LastMigration,
        string? Message);
}
