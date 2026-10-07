namespace Kermaria.ApiInternal.Services;

public sealed class SiteFeatureSchemaUnavailableException : Exception
{
    public SiteFeatureSchemaUnavailableException(string feature)
        : base($"Le schéma MariaDB requis pour {feature} n'est pas disponible.")
    {
    }
}
