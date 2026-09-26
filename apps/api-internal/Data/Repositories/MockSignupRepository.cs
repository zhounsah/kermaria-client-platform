using System.Collections.Concurrent;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MockSignupRow
{
    public required string Id { get; set; }
    public required string Status { get; set; }
    public required string CompanyName { get; set; }
    public required string ContactName { get; set; }
    public required string Email { get; set; }
    public string? Phone { get; set; }
    public string? Message { get; set; }
    public required SignupCustomerData Customer { get; set; }
    public required SignupUserData PrimaryUser { get; set; }
    public BillingV2PublicSelection? BillingV2Selection { get; set; }
    public string? VerificationTokenHash { get; set; }
    public DateTime? VerificationTokenExpiresAtUtc { get; set; }
    public DateTime? EmailVerifiedAtUtc { get; set; }
    public string? SelfServiceFlow { get; set; }
    public string? PasswordSetupTokenHash { get; set; }
    public DateTime? PasswordSetupExpiresAtUtc { get; set; }
    public string? SourceAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? ApprovedUserId { get; set; }
    public string? ApprovedCustomerId { get; set; }
    public string? ApprovedCustomerReference { get; set; }
    public string? ApprovedUserKoxoUniqueIdentifier { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? AdProvisioningStatus { get; set; }
    public string? LastPasswordSyncStatus { get; set; }
    public string? KoxoExportStatus { get; set; }
    public string? ApprovedUserSamAccountName { get; set; }
    public string? ApprovedUserPrincipalName { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
    public string? RejectedReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>Ligne simulee de <c>portal_user_identity_bootstrap</c>.</summary>
public sealed class MockPrimaryIdentityBootstrapRow
{
    public required string Id { get; init; }
    public required string PortalUserId { get; init; }
    public required string CustomerId { get; init; }
    public required string SignupId { get; init; }
    public required string KoxoUniqueIdentifier { get; init; }
    public required string Origin { get; init; }
    public required bool EmailVerificationRequired { get; init; }
    public required string Status { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureDetail { get; set; }
    public string? DirectoryObjectGuid { get; set; }
    public DateTime? PasswordSetAtUtc { get; set; }
    public DateTime? KoxoTriggeredAtUtc { get; set; }
    public DateTime? DirectoryResolvedAtUtc { get; set; }
    public DateTime? DirectoryLinkedAtUtc { get; set; }
    public DateTime? RecoveryRequestedAtUtc { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    public MockPrimaryIdentityBootstrapRow Clone()
        => (MockPrimaryIdentityBootstrapRow)MemberwiseClone();
}

public sealed class MockSignupStore
{
    public ConcurrentDictionary<string, MockSignupRow> Rows { get; } =
        new(StringComparer.Ordinal);

    public long NextKoxoSequenceSeed = 1;
    public ConcurrentDictionary<string, ManualCustomerCreateResult> ManualCustomersByEmail { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Amorcages, par utilisateur portail (unicite de la migration 096).</summary>
    public ConcurrentDictionary<string, MockPrimaryIdentityBootstrapRow> PrimaryIdentityBootstraps { get; } =
        new(StringComparer.Ordinal);

    /// <summary>Tient lieu de verrou de ligne pour les transitions d'amorcage.</summary>
    public object PrimaryIdentitySync { get; } = new();
}

public sealed class MockSignupRepository : ISignupRepository
{
    private readonly ConcurrentDictionary<string, MockSignupRow> _rows;
    private readonly MockAuthenticationStore _authenticationStore;
    private readonly MockSignupStore _store;

    public MockSignupRepository(
        MockSignupStore store,
        MockAuthenticationStore authenticationStore)
    {
        _store = store;
        _rows = store.Rows;
        _authenticationStore = authenticationStore;
    }

    public bool IsPersistent => false;

    public Task<bool> HasBlockingSignupOrUserAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        if (_authenticationStore.Users.ContainsKey(normalizedEmail))
        {
            return Task.FromResult(true);
        }

        var exists = _rows.Values.Any(row =>
            string.Equals(row.Email, normalizedEmail, StringComparison.Ordinal)
            && row.Status is "email_pending" or "email_verified" or "approved");
        return Task.FromResult(exists);
    }

    public Task<bool> HasExistingCustomerEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
        => Task.FromResult(_store.ManualCustomersByEmail.ContainsKey(normalizedEmail));

    public Task<PortalEmailVerificationState> GetPortalUserEmailVerificationStateAsync(
        string portalUserId,
        CancellationToken cancellationToken)
    {
        var row = _rows.Values.FirstOrDefault(candidate => candidate.ApprovedUserId == portalUserId);
        var verificationRequired = row?.SelfServiceFlow is "cart" or "vps";
        return Task.FromResult(new PortalEmailVerificationState(
            row?.EmailVerifiedAtUtc is not null,
            verificationRequired));
    }

    public Task<ManualCustomerCreateResult> CreateManualCustomerAsync(
        ManualCustomerCreateRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Customer.BillingEmail
            ?? throw new InvalidOperationException("Customer billing email is required.");
        var displayName = request.Customer.DisplayName
            ?? throw new InvalidOperationException("Customer display name is required.");
        var created = new ManualCustomerCreateResult(
            request.CustomerReference,
            displayName,
            email,
            "active");
        if (!_store.ManualCustomersByEmail.TryAdd(email, created))
        {
            throw new InvalidOperationException("CUSTOMER_EMAIL_ALREADY_USED");
        }

        return Task.FromResult(created);
    }

    public Task<int> CountRecentSignupsByEmailAsync(
        string normalizedEmail,
        DateTime windowStartUtc,
        CancellationToken cancellationToken)
        => Task.FromResult(_rows.Values.Count(row =>
            string.Equals(row.Email, normalizedEmail, StringComparison.Ordinal)
            && row.CreatedAtUtc >= windowStartUtc));

    public Task<int> CountRecentSignupsBySourceAddressAsync(
        string sourceAddress,
        DateTime windowStartUtc,
        CancellationToken cancellationToken)
        => Task.FromResult(_rows.Values.Count(row =>
            string.Equals(row.SourceAddress, sourceAddress, StringComparison.Ordinal)
            && row.CreatedAtUtc >= windowStartUtc));

    public Task InsertPendingAsync(
        SignupInsert insert,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        _rows[insert.Id] = new MockSignupRow
        {
            Id = insert.Id,
            Status = "email_pending",
            CompanyName = insert.CompanyName,
            ContactName = insert.ContactName,
            Email = insert.Email,
            Phone = insert.Phone,
            Message = insert.Message,
            Customer = insert.Customer,
            PrimaryUser = insert.PrimaryUser,
            BillingV2Selection = insert.BillingV2Selection,
            VerificationTokenHash = insert.VerificationTokenHash,
            VerificationTokenExpiresAtUtc = insert.VerificationTokenExpiresAtUtc,
            SelfServiceFlow = insert.SelfServiceFlow,
            SourceAddress = insert.SourceAddress,
            UserAgent = insert.UserAgent,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        return Task.CompletedTask;
    }

    public Task<SignupVerificationTarget?> FindPendingByVerificationHashAsync(
        string verificationTokenHash,
        CancellationToken cancellationToken)
    {
        var row = _rows.Values.FirstOrDefault(candidate =>
            string.Equals(
                candidate.VerificationTokenHash,
                verificationTokenHash,
                StringComparison.Ordinal));
        return Task.FromResult(row is null
            ? null
            : new SignupVerificationTarget(
                row.Id,
                row.Status,
                row.VerificationTokenExpiresAtUtc,
                row.ApprovedUserId,
                row.SelfServiceFlow));
    }

    public Task MarkEmailVerifiedAsync(
        string id,
        CancellationToken cancellationToken)
    {
        if (_rows.TryGetValue(id, out var row)
            && row.Status == "email_pending")
        {
            row.Status = row.ApprovedUserId is null ? "email_verified" : "approved";
            row.EmailVerifiedAtUtc = DateTime.UtcNow;
            row.UpdatedAtUtc = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    public Task<SignupVerificationResendTarget?> RotateSelfServiceVerificationTokenAsync(
        string portalUserId,
        string verificationTokenHash,
        DateTime verificationTokenExpiresAtUtc,
        DateTime resendAllowedBeforeUtc,
        CancellationToken cancellationToken)
    {
        var row = FindResendableRow(candidate => candidate.ApprovedUserId == portalUserId, resendAllowedBeforeUtc);
        return Task.FromResult(Rotate(row, verificationTokenHash, verificationTokenExpiresAtUtc));
    }

    public Task<SignupVerificationResendTarget?> RotateSelfServiceVerificationTokenByEmailAsync(
        string normalizedEmail,
        string verificationTokenHash,
        DateTime verificationTokenExpiresAtUtc,
        DateTime resendAllowedBeforeUtc,
        CancellationToken cancellationToken)
    {
        var row = FindResendableRow(candidate => string.Equals(candidate.Email, normalizedEmail, StringComparison.Ordinal), resendAllowedBeforeUtc);
        return Task.FromResult(Rotate(row, verificationTokenHash, verificationTokenExpiresAtUtc));
    }

    private MockSignupRow? FindResendableRow(Func<MockSignupRow, bool> matches, DateTime resendAllowedBeforeUtc)
    {
        var now = DateTime.UtcNow;
        return _rows.Values
            .Where(candidate => matches(candidate)
                && candidate.Status == "email_pending"
                && candidate.EmailVerifiedAtUtc is null
                && candidate.SelfServiceFlow is "cart" or "vps")
            .Where(candidate => candidate.VerificationTokenExpiresAtUtc is not { } expiry
                || expiry <= now
                || candidate.UpdatedAtUtc <= resendAllowedBeforeUtc)
            .OrderByDescending(candidate => candidate.UpdatedAtUtc)
            .FirstOrDefault();
    }

    private static SignupVerificationResendTarget? Rotate(
        MockSignupRow? row,
        string verificationTokenHash,
        DateTime verificationTokenExpiresAtUtc)
    {
        var now = DateTime.UtcNow;
        if (row is null
            ) return null;

        row.VerificationTokenHash = verificationTokenHash;
        row.VerificationTokenExpiresAtUtc = verificationTokenExpiresAtUtc;
        row.UpdatedAtUtc = now;
        return new SignupVerificationResendTarget(row.Email, row.ContactName, row.SelfServiceFlow!);
    }

    public Task<IReadOnlyList<SignupPendingRecord>> ListAsync(
        string? statusFilter,
        int limit,
        CancellationToken cancellationToken)
    {
        var capped = Math.Clamp(limit, 1, 200);
        var records = _rows.Values
            .Where(row => string.IsNullOrWhiteSpace(statusFilter)
                || string.Equals(
                    row.Status,
                    statusFilter,
                    StringComparison.Ordinal))
            .OrderByDescending(row => row.CreatedAtUtc)
            .Take(capped)
            .Select(ToRecord)
            .ToList();
        return Task.FromResult<IReadOnlyList<SignupPendingRecord>>(records);
    }

    public Task<SignupPendingRecord?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(_rows.TryGetValue(id, out var row)
            ? ToRecord(row)
            : null);
    }

    public Task<SignupPendingRecord?> GetLatestApprovedByCustomerIdAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        var row = _rows.Values
            .Where(candidate =>
                candidate.Status == "approved"
                && candidate.ApprovedCustomerId == customerId
                && candidate.BillingV2Selection is not null)
            .OrderByDescending(candidate => candidate.ApprovedAtUtc)
            .ThenByDescending(candidate => candidate.CreatedAtUtc)
            .FirstOrDefault();
        return Task.FromResult(row is null ? null : ToRecord(row));
    }

    public Task<SignupApprovalResult?> ApproveAsync(
        SignupApprovalRequest request,
        CancellationToken cancellationToken)
    {
        if (!_rows.TryGetValue(request.SignupId, out var row)
            || (request.EmailVerified
                ? row.Status != "email_verified"
                : row.Status != "email_pending"
                  || !string.Equals(row.SelfServiceFlow, request.SelfServiceFlow, StringComparison.Ordinal)))
        {
            return Task.FromResult<SignupApprovalResult?>(null);
        }

        if (request.InitialKoxoSecret is not null && SealSink is null)
        {
            throw new InvalidOperationException(
                "Aucun point d'attache pour le secret KoXo.");
        }

        if (_store.PrimaryIdentityBootstraps.ContainsKey(request.UserId))
        {
            // Unicite de la migration 096 : un compte n'a qu'un amorcage.
            return Task.FromResult<SignupApprovalResult?>(null);
        }

        var email = request.PrimaryUser.Email
            ?? request.Customer.BillingEmail
            ?? row.Email;
        var displayName = request.PrimaryUser.DisplayName
            ?? row.ContactName;
        var koxoUniqueIdentifier = AllocateKoxoUniqueIdentifier();

        _authenticationStore.Users[email] =
            new PortalUserCredential(
                request.UserId,
                request.CustomerId,
                request.CustomerReference,
                email,
                displayName,
                "active",
                PortalRoles.ClientUser,
                request.InitialPasswordHash,
                null,
                0,
                null,
                null);

        row.Status = request.EmailVerified ? "approved" : "email_pending";
        row.ApprovedUserId = request.UserId;
        row.ApprovedCustomerId = request.CustomerId;
        row.ApprovedCustomerReference = request.CustomerReference;
        row.ApprovedUserKoxoUniqueIdentifier = koxoUniqueIdentifier;
        row.ApprovedAtUtc = DateTime.UtcNow;
        row.PasswordSetupTokenHash = request.PasswordSetupTokenHash;
        row.PasswordSetupExpiresAtUtc = request.PasswordSetupExpiresAtUtc;
        row.Customer = request.Customer;
        row.PrimaryUser = request.PrimaryUser;
        row.UpdatedAtUtc = DateTime.UtcNow;

        // Meme unite de travail que le compte, comme en base reelle.
        var origin = PrimaryIdentityBootstrapOrigins.FromSelfServiceFlow(
            request.SelfServiceFlow);
        _store.PrimaryIdentityBootstraps[request.UserId] = new MockPrimaryIdentityBootstrapRow
        {
            Id = Guid.NewGuid().ToString("D"),
            PortalUserId = request.UserId,
            CustomerId = request.CustomerId,
            SignupId = request.SignupId,
            KoxoUniqueIdentifier = koxoUniqueIdentifier,
            Origin = origin,
            EmailVerificationRequired =
                PrimaryIdentityBootstrapOrigins.RequiresEmailVerification(origin),
            Status = request.InitialKoxoSecret is null
                ? PrimaryIdentityBootstrapStatuses.AwaitingPassword
                : PrimaryIdentityBootstrapStatuses.KoxoPending,
            PasswordSetAtUtc = request.InitialKoxoSecret is null ? null : DateTime.UtcNow
        };
        if (request.InitialKoxoSecret is not null)
        {
            SealSink!.AttachSealed(request.UserId, request.InitialKoxoSecret);
        }

        return Task.FromResult<SignupApprovalResult?>(new SignupApprovalResult(
            request.SignupId,
            request.CustomerId,
            request.CustomerReference,
            request.UserId,
            koxoUniqueIdentifier,
            email,
            displayName));
    }

    public Task<bool> RejectAsync(
        string id,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (_rows.TryGetValue(id, out var row)
            && row.Status is "email_pending" or "email_verified")
        {
            row.Status = "rejected";
            row.RejectedAtUtc = DateTime.UtcNow;
            row.RejectedReason = reason;
            row.VerificationTokenHash = null;
            row.VerificationTokenExpiresAtUtc = null;
            row.UpdatedAtUtc = DateTime.UtcNow;
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<SignupPasswordTarget?> FindApprovedByPasswordHashAsync(
        string passwordSetupTokenHash,
        CancellationToken cancellationToken)
    {
        var row = _rows.Values.FirstOrDefault(candidate =>
            candidate.Status == "approved"
            && candidate.ApprovedUserId is not null
            && string.Equals(
                candidate.PasswordSetupTokenHash,
                passwordSetupTokenHash,
                StringComparison.Ordinal));
        return Task.FromResult(row is null
            ? null
            : new SignupPasswordTarget(
                row.Id,
                row.ApprovedUserId!,
                row.PasswordSetupExpiresAtUtc));
    }

    public Task RefreshPasswordSetupTokenAsync(
        string signupId,
        string passwordSetupTokenHash,
        DateTime passwordSetupExpiresAtUtc,
        CancellationToken cancellationToken)
    {
        if (_rows.TryGetValue(signupId, out var row)
            && row.Status == "approved"
            && row.ApprovedUserId is not null)
        {
            row.PasswordSetupTokenHash = passwordSetupTokenHash;
            row.PasswordSetupExpiresAtUtc = passwordSetupExpiresAtUtc;
            row.UpdatedAtUtc = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Point d'attache du secret scelle, renseigne apres construction.
    /// </summary>
    public IKoxoPendingPasswordSealSink? SealSink { get; set; }

    /// <summary>
    /// Nombre de liens annuaire que l'ecriture de l'etat de synchronisation
    /// toucherait. Reserve aux tests.
    /// </summary>
    public int PasswordSyncRowsAffected { get; set; } = 1;

    /// <remarks>
    /// Tout ou rien, comme la transaction reelle : le scelle est attache avant
    /// l'ecriture du condensat et defait si celle-ci echoue. C'est l'ordre qui
    /// exerce l'annulation ; l'ordre inverse ne prouverait rien.
    /// </remarks>
    public Task SetPasswordAsync(
        string signupId,
        string portalUserId,
        string passwordHash,
        PortalPasswordSecret? koxoSecret,
        DateTime atUtc,
        CancellationToken cancellationToken)
    {
        if (koxoSecret is not null)
        {
            if (SealSink is null)
            {
                throw new InvalidOperationException(
                    "Aucun point d'attache pour le secret KoXo.");
            }

            SealSink.AttachSealed(portalUserId, koxoSecret);
        }

        var credential = _authenticationStore.Users.Values.FirstOrDefault(
            user => user.Id == portalUserId);
        _rows.TryGetValue(signupId, out var row);
        var previousTokenHash = row?.PasswordSetupTokenHash;
        var previousTokenExpiry = row?.PasswordSetupExpiresAtUtc;
        var previousSyncStatus = row?.LastPasswordSyncStatus;

        try
        {
            MockPortalPasswordFailureSwitch.ThrowIfArmed();

            if (credential is not null)
            {
                _authenticationStore.Users[credential.Email] =
                    credential with { PasswordHash = passwordHash };
            }

            if (row is not null)
            {
                row.PasswordSetupTokenHash = null;
                row.PasswordSetupExpiresAtUtc = null;
                row.LastPasswordSyncStatus = koxoSecret is not null
                    ? "pending"
                    : row.LastPasswordSyncStatus;
                row.UpdatedAtUtc = DateTime.UtcNow;
            }

            // Exactement un lien annuaire, comme la persistance reelle : sinon
            // le secret partirait a KoXo sans etat de synchronisation en face.
            if (koxoSecret is not null && PasswordSyncRowsAffected != 1)
            {
                throw new InvalidOperationException(
                    "L'etat de synchronisation KoXo n'a pas pu etre pose sur exactement un lien annuaire.");
            }
        }
        catch
        {
            // Une seule unite de travail : le jeton reste utilisable et rien
            // n'a bouge.
            if (credential is not null)
            {
                _authenticationStore.Users[credential.Email] = credential;
            }

            if (row is not null)
            {
                row.PasswordSetupTokenHash = previousTokenHash;
                row.PasswordSetupExpiresAtUtc = previousTokenExpiry;
                row.LastPasswordSyncStatus = previousSyncStatus;
            }

            if (koxoSecret is not null)
            {
                SealSink!.DiscardSealed(portalUserId, koxoSecret);
            }

            throw;
        }

        return Task.CompletedTask;
    }

    public Task<string?> GetKoxoUniqueIdentifierAsync(
        string portalUserId,
        CancellationToken cancellationToken)
        => Task.FromResult(_rows.Values
            .FirstOrDefault(row => row.ApprovedUserId == portalUserId)
            ?.ApprovedUserKoxoUniqueIdentifier);

    // ------------------------------------------------------------------
    // Amorcage de l'identite AD du compte principal
    // ------------------------------------------------------------------

    /// <summary>
    /// Liens annuaire, pour les memes gardes que la base reelle (aucun
    /// amorcage ne se conclut, ni ne recoit de secret, a cote d'un lien).
    /// Renseigne apres construction, comme <see cref="SealSink"/>.
    /// </summary>
    public IActiveDirectoryLinkRepository? LinkRepository { get; set; }

    public Task<PrimaryIdentityBootstrapRecord?> GetPrimaryIdentityBootstrapAsync(
        string portalUserId,
        CancellationToken cancellationToken)
    {
        lock (_store.PrimaryIdentitySync)
        {
            return Task.FromResult(
                _store.PrimaryIdentityBootstraps.TryGetValue(portalUserId, out var row)
                    ? ToBootstrapRecord(row)
                    : null);
        }
    }

    public Task<IReadOnlyList<PrimaryIdentityBootstrapRecord>>
        ListPrimaryIdentityBootstrapCandidatesAsync(
            int limit,
            CancellationToken cancellationToken)
    {
        lock (_store.PrimaryIdentitySync)
        {
            var records = _store.PrimaryIdentityBootstraps.Values
                .Where(row => PrimaryIdentityBootstrapStatuses.IsBootstrapping(row.Status))
                .Select(row => (row, record: ToBootstrapRecord(row)))
                .Where(entry => entry.record is not null
                    && (!entry.row.EmailVerificationRequired || entry.record.EmailVerified))
                .OrderBy(entry => entry.row.LastAttemptAtUtc ?? entry.row.CreatedAtUtc)
                .ThenBy(entry => entry.row.Id, StringComparer.Ordinal)
                .Take(Math.Clamp(limit, 1, 200))
                .Select(entry => entry.record!)
                .ToArray();
            return Task.FromResult<IReadOnlyList<PrimaryIdentityBootstrapRecord>>(records);
        }
    }

    /// <remarks>
    /// Tout ou rien, comme la transaction reelle : le scelle est attache
    /// d'abord et defait si la suite echoue.
    /// </remarks>
    public Task SetPasswordForPrimaryIdentityBootstrapAsync(
        string signupId,
        string portalUserId,
        string passwordHash,
        PortalPasswordSecret? koxoSecret,
        DateTime atUtc,
        CancellationToken cancellationToken)
    {
        lock (_store.PrimaryIdentitySync)
        {
            if (HasUserLink(portalUserId))
            {
                throw new InvalidOperationException("PRIMARY_IDENTITY_ALREADY_LINKED");
            }

            if (!_rows.TryGetValue(signupId, out var row)
                || !string.Equals(row.ApprovedUserId, portalUserId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("SIGNUP_USER_MISMATCH");
            }

            var credential = _authenticationStore.Users.Values.FirstOrDefault(
                user => user.Id == portalUserId)
                ?? throw new InvalidOperationException("PORTAL_USER_NOT_FOUND");

            row.ApprovedUserKoxoUniqueIdentifier ??= AllocateKoxoUniqueIdentifier();
            _store.PrimaryIdentityBootstraps.TryGetValue(portalUserId, out var existing);
            if (existing is not null
                && !string.Equals(
                    existing.KoxoUniqueIdentifier,
                    row.ApprovedUserKoxoUniqueIdentifier,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException("PRIMARY_IDENTITY_KOXO_ID_MISMATCH");
            }

            if (existing?.Status == PrimaryIdentityBootstrapStatuses.Completed)
            {
                throw new InvalidOperationException("PRIMARY_IDENTITY_COMPLETED_WITHOUT_LINK");
            }

            var writeSecret = koxoSecret is not null
                && existing?.Status != PrimaryIdentityBootstrapStatuses.Failed;
            if (writeSecret)
            {
                if (SealSink is null)
                {
                    throw new InvalidOperationException(
                        "Aucun point d'attache pour le secret KoXo.");
                }

                SealSink.AttachSealed(portalUserId, koxoSecret!);
            }

            var previousTokenHash = row.PasswordSetupTokenHash;
            var previousTokenExpiry = row.PasswordSetupExpiresAtUtc;
            var previousBootstrap = existing?.Clone();
            try
            {
                MockPortalPasswordFailureSwitch.ThrowIfArmed();

                _authenticationStore.Users[credential.Email] =
                    credential with { PasswordHash = passwordHash };
                row.PasswordSetupTokenHash = null;
                row.PasswordSetupExpiresAtUtc = null;
                row.UpdatedAtUtc = DateTime.UtcNow;

                if (existing is null)
                {
                    var origin = PrimaryIdentityBootstrapOrigins.FromSelfServiceFlow(
                        row.SelfServiceFlow);
                    _store.PrimaryIdentityBootstraps[portalUserId] = new MockPrimaryIdentityBootstrapRow
                    {
                        Id = Guid.NewGuid().ToString("D"),
                        PortalUserId = portalUserId,
                        CustomerId = row.ApprovedCustomerId ?? credential.CustomerId,
                        SignupId = signupId,
                        KoxoUniqueIdentifier = row.ApprovedUserKoxoUniqueIdentifier,
                        Origin = origin,
                        EmailVerificationRequired =
                            PrimaryIdentityBootstrapOrigins.RequiresEmailVerification(origin),
                        Status = writeSecret
                            ? PrimaryIdentityBootstrapStatuses.KoxoPending
                            : PrimaryIdentityBootstrapStatuses.AwaitingPassword,
                        PasswordSetAtUtc = writeSecret ? atUtc : null
                    };
                }
                else if (writeSecret)
                {
                    if (existing.Status != PrimaryIdentityBootstrapStatuses.DirectoryReady)
                    {
                        existing.Status = PrimaryIdentityBootstrapStatuses.KoxoPending;
                    }

                    existing.PasswordSetAtUtc = atUtc;
                    existing.FailureCode = null;
                    existing.FailureDetail = null;
                }
            }
            catch
            {
                _authenticationStore.Users[credential.Email] = credential;
                row.PasswordSetupTokenHash = previousTokenHash;
                row.PasswordSetupExpiresAtUtc = previousTokenExpiry;
                if (previousBootstrap is null)
                {
                    _store.PrimaryIdentityBootstraps.TryRemove(portalUserId, out _);
                }
                else
                {
                    _store.PrimaryIdentityBootstraps[portalUserId] = previousBootstrap;
                }

                if (writeSecret)
                {
                    SealSink!.DiscardSealed(portalUserId, koxoSecret!);
                }

                throw;
            }
        }

        return Task.CompletedTask;
    }

    public Task<bool> MarkPrimaryIdentityDirectoryResolvedAsync(
        string id,
        string directoryObjectGuid,
        DateTime resolvedAtUtc,
        CancellationToken cancellationToken)
        => MutateBootstrap(id, row =>
        {
            if (!PrimaryIdentityBootstrapStatuses.IsBootstrapping(row.Status)
                || (row.DirectoryObjectGuid is not null
                    && !string.Equals(
                        row.DirectoryObjectGuid,
                        directoryObjectGuid,
                        StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            row.Status = PrimaryIdentityBootstrapStatuses.DirectoryReady;
            row.DirectoryObjectGuid = directoryObjectGuid;
            row.DirectoryResolvedAtUtc ??= resolvedAtUtc;
            row.FailureCode = null;
            row.FailureDetail = null;
            return true;
        });

    public Task<bool> MarkPrimaryIdentityCompletedAsync(
        string id,
        DateTime linkedAtUtc,
        CancellationToken cancellationToken)
        => MutateBootstrap(id, row =>
        {
            if (row.Status is not (PrimaryIdentityBootstrapStatuses.DirectoryReady
                    or PrimaryIdentityBootstrapStatuses.Completed)
                || row.DirectoryObjectGuid is null)
            {
                return false;
            }

            // Meme preuve que la clause SQL : le lien de CE compte, sur CET
            // objectGUID.
            var link = LinkRepository?
                .FindUserLinkByPortalUserIdAsync(row.PortalUserId, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            if (link is null
                || !string.Equals(
                    link.ObjectGuid,
                    row.DirectoryObjectGuid,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            row.Status = PrimaryIdentityBootstrapStatuses.Completed;
            row.DirectoryLinkedAtUtc ??= linkedAtUtc;
            row.FailureCode = null;
            row.FailureDetail = null;
            return true;
        });

    public Task<bool> MarkPrimaryIdentityFailedAsync(
        string id,
        string failureCode,
        string? failureDetail,
        CancellationToken cancellationToken)
        => MutateBootstrap(id, row =>
        {
            if (row.Status == PrimaryIdentityBootstrapStatuses.Completed)
            {
                return false;
            }

            row.Status = PrimaryIdentityBootstrapStatuses.Failed;
            row.FailureCode = failureCode;
            row.FailureDetail = failureDetail;
            return true;
        });

    public Task<bool> MarkPrimaryIdentityAwaitingPasswordAsync(
        string id,
        string reasonCode,
        CancellationToken cancellationToken)
        => MutateBootstrap(id, row =>
        {
            if (row.Status != PrimaryIdentityBootstrapStatuses.KoxoPending)
            {
                return false;
            }

            row.Status = PrimaryIdentityBootstrapStatuses.AwaitingPassword;
            row.FailureCode = reasonCode;
            return true;
        });

    public Task TouchPrimaryIdentityAttemptAsync(
        string id,
        bool koxoTriggered,
        DateTime atUtc,
        CancellationToken cancellationToken)
        => MutateBootstrap(id, row =>
        {
            row.LastAttemptAtUtc = atUtc;
            row.AttemptCount++;
            if (koxoTriggered)
            {
                row.KoxoTriggeredAtUtc = atUtc;
            }

            return true;
        });

    public Task<PrimaryIdentityRecoveryTarget> RequestPrimaryIdentityRecoveryAsync(
        string signupId,
        string passwordSetupTokenHash,
        DateTime passwordSetupExpiresAtUtc,
        DateTime atUtc,
        CancellationToken cancellationToken)
    {
        lock (_store.PrimaryIdentitySync)
        {
            if (!_rows.TryGetValue(signupId, out var row))
            {
                return Task.FromResult(
                    new PrimaryIdentityRecoveryTarget(PrimaryIdentityRecoveryCodes.SignupNotFound));
            }

            var rejection = PrimaryIdentityRecoveryRules.ClassifySignup(
                row.Status,
                row.ApprovedUserId);
            if (rejection is not null)
            {
                return Task.FromResult(new PrimaryIdentityRecoveryTarget(rejection));
            }

            var portalUserId = row.ApprovedUserId!;
            if (HasUserLink(portalUserId))
            {
                return Task.FromResult(
                    new PrimaryIdentityRecoveryTarget(PrimaryIdentityRecoveryCodes.AlreadyLinked));
            }

            _store.PrimaryIdentityBootstraps.TryGetValue(portalUserId, out var existing);
            var bootstrapRejection = PrimaryIdentityRecoveryRules.ClassifyBootstrap(
                existing?.Status,
                HasSecret(portalUserId));
            if (bootstrapRejection is not null)
            {
                return Task.FromResult(new PrimaryIdentityRecoveryTarget(bootstrapRejection));
            }

            row.ApprovedUserKoxoUniqueIdentifier ??= AllocateKoxoUniqueIdentifier();
            if (existing is null)
            {
                var credential = _authenticationStore.Users.Values.FirstOrDefault(
                    user => user.Id == portalUserId);
                var origin = PrimaryIdentityBootstrapOrigins.FromSelfServiceFlow(
                    row.SelfServiceFlow);
                existing = new MockPrimaryIdentityBootstrapRow
                {
                    Id = Guid.NewGuid().ToString("D"),
                    PortalUserId = portalUserId,
                    CustomerId = row.ApprovedCustomerId ?? credential?.CustomerId ?? string.Empty,
                    SignupId = signupId,
                    KoxoUniqueIdentifier = row.ApprovedUserKoxoUniqueIdentifier,
                    Origin = origin,
                    EmailVerificationRequired =
                        PrimaryIdentityBootstrapOrigins.RequiresEmailVerification(origin),
                    Status = PrimaryIdentityBootstrapStatuses.AwaitingPassword
                };
                _store.PrimaryIdentityBootstraps[portalUserId] = existing;
            }

            existing.Status = PrimaryIdentityBootstrapStatuses.AwaitingPassword;
            existing.RecoveryRequestedAtUtc = atUtc;
            existing.FailureCode = null;
            existing.FailureDetail = null;

            row.PasswordSetupTokenHash = passwordSetupTokenHash;
            row.PasswordSetupExpiresAtUtc = passwordSetupExpiresAtUtc;
            row.UpdatedAtUtc = DateTime.UtcNow;

            return Task.FromResult(new PrimaryIdentityRecoveryTarget(
                PrimaryIdentityRecoveryCodes.Issued,
                portalUserId,
                row.Email,
                row.ContactName));
        }
    }

    private Task<bool> MutateBootstrap(
        string id,
        Func<MockPrimaryIdentityBootstrapRow, bool> mutate)
    {
        lock (_store.PrimaryIdentitySync)
        {
            var row = _store.PrimaryIdentityBootstraps.Values.FirstOrDefault(
                candidate => candidate.Id == id);
            return Task.FromResult(row is not null && mutate(row));
        }
    }

    private PrimaryIdentityBootstrapRecord? ToBootstrapRecord(
        MockPrimaryIdentityBootstrapRow row)
    {
        if (!_rows.TryGetValue(row.SignupId, out var signup))
        {
            return null;
        }

        var credential = _authenticationStore.Users.Values.FirstOrDefault(
            user => user.Id == row.PortalUserId);
        if (credential is null
            || !string.Equals(credential.CustomerId, row.CustomerId, StringComparison.Ordinal))
        {
            // Meme regle que la jointure SQL : un amorcage qui ne designe pas
            // le client de son compte est invisible, donc inerte.
            return null;
        }

        var user = signup.PrimaryUser;
        return new PrimaryIdentityBootstrapRecord(
            row.Id,
            row.PortalUserId,
            row.CustomerId,
            credential.CustomerReference,
            KoxoGroupReference: null,
            row.SignupId,
            row.KoxoUniqueIdentifier,
            signup.ApprovedUserKoxoUniqueIdentifier,
            row.Origin,
            row.EmailVerificationRequired,
            signup.EmailVerifiedAtUtc is not null,
            row.Status,
            row.FailureCode,
            row.DirectoryObjectGuid,
            row.KoxoTriggeredAtUtc,
            PortalUserActive: string.Equals(credential.Status, "active", StringComparison.Ordinal),
            CustomerActive: true,
            IsDemo: false,
            DemoKind: null,
            IdentityComplete: user.PersonalTitle is not null
                && user.GivenName is not null
                && user.Surname is not null
                && user.BirthDate is not null,
            HasUserLink: HasUserLink(row.PortalUserId),
            SecretAvailable: HasSecret(row.PortalUserId),
            HasAdditionalUserLifecycle: false);
    }

    private bool HasUserLink(string portalUserId)
        => LinkRepository?
            .FindUserLinkByPortalUserIdAsync(portalUserId, CancellationToken.None)
            .GetAwaiter()
            .GetResult() is not null;

    private bool HasSecret(string portalUserId)
        => SealSink is KoxoPendingPasswordStore store && store.HasPending(portalUserId);

    private string AllocateKoxoUniqueIdentifier()
    {
        var next = Interlocked.Increment(ref _store.NextKoxoSequenceSeed);
        return KoxoNamespace.Current.FormatIdentifier(next - 1);
    }

    private static SignupPendingRecord ToRecord(MockSignupRow row)
        => new(
            row.Id,
            row.Status,
            row.CompanyName,
            row.ContactName,
            row.Email,
            row.Phone,
            row.Message,
            row.Customer,
            row.PrimaryUser,
            row.SourceAddress,
            row.VerificationTokenExpiresAtUtc,
            row.ApprovedUserId,
            row.ApprovedCustomerId,
            row.ApprovedCustomerReference,
            row.ApprovedAtUtc,
            row.PasswordSetupExpiresAtUtc,
            HasDefinedPassword(row),
            row.AdProvisioningStatus,
            row.LastPasswordSyncStatus,
            row.KoxoExportStatus,
            row.ApprovedUserSamAccountName,
            row.ApprovedUserPrincipalName,
            row.RejectedAtUtc,
            row.RejectedReason,
            row.CreatedAtUtc,
            row.UpdatedAtUtc,
            BillingV2Selection: row.BillingV2Selection,
            EmailVerifiedAtUtc: row.EmailVerifiedAtUtc,
            SelfServiceFlow: row.SelfServiceFlow);

    private static bool HasDefinedPassword(MockSignupRow row)
        => row.ApprovedUserId is not null
            && row.PasswordSetupTokenHash is null
            && row.PasswordSetupExpiresAtUtc is null;
}
