using Kermaria.ApiInternal.Data.Configuration;

namespace Kermaria.ApiInternal.Services.Provisioning;

public static class KoxoQualityGroupScope
{
    public static bool Allows(AdRuntimeConfiguration? scope,
        SubscriptionProvisioningRuntimeConfiguration configuration, IEnumerable<string> groups)
        => scope is { Mode: AdIntegrationMode.ControlledWrite, ConfigurationValid: true }
            && groups.All(group => configuration.TryGetGroupDistinguishedName(group, out var dn)
                && scope.IsWithinAllowedRoots(dn));
}
