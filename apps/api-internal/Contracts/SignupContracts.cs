namespace Kermaria.ApiInternal.Contracts;

public sealed record SignupPackSelectionSnapshot(
    string PackKey,
    string PackLabel,
    string OfferId,
    string OfferExternalReference,
    int CommitmentMonths,
    string PaymentMode,
    int BillingIntervalMonths,
    int DiscountPercent,
    int MonthlyPriceAmountCents,
    int BillingPriceAmountCents,
    int SetupFeeAmountCents,
    int FirstChargeAmountCents,
    string Currency);

public sealed record PendingPackSelectionSummary(
    string SignupId,
    string Status,
    string? ApprovedAt,
    string CreatedAt,
    SignupPackSelectionSnapshot Snapshot);

public sealed record SignupCustomerData(
    string? CustomerType,
    string? DisplayName,
    string? BillingEmail,
    string? Phone,
    string? AddressLine1,
    string? AddressLine2,
    string? PostalCode,
    string? City,
    string? Country);

public sealed record SignupUserData(
    string? PersonalTitle,
    string? GivenName,
    string? Surname,
    string? Initials,
    string? DisplayName,
    string? Email,
    string? Phone,
    bool? IsPrimaryContact);

// V0.38 : l'inscription reste mono-utilisateur a ce stade, mais le contrat
// public devient structure pour preparer l'alignement site -> AD.
public sealed record SignupSubmitPayload(
    string? CompanyName,
    string? ContactName,
    string? Email,
    string? Phone,
    string? Message,
    SignupCustomerData? Customer,
    SignupUserData? PrimaryUser,
    SignupPackSelectionSnapshot? PackSelection,
    string? SourceAddress,
    string? UserAgent);

public sealed record SignupVerifyPayload(string? Token);

public sealed record SignupSetPasswordPayload(string? Token, string? Password);

public sealed record SignupRejectPayload(string? Reason);

public sealed record SignupAdminInitializePasswordPayload(string? Password);

public sealed record SignupAdminAccountAccess(
    string? CustomerReference,
    bool PasswordDefined,
    string? PasswordSetupExpiresAt,
    string? AdProvisioningStatus,
    string? LastPasswordSyncStatus,
    string? KoxoExportStatus,
    string? SamAccountName,
    string? UserPrincipalName);

public sealed record SignupAdminSummary(
    string Id,
    string Status,
    string CompanyName,
    string ContactName,
    string Email,
    bool EmailVerified,
    string CreatedAt,
    string? ApprovedAt,
    string? RejectedAt);

public sealed record SignupAdminDetail(
    string Id,
    string Status,
    string CompanyName,
    string ContactName,
    string Email,
    string? Phone,
    string? Message,
    SignupPackSelectionSnapshot? PackSelection,
    string? SourceAddress,
    string? RejectedReason,
    string CreatedAt,
    string UpdatedAt,
    string? ApprovedAt,
    string? RejectedAt,
    SignupCustomerData? Customer,
    SignupUserData? PrimaryUser,
    SignupAdminAccountAccess? AccountAccess);
