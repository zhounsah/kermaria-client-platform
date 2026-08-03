namespace Kermaria.ApiInternal.Data.Repositories;

/// <summary>
/// Profil de demo tel que stocke (avec identifiant interne). Le registre est
/// administrable : la table <c>demo_profiles</c> est la source de verite.
/// </summary>
public sealed record DemoProfile(
    string Id,
    string Key,
    string Label,
    string Kind,
    string? ContentTemplateKey,
    string EmailMode,
    string BpceMode,
    string PaymentMode,
    string AdProvisioningMode,
    IReadOnlyList<string> AdGroups,
    int? StorageQuotaGo,
    string RdsSessionMode,
    int LifetimeDays,
    string Status);

/// <summary>CRUD du registre administrable des profils de demo (V1.1 Lot 2).</summary>
public interface IDemoProfileRepository
{
    bool IsPersistent { get; }

    Task<IReadOnlyList<DemoProfile>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<DemoProfile?> GetByKeyAsync(
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>Cree ou met a jour un profil par sa cle. Retourne le profil persiste.</summary>
    Task<DemoProfile> UpsertAsync(
        DemoProfile profile,
        CancellationToken cancellationToken = default);

    /// <summary>Supprime un profil par cle. Retourne false si absent.</summary>
    Task<bool> DeleteByKeyAsync(
        string key,
        CancellationToken cancellationToken = default);
}
