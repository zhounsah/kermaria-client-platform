using System.Collections.Concurrent;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Retient le mot de passe en clair le temps qu'il atteigne KoXo.
/// </summary>
/// <remarks>
/// Depuis que KoXo fait autorite sur l'annuaire, le mot de passe voyage par la
/// colonne 14 du CSV. Or l'export est un instantane complet regenere a la
/// demande, alors que le mot de passe n'existe en clair qu'a l'instant ou le
/// client le saisit. Il faut donc le retenir entre les deux.
///
/// <para>Volontairement <b>en memoire</b> et non en base : persister le mot de
/// passe en clair dans MariaDB creerait un magasin de secrets durable, alors
/// que le besoin ne dure que quelques secondes. Il est en outre a
/// <b>usage unique</b> et a duree bornee, pour qu'un export ulterieur ne le
/// republie pas indefiniment.</para>
///
/// <para><b>Limite assumee</b> : un redemarrage de l'API, ou un deploiement
/// multi-instances sans affinite, perd l'entree. Le mot de passe du portail
/// reste correct — seul l'alignement annuaire est manque — et l'expiration
/// sans consommation est journalisee en avertissement pour que la divergence
/// ne soit pas silencieuse.</para>
/// </remarks>
public interface IKoxoPendingPasswordStore
{
    /// <summary>Retient le mot de passe destine a cet utilisateur portail.</summary>
    void Publish(string portalUserId, string password);

    /// <summary>
    /// Rend le mot de passe en attente et le retire, ou <c>null</c> s'il n'y en
    /// a pas ou s'il a expire.
    /// </summary>
    string? Consume(string portalUserId);

    /// <summary>Identifiants dont le mot de passe a expire sans etre consomme.</summary>
    IReadOnlyList<string> DrainExpired();
}

public sealed class KoxoPendingPasswordStore : IKoxoPendingPasswordStore
{
    private sealed record Entry(string Password, DateTime ExpiresAtUtc);

    private readonly ConcurrentDictionary<string, Entry> _entries =
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

    public void Publish(string portalUserId, string password)
    {
        if (string.IsNullOrWhiteSpace(portalUserId)
            || string.IsNullOrEmpty(password))
        {
            return;
        }

        _entries[portalUserId] = new Entry(
            password,
            DateTime.UtcNow.Add(_lifetime));
    }

    public string? Consume(string portalUserId)
    {
        if (string.IsNullOrWhiteSpace(portalUserId)
            || !_entries.TryRemove(portalUserId, out var entry))
        {
            return null;
        }

        // Expire : on ne republie pas un mot de passe perime, mais on ne le
        // remet pas non plus en attente — l'entree est consommee dans tous les
        // cas, sinon un export tardif la ferait ressurgir.
        if (entry.ExpiresAtUtc <= DateTime.UtcNow)
        {
            _logger.LogWarning(
                "Pending KoXo password expired before export for portal_user_id {PortalUserId}; directory password left unchanged.",
                portalUserId);
            return null;
        }

        return entry.Password;
    }

    public IReadOnlyList<string> DrainExpired()
    {
        var now = DateTime.UtcNow;
        var expired = new List<string>();
        foreach (var pair in _entries)
        {
            if (pair.Value.ExpiresAtUtc <= now
                && _entries.TryRemove(pair.Key, out _))
            {
                expired.Add(pair.Key);
                _logger.LogWarning(
                    "Pending KoXo password expired before export for portal_user_id {PortalUserId}; directory password left unchanged.",
                    pair.Key);
            }
        }

        return expired;
    }
}
