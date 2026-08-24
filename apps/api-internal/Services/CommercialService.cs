using System.Text.RegularExpressions;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;

namespace Kermaria.ApiInternal.Services;

public static partial class CommercialStatuses
{
    public const string Draft = "draft";
    public const string PendingReview = "pending_review";
    public const string SharedWithCustomer = "shared_with_customer";
    public const string Cancelled = "cancelled";
    public const string QuoteDraft = "quote_draft";
    public const string BillingDraft = "billing_draft";
    public const string InformationalInvoice = "informational_invoice";
    public const string DefaultDisclaimer =
        "Document informatif — ne constitue pas une facture officielle.";
    public const string CadenceOneTime = "one_time";
    public const string CadenceMonthly = "monthly";
    public const string PaymentModeMonthly = "monthly";
    public const string PaymentModeUpfront = "upfront";

    public static readonly IReadOnlySet<string> BillingCadences =
        new HashSet<string>(StringComparer.Ordinal)
        {
            CadenceOneTime,
            CadenceMonthly
        };

    public static readonly IReadOnlySet<string> PaymentModes =
        new HashSet<string>(StringComparer.Ordinal)
        {
            PaymentModeMonthly,
            PaymentModeUpfront
        };

    public static readonly IReadOnlySet<string> Document =
        new HashSet<string>(StringComparer.Ordinal)
        {
            Draft,
            PendingReview,
            SharedWithCustomer,
            Cancelled
        };

    public static readonly IReadOnlySet<string> MutableDocument =
        new HashSet<string>(StringComparer.Ordinal)
        {
            Draft,
            PendingReview
        };

    public static readonly IReadOnlySet<string> DocumentTypes =
        new HashSet<string>(StringComparer.Ordinal)
        {
            QuoteDraft,
            BillingDraft,
            InformationalInvoice
        };
}

public sealed record ValidatedCommercialDocument(
    string? CustomerReference,
    string DocumentType,
    string Title,
    string Currency,
    string? ServiceRequestId,
    string Disclaimer,
    string Status);

public sealed record ValidatedCommercialDocumentLine(
    string? Label,
    string Description,
    decimal Quantity,
    string? UnitLabel,
    int? UnitPriceCents,
    int? TaxRateBasisPoints,
    int SortOrder);

public interface ICommercialService
{
    bool IsPersistent { get; }

    Task<IReadOnlyList<CommercialDocumentSummary>> GetClientDocumentsAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken);
    Task<CommercialDocumentDetail> GetClientDocumentAsync(
        PortalSessionContext session,
        string documentId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminCommercialDocumentSummary>> GetAdminDocumentsAsync(
        CancellationToken cancellationToken);
    Task<AdminCommercialDocumentDetail> GetAdminDocumentAsync(
        string documentId,
        CancellationToken cancellationToken);
    Task<CommercialDocumentMutationResponse> CreateDocumentAsync(
        PortalSessionContext actor,
        CommercialDocumentPayload payload,
        string correlationId,
        CancellationToken cancellationToken);
    Task<CommercialDocumentMutationResponse> UpdateDocumentAsync(
        PortalSessionContext actor,
        string documentId,
        CommercialDocumentPayload payload,
        string correlationId,
        CancellationToken cancellationToken);
    Task<CommercialDocumentLineMutationResponse> AddLineAsync(
        PortalSessionContext actor,
        string documentId,
        CommercialDocumentLinePayload payload,
        string correlationId,
        CancellationToken cancellationToken);
    Task<CommercialDocumentLineMutationResponse> UpdateLineAsync(
        PortalSessionContext actor,
        string documentId,
        string lineId,
        CommercialDocumentLinePayload payload,
        string correlationId,
        CancellationToken cancellationToken);
    Task<CommercialDocumentMutationResponse> ShareDocumentAsync(
        PortalSessionContext actor,
        string documentId,
        string correlationId,
        CancellationToken cancellationToken);
    Task<CommercialDocumentMutationResponse> CancelDocumentAsync(
        PortalSessionContext actor,
        string documentId,
        string correlationId,
        CancellationToken cancellationToken);

    Task<CommercialDocumentMutationResponse> SelectClientDocumentPaymentMethodAsync(
        PortalSessionContext actor,
        string documentId,
        PaymentMethodSelectionPayload payload,
        string correlationId,
        CancellationToken cancellationToken);
}

public sealed partial class CommercialService : ICommercialService
{
    private const int MaxShortTextLength = 200;
    private const int MaxDescriptionLength = 1000;
    private const int MaxDisclaimerLength = 500;
    private const int MaxDisplayOrder = 100000;
    private const int MaxPriceAmountCents = 100_000_000;
    private const int MaxSortOrder = 100000;
    private const int MaxTaxRateBasisPoints = 10000;
    private const decimal MaxQuantity = 1000000m;
    private const int MaxReferenceListLength = 50;
    private readonly ICommercialDocumentRepository _repository;

    public CommercialService(ICommercialDocumentRepository repository)
    {
        _repository = repository;
    }

    public bool IsPersistent => _repository.IsPersistent;

    public Task<IReadOnlyList<CommercialDocumentSummary>> GetClientDocumentsAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
        => _repository.GetClientDocumentsAsync(session, cancellationToken);

    public async Task<CommercialDocumentDetail> GetClientDocumentAsync(
        PortalSessionContext session,
        string documentId,
        CancellationToken cancellationToken)
        => await _repository.GetClientDocumentAsync(
                session,
                ValidateIdentifier(documentId),
                cancellationToken)
            ?? throw new PortalDataNotFoundException();

    public Task<IReadOnlyList<AdminCommercialDocumentSummary>>
        GetAdminDocumentsAsync(
            CancellationToken cancellationToken)
        => _repository.GetAdminDocumentsAsync(cancellationToken);

    public async Task<AdminCommercialDocumentDetail> GetAdminDocumentAsync(
        string documentId,
        CancellationToken cancellationToken)
        => await _repository.GetAdminDocumentAsync(
                ValidateIdentifier(documentId),
                cancellationToken)
            ?? throw new PortalDataNotFoundException();

    public Task<CommercialDocumentMutationResponse> CreateDocumentAsync(
        PortalSessionContext actor,
        CommercialDocumentPayload payload,
        string correlationId,
        CancellationToken cancellationToken)
        => _repository.CreateDocumentAsync(
            actor,
            ValidateDocumentPayload(payload, creating: true),
            correlationId,
            cancellationToken);

    public Task<CommercialDocumentMutationResponse> UpdateDocumentAsync(
        PortalSessionContext actor,
        string documentId,
        CommercialDocumentPayload payload,
        string correlationId,
        CancellationToken cancellationToken)
        => _repository.UpdateDocumentAsync(
            actor,
            ValidateIdentifier(documentId),
            ValidateDocumentPayload(payload, creating: false),
            correlationId,
            cancellationToken);

    public Task<CommercialDocumentLineMutationResponse> AddLineAsync(
        PortalSessionContext actor,
        string documentId,
        CommercialDocumentLinePayload payload,
        string correlationId,
        CancellationToken cancellationToken)
        => _repository.AddLineAsync(
            actor,
            ValidateIdentifier(documentId),
            ValidateLinePayload(payload),
            correlationId,
            cancellationToken);

    public Task<CommercialDocumentLineMutationResponse> UpdateLineAsync(
        PortalSessionContext actor,
        string documentId,
        string lineId,
        CommercialDocumentLinePayload payload,
        string correlationId,
        CancellationToken cancellationToken)
        => _repository.UpdateLineAsync(
            actor,
            ValidateIdentifier(documentId),
            ValidateIdentifier(lineId),
            ValidateLinePayload(payload),
            correlationId,
            cancellationToken);

    public Task<CommercialDocumentMutationResponse> ShareDocumentAsync(
        PortalSessionContext actor,
        string documentId,
        string correlationId,
        CancellationToken cancellationToken)
        => _repository.ShareDocumentAsync(
            actor,
            ValidateIdentifier(documentId),
            correlationId,
            cancellationToken);

    public Task<CommercialDocumentMutationResponse> CancelDocumentAsync(
        PortalSessionContext actor,
        string documentId,
        string correlationId,
        CancellationToken cancellationToken)
        => _repository.CancelDocumentAsync(
            actor,
            ValidateIdentifier(documentId),
            correlationId,
            cancellationToken);

    public async Task<CommercialDocumentMutationResponse>
        SelectClientDocumentPaymentMethodAsync(
            PortalSessionContext actor,
            string documentId,
            PaymentMethodSelectionPayload payload,
            string correlationId,
            CancellationToken cancellationToken)
    {
        var normalizedDocumentId = ValidateIdentifier(documentId);
        var paymentMethod = payload.PaymentMethod?.Trim();
        if (!string.Equals(paymentMethod, "manual", StringComparison.Ordinal))
        {
            throw new PortalValidationException();
        }

        var current = await _repository.GetClientDocumentAsync(
            actor,
            normalizedDocumentId,
            cancellationToken)
            ?? throw new PortalDataNotFoundException();

        if (current.Status != "issued")
        {
            throw new PortalValidationException();
        }

        var changed = !string.Equals(
            current.PaymentMethod,
            paymentMethod,
            StringComparison.Ordinal);

        if (changed)
        {
            await _repository.SetDocumentPaymentMethodAsync(
                normalizedDocumentId,
                paymentMethod,
                cancellationToken);
        }

        return new CommercialDocumentMutationResponse(
            current.Id,
            current.InternalReference,
            current.Status,
            changed,
            correlationId);
    }

    private static string? ValidatePayPalPlanId(string? value)
    {
        var normalized = Normalize(value);
        if (normalized is null)
        {
            return null;
        }

        if (normalized.Length > 64 || !PayPalPlanIdPattern().IsMatch(normalized))
        {
            throw new PortalValidationException();
        }

        return normalized;
    }

    private static string? ValidateStripePriceId(string? value)
    {
        var normalized = Normalize(value);
        if (normalized is null)
        {
            return null;
        }

        if (normalized.Length > 64 || !PayPalPlanIdPattern().IsMatch(normalized))
        {
            throw new PortalValidationException();
        }

        return normalized;
    }

    private static string? ValidatePackCode(string? value)
    {
        var normalized = Normalize(value);
        if (normalized is null)
        {
            return null;
        }

        if (normalized.Length > 64 || !PackCodePattern().IsMatch(normalized))
        {
            throw new PortalValidationException();
        }

        return normalized;
    }

    private static IReadOnlyList<string> ValidateReferenceList(
        IReadOnlyList<string>? values,
        Regex pattern)
    {
        if (values is null || values.Count == 0)
        {
            return Array.Empty<string>();
        }

        if (values.Count > MaxReferenceListLength)
        {
            throw new PortalValidationException();
        }

        var normalized = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            var entry = Normalize(value);
            if (entry is null
                || entry.Length > 100
                || !pattern.IsMatch(entry))
            {
                throw new PortalValidationException();
            }

            normalized.Add(entry);
        }

        return normalized.ToArray();
    }

    private static ValidatedCommercialDocument ValidateDocumentPayload(
        CommercialDocumentPayload payload,
        bool creating)
    {
        var customerReference = creating
            ? ValidateCustomerReference(payload.CustomerReference)
            : ValidateOptionalCustomerReference(payload.CustomerReference);
        var documentType = Normalize(payload.DocumentType);
        if (documentType is null
            || !CommercialStatuses.DocumentTypes.Contains(documentType))
        {
            throw new PortalValidationException();
        }

        var title = ValidateText(payload.Title, 3, MaxShortTextLength);
        var currency = Normalize(payload.Currency) ?? "EUR";
        if (!string.Equals(currency, "EUR", StringComparison.Ordinal))
        {
            throw new PortalValidationException();
        }

        var disclaimer = Normalize(payload.Disclaimer);
        disclaimer = string.IsNullOrWhiteSpace(disclaimer)
            ? CommercialStatuses.DefaultDisclaimer
            : ValidateText(disclaimer, 10, MaxDisclaimerLength);

        var status = Normalize(payload.Status) ?? CommercialStatuses.Draft;
        if (!CommercialStatuses.MutableDocument.Contains(status))
        {
            throw new PortalValidationException();
        }

        return new ValidatedCommercialDocument(
            customerReference,
            documentType,
            title,
            currency,
            NormalizeNullableIdentifier(payload.ServiceRequestId),
            disclaimer,
            status);
    }

    private static ValidatedCommercialDocumentLine ValidateLinePayload(
        CommercialDocumentLinePayload payload)
    {
        var quantity = payload.Quantity ?? throw new PortalValidationException();
        if (quantity <= 0 || quantity > MaxQuantity)
        {
            throw new PortalValidationException();
        }

        if (decimal.Round(quantity, 2) != quantity)
        {
            throw new PortalValidationException();
        }

        var unitPriceCents = payload.UnitPriceCents;
        if (unitPriceCents is < 0 or > MaxPriceAmountCents)
        {
            throw new PortalValidationException();
        }

        var taxRateBasisPoints = payload.TaxRateBasisPoints;
        if (taxRateBasisPoints is < 0 or > MaxTaxRateBasisPoints)
        {
            throw new PortalValidationException();
        }

        var sortOrder = payload.SortOrder ?? 0;
        if (sortOrder is < 0 or > MaxSortOrder)
        {
            throw new PortalValidationException();
        }

        // Une ligne doit se suffire a elle-meme : sans libelle, il ne reste
        // plus rien pour la decrire une fois le catalogue modifie.
        var label = Normalize(payload.Label);
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new PortalValidationException();
        }

        return new ValidatedCommercialDocumentLine(
            ValidateText(label, 2, MaxShortTextLength),
            ValidateOptionalText(payload.Description, MaxDescriptionLength),
            quantity,
            ValidateNullableText(payload.UnitLabel, 40),
            unitPriceCents,
            taxRateBasisPoints,
            sortOrder);
    }

    private static string ValidateIdentifier(string value)
    {
        var normalized = Normalize(value);
        if (normalized is null || !IdentifierPattern().IsMatch(normalized))
        {
            throw new PortalValidationException();
        }

        return normalized;
    }

    private static string ValidateCustomerReference(string? value)
        => ValidateOptionalCustomerReference(value)
            ?? throw new PortalValidationException();

    private static string? ValidateOptionalCustomerReference(string? value)
    {
        var normalized = Normalize(value);
        if (normalized is null)
        {
            return null;
        }

        if (normalized.Length > 80 || !IdentifierPattern().IsMatch(normalized))
        {
            throw new PortalValidationException();
        }

        return normalized;
    }

    private static string? NormalizeNullableIdentifier(string? value)
    {
        var normalized = Normalize(value);
        return normalized is null ? null : ValidateIdentifier(normalized);
    }

    private static string ValidateText(
        string? value,
        int minimumLength,
        int maximumLength)
    {
        var normalized = Normalize(value);
        if (normalized is null
            || normalized.Length < minimumLength
            || normalized.Length > maximumLength)
        {
            throw new PortalValidationException();
        }

        return normalized;
    }

    private static string ValidateOptionalText(string? value, int maximumLength)
    {
        var normalized = Normalize(value);
        if (normalized is null)
        {
            return string.Empty;
        }

        if (normalized.Length > maximumLength)
        {
            throw new PortalValidationException();
        }

        return normalized;
    }

    private static string? ValidateNullableText(string? value, int maximumLength)
    {
        var normalized = Normalize(value);
        if (normalized is null)
        {
            return null;
        }

        if (normalized.Length > maximumLength)
        {
            throw new PortalValidationException();
        }

        return normalized;
    }

    private static string? Normalize(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    [GeneratedRegex("^[A-Za-z0-9-]{1,100}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierPattern();

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex PayPalPlanIdPattern();

    [GeneratedRegex("^[a-z0-9-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex PackCodePattern();

    [GeneratedRegex(
        "^[A-Za-z0-9._-]{1,100}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex TechnicalReferencePattern();

    [GeneratedRegex(
        "^[A-Za-z0-9._-]{1,100}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex GroupSamAccountNamePattern();
}
