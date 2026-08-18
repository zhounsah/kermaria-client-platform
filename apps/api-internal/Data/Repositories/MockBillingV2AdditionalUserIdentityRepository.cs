using System.Collections.Concurrent;
using System.Globalization;

namespace Kermaria.ApiInternal.Data.Repositories;

/// <summary>
/// Cycle de vie d'identite en memoire.
/// </summary>
/// <remarks>
/// <para>
/// Reproduit les gardes structurelles de la base : verrou exclusif pendant
/// l'attribution, unicite de la place, unicite de l'utilisateur portail,
/// unicite de l'adresse e-mail et allocation serialisee de <c>CLI-NNNNNN</c>.
/// Un mock permissif rendrait tous les tests de concurrence decoratifs.
/// </para>
/// <para>
/// La validation n'est pas dupliquee ici : le rappel recu est exactement celui
/// que MariaDB execute.
/// </para>
/// </remarks>
public sealed class MockBillingV2AdditionalUserIdentityRepository
    : IBillingV2AdditionalUserIdentityRepository
{
    /// <summary>Place d'abonnement simulee.</summary>
    public sealed class Slot
    {
        public required string Id { get; init; }
        public required string SubscriptionId { get; init; }
        public required string SubscriptionCustomerId { get; init; }
        public required string SubscriptionStatus { get; set; }
        public required bool IsPrimary { get; init; }
        public required string Status { get; set; }
        public string? IdentityReference { get; set; }
        public string? DisplayName { get; set; }
        public string? Email { get; set; }
        public bool HasActiveUserSlotEntitlement { get; set; } = true;
        public int IncompatibleScopedItemCount { get; set; }
    }

    private sealed class Lifecycle
    {
        public required string Id { get; init; }
        public required string SubscriptionUserId { get; init; }
        public required string SubscriptionId { get; init; }
        public required string CustomerId { get; init; }
        public required string PortalUserId { get; init; }
        public required string KoxoUniqueIdentifier { get; init; }
        public required string Status { get; set; }
        public string? FailureCode { get; set; }
        public string? FailureDetail { get; set; }
        public string? DirectoryObjectGuid { get; set; }
        public DateTime? PasswordSetAtUtc { get; set; }
        public DateTime? DirectoryLinkedAtUtc { get; set; }
    }

    private readonly ConcurrentDictionary<string, Slot> _slots =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _customerReferences =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Lifecycle> _lifecycles =
        new(StringComparer.Ordinal);
    private readonly MockPortalUserStore _portalUsers;
    private readonly MockPortalPasswordSetupRepository _passwordSetups;
    private readonly object _gate = new();
    private long _nextKoxoSequence = 1;

    public MockBillingV2AdditionalUserIdentityRepository(
        MockPortalUserStore portalUsers,
        MockPortalPasswordSetupRepository passwordSetups)
    {
        _portalUsers = portalUsers;
        _passwordSetups = passwordSetups;
    }

    public bool IsPersistent => false;

    public IReadOnlyCollection<string> AllocatedKoxoIdentifiers
        => _lifecycles.Values
            .Select(lifecycle => lifecycle.KoxoUniqueIdentifier)
            .ToArray();

    public void RegisterCustomer(string customerId, string customerReference)
        => _customerReferences[customerId] = customerReference;

    public Slot RegisterSlot(Slot slot)
    {
        _slots[slot.Id] = slot;
        return slot;
    }

    public Slot? FindSlot(string subscriptionUserId)
        => _slots.TryGetValue(subscriptionUserId, out var slot) ? slot : null;

    public string? StatusOf(string lifecycleId)
        => _lifecycles.TryGetValue(lifecycleId, out var lifecycle)
            ? lifecycle.Status
            : null;

    public Task<BillingV2AdditionalUserAssignmentResult> AssignAsync(
        BillingV2AdditionalUserAssignmentCommand command,
        Func<BillingV2AdditionalUserSlotSnapshot, string?> validate,
        CancellationToken cancellationToken)
    {
        // Le verrou global tient lieu de `FOR UPDATE` : la lecture de decision
        // et l'ecriture doivent etre indissociables, faute de quoi deux
        // attributions concurrentes verraient toutes les deux une place libre.
        lock (_gate)
        {
            if (!_slots.TryGetValue(command.SubscriptionUserId, out var slot))
            {
                return Reject(
                    BillingV2AdditionalUserRejectionCodes.SlotNotFound);
            }

            _customerReferences.TryGetValue(
                command.CustomerId,
                out var customerReference);

            var snapshot = new BillingV2AdditionalUserSlotSnapshot(
                slot.Id,
                slot.SubscriptionId,
                slot.SubscriptionCustomerId,
                slot.SubscriptionStatus,
                slot.IsPrimary,
                slot.Status,
                slot.IdentityReference,
                customerReference,
                slot.HasActiveUserSlotEntitlement,
                slot.IncompatibleScopedItemCount,
                _lifecycles.Values.Any(lifecycle => string.Equals(
                    lifecycle.SubscriptionUserId,
                    slot.Id,
                    StringComparison.Ordinal)),
                _portalUsers.IsEmailTaken(command.NormalizedEmail));

            var rejection = validate(snapshot);
            if (rejection is not null)
            {
                return Reject(rejection);
            }

            var koxoUniqueIdentifier =
                $"CLI-{_nextKoxoSequence.ToString("D6", CultureInfo.InvariantCulture)}";

            if (!_portalUsers.TryAdd(new MockPortalUserStore.Entry(
                    command.PortalUserId,
                    command.CustomerId,
                    command.NormalizedEmail,
                    command.DisplayName,
                    koxoUniqueIdentifier,
                    PasswordHash: null)))
            {
                return Reject(
                    BillingV2AdditionalUserRejectionCodes.EmailAlreadyUsed);
            }

            _nextKoxoSequence++;
            slot.IdentityReference = command.PortalUserId;
            slot.DisplayName = command.DisplayName;
            slot.Email = command.NormalizedEmail;

            _lifecycles[command.LifecycleId] = new Lifecycle
            {
                Id = command.LifecycleId,
                SubscriptionUserId = slot.Id,
                SubscriptionId = slot.SubscriptionId,
                CustomerId = command.CustomerId,
                PortalUserId = command.PortalUserId,
                KoxoUniqueIdentifier = koxoUniqueIdentifier,
                Status = BillingV2UserIdentityStatuses.AwaitingPassword
            };

            _passwordSetups.IssueAsync(
                new PortalPasswordSetupIssue(
                    command.PasswordSetupId,
                    command.PortalUserId,
                    command.PasswordSetupPurpose,
                    command.PasswordSetupTokenHash,
                    command.PasswordSetupExpiresAtUtc),
                cancellationToken).GetAwaiter().GetResult();

            return Task.FromResult(
                BillingV2AdditionalUserAssignmentResult.Success(
                    new BillingV2AdditionalUserIdentityRecord(
                        command.LifecycleId,
                        slot.Id,
                        slot.SubscriptionId,
                        command.CustomerId,
                        customerReference!,
                        command.PortalUserId,
                        koxoUniqueIdentifier,
                        BillingV2UserIdentityStatuses.AwaitingPassword,
                        FailureCode: null,
                        DirectoryObjectGuid: null,
                        command.NormalizedEmail,
                        command.DisplayName)));
        }
    }

    private static Task<BillingV2AdditionalUserAssignmentResult> Reject(
        string code)
        => Task.FromResult(
            BillingV2AdditionalUserAssignmentResult.Reject(code));

    public Task<BillingV2AdditionalUserIdentityRecord?> FindByPortalUserIdAsync(
        string portalUserId,
        CancellationToken cancellationToken)
        => Task.FromResult(Project(_lifecycles.Values.FirstOrDefault(
            lifecycle => string.Equals(
                lifecycle.PortalUserId,
                portalUserId,
                StringComparison.Ordinal))));

    public Task<BillingV2AdditionalUserIdentityRecord?>
        FindBySubscriptionUserIdAsync(
            string subscriptionUserId,
            CancellationToken cancellationToken)
        => Task.FromResult(Project(_lifecycles.Values.FirstOrDefault(
            lifecycle => string.Equals(
                lifecycle.SubscriptionUserId,
                subscriptionUserId,
                StringComparison.Ordinal))));

    private BillingV2AdditionalUserIdentityRecord? Project(Lifecycle? lifecycle)
    {
        if (lifecycle is null)
        {
            return null;
        }

        var portalUser = _portalUsers.Find(lifecycle.PortalUserId);
        _customerReferences.TryGetValue(
            lifecycle.CustomerId,
            out var customerReference);

        return new BillingV2AdditionalUserIdentityRecord(
            lifecycle.Id,
            lifecycle.SubscriptionUserId,
            lifecycle.SubscriptionId,
            lifecycle.CustomerId,
            customerReference ?? string.Empty,
            lifecycle.PortalUserId,
            lifecycle.KoxoUniqueIdentifier,
            lifecycle.Status,
            lifecycle.FailureCode,
            lifecycle.DirectoryObjectGuid,
            portalUser?.Email ?? string.Empty,
            portalUser?.DisplayName ?? string.Empty);
    }

    public Task<bool> MarkKoxoPendingAsync(
        string id,
        DateTime passwordSetAtUtc,
        DateTime koxoTriggeredAtUtc,
        CancellationToken cancellationToken)
        => Transition(
            id,
            [
                BillingV2UserIdentityStatuses.AwaitingPassword,
                BillingV2UserIdentityStatuses.KoxoPending,
                BillingV2UserIdentityStatuses.Failed
            ],
            lifecycle =>
            {
                lifecycle.Status = BillingV2UserIdentityStatuses.KoxoPending;
                lifecycle.PasswordSetAtUtc ??= passwordSetAtUtc;
                lifecycle.FailureCode = null;
                lifecycle.FailureDetail = null;
            });

    public Task<bool> MarkDirectoryResolvedAsync(
        string id,
        string directoryObjectGuid,
        DateTime resolvedAtUtc,
        CancellationToken cancellationToken)
        => Transition(
            id,
            [
                BillingV2UserIdentityStatuses.KoxoPending,
                BillingV2UserIdentityStatuses.DirectoryReady,
                BillingV2UserIdentityStatuses.Failed
            ],
            lifecycle =>
            {
                if (lifecycle.DirectoryObjectGuid is not null
                    && !string.Equals(
                        lifecycle.DirectoryObjectGuid,
                        directoryObjectGuid,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Directory object GUID conflict.");
                }

                lifecycle.Status = BillingV2UserIdentityStatuses.DirectoryReady;
                lifecycle.DirectoryObjectGuid = directoryObjectGuid;
                lifecycle.FailureCode = null;
                lifecycle.FailureDetail = null;
            });

    public Task<bool> MarkReadyAsync(
        string id,
        DateTime linkedAtUtc,
        CancellationToken cancellationToken)
        => Transition(
            id,
            [
                BillingV2UserIdentityStatuses.DirectoryReady,
                BillingV2UserIdentityStatuses.Ready
            ],
            lifecycle =>
            {
                if (lifecycle.DirectoryObjectGuid is null)
                {
                    throw new InvalidOperationException(
                        "Directory object GUID is required before ready.");
                }

                lifecycle.Status = BillingV2UserIdentityStatuses.Ready;
                lifecycle.DirectoryLinkedAtUtc ??= linkedAtUtc;
                lifecycle.FailureCode = null;
                lifecycle.FailureDetail = null;
            });

    public Task<bool> MarkFailedAsync(
        string id,
        string failureCode,
        string? failureDetail,
        CancellationToken cancellationToken)
        => Transition(
            id,
            BillingV2UserIdentityStatuses.All
                .Where(status =>
                    !string.Equals(
                        status,
                        BillingV2UserIdentityStatuses.Ready,
                        StringComparison.Ordinal)
                    && !string.Equals(
                        status,
                        BillingV2UserIdentityStatuses.Disabled,
                        StringComparison.Ordinal))
                .ToArray(),
            lifecycle =>
            {
                lifecycle.Status = BillingV2UserIdentityStatuses.Failed;
                lifecycle.FailureCode = failureCode;
                lifecycle.FailureDetail = failureDetail;
            });

    public Task<bool> MarkDisabledAsync(
        string id,
        DateTime disabledAtUtc,
        CancellationToken cancellationToken)
        => Transition(
            id,
            BillingV2UserIdentityStatuses.All
                .Where(status => !string.Equals(
                    status,
                    BillingV2UserIdentityStatuses.Disabled,
                    StringComparison.Ordinal))
                .ToArray(),
            lifecycle => lifecycle.Status =
                BillingV2UserIdentityStatuses.Disabled);

    private Task<bool> Transition(
        string id,
        IReadOnlyList<string> allowedFrom,
        Action<Lifecycle> apply)
    {
        lock (_gate)
        {
            if (!_lifecycles.TryGetValue(id, out var lifecycle)
                || !allowedFrom.Contains(lifecycle.Status, StringComparer.Ordinal))
            {
                return Task.FromResult(false);
            }

            try
            {
                apply(lifecycle);
            }
            catch (InvalidOperationException)
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }
    }
}
