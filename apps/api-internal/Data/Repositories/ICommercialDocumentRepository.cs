using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.Data.Repositories;

/// <summary>
/// Documents commerciaux : devis, factures et leurs lignes.
/// </summary>
/// <remarks>
/// <para>
/// Cette abstraction ne connait <b>aucun</b> catalogue. Le catalogue commercial
/// est Billing V2 et n'est pas administre ici ; un document, lui, est une piece
/// datee qui doit rester lisible telle qu'elle a ete emise. Melanger les deux
/// dans un meme depot avait pour effet qu'une ligne de facture allait rechercher
/// son libelle et son prix dans le catalogue <i>courant</i> : reediter une
/// facture apres un changement de tarif en changeait le montant affiche.
/// </para>
/// <para>
/// Chaque ligne porte donc son propre instantane — libelle, description,
/// quantite, unite, prix unitaire, taux de TVA — et ne reference plus d'offre.
/// </para>
/// </remarks>
public interface ICommercialDocumentRepository
{
    bool IsPersistent { get; }

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

    // Rattachement par `billing_v2_subscription_documents`, la seule table de
    // liaison encore alimentee. L'ancienne colonne
    // `commercial_documents.subscription_id` pointait la table `subscriptions`,
    // qui n'existe plus.
    Task<IReadOnlyList<CommercialDocumentSummary>>
        GetDocumentsForSubscriptionAsync(
            string subscriptionId,
            CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetLinkedSubscriptionIdsForDocumentAsync(
        string documentId,
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
