using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Services;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Migration;

/// <summary>
/// Interactive bootstrap of the first internal_admin portal user, usable
/// outside Development (staging/prod) via the --seed-admin CLI flag.
///
/// The command is interactive by design: it prompts for email, display
/// name, and password on stdin so no credential ever transits through
/// command-line args (visible in Get-Process, event logs, etc.).
///
/// If no customer row exists yet, a sentinel "Kermaria Internal" customer
/// is created first so the FK constraint on portal_users.customer_id is
/// satisfied. Existing customers are reused (first by created_at).
///
/// Refuses to create a duplicate user with the same email (case-insensitive).
/// </summary>
public sealed class MariaDbAdminSeeder
{
    private const string SentinelCustomerReference = "INTERNAL";
    private const int MinPasswordLength = 12;

    private readonly SqlRuntimeConfiguration _configuration;
    private readonly IPortalPasswordService _passwordService;
    private readonly ILogger<MariaDbAdminSeeder> _logger;

    public MariaDbAdminSeeder(
        SqlRuntimeConfiguration configuration,
        IPortalPasswordService passwordService,
        ILogger<MariaDbAdminSeeder> logger)
    {
        _configuration = configuration;
        _passwordService = passwordService;
        _logger = logger;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        if (!_configuration.IsPersistent
            || string.IsNullOrWhiteSpace(_configuration.ConnectionString))
        {
            Console.Error.WriteLine(
                "MariaDB doit être configurée pour utiliser --seed-admin.");
            return 2;
        }

        Console.Write("Email de l'admin : ");
        var email = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            Console.Error.WriteLine("Email invalide, abandon.");
            return 3;
        }

        Console.Write("Nom d'affichage : ");
        var displayName = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = email;
        }

        Console.Write($"Mot de passe (>= {MinPasswordLength} caractères) : ");
        var password = ReadPasswordMasked();
        Console.WriteLine();
        Console.Write("Confirmation : ");
        var confirm = ReadPasswordMasked();
        Console.WriteLine();

        if (password != confirm)
        {
            Console.Error.WriteLine("Les mots de passe ne correspondent pas.");
            return 4;
        }
        if (password.Length < MinPasswordLength)
        {
            Console.Error.WriteLine(
                $"Le mot de passe doit faire au moins {MinPasswordLength} caractères.");
            return 5;
        }

        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var existingLookup = connection.CreateCommand();
        existingLookup.CommandText =
            "SELECT id FROM portal_users WHERE LOWER(email) = @email LIMIT 1;";
        existingLookup.Parameters.AddWithValue(
            "@email",
            email.ToLowerInvariant());
        var existingId = (
            await existingLookup.ExecuteScalarAsync(cancellationToken))?.ToString();
        if (!string.IsNullOrWhiteSpace(existingId))
        {
            Console.Error.WriteLine(
                $"Un utilisateur avec l'email {email} existe déjà (id {existingId}).");
            return 6;
        }

        var customerId = await EnsureCustomerAsync(connection, cancellationToken);
        var userId = Guid.NewGuid().ToString("D");
        var passwordHash = _passwordService.HashPassword(userId, password);
        var now = DateTime.UtcNow;

        await using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText =
            """
            INSERT INTO portal_users (
                id, customer_id, identity_provider_subject, email,
                password_hash, display_name, status, role,
                created_at, updated_at
            ) VALUES (
                @id, @customer_id, @subject, @email,
                @password_hash, @display_name, 'active', @role,
                @created_at, @updated_at
            );
            """;
        insertCommand.Parameters.AddWithValue("@id", userId);
        insertCommand.Parameters.AddWithValue("@customer_id", customerId);
        insertCommand.Parameters.AddWithValue(
            "@subject",
            $"local:{email.ToLowerInvariant()}");
        insertCommand.Parameters.AddWithValue(
            "@email",
            email.ToLowerInvariant());
        insertCommand.Parameters.AddWithValue("@password_hash", passwordHash);
        insertCommand.Parameters.AddWithValue("@display_name", displayName);
        insertCommand.Parameters.AddWithValue(
            "@role",
            PortalRoles.InternalAdmin);
        insertCommand.Parameters.AddWithValue("@created_at", now);
        insertCommand.Parameters.AddWithValue("@updated_at", now);

        await insertCommand.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogInformation(
            "Seeded internal_admin portal user {UserId} for customer {CustomerId}",
            userId,
            customerId);

        Console.WriteLine();
        Console.WriteLine("Compte admin créé.");
        Console.WriteLine($"  id           : {userId}");
        Console.WriteLine($"  email        : {email}");
        Console.WriteLine($"  customer_id  : {customerId}");
        Console.WriteLine($"  role         : {PortalRoles.InternalAdmin}");
        return 0;
    }

    private static async Task<string> EnsureCustomerAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var lookup = connection.CreateCommand();
        lookup.CommandText =
            "SELECT id FROM customers ORDER BY created_at LIMIT 1;";
        var existing = (
            await lookup.ExecuteScalarAsync(cancellationToken))?.ToString();
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        var customerId = Guid.NewGuid().ToString("D");
        var now = DateTime.UtcNow;
        await using var insert = connection.CreateCommand();
        insert.CommandText =
            """
            INSERT INTO customers (
                id, external_reference, display_name, status,
                created_at, updated_at
            ) VALUES (
                @id, @ref, @name, 'active', @created_at, @updated_at
            );
            """;
        insert.Parameters.AddWithValue("@id", customerId);
        insert.Parameters.AddWithValue("@ref", SentinelCustomerReference);
        insert.Parameters.AddWithValue("@name", "Kermaria Internal");
        insert.Parameters.AddWithValue("@created_at", now);
        insert.Parameters.AddWithValue("@updated_at", now);
        await insert.ExecuteNonQueryAsync(cancellationToken);
        return customerId;
    }

    private static string ReadPasswordMasked()
    {
        var buffer = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                break;
            }
            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Length > 0)
                {
                    buffer.Length--;
                    Console.Write("\b \b");
                }
                continue;
            }
            if (char.IsControl(key.KeyChar))
            {
                continue;
            }
            buffer.Append(key.KeyChar);
            Console.Write('*');
        }
        return buffer.ToString();
    }
}
