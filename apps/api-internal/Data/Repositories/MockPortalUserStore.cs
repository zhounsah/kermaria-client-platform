using System.Collections.Concurrent;

namespace Kermaria.ApiInternal.Data.Repositories;

/// <summary>
/// Utilisateurs portail simules, partages par les depots mock du cycle de vie
/// des utilisateurs additionnels.
/// </summary>
/// <remarks>
/// <para>
/// Le mode mock n'a pas de <c>portal_users</c>. Or deux depots distincts — le
/// cycle de vie d'identite, qui cree l'utilisateur, et les jetons de mot de
/// passe, qui ecrivent son condensat — doivent voir le meme utilisateur. Sans
/// magasin commun, le second ecrirait dans le vide et un test passerait sans
/// rien prouver.
/// </para>
/// <para>
/// L'unicite de l'e-mail est portee ici, comme l'index unique de
/// <c>portal_users.email</c> en base reelle : c'est ce qui rend le refus d'une
/// adresse deja prise verifiable hors MariaDB.
/// </para>
/// </remarks>
public sealed class MockPortalUserStore
{
    public sealed record Entry(
        string Id,
        string CustomerId,
        string Email,
        string DisplayName,
        string KoxoUniqueIdentifier,
        string? PasswordHash);

    private readonly ConcurrentDictionary<string, Entry> _byId =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _idByEmail =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<Entry> Entries => _byId.Values.ToArray();

    public Entry? Find(string portalUserId)
        => _byId.TryGetValue(portalUserId, out var entry) ? entry : null;

    public bool IsEmailTaken(string normalizedEmail)
        => _idByEmail.ContainsKey(normalizedEmail);

    /// <summary>
    /// Enregistre un utilisateur, ou <c>false</c> si l'adresse est deja prise.
    /// </summary>
    public bool TryAdd(Entry entry)
    {
        if (!_idByEmail.TryAdd(entry.Email, entry.Id))
        {
            return false;
        }

        if (_byId.TryAdd(entry.Id, entry))
        {
            return true;
        }

        _idByEmail.TryRemove(entry.Email, out _);
        return false;
    }

    public void Remove(string portalUserId)
    {
        if (_byId.TryRemove(portalUserId, out var entry))
        {
            _idByEmail.TryRemove(entry.Email, out _);
        }
    }

    /// <param name="passwordHash">
    /// Nul pour revenir a l'etat « sans mot de passe » : c'est ce que fait une
    /// annulation de transaction simulee.
    /// </param>
    public bool TrySetPasswordHash(string portalUserId, string? passwordHash)
    {
        if (!_byId.TryGetValue(portalUserId, out var entry))
        {
            return false;
        }

        return _byId.TryUpdate(
            portalUserId,
            entry with { PasswordHash = passwordHash },
            entry);
    }
}
