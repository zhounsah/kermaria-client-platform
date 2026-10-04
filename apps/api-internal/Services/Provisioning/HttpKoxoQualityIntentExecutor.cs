using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services.ActiveDirectory;

namespace Kermaria.ApiInternal.Services.Provisioning;

// DEV uniquement. Non enregistre tant que les tests MariaDB et le profil
// KoXo reel ne sont pas valides. Aucune ecriture LDAP dans cet executant.
public sealed class HttpKoxoQualityIntentExecutor(
    IKoxoRepository candidates,
    IActiveDirectoryLinkRepository links,
    IActiveDirectoryService directory,
    IAdGroupProvisioner identityResolver,
    SubscriptionProvisioningRuntimeConfiguration groupsConfiguration,
    KoxoSyncWebhookRuntimeConfiguration webhookConfiguration,
    IKoxoSyncWebhookTriggerService trigger,
    IHttpClientFactory clients,
    AdRuntimeConfiguration adScope) : IKoxoQualityIntentExecutor
{
    public async Task<KoxoQualityEvidence?> ExecuteAndVerifyAsync(KoxoQualityLease lease, CancellationToken cancellationToken)
    {
        if (!webhookConfiguration.Enabled || webhookConfiguration.Url is null || directory.ModeName == "disabled")
            throw new InvalidOperationException("KOXO_QUALITY_EXECUTOR_UNAVAILABLE");
        var desired = KoxoQualityIntentPolicy.Deserialize(lease.Intent.DesiredJson);
        if (!KoxoQualityGroupScope.Allows(adScope, groupsConfiguration, desired.Values.SelectMany(groups => groups)))
            throw new InvalidOperationException("KOXO_QUALITY_GROUP_OUTSIDE_ALLOWED_ROOTS");
        if (desired.Count == 0 || desired.Count > 128) throw new InvalidOperationException("KOXO_QUALITY_TARGET_COUNT_INVALID");
        var exportable = await candidates.ListExportCandidatesAsync(cancellationToken);
        var targets = new List<ProofTarget>();
        var bound = new Dictionary<string, PortalUserAdLinkRecord>(StringComparer.Ordinal);
        foreach (var (identity, expected) in desired)
        {
            var matches = exportable.Where(candidate => candidate.PortalUserId == identity && candidate.CustomerId == lease.CustomerId).ToArray();
            if (matches.Length != 1 || matches[0].IsDemo ||
                !System.Text.RegularExpressions.Regex.IsMatch(matches[0].KoxoUniqueIdentifier ?? "", @"\ACLI-D[0-9]{6}\z") ||
                !System.Text.RegularExpressions.Regex.IsMatch(matches[0].CustomerReference, @"\ADEV-CLI-[A-Z0-9]+\z"))
                throw new InvalidOperationException("KOXO_QUALITY_TARGET_NOT_EXPORTABLE_DEV");
            var candidate = matches[0];
            var userLinks = await links.GetUserLinksByPortalUserIdAsync(identity, cancellationToken);
            if (userLinks.Count != 1 || userLinks[0].CustomerId != lease.CustomerId
                || userLinks[0].CustomerReference != candidate.CustomerReference)
                throw new InvalidOperationException("KOXO_QUALITY_LINK_AMBIGUOUS");
            var link = userLinks[0];
            var actual = await identityResolver.ResolveUserByEmployeeNumberAsync(candidate.KoxoUniqueIdentifier!, cancellationToken);
            if (actual is null || actual.IsDisabled || !string.Equals(actual.ObjectGuid, link.ObjectGuid, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(actual.SamAccountName, link.SamAccountName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("KOXO_QUALITY_AD_IDENTITY_MISMATCH");
            if (expected.Any(group => !groupsConfiguration.TryGetGroupDistinguishedName(group, out _)))
                throw new InvalidOperationException("KOXO_QUALITY_GROUP_UNRESOLVED");
            bound.Add(identity, link);
            targets.Add(new(identity, candidate.KoxoUniqueIdentifier!, candidate.CustomerReference, expected));
        }
        var digest = KoxoQualityDispatcher.Digest(lease.Intent);
        using var message = new HttpRequestMessage(HttpMethod.Post,
            new Uri(webhookConfiguration.Url, "/internal/koxo/qualities/proof/"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", webhookConfiguration.BearerToken);
        message.Content = JsonContent.Create(new { lease.CustomerId, revision=lease.Intent.Revision, desiredSha256=digest, targets });
        using var response = await clients.CreateClient(KoxoSyncWebhookTriggerService.HttpClientName).SendAsync(message, cancellationToken);
        // Un recepteur ancien/occupe n'est jamais traite comme une preuve et
        // ne declenche pas de nouvelle synchro par cette branche.
        if (response.StatusCode == HttpStatusCode.Conflict) return null;
        response.EnsureSuccessStatusCode();
        if (response.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException("KOXO_QUALITY_PROOF_NOT_COMPLETED");
        var proof = await response.Content.ReadFromJsonAsync<ProofResponse>(cancellationToken);
        if (proof is null || proof.ProtocolVersion != 1 || proof.CustomerId != lease.CustomerId
            || proof.Revision != lease.Intent.Revision || proof.DesiredSha256 != digest || proof.Targets is null
            || proof.Targets.Count != targets.Count)
            throw new InvalidOperationException("KOXO_QUALITY_PROOF_CONTRACT_INVALID");
        var csvVerified = true;
        var koxoVerified = true;
        foreach (var target in targets)
        {
            var observed = proof.Targets.Where(item => item.PortalUserId == target.PortalUserId && item.UniqueId == target.UniqueId).ToArray();
            if (observed.Length != 1 || !string.Equals(observed[0].UserId, bound[target.PortalUserId].SamAccountName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("KOXO_QUALITY_PROOF_IDENTITY_MISMATCH");
            csvVerified &= observed[0].CsvVerified;
            koxoVerified &= observed[0].KoxoVerified;
        }
        var adVerified = true;
        foreach (var target in targets)
        {
            var link = bound[target.PortalUserId];
            var membership = await directory.GetUserEffectiveGroupsAsync(link.CustomerReference, link.SamAccountName, cancellationToken);
            if (membership.StatusCode != 200 || membership.Value is null) { adVerified=false; continue; }
            var managed = membership.Value.Where(group => groupsConfiguration.GroupDistinguishedNamesBySamAccountName.ContainsKey(group.SamAccountName))
                .Select(group => group.SamAccountName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            adVerified &= managed.SetEquals(target.Groups);
            adVerified &= membership.Value.Where(group => managed.Contains(group.SamAccountName)).All(group =>
                string.Equals(group.DistinguishedName, groupsConfiguration.GroupDistinguishedNamesBySamAccountName[group.SamAccountName], StringComparison.OrdinalIgnoreCase));
        }
        if (!csvVerified)
        {
            // Actualiser seulement le CSV. L'operateur applique les qualites
            // via l'import avec remplacement, puis le worker relit les preuves.
            // Un XML/AD encore different ne justifie pas un nouvel export global.
            var first = targets[0];
            await trigger.TriggerAsync(new("", first.PortalUserId, first.SecondaryGroup,
                "qualities_changed", Guid.NewGuid().ToString("D"), DateTime.UtcNow.ToString("O")), cancellationToken);
        }
        return new(lease.CustomerId, lease.Intent.Revision, digest, csvVerified, koxoVerified, adVerified);
    }

    public sealed record ProofTarget(string PortalUserId, string UniqueId, string SecondaryGroup, IReadOnlyList<string> Groups);
    public sealed record ObservedTarget(string PortalUserId, string UniqueId, string? UserId, bool CsvVerified, bool KoxoVerified);
    public sealed record ProofResponse(int ProtocolVersion, string CustomerId, long Revision, string DesiredSha256, IReadOnlyList<ObservedTarget> Targets);
}
