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

// V0.26 : contrats de l'inscription self-service. La vérification
// hCaptcha et les rate limits IP sont assurés côté webportal ; l'API ne
// reçoit que les données métier + l'adresse source pour l'audit.
public sealed record SignupSubmitPayload(
    string? CompanyName,
    string? ContactName,
    string? Email,
    string? Phone,
    string? Message,
    SignupPackSelectionSnapshot? PackSelection,
    string? SourceAddress,
    string? UserAgent);

public sealed record SignupVerifyPayload(string? Token);

public sealed record SignupSetPasswordPayload(string? Token, string? Password);

public sealed record SignupRejectPayload(string? Reason);

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
    string? RejectedAt);
