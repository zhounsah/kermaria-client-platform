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
        CancellationToken cancellationToken);
}

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
