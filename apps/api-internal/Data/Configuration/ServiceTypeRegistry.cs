namespace Kermaria.ApiInternal.Data.Configuration;

/// <summary>
/// Registre ferme des types de service connus de la plateforme.
///
/// Il existe pour qu'aucune surface d'administration ne puisse introduire un
/// type que le code ne sait ni provisionner ni cibler. Ajouter un type ici est
/// un acte de code, jamais une saisie.
/// </summary>
public static class ServiceTypeRegistry
{
    public static readonly IReadOnlySet<string> Known =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "personal_hosting",
            "storage",
            "backup",
            "vpn",
            "rds",
            "support",
            "cloud",
            "documentation",
            "monitoring",
            "user",
            "other"
        };

    public static bool Contains(string? value)
        => value is not null && Known.Contains(value);
}
