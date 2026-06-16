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
    private static readonly Dictionary<string, List<CustomerAdLinkSummary>> LinksByCustomer =
        new(StringComparer.OrdinalIgnoreCase);
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

    public Task<CustomerAdLinkUpsertResult> UpsertCustomerLinkAsync(
        string customerReference,
        string? actorUserId,
        AdDirectoryObjectSummary directoryObject,
        CancellationToken cancellationToken)
    {
        lock (SyncRoot)
        {
            if (!LinksByCustomer.TryGetValue(customerReference, out var links))
            {
                links = [];
                LinksByCustomer[customerReference] = links;
            }

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
            links.Add(new CustomerAdLinkSummary(
                id,
                customerReference,
                directoryObject.ObjectGuid,
                directoryObject.ObjectSid,
                directoryObject.ObjectType,
                directoryObject.SamAccountName,
                directoryObject.UserPrincipalName,
                directoryObject.DisplayName,
                directoryObject.DistinguishedName,
                DateTime.UtcNow.ToString("O"),
                actorUserId is null ? "API-INTERNAL" : "Administrateur mock"));

            return Task.FromResult(new CustomerAdLinkUpsertResult(id, true));
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
            return Task.FromResult(removedCount > 0);
        }
    }
}
