using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Services.Provisioning;

namespace Kermaria.ApiInternal.SmokeTests;

public static class BillingV2ManagedMembershipReconciliationTests
{
    public static async Task RunAsync()
    {
        await VerifyAddNoopAndRemoveAsync();
        await VerifyManualAndSharedMembershipsAsync();
        await VerifyRuleChangeAndInactiveHistoryAsync();
        VerifyLifecyclePolicy();
        VerifyCustomerAndEnvironmentIsolation();
        Console.WriteLine("Tests reconciliation des memberships Billing V2 reussis.");
    }

    private static async Task VerifyAddNoopAndRemoveAsync()
    {
        var added = await ExecuteAsync(["GROUP_DEV"], ["GROUP_DEV"], []);
        Ensure(added.Added.SequenceEqual(["GROUP_DEV"]), "Ajout attendu.");

        var stable = await ExecuteAsync(["GROUP_DEV"], ["GROUP_DEV"], ["GROUP_DEV"]);
        Ensure(stable.Added.Count == 0 && stable.Removed.Count == 0, "Noop attendu.");

        var removed = await ExecuteAsync([], ["GROUP_DEV"], ["GROUP_DEV"]);
        Ensure(removed.Removed.SequenceEqual(["GROUP_DEV"]), "Retrait apres resiliation attendu.");
    }

    private static async Task VerifyManualAndSharedMembershipsAsync()
    {
        var manual = await ExecuteAsync([], ["GROUP_DEV"], ["GROUP_DEV", "MANUAL_GROUP"]);
        Ensure(manual.Removed.SequenceEqual(["GROUP_DEV"])
            && manual.Memberships.Contains("MANUAL_GROUP"), "Un groupe manuel ne doit jamais etre touche.");

        var shared = await ExecuteAsync(["GROUP_DEV"], ["GROUP_DEV"], ["GROUP_DEV"]);
        Ensure(shared.Removed.Count == 0, "Un entitlement encore actif conserve le groupe partage.");
    }

    private static async Task VerifyRuleChangeAndInactiveHistoryAsync()
    {
        var changed = await ExecuteAsync(["GROUP_NEW_DEV"], ["GROUP_OLD_DEV"], ["GROUP_OLD_DEV"]);
        Ensure(changed.Added.SequenceEqual(["GROUP_NEW_DEV"])
            && changed.Removed.SequenceEqual(["GROUP_OLD_DEV"]), "Changement ancien vers nouveau attendu.");

        var inactive = await ExecuteAsync([], ["GROUP_OLD_DEV"], ["GROUP_OLD_DEV"]);
        Ensure(inactive.Removed.SequenceEqual(["GROUP_OLD_DEV"]), "Une regle inactive conserve l'autorite historique de retrait.");
    }

    private static void VerifyLifecyclePolicy()
    {
        Ensure(
            BillingV2ProvisioningLifecyclePolicy.CanReconcile(
                requireActiveTrigger: true,
                triggeringSubscriptionActive: true,
                ownedMembershipCount: 0),
            "Une activation active reste eligible a la convergence.");
        Ensure(
            !BillingV2ProvisioningLifecyclePolicy.CanReconcile(
                requireActiveTrigger: true,
                triggeringSubscriptionActive: false,
                ownedMembershipCount: 1),
            "Une activation deja terminale ne doit pas rejouer le chemin d ajout.");
        Ensure(
            BillingV2ProvisioningLifecyclePolicy.CanReconcile(
                requireActiveTrigger: false,
                triggeringSubscriptionActive: false,
                ownedMembershipCount: 1),
            "Une desactivation terminale avec ownership doit converger vers le retrait.");
        Ensure(
            !BillingV2ProvisioningLifecyclePolicy.CanReconcile(
                requireActiveTrigger: false,
                triggeringSubscriptionActive: true,
                ownedMembershipCount: 1),
            "Le deprovisioning ne doit pas partir avant la fermeture locale de la souscription.");
        Ensure(
            !BillingV2ProvisioningLifecyclePolicy.CanReconcile(
                requireActiveTrigger: false,
                triggeringSubscriptionActive: false,
                ownedMembershipCount: 0),
            "Sans membership possede par Billing V2, une desactivation ne doit toucher aucun groupe.");
    }

    private static void VerifyCustomerAndEnvironmentIsolation()
    {
        var customerA = Requests([], new Dictionary<string, IReadOnlyList<string>>
        { ["identity-a"] = ["GROUP_DEV"] }, "identity-a");
        var customerB = Requests([], new Dictionary<string, IReadOnlyList<string>>
        { ["identity-b"] = ["GROUP_OTHER_DEV"] }, "identity-a");
        Ensure(customerA.Single().ManagedGroupSamAccountNames.SequenceEqual(["GROUP_DEV"])
            && customerB.Count == 0, "Aucune autorite d'un autre client ne doit traverser l'identite.");
        Ensure(DeploymentEnvironmentGuard.TryValidateAdGroupTarget(DeploymentEnvironment.Development, "GG_SERVICE_E2E_DEV", out _)
            && !DeploymentEnvironmentGuard.TryValidateAdGroupTarget(DeploymentEnvironment.Development, "GG_VPN", out _)
            && !DeploymentEnvironmentGuard.TryValidateAdGroupTarget(DeploymentEnvironment.Production, "GG_SERVICE_E2E_DEV", out _),
            "Les gardes DEV/PROD restent applicables.");
    }

    private static async Task<Execution> ExecuteAsync(IReadOnlyList<string> desired, IReadOnlyList<string> owned, IReadOnlyList<string> current)
    {
        var provisioner = new RecordingProvisioner(current);
        var service = new ProvisioningService(provisioner, new SubscriptionProvisioningRuntimeConfiguration(
            new Dictionary<string, IReadOnlyList<string>>(), new Dictionary<string, string>(),
            new Dictionary<string, string>(), 1, 0));
        foreach (var request in Requests(desired, new Dictionary<string, IReadOnlyList<string>> { ["identity-a"] = owned }, "identity-a"))
            await service.ReconcileAsync(request, CancellationToken.None);
        return new Execution(provisioner.Added, provisioner.Removed, provisioner.Memberships);
    }

    private static IReadOnlyList<ProvisioningExecutionRequest> Requests(IReadOnlyList<string> desired, IReadOnlyDictionary<string, IReadOnlyList<string>> owned, string identity)
        => BillingV2ProvisioningExecutionPlanner.BuildPerUserRequests(
            BillingV2ProvisioningGateDecision.Allow(addOnlyMode: true),
            [new BillingV2ResolvedProvisioningTarget(new BillingV2UserDesiredState("user-a", identity, desired, null, [], []), User())],
            desired.Concat(owned.SelectMany(entry => entry.Value)).Distinct().ToDictionary(group => group, group => (string?)"CN=" + group + ",OU=CLIENTS DEV,DC=clients,DC=home,DC=bzh"),
            owned);

    private static CustomerAdLinkSummary User() => new("link", "customer-a", "11111111-1111-1111-1111-111111111111", "S-1-5-21-1", "user", "melis", null, "Melis", "CN=Melis,OU=CLIENTS DEV,DC=clients,DC=home,DC=bzh", "now", null);
    private sealed record Execution(IReadOnlyList<string> Added, IReadOnlyList<string> Removed, IReadOnlySet<string> Memberships);
    private sealed class RecordingProvisioner(IReadOnlyList<string> current) : IAdGroupProvisioner
    {
        public HashSet<string> Memberships { get; } = new(current, StringComparer.OrdinalIgnoreCase);
        public List<string> Added { get; } = []; public List<string> Removed { get; } = [];
        public string ModeName => "test"; public bool RequiresConfiguredGroupDistinguishedNames => true;
        public Task<AdDirectoryObjectSummary?> ResolveUserByEmployeeNumberAsync(string employeeNumber, CancellationToken cancellationToken) => Task.FromResult<AdDirectoryObjectSummary?>(null);
        public Task<AdGroupProvisionerResult> AddUserToGroupAsync(CustomerAdLinkSummary user, string group, string? dn, CancellationToken token) { var changed = Memberships.Add(group); if (changed) Added.Add(group); return Task.FromResult(new AdGroupProvisionerResult(200, changed ? "AD_GROUP_MEMBER_ADDED" : "AD_GROUP_MEMBER_ALREADY_PRESENT", "", changed)); }
        public Task<AdGroupProvisionerResult> RemoveUserFromGroupAsync(CustomerAdLinkSummary user, string group, string? dn, CancellationToken token) { var changed = Memberships.Remove(group); if (changed) Removed.Add(group); return Task.FromResult(new AdGroupProvisionerResult(200, changed ? "AD_GROUP_MEMBER_REMOVED" : "AD_GROUP_MEMBER_ALREADY_ABSENT", "", changed)); }
    }
    private static void Ensure(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
