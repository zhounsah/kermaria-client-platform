using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Retient le mot de passe destine a KoXo entre sa saisie et sa reprise.
/// </summary>
/// <remarks>
/// <para>
/// Depuis que KoXo fait autorite sur l'annuaire, le mot de passe voyage par la
/// colonne 14 du CSV. Or l'export est un instantane complet regenere a la
/// demande, alors que le mot de passe n'existe en clair qu'a l'instant ou la
/// personne le saisit. Il faut donc le retenir entre les deux.
/// </para>
/// <para>
/// <b>Lecture non destructive.</b> Un instantane <b>relit</b> le secret et ne
/// le retire pas : c'est <see cref="AcknowledgeAsync"/>, appele une fois le
/// lien annuaire attendu prouve, qui l'efface. Le retirer au premier
/// instantane — comportement d'origine — perdait le seul secret reversible du
/// systeme des que l'export echouait ensuite, ou que l'API redemarrait entre
/// les deux. Republier le meme mot de passe est sans effet de bord : KoXo
/// l'applique a l'identique.
/// </para>
/// <para>
/// L'expiration reste la seule autre sortie, pour les flux qui n'acquittent
/// pas explicitement (inscription). Elle est journalisee, jamais silencieuse.
/// </para>
/// </remarks>
public interface IKoxoPendingPasswordStore
{
    /// <summary>
    /// Vrai si le magasin peut reellement retenir un secret.
    /// </summary>
    /// <remarks>
    /// A interroger <b>avant</b> tout point de non-retour. Faux signifie que
    /// la configuration necessaire manque : mieux vaut refuser l'operation que
    /// consommer un jeton et decouvrir ensuite que le mot de passe n'atteindra
    /// jamais l'annuaire.
    /// </remarks>
    bool IsOperational { get; }

    /// <summary>
    /// Scelle le mot de passe, sans rien ecrire.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Separe de l'ecriture pour que celle-ci puisse avoir lieu dans la meme
    /// transaction que la consommation du jeton. Le clair ne franchit pas
    /// cette frontiere : ce qui en sort est deja un chiffre authentifie.
    /// </para>
    /// <para>
    /// <c>null</c> quand la configuration necessaire manque — l'appelant doit
    /// alors refuser avant tout point de non-retour.
    /// </para>
    /// </remarks>
    PortalPasswordSecret? Seal(string portalUserId, string password);

    /// <summary>Retient le mot de passe destine a cet utilisateur portail.</summary>
    /// <returns>Faux si le secret n'a pas pu etre retenu durablement.</returns>
    Task<bool> PublishAsync(
        string portalUserId,
        string password,
        CancellationToken cancellationToken);

    /// <summary>
    /// Relit le mot de passe en attente <b>sans le retirer</b>, ou
    /// <c>null</c> s'il n'y en a pas ou s'il a expire.
    /// </summary>
    Task<string?> PeekAsync(
        string portalUserId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Efface le secret, l'identite annuaire etant desormais etablie.
    /// </summary>
    Task AcknowledgeAsync(
        string portalUserId,
        CancellationToken cancellationToken);

    /// <summary>Identifiants dont le mot de passe a expire sans etre repris.</summary>
    Task<IReadOnlyList<string>> DrainExpiredAsync(
        CancellationToken cancellationToken);
}

/// <summary>
/// Magasin en memoire de processus.
/// </summary>
/// <remarks>
/// Reserve au mode mock et au developpement : il ne survit ni a un
/// redemarrage ni a une seconde instance. En persistance SQL c'est
/// <see cref="MariaDbKoxoPendingPasswordStore"/> qui est enregistre.
/// </remarks>
public sealed class KoxoPendingPasswordStore
    : IKoxoPendingPasswordStore, IKoxoPendingPasswordSealSink
{
    private sealed record Entry(string Password, DateTime ExpiresAtUtc);

    private readonly ConcurrentDictionary<string, Entry> _entries =
        new(StringComparer.Ordinal);
    // Scelles pas encore attaches. Le clair y attend, comme il attend dans une
    // transaction ouverte cote SQL : visible de personne tant que l'unite de
    // travail n'a pas abouti.
    private readonly ConcurrentDictionary<string, Entry> _staged =
        new(StringComparer.Ordinal);
    private readonly TimeSpan _lifetime;
    private readonly ILogger<KoxoPendingPasswordStore> _logger;

    public KoxoPendingPasswordStore(
        ILogger<KoxoPendingPasswordStore> logger,
        TimeSpan? lifetime = null)
    {
        _logger = logger;
        _lifetime = lifetime ?? TimeSpan.FromMinutes(15);
    }

    public bool IsOperational => true;

    /// <remarks>
    /// Sans cle en mode mock : le « chiffre » est une poignee opaque, et le
    /// clair reste dans le magasin. L'important est que la frontiere ait la
    /// meme forme qu'en production — rien de lisible n'en sort.
    /// </remarks>
    public PortalPasswordSecret? Seal(string portalUserId, string password)
    {
        if (string.IsNullOrWhiteSpace(portalUserId)
            || string.IsNullOrEmpty(password))
        {
            return null;
        }

        var handle = Guid.NewGuid().ToString("N");
        var expiresAt = DateTime.UtcNow.Add(_lifetime);
        _staged[handle] = new Entry(password, expiresAt);
        return new PortalPasswordSecret(handle, "mock", expiresAt);
    }

    public void AttachSealed(string portalUserId, PortalPasswordSecret secret)
    {
        if (!_staged.TryRemove(secret.Ciphertext, out var entry))
        {
            throw new InvalidOperationException(
                "Le scelle presente n'existe pas ou a deja ete attache.");
        }

        _entries[portalUserId] = entry;
    }

    public Task<bool> PublishAsync(
        string portalUserId,
        string password,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(portalUserId)
            || string.IsNullOrEmpty(password))
        {
            return Task.FromResult(false);
        }

        _entries[portalUserId] = new Entry(
            password,
            DateTime.UtcNow.Add(_lifetime));
        return Task.FromResult(true);
    }

    public Task<string?> PeekAsync(
        string portalUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(portalUserId)
            || !_entries.TryGetValue(portalUserId, out var entry))
        {
            return Task.FromResult<string?>(null);
        }

        if (entry.ExpiresAtUtc <= DateTime.UtcNow)
        {
            // Perime : on ne le republie pas, et on le retire pour qu'un
            // export tardif ne le fasse pas ressurgir.
            if (_entries.TryRemove(portalUserId, out _))
            {
                LogExpiry(portalUserId);
            }

            return Task.FromResult<string?>(null);
        }

        return Task.FromResult<string?>(entry.Password);
    }

    public Task AcknowledgeAsync(
        string portalUserId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(portalUserId))
        {
            _entries.TryRemove(portalUserId, out _);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> DrainExpiredAsync(
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var expired = new List<string>();
        foreach (var pair in _entries)
        {
            if (pair.Value.ExpiresAtUtc <= now
                && _entries.TryRemove(pair.Key, out _))
            {
                expired.Add(pair.Key);
                LogExpiry(pair.Key);
            }
        }

        return Task.FromResult<IReadOnlyList<string>>(expired);
    }

    private void LogExpiry(string portalUserId)
        => _logger.LogWarning(
            "Pending KoXo password expired before export for portal_user_id {PortalUserId}; directory password left unchanged.",
            portalUserId);
}

/// <summary>
/// Cle de chiffrement des mots de passe en attente.
/// </summary>
/// <remarks>
/// <para>
/// Lue dans <c>KOXO_PENDING_PASSWORD_KEY</c> : 32 octets en Base64, fournis
/// hors depot et hors configuration versionnee. Jamais de cle generee au
/// demarrage — elle ne survivrait pas au redemarrage qu'on cherche justement a
/// franchir, et rendrait toutes les lignes existantes indechiffrables.
/// </para>
/// <para>
/// <see cref="KeyId"/> est une empreinte courte, non secrete, stockee avec
/// chaque ligne : apres rotation, les lignes de l'ancienne cle sont
/// <b>ignorees</b> et signalees, au lieu d'etre dechiffrees en silence vers du
/// n'importe quoi.
/// </para>
/// </remarks>
public sealed class KoxoPendingPasswordProtector
{
    private const int NonceLength = 12;
    private const int TagLength = 16;
    private const int KeyLength = 32;

    private readonly byte[] _key;

    private KoxoPendingPasswordProtector(byte[] key)
    {
        _key = key;
        KeyId = Convert.ToHexString(SHA256.HashData(key))[..16].ToLowerInvariant();
    }

    public string KeyId { get; }

    /// <summary>
    /// Construit le protecteur, ou <c>null</c> si la cle manque ou est
    /// inutilisable. L'appelant doit alors echouer fermement.
    /// </summary>
    public static KoxoPendingPasswordProtector? TryCreate(string? base64Key)
    {
        if (string.IsNullOrWhiteSpace(base64Key))
        {
            return null;
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(base64Key.Trim());
        }
        catch (FormatException)
        {
            return null;
        }

        return key.Length == KeyLength
            ? new KoxoPendingPasswordProtector(key)
            : null;
    }

    /// <param name="associatedData">
    /// Lie le chiffre a sa ligne : deplacer un chiffre d'un utilisateur a un
    /// autre le rend indechiffrable, ce qui interdit d'attribuer le mot de
    /// passe d'une personne a une autre.
    /// </param>
    public string Protect(string plaintext, string associatedData)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceLength);
        var payload = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[payload.Length];
        var tag = new byte[TagLength];

        using var aes = new AesGcm(_key, TagLength);
        aes.Encrypt(
            nonce,
            payload,
            ciphertext,
            tag,
            Encoding.UTF8.GetBytes(associatedData));

        var envelope = new byte[NonceLength + TagLength + ciphertext.Length];
        nonce.CopyTo(envelope, 0);
        tag.CopyTo(envelope, NonceLength);
        ciphertext.CopyTo(envelope, NonceLength + TagLength);
        return Convert.ToBase64String(envelope);
    }

    /// <summary>
    /// Dechiffre, ou <c>null</c> si l'authentification echoue. Aucune
    /// exception ne remonte : un chiffre altere est un secret perdu, pas une
    /// panne de l'API.
    /// </summary>
    public string? Unprotect(string envelope, string associatedData)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(envelope);
        }
        catch (FormatException)
        {
            return null;
        }

        if (bytes.Length <= NonceLength + TagLength)
        {
            return null;
        }

        var nonce = bytes.AsSpan(0, NonceLength);
        var tag = bytes.AsSpan(NonceLength, TagLength);
        var ciphertext = bytes.AsSpan(NonceLength + TagLength);
        var plaintext = new byte[ciphertext.Length];

        try
        {
            using var aes = new AesGcm(_key, TagLength);
            aes.Decrypt(
                nonce,
                ciphertext,
                tag,
                plaintext,
                Encoding.UTF8.GetBytes(associatedData));
        }
        catch (CryptographicException)
        {
            return null;
        }

        return Encoding.UTF8.GetString(plaintext);
    }
}

/// <summary>
/// Magasin persistant et chiffre, utilise des que la persistance est reelle.
/// </summary>
/// <remarks>
/// <para>
/// Fail-closed : sans cle exploitable, <see cref="IsOperational"/> est faux et
/// <see cref="PublishAsync"/> refuse. Aucun repli en memoire, aucun repli en
/// clair — les deux transformeraient une configuration absente en perte
/// silencieuse du secret.
/// </para>
/// <para>
/// Le compteur de relectures n'est pas decoratif : il rend visible un cycle
/// KoXo qui ne converge jamais, cas qui restait invisible tant que le secret
/// disparaissait au premier instantane.
/// </para>
/// </remarks>
public sealed class MariaDbKoxoPendingPasswordStore : IKoxoPendingPasswordStore
{
    public const string KeyVariable = "KOXO_PENDING_PASSWORD_KEY";
    public const string LifetimeVariable = "KOXO_PENDING_PASSWORD_TTL_MINUTES";
    public const int DefaultLifetimeMinutes = 1440;
    public const int MinimumLifetimeMinutes = 15;

    private readonly string _connectionString;
    private readonly KoxoPendingPasswordProtector? _protector;
    private readonly TimeSpan _lifetime;
    private readonly ILogger<MariaDbKoxoPendingPasswordStore> _logger;

    public MariaDbKoxoPendingPasswordStore(
        SqlRuntimeConfiguration configuration,
        KoxoPendingPasswordProtector? protector,
        TimeSpan lifetime,
        ILogger<MariaDbKoxoPendingPasswordStore> logger)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
        _protector = protector;
        _lifetime = lifetime;
        _logger = logger;
    }

    /// <summary>
    /// Duree de retention : bien plus longue qu'en memoire, parce qu'un cycle
    /// KoXo reel peut prendre des heures et qu'un secret expire est perdu.
    /// </summary>
    public static TimeSpan ResolveLifetime(string? rawValue)
        => TimeSpan.FromMinutes(
            int.TryParse(rawValue, out var minutes)
            && minutes >= MinimumLifetimeMinutes
                ? minutes
                : DefaultLifetimeMinutes);

    public bool IsOperational => _protector is not null;

    public PortalPasswordSecret? Seal(string portalUserId, string password)
    {
        if (_protector is null
            || string.IsNullOrWhiteSpace(portalUserId)
            || string.IsNullOrEmpty(password))
        {
            return null;
        }

        return new PortalPasswordSecret(
            _protector.Protect(password, portalUserId),
            _protector.KeyId,
            DateTime.UtcNow.Add(_lifetime));
    }

    public async Task<bool> PublishAsync(
        string portalUserId,
        string password,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(portalUserId)
            || string.IsNullOrEmpty(password))
        {
            return false;
        }

        if (_protector is null)
        {
            _logger.LogError(
                "{Variable} is missing or invalid: the KoXo directory password handoff is disabled for portal_user_id {PortalUserId}.",
                KeyVariable,
                portalUserId);
            return false;
        }

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO koxo_pending_directory_passwords (
                portal_user_id, ciphertext, key_id, expires_at,
                published_count, created_at, updated_at
            ) VALUES (
                @portal_user_id, @ciphertext, @key_id, @expires_at,
                0, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
            )
            ON DUPLICATE KEY UPDATE
                ciphertext = VALUES(ciphertext),
                key_id = VALUES(key_id),
                expires_at = VALUES(expires_at),
                published_count = 0,
                last_published_at = NULL,
                updated_at = UTC_TIMESTAMP(6);
            """;
        command.Parameters.AddWithValue("@portal_user_id", portalUserId);
        command.Parameters.AddWithValue(
            "@ciphertext",
            _protector.Protect(password, portalUserId));
        command.Parameters.AddWithValue("@key_id", _protector.KeyId);
        command.Parameters.AddWithValue(
            "@expires_at",
            DateTime.UtcNow.Add(_lifetime));
        await command.ExecuteNonQueryAsync(cancellationToken);
        return true;
    }

    public async Task<string?> PeekAsync(
        string portalUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(portalUserId) || _protector is null)
        {
            return null;
        }

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        string ciphertext;
        string keyId;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT ciphertext, key_id
                FROM koxo_pending_directory_passwords
                WHERE portal_user_id = @portal_user_id
                  AND expires_at > UTC_TIMESTAMP(6);
                """;
            command.Parameters.AddWithValue("@portal_user_id", portalUserId);
            await using var reader = await command.ExecuteReaderAsync(
                cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            ciphertext = reader.GetString("ciphertext");
            keyId = reader.GetString("key_id");
        }

        if (!string.Equals(keyId, _protector.KeyId, StringComparison.Ordinal))
        {
            // Rotation de cle : la ligne appartient a une cle qu'on n'a plus.
            // La deviner produirait un mot de passe faux applique a un compte
            // reel — on la laisse expirer en le disant.
            _logger.LogError(
                "Pending KoXo password for portal_user_id {PortalUserId} was sealed with key {KeyId} and cannot be read by the current key.",
                portalUserId,
                keyId);
            return null;
        }

        var password = _protector.Unprotect(ciphertext, portalUserId);
        if (password is null)
        {
            _logger.LogError(
                "Pending KoXo password for portal_user_id {PortalUserId} failed authentication and was not republished.",
                portalUserId);
            return null;
        }

        await using (var touch = connection.CreateCommand())
        {
            touch.CommandText =
                """
                UPDATE koxo_pending_directory_passwords
                SET published_count = published_count + 1,
                    last_published_at = UTC_TIMESTAMP(6),
                    updated_at = UTC_TIMESTAMP(6)
                WHERE portal_user_id = @portal_user_id;
                """;
            touch.Parameters.AddWithValue("@portal_user_id", portalUserId);
            await touch.ExecuteNonQueryAsync(cancellationToken);
        }

        return password;
    }

    public async Task AcknowledgeAsync(
        string portalUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(portalUserId))
        {
            return;
        }

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            DELETE FROM koxo_pending_directory_passwords
            WHERE portal_user_id = @portal_user_id;
            """;
        command.Parameters.AddWithValue("@portal_user_id", portalUserId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> DrainExpiredAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var expired = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT portal_user_id
                FROM koxo_pending_directory_passwords
                WHERE expires_at <= UTC_TIMESTAMP(6);
                """;
            await using var reader = await command.ExecuteReaderAsync(
                cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                expired.Add(reader.GetValue(0)?.ToString() ?? string.Empty);
            }
        }

        if (expired.Count == 0)
        {
            return expired;
        }

        await using (var delete = connection.CreateCommand())
        {
            delete.CommandText =
                """
                DELETE FROM koxo_pending_directory_passwords
                WHERE expires_at <= UTC_TIMESTAMP(6);
                """;
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var portalUserId in expired)
        {
            _logger.LogWarning(
                "Pending KoXo password expired before export for portal_user_id {PortalUserId}; directory password left unchanged.",
                portalUserId);
        }

        return expired;
    }
}
