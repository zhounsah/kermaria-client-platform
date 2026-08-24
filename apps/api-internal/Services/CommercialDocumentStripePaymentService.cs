using System.Text.Json;

using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Confirmation d'un reglement Stripe portant sur un document commercial.
/// </summary>
/// <remarks>
/// A ne pas confondre avec le rail d'abonnement : Billing V2 traite les
/// evenements d'abonnement et de cycle, et cette classe ne voit que les
/// paiements ponctuels d'un devis ou d'une facture. Le retour navigateur
/// Stripe n'est qu'une redirection — contrairement a PayPal, il ne confirme
/// rien — donc sans ce chemin, une facture reglee par carte resterait
/// indefiniment impayee cote BPCE.
/// </remarks>
public interface ICommercialDocumentStripePaymentService
{
    /// <summary>
    /// Retourne <c>"processed"</c> si le document a ete marque paye,
    /// <c>"ignored"</c> si l'evenement ne concerne pas un document.
    /// </summary>
    Task<string> HandlePaymentIntentSucceededAsync(
        StripeWebhookEventPayload payload,
        string correlationId,
        CancellationToken cancellationToken);
}

public sealed class CommercialDocumentStripePaymentService
    : ICommercialDocumentStripePaymentService
{
    private readonly IInvoiceIssuingService _issuing;
    private readonly ILogger<CommercialDocumentStripePaymentService> _logger;

    public CommercialDocumentStripePaymentService(
        IInvoiceIssuingService issuing,
        ILogger<CommercialDocumentStripePaymentService> logger)
    {
        _issuing = issuing;
        _logger = logger;
    }

    public async Task<string> HandlePaymentIntentSucceededAsync(
        StripeWebhookEventPayload payload,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
                payload.EventType,
                "payment_intent.succeeded",
                StringComparison.Ordinal))
        {
            return "ignored";
        }

        var rawPayload = payload.RawPayload;
        if (string.IsNullOrWhiteSpace(rawPayload))
        {
            return "ignored";
        }

        // Un payment_intent rattache a une invoice Stripe appartient au rail
        // d'abonnement : Billing V2 le traite par relecture de l'invoice, et le
        // confirmer ici marquerait un document comme paye sur la foi d'un
        // evenement qui ne le concerne pas.
        if (!string.IsNullOrWhiteSpace(
                ReadDataObjectString(rawPayload, "invoice")))
        {
            return "ignored";
        }

        var documentId = ReadDataObjectMetadataString(rawPayload, "document_id");
        if (string.IsNullOrWhiteSpace(documentId))
        {
            _logger.LogWarning(
                "Stripe payment_intent.succeeded {PaymentIntentId} ignore : metadata.document_id absent.",
                ReadDataObjectString(rawPayload, "id") ?? "<inconnu>");
            return "ignored";
        }

        var confirmResult = await _issuing.ConfirmPaymentAsync(
            documentId,
            correlationId,
            "stripe",
            cancellationToken);
        if (!confirmResult.Succeeded)
        {
            // Un echec definitif ne doit pas etre rejoue : Stripe reessaie
            // pendant plusieurs jours puis desactive l'endpoint, ce qui
            // couperait aussi le rail d'abonnement Billing V2. On acquitte
            // donc, en journalisant au niveau erreur pour que l'exploitant
            // rattrape le document a la main.
            if (string.Equals(
                    confirmResult.Code,
                    "INVOICE_NOT_FOUND",
                    StringComparison.Ordinal))
            {
                _logger.LogError(
                    "Stripe a confirme un paiement pour le document {DocumentId}, "
                    + "mais aucune facture emise ne lui correspond. Reglement a "
                    + "rattacher manuellement.",
                    documentId);
                return "ignored";
            }

            // Tout le reste est traite comme transitoire : l'appelant repond
            // 500 et Stripe rejoue. Repondre 200 perdrait le reglement.
            throw new InvalidOperationException(
                $"Stripe payment confirm failed: {confirmResult.Code} {confirmResult.Message}");
        }

        return "processed";
    }

    private static string? ReadDataObjectString(
        string rawPayload,
        string property)
    {
        try
        {
            using var document = JsonDocument.Parse(rawPayload);
            if (document.RootElement.TryGetProperty("data", out var data)
                && data.TryGetProperty("object", out var dataObject)
                && dataObject.TryGetProperty(property, out var value)
                && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static string? ReadDataObjectMetadataString(
        string rawPayload,
        string property)
    {
        try
        {
            using var document = JsonDocument.Parse(rawPayload);
            if (document.RootElement.TryGetProperty("data", out var data)
                && data.TryGetProperty("object", out var dataObject)
                && dataObject.TryGetProperty("metadata", out var metadata)
                && metadata.TryGetProperty(property, out var value)
                && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }
}
