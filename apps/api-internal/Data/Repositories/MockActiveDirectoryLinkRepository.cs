using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MockActiveDirectoryLinkRepository
    : IActiveDirectoryLinkRepository
{
    private static readonly IReadOnlyDictionary<string, AdCustomerContext>
        CustomerContexts =
            new Dictionary<string, AdCustomerContext>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["CLI-DEMO-0042"] = new(
                    "mock-customer-0042",
                    "CLI-DEMO-0042",
                    "Client Demo 0042"),
                ["CLI-DEMO-0100"] = new(
                    "mock-customer-0100",
                    "CLI-DEMO-0100",
                    "Client Demo 0100"),
                ["CLI-DEMO-0200"] = new(
                    "mock-customer-0200",
                    "CLI-DEMO-0200",
                    "Client Demo 0200")
            };

    private static readonly Dictionary<string, List<CustomerAdLinkSummary>>
        LinksByCustomer =
            new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, PortalUserAdLinkRecord>
        PortalLinksByUserId =
            new(StringComparer.Ordinal);

    private static readonly object SyncRoot = new();

    public bool IsPersistent => false;

    public Task<AdCustomerContext?> GetCustomerContextAsync(
        string customerReference,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            CustomerContexts.TryGetValue(
                customerReference,
                out var customerContext)
                ? customerContext
                : null);
    }

    public Task<IReadOnlyList<CustomerAdLinkSummary>> GetCustomerLinksAsync(
        string customerReference,
        CancellationToken cancellationToken)
    {
        lock (SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<CustomerAdLinkSummary>>(
                LinksByCustomer.TryGetValue(
                    customerReference,
                    out var links)
                    ? links.OrderByDescending(link => link.LinkedAt).ToArray()
                    : Array.Empty<CustomerAdLinkSummary>());
        }
    }

    public Task<IReadOnlyList<CustomerAdLinkSummary>> GetCustomerUserLinksAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        var customerReference = CustomerContexts.Values
            .FirstOrDefault(context => context.CustomerId == customerId)
            ?.CustomerReference;
        if (customerReference is null)
        {
            return Task.FromResult<IReadOnlyList<CustomerAdLinkSummary>>(
                Array.Empty<CustomerAdLinkSummary>());
        }

        lock (SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<CustomerAdLinkSummary>>(
                LinksByCustomer.TryGetValue(
                    customerReference,
                    out var links)
                    ? links
                        .Where(link => string.Equals(
                            link.ObjectType,
                            "user",
                            StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(link => link.LinkedAt)
                        .ToArray()
                    : Array.Empty<CustomerAdLinkSummary>());
        }
    }

    public Task<CustomerAdLinkUpsertResult> UpsertCustomerLinkAsync(
        string customerReference,
        string? actorUserId,
        AdDirectoryObjectSummary directoryObject,
        CancellationToken cancellationToken)
    {
        lock (SyncRoot)
        {
            var links = EnsureCustomerLinks(customerReference);
            var existing = links.FirstOrDefault(link =>
                link.ObjectGuid.Equals(
                    directoryObject.ObjectGuid,
                    StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                return Task.FromResult(new CustomerAdLinkUpsertResult(
                    existing.Id,
                    false));
            }

            var id = Guid.NewGuid().ToString("D");
            links.Add(BuildSummary(
                id,
                customerReference,
                directoryObject,
                linkedAtIso: DateTime.UtcNow.ToString("O"),
                linkedBy: actorUserId is null ? "API-INTERNAL" : "Administrateur mock"));
            return Task.FromResult(new CustomerAdLinkUpsertResult(id, true));
        }
    }

    public Task<CustomerAdLinkUpsertResult> UpsertPortalUserLinkAsync(
        string customerReference,
        string portalUserId,
        string? actorUserId,
        AdDirectoryObjectSummary directoryObject,
        string? adDomain,
        string? adProvisioningStatus,
        DateTime? adProvisionedAtUtc,
        string? lastPasswordSyncStatus,
        DateTime? lastPasswordSyncAtUtc,
        string? koxoExportStatus,
        CancellationToken cancellationToken)
    {
        lock (SyncRoot)
        {
            PortalLinksByUserId.TryGetValue(portalUserId, out var existingPortalLink);
            var existingSummary = FindSummaryByObjectGuid(directoryObject.ObjectGuid);
            var id = existingPortalLink?.Id
                ?? existingSummary?.Id
                ?? Guid.NewGuid().ToString("D");
            var linkedAtIso = existingPortalLink is not null
                ? DateTime.UtcNow.ToString("O")
                : existingSummary?.LinkedAt
                    ?? DateTime.UtcNow.ToString("O");
            var linkedBy = actorUserId is null ? "API-INTERNAL" : "Administrateur mock";

            RemoveSummary(id);
            var summary = BuildSummary(
                id,
                customerReference,
                directoryObject,
                linkedAtIso,
                linkedBy);
            EnsureCustomerLinks(customerReference).Add(summary);

            var customerContext = CustomerContexts.TryGetValue(
                customerReference,
                out var context)
                ? context
                : new AdCustomerContext(
                    $"mock-{customerReference.ToLowerInvariant()}",
                    customerReference,
                    customerReference);
            PortalLinksByUserId[portalUserId] = new PortalUserAdLinkRecord(
                id,
                customerContext.CustomerId,
                customerReference,
                portalUserId,
                directoryObject.ObjectGuid,
                directoryObject.ObjectSid,
                directoryObject.SamAccountName,
                directoryObject.UserPrincipalName,
                directoryObject.DisplayName,
                directoryObject.DistinguishedName,
                adDomain,
                adProvisioningStatus,
                adProvisionedAtUtc,
                lastPasswordSyncAtUtc,
                lastPasswordSyncStatus,
                koxoExportStatus);

            return Task.FromResult(new CustomerAdLinkUpsertResult(id, true));
        }
    }

    public Task<bool> UpdateUserPasswordSyncStatusAsync(
        string portalUserId,
        string status,
        DateTime changedAtUtc,
        CancellationToken cancellationToken)
    {
        lock (SyncRoot)
        {
            if (!PortalLinksByUserId.TryGetValue(portalUserId, out var link))
            {
                return Task.FromResult(false);
            }

            PortalLinksByUserId[portalUserId] = link with
            {
                LastPasswordSyncAtUtc = changedAtUtc,
                LastPasswordSyncStatus = status
            };
            return Task.FromResult(true);
        }
    }

    public Task<CustomerAdLinkSummary?> FindUserLinkByEmailAsync(
        string customerReference,
        string email,
        CancellationToken cancellationToken)
    {
        lock (SyncRoot)
        {
            if (!LinksByCustomer.TryGetValue(customerReference, out var links))
            {
                return Task.FromResult<CustomerAdLinkSummary?>(null);
            }

            var match = links.FirstOrDefault(link =>
                link.ObjectType.Equals("user", StringComparison.OrdinalIgnoreCase)
                && link.UserPrincipalName is not null
                && link.UserPrincipalName.Equals(
                    email,
                    StringComparison.OrdinalIgnoreCase));
            return Task.FromResult<CustomerAdLinkSummary?>(match);
        }
    }

    public Task<PortalUserAdLinkRecord?> FindUserLinkByPortalUserIdAsync(
        string portalUserId,
        CancellationToken cancellationToken)
    {
        lock (SyncRoot)
        {
            PortalLinksByUserId.TryGetValue(portalUserId, out var link);
            return Task.FromResult(link);
        }
    }

    public Task<bool> RefreshCustomerLinkAsync(
        string targetCustomerReference,
        AdDirectoryObjectSummary directoryObject,
        CancellationToken cancellationToken)
    {
        lock (SyncRoot)
        {
            var existing = FindSummaryByObjectGuid(directoryObject.ObjectGuid);
            if (existing is null)
            {
                return Task.FromResult(false);
            }

            RemoveSummary(existing.Id);
            EnsureCustomerLinks(targetCustomerReference).Add(BuildSummary(
                existing.Id,
                targetCustomerReference,
                directoryObject,
                existing.LinkedAt,
                existing.LinkedBy));

            foreach (var entry in PortalLinksByUserId.ToArray())
            {
                if (!entry.Value.ObjectGuid.Equals(
                        directoryObject.ObjectGuid,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                PortalLinksByUserId[entry.Key] = entry.Value with
                {
                    CustomerReference = targetCustomerReference,
                    ObjectSid = directoryObject.ObjectSid,
                    SamAccountName = directoryObject.SamAccountName,
                    UserPrincipalName = directoryObject.UserPrincipalName,
                    DisplayName = directoryObject.DisplayName,
                    DistinguishedName = directoryObject.DistinguishedName
                };
            }

            return Task.FromResult(true);
        }
    }

    public Task<bool> DeleteCustomerLinkAsync(
        string customerReference,
        string linkId,
        CancellationToken cancellationToken)
    {
        lock (SyncRoot)
        {
            if (!LinksByCustomer.TryGetValue(customerReference, out var links))
            {
                return Task.FromResult(false);
            }

            var removedCount = links.RemoveAll(link =>
                link.Id.Equals(linkId, StringComparison.OrdinalIgnoreCase));
            if (removedCount <= 0)
            {
                return Task.FromResult(false);
            }

            foreach (var entry in PortalLinksByUserId.ToArray())
            {
                if (entry.Value.Id.Equals(linkId, StringComparison.OrdinalIgnoreCase))
                {
                    PortalLinksByUserId.Remove(entry.Key);
                }
            }

            return Task.FromResult(true);
        }
    }

    private static List<CustomerAdLinkSummary> EnsureCustomerLinks(
        string customerReference)
    {
        if (!LinksByCustomer.TryGetValue(customerReference, out var links))
        {
            links = [];
            LinksByCustomer[customerReference] = links;
        }

        return links;
    }

    private static CustomerAdLinkSummary BuildSummary(
        string id,
        string customerReference,
        AdDirectoryObjectSummary directoryObject,
        string linkedAtIso,
        string? linkedBy)
        => new(
            id,
            customerReference,
            directoryObject.ObjectGuid,
            directoryObject.ObjectSid,
            directoryObject.ObjectType,
            directoryObject.SamAccountName,
            directoryObject.UserPrincipalName,
            directoryObject.DisplayName,
            directoryObject.DistinguishedName,
            linkedAtIso,
            linkedBy);

    private static CustomerAdLinkSummary? FindSummaryByObjectGuid(
        string objectGuid)
    {
        foreach (var entry in LinksByCustomer)
        {
            var match = entry.Value.FirstOrDefault(link =>
                link.ObjectGuid.Equals(
                    objectGuid,
                    StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static void RemoveSummary(string id)
    {
        foreach (var entry in LinksByCustomer.Values)
        {
            entry.RemoveAll(link => link.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        }
    }
}
