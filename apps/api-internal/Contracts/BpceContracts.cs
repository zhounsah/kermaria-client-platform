namespace Kermaria.ApiInternal.Contracts;

public sealed record BpceStatusResponse(
    string Mode,
    string Status,
    bool ConfigurationValid,
    bool SenderConfigured,
    string BaseUrl,
    int RequestTimeoutMs);

public sealed record BpceServiceResult<T>(
    int StatusCode,
    string Code,
    string Message,
    T? Value = default);

public sealed record BpceIssueInvoiceRequest(
    bool SendEmail = false);

public sealed record BpceIssuedInvoiceInfo(
    string BpceInvoiceId,
    string? FiscalNumber,
    string Status,
    string IssueDate,
    int TotalAmountCents,
    string Currency,
    bool PdfAvailable);

public sealed record BpceSenderInfo(
    string Id,
    string? Name,
    string? ProfileName,
    string? Siren,
    string? Siret,
    string? Email,
    string? Country,
    string? Locale,
    bool IsDefault,
    bool IsArchived);
