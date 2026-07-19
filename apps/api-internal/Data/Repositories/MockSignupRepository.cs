using System.Collections.Concurrent;
using Kermaria.ApiInternal.Contracts;

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
    public SignupPackSelectionSnapshot? PackSelection { get; set; }
    public string? VerificationTokenHash { get; set; }
    public DateTime? VerificationTokenExpiresAtUtc { get; set; }
    public string? PasswordSetupTokenHash { get; set; }
    public DateTime? PasswordSetupExpiresAtUtc { get; set; }
    public string? SourceAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? ApprovedUserId { get; set; }
    public string? ApprovedCustomerId { get; set; }
    public string? ApprovedCustomerReference { get; set; }
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

public sealed class MockSignupStore
{
    public ConcurrentDictionary<string, MockSignupRow> Rows { get; } =
        new(StringComparer.Ordinal);
}

public sealed class MockSignupRepository : ISignupRepository
{
    private readonly ConcurrentDictionary<string, MockSignupRow> _rows;
    private readonly MockAuthenticationStore _authenticationStore;

    public MockSignupRepository(
        MockSignupStore store,
        MockAuthenticationStore authenticationStore)
    {
        _rows = store.Rows;
        _authenticationStore = authenticationStore;
    }

    public bool IsPersistent => false;

    public Task<bool> HasRecentSignupOrUserAsync(
        string normalizedEmail,
        DateTime windowStartUtc,
        CancellationToken cancellationToken)
    {
        if (_authenticationStore.Users.ContainsKey(normalizedEmail))
        {
            return Task.FromResult(true);
        }

        var exists = _rows.Values.Any(row =>
            string.Equals(row.Email, normalizedEmail, StringComparison.Ordinal)
            && (row.Status is "email_pending" or "email_verified" or "approved"
                || row.CreatedAtUtc >= windowStartUtc));
        return Task.FromResult(exists);
    }

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
            PackSelection = insert.PackSelection,
            VerificationTokenHash = insert.VerificationTokenHash,
            VerificationTokenExpiresAtUtc = insert.VerificationTokenExpiresAtUtc,
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
                row.VerificationTokenExpiresAtUtc));
    }

    public Task MarkEmailVerifiedAsync(
        string id,
        CancellationToken cancellationToken)
    {
        if (_rows.TryGetValue(id, out var row)
            && row.Status == "email_pending")
        {
            row.Status = "email_verified";
            row.VerificationTokenHash = null;
            row.VerificationTokenExpiresAtUtc = null;
            row.UpdatedAtUtc = DateTime.UtcNow;
        }

        return Task.CompletedTask;
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
                && candidate.PackSelection is not null)
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
            || row.Status != "email_verified")
        {
            return Task.FromResult<SignupApprovalResult?>(null);
        }

        var email = request.PrimaryUser.Email
            ?? request.Customer.BillingEmail
            ?? row.Email;
        var displayName = request.PrimaryUser.DisplayName
            ?? row.ContactName;

        _authenticationStore.Users[email] =
            new PortalUserCredential(
                request.UserId,
                request.CustomerId,
                request.CustomerReference,
                email,
                displayName,
                "active",
                PortalRoles.ClientUser,
                null,
                null,
                0,
                null,
                null);

        row.Status = "approved";
        row.ApprovedUserId = request.UserId;
        row.ApprovedCustomerId = request.CustomerId;
        row.ApprovedCustomerReference = request.CustomerReference;
        row.ApprovedAtUtc = DateTime.UtcNow;
        row.PasswordSetupTokenHash = request.PasswordSetupTokenHash;
        row.PasswordSetupExpiresAtUtc = request.PasswordSetupExpiresAtUtc;
        row.Customer = request.Customer;
        row.PrimaryUser = request.PrimaryUser;
        row.UpdatedAtUtc = DateTime.UtcNow;

        return Task.FromResult<SignupApprovalResult?>(new SignupApprovalResult(
            request.SignupId,
            request.CustomerId,
            request.CustomerReference,
            request.UserId,
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

    public Task SetPasswordAsync(
        string signupId,
        string portalUserId,
        string passwordHash,
        CancellationToken cancellationToken)
    {
        var credential = _authenticationStore.Users.Values.FirstOrDefault(
            user => user.Id == portalUserId);
        if (credential is not null)
        {
            _authenticationStore.Users[credential.Email] =
                credential with { PasswordHash = passwordHash };
        }

        if (_rows.TryGetValue(signupId, out var row))
        {
            row.PasswordSetupTokenHash = null;
            row.PasswordSetupExpiresAtUtc = null;
            row.UpdatedAtUtc = DateTime.UtcNow;
        }

        return Task.CompletedTask;
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
            row.PackSelection,
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
            row.UpdatedAtUtc);

    private static bool HasDefinedPassword(MockSignupRow row)
        => row.ApprovedUserId is not null
            && row.PasswordSetupTokenHash is null
            && row.PasswordSetupExpiresAtUtc is null;
}
