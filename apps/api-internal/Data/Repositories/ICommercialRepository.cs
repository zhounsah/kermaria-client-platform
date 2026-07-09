using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.Data.Repositories;

public interface ICommercialRepository
{
    bool IsPersistent { get; }

    Task<IReadOnlyList<CommercialOfferSummary>> GetClientCatalogAsync(
        CancellationToken cancellationToken);
    Task<IReadOnlyList<CommercialOfferSummary>> GetAdminCatalogAsync(
        CancellationToken cancellationToken);
    Task<CommercialOfferMutationResponse> CreateOfferAsync(
        ValidatedCommercialOffer offer,
        string correlationId,
        CancellationToken cancellationToken);
    Task<CommercialOfferMutationResponse> UpdateOfferAsync(
        string offerId,
        ValidatedCommercialOffer offer,
        string correlationId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<CommercialDocumentSummary>> GetClientDocumentsAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken);
    Task<CommercialDocumentDetail?> GetClientDocumentAsync(
        PortalSessionContext session,
        string documentId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminCommercialDocumentSummary>> GetAdminDocumentsAsync(
        CancellationToken cancellationToken);
    Task<AdminCommercialDocumentDetail?> GetAdminDocumentAsync(
        string documentId,
        CancellationToken cancellationToken);
    Task<CommercialDocumentMutationResponse> CreateDocumentAsync(
        PortalSessionContext actor,
        ValidatedCommercialDocument document,
        string correlationId,
        CancellationToken cancellationToken);
    Task<CommercialDocumentMutationResponse> UpdateDocumentAsync(
        PortalSessionContext actor,
        string documentId,
        ValidatedCommercialDocument document,
        string correlationId,
        CancellationToken cancellationToken);
    Task<CommercialDocumentLineMutationResponse> AddLineAsync(
        PortalSessionContext actor,
        string documentId,
        ValidatedCommercialDocumentLine line,
        string correlationId,
        CancellationToken cancellationToken);
    Task<CommercialDocumentLineMutationResponse> UpdateLineAsync(
        PortalSessionContext actor,
        string documentId,
        string lineId,
        ValidatedCommercialDocumentLine line,
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

    Task<DocumentForIssuing?> GetDocumentForIssuingAsync(
        string documentId,
        CancellationToken cancellationToken);

    Task MarkDocumentIssuedAsync(
        string documentId,
        string correlationId,
        CancellationToken cancellationToken);

    Task MarkDocumentPaidAsync(
        string documentId,
        string correlationId,
        string paymentMethod,
        CancellationToken cancellationToken);

    Task SetDocumentPaymentMethodAsync(
        string documentId,
        string? paymentMethod,
        CancellationToken cancellationToken);

    Task<string> CreateBillingDocumentForSubscriptionAsync(
        SubscriptionBillingDocumentRequest request,
        string correlationId,
        CancellationToken cancellationToken);

    Task<RecurringCheckoutDocumentCreationResult> CreateRecurringCheckoutDocumentAsync(
        RecurringCheckoutDocumentRequest request,
        string correlationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CommercialDocumentSummary>>
        GetDocumentsForSubscriptionAsync(
            string subscriptionId,
            CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetLinkedSubscriptionIdsForDocumentAsync(
        string documentId,
        CancellationToken cancellationToken);

    // V0.35 — panier a la carte : materialise le panier confirme en un unique
    // document commercial multi-lignes (statut shared_with_customer, prêt a
    // etre emis), tag origin = 'client_cart'.
    Task<CartDocumentCreationResult> CreateCartDocumentAsync(
        string customerId,
        string actorUserId,
        string title,
        IReadOnlyList<CartDocumentLineInput> lines,
        string correlationId,
        CancellationToken cancellationToken);

    // Contexte minimal utilise au reglement pour cibler le provisioning
    // « le cas echeant » sans impacter les autres documents.
    Task<CartPaidDocumentContext?> GetCartPaidDocumentContextAsync(
        string documentId,
        CancellationToken cancellationToken);
}

public sealed record CartDocumentLineInput(
    string OfferId,
    string Label,
    string Description,
    decimal Quantity,
    string UnitLabel,
    int UnitPriceCents,
    int? TaxRateBasisPoints,
    int SortOrder);

public sealed record CartDocumentCreationResult(
    string DocumentId,
    int TotalAmountCents);

public sealed record CartPaidDocumentContext(
    string? Origin,
    string CustomerId);

public sealed record DocumentForIssuing(
    string DocumentId,
    string CustomerId,
    string CustomerExternalReference,
    string CustomerDisplayName,
    string? CustomerBillingEmail,
    string? CustomerAddress,
    string? CustomerCity,
    string? CustomerCountry,
    string DocumentTitle,
    string InternalReference,
    string Currency,
    int TotalAmountCents,
    string Status,
    IReadOnlyList<CommercialDocumentLine> Lines);

public sealed record SubscriptionBillingDocumentRequest(
    string CustomerId,
    string OfferId,
    string SubscriptionId,
    string Title,
    IReadOnlyList<SubscriptionBillingDocumentLineRequest> Lines);

public sealed record SubscriptionBillingDocumentLineRequest(
    string? OfferId,
    string Label,
    string Description,
    decimal Quantity,
    string UnitLabel,
    int UnitPriceCents,
    int? TaxRateBasisPoints,
    int SortOrder);

public sealed record RecurringCheckoutDocumentRequest(
    string CustomerId,
    string ActorUserId,
    string Title,
    IReadOnlyList<RecurringCheckoutDocumentSubscriptionRequest> Items);

public sealed record RecurringCheckoutDocumentSubscriptionRequest(
    string SubscriptionId,
    string OfferId,
    int SetupFeeAmountCents);

public sealed record RecurringCheckoutDocumentCreationResult(
    string DocumentId,
    int TotalAmountCents);
