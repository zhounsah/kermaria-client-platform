using System.Globalization;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbKoxoRepository : IKoxoRepository
{
    private readonly string _connectionString;

    public MariaDbKoxoRepository(SqlRuntimeConfiguration configuration)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
    }

    public bool IsPersistent => true;

    public async Task<IReadOnlyList<KoxoExportCandidate>> ListExportCandidatesAsync(
        CancellationToken cancellationToken)
    {
        var items = new List<KoxoExportCandidate>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                portal_user.id AS portal_user_id,
                customer.external_reference AS customer_reference,
                portal_user.koxo_unique_identifier AS koxo_unique_identifier,
                portal_user.personal_title AS personal_title,
                portal_user.given_name AS given_name,
                portal_user.surname AS surname,
                portal_user.birth_date AS birth_date,
                portal_user.email AS email,
                customer.is_demo AS is_demo,
                customer.koxo_group_reference AS koxo_group_reference
            FROM portal_users portal_user
            INNER JOIN customers customer
                ON customer.id = portal_user.customer_id
            -- LEFT et non INNER : un essai de demonstration n'a pas encore
            -- d'identite AD, et c'est justement KoXo qui doit la creer. Avec une
            -- jointure stricte il serait exclu du CSV, donc jamais cree, donc
            -- toujours exclu — l'impasse qui laissait OU=CLI-DEMO vide.
            LEFT JOIN customer_ad_links ad_link
                ON ad_link.portal_user_id = portal_user.id
               AND ad_link.object_type = 'user'
            WHERE portal_user.status = 'active'
              AND customer.status = 'active'
              -- La regle stricte reste la norme : seuls les essais de demo sont
              -- exportes sans identite prealable, pour qu'aucun vrai client dont
              -- le provisioning AD a echoue ne parte dans le CSV par accident.
              --
              -- L'etat civil complet est exige pour ce cas : un compte incomplet
              -- serait rejete par la validation de l'export, or un seul invalide
              -- bloque l'export GLOBAL. Le laisser dehors le maintient en attente
              -- sans jamais casser la synchronisation des autres comptes.
              AND (
                    ad_link.portal_user_id IS NOT NULL
                 OR (
                        customer.is_demo = TRUE
                    AND customer.demo_kind = 'trial'
                    AND portal_user.personal_title IS NOT NULL
                    AND portal_user.given_name IS NOT NULL
                    AND portal_user.surname IS NOT NULL
                    AND portal_user.birth_date IS NOT NULL
                    AND portal_user.koxo_unique_identifier IS NOT NULL
                 )
              )
              -- Une vitrine est inerte par construction : elle ne doit jamais
              -- atteindre le pipeline d'identites reelles. Seuls les essais
              -- (trial), qui ont besoin d'une identite AD, sont exportes.
              AND NOT (customer.is_demo = TRUE AND customer.demo_kind = 'showcase')
            ORDER BY
                customer.external_reference ASC,
                portal_user.koxo_unique_identifier ASC,
                portal_user.id ASC;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new KoxoExportCandidate(
                MariaDbIdentifierReader.ReadRequired(reader, "portal_user_id"),
                reader.GetString("customer_reference"),
                ReadNullableString(reader, "koxo_unique_identifier"),
                ReadNullableString(reader, "personal_title"),
                ReadNullableString(reader, "given_name"),
                ReadNullableString(reader, "surname"),
                ReadNullableDate(reader, "birth_date"),
                reader.GetString("email"),
                reader.GetBoolean("is_demo"),
                ReadNullableString(reader, "koxo_group_reference")));
        }

        return items;
    }

    public async Task InsertRunAsync(
        KoxoRunInsert run,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO koxo_export_runs (
                id,
                source,
                status,
                schema_version,
                user_count,
                invalid_user_count,
                correlation_id,
                source_address,
                summary_message,
                generated_at,
                preview_json,
                validation_errors_json,
                created_at
            ) VALUES (
                @id,
                @source,
                @status,
                @schema_version,
                @user_count,
                @invalid_user_count,
                @correlation_id,
                @source_address,
                @summary_message,
                @generated_at,
                @preview_json,
                @validation_errors_json,
                UTC_TIMESTAMP(6)
            );
            """;
        command.Parameters.AddWithValue("@id", run.Id);
        command.Parameters.AddWithValue("@source", run.Source);
        command.Parameters.AddWithValue("@status", run.Status);
        command.Parameters.AddWithValue(
            "@schema_version",
            run.SchemaVersion is null ? DBNull.Value : run.SchemaVersion.Value);
        command.Parameters.AddWithValue("@user_count", run.UserCount);
        command.Parameters.AddWithValue("@invalid_user_count", run.InvalidUserCount);
        command.Parameters.AddWithValue("@correlation_id", run.CorrelationId);
        command.Parameters.AddWithValue(
            "@source_address",
            DbValue(run.SourceAddress));
        command.Parameters.AddWithValue("@summary_message", run.SummaryMessage);
        command.Parameters.AddWithValue(
            "@generated_at",
            run.GeneratedAtUtc is null ? DBNull.Value : run.GeneratedAtUtc.Value);
        command.Parameters.AddWithValue("@preview_json", DbValue(run.PreviewJson));
        command.Parameters.AddWithValue(
            "@validation_errors_json",
            DbValue(run.ValidationErrorsJson));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task<KoxoRunSummary?> GetLatestRunAsync(CancellationToken cancellationToken)
        => GetLatestRunCoreAsync(null, cancellationToken);

    public Task<KoxoRunSummary?> GetLatestRunBySourceAsync(
        string source,
        CancellationToken cancellationToken)
        => GetLatestRunCoreAsync(source, cancellationToken);

    private async Task<KoxoRunSummary?> GetLatestRunCoreAsync(
        string? source,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT
                created_at,
                source,
                status,
                schema_version,
                user_count,
                invalid_user_count,
                correlation_id,
                source_address,
                summary_message,
                generated_at
            FROM koxo_export_runs
            {(string.IsNullOrWhiteSpace(source) ? string.Empty : "WHERE source = @source")}
            ORDER BY created_at DESC
            LIMIT 1;
            """;
        if (!string.IsNullOrWhiteSpace(source))
        {
            command.Parameters.AddWithValue("@source", source);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new KoxoRunSummary(
            ToUtcIso(reader.GetDateTime("created_at")),
            reader.GetString("source"),
            reader.GetString("status"),
            reader.IsDBNull(reader.GetOrdinal("schema_version"))
                ? null
                : reader.GetInt32("schema_version"),
            reader.GetInt32("user_count"),
            reader.GetInt32("invalid_user_count"),
            reader.GetString("correlation_id"),
            ReadNullableString(reader, "source_address"),
            reader.GetString("summary_message"),
            reader.IsDBNull(reader.GetOrdinal("generated_at"))
                ? null
                : ToUtcIso(reader.GetDateTime("generated_at")));
    }

    private static string? ReadNullableString(
        MySqlDataReader reader,
        string columnName)
        => reader.IsDBNull(reader.GetOrdinal(columnName))
            ? null
            : reader.GetString(columnName);

    private static string? ReadNullableDate(
        MySqlDataReader reader,
        string columnName)
        => reader.IsDBNull(reader.GetOrdinal(columnName))
            ? null
            : reader.GetDateTime(columnName)
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static object DbValue(string? value)
        => value is null ? DBNull.Value : value;

    private static string ToUtcIso(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O");
}
