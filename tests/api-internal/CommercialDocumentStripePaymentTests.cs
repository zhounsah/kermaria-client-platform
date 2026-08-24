using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services;

using Microsoft.Extensions.Logging;

namespace Kermaria.ApiInternal.SmokeTests;

/// <summary>
/// Reglement Stripe d'un document commercial.
/// </summary>
/// <remarks>
/// Ce chemin est le seul qui confirme un paiement Stripe de document : le
/// retour navigateur Stripe n'est qu'une redirection, contrairement a PayPal
/// qui confirme depuis sa route de retour. Il partage l'endpoint webhook avec
/// Billing V2, d'ou l'attention portee ici a ne PAS confondre les deux rails.
/// </remarks>
public static class CommercialDocumentStripePaymentTests
{
    public static async Task RunAsync()
    {
        await VerifyDocumentPaymentIsConfirmedAsync();
        await VerifyInvoiceBackedIntentIsLeftToBillingV2Async();
        await VerifyIntentWithoutDocumentIsIgnoredAsync();
        await VerifyOtherEventTypesAreIgnoredAsync();
        await VerifyTransientConfirmationFailureIsRetriedAsync();
        await VerifyPermanentConfirmationFailureIsAcknowledgedAsync();
    }

    private static async Task VerifyDocumentPaymentIsConfirmedAsync()
    {
        var issuing = new RecordingInvoiceIssuingService();
        var status = await Service(issuing)
            .HandlePaymentIntentSucceededAsync(
                Payload(Raw(documentId: "doc-42")),
                "corr-1",
                CancellationToken.None);

        Ensure(
            status == "processed"
            && issuing.Confirmations.Count == 1
            && issuing.Confirmations[0] == ("doc-42", "stripe"),
            "Un payment_intent portant metadata.document_id doit marquer le document paye en Stripe.");
    }

    private static async Task VerifyInvoiceBackedIntentIsLeftToBillingV2Async()
    {
        var issuing = new RecordingInvoiceIssuingService();
        var status = await Service(issuing)
            .HandlePaymentIntentSucceededAsync(
                Payload(Raw(documentId: "doc-42", invoiceId: "in_123")),
                "corr-2",
                CancellationToken.None);

        Ensure(
            status == "ignored" && issuing.Confirmations.Count == 0,
            "Un payment_intent rattache a une invoice appartient au rail d'abonnement : "
            + "le confirmer ici marquerait un document paye sur un evenement qui ne le concerne pas.");
    }

    private static async Task VerifyIntentWithoutDocumentIsIgnoredAsync()
    {
        var issuing = new RecordingInvoiceIssuingService();
        var status = await Service(issuing)
            .HandlePaymentIntentSucceededAsync(
                Payload(Raw(documentId: null)),
                "corr-3",
                CancellationToken.None);

        Ensure(
            status == "ignored" && issuing.Confirmations.Count == 0,
            "Sans metadata.document_id, aucun document ne doit etre devine.");
    }

    private static async Task VerifyOtherEventTypesAreIgnoredAsync()
    {
        var issuing = new RecordingInvoiceIssuingService();
        var status = await Service(issuing)
            .HandlePaymentIntentSucceededAsync(
                new StripeWebhookEventPayload(
                    "evt_1",
                    "checkout.session.completed",
                    "cs_1",
                    Raw(documentId: "doc-42")),
                "corr-4",
                CancellationToken.None);

        Ensure(
            status == "ignored" && issuing.Confirmations.Count == 0,
            "Seul payment_intent.succeeded regle un document : les autres evenements "
            + "appartiennent au rail Billing V2.");
    }

    private static async Task VerifyTransientConfirmationFailureIsRetriedAsync()
    {
        var issuing = new RecordingInvoiceIssuingService
        {
            ConfirmFailureCode = "BPCE_UNAVAILABLE"
        };

        var raised = false;
        try
        {
            await Service(issuing).HandlePaymentIntentSucceededAsync(
                Payload(Raw(documentId: "doc-42")),
                "corr-5",
                CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            raised = true;
        }

        Ensure(
            raised,
            "Un echec transitoire doit lever : acquitter en 200 perdrait "
            + "definitivement le reglement, Stripe ne rejouant plus l'evenement.");
    }

    private static async Task VerifyPermanentConfirmationFailureIsAcknowledgedAsync()
    {
        // Stripe reessaie un endpoint en echec pendant plusieurs jours puis le
        // desactive. Rejouer sans fin une erreur definitive finirait donc par
        // couper aussi le rail d'abonnement Billing V2, qui partage ce webhook.
        var issuing = new RecordingInvoiceIssuingService
        {
            ConfirmFailureCode = "INVOICE_NOT_FOUND"
        };

        var status = await Service(issuing).HandlePaymentIntentSucceededAsync(
            Payload(Raw(documentId: "doc-inconnu")),
            "corr-6",
            CancellationToken.None);

        Ensure(
            status == "ignored",
            "Un document sans facture emise ne peut pas etre confirme par un "
            + "rejeu : l'evenement doit etre acquitte, pas boucle.");
    }

    private static CommercialDocumentStripePaymentService Service(
        IInvoiceIssuingService issuing)
        => new(
            issuing,
            LoggerFactory.Create(_ => { })
                .CreateLogger<CommercialDocumentStripePaymentService>());

    private static StripeWebhookEventPayload Payload(string raw)
        => new("evt_1", "payment_intent.succeeded", "pi_1", raw);

    private static string Raw(string? documentId, string? invoiceId = null)
    {
        var metadata = documentId is null
            ? "{}"
            : "{\"document_id\":\"" + documentId + "\"}";
        var invoice = invoiceId is null
            ? string.Empty
            : ",\"invoice\":\"" + invoiceId + "\"";

        return "{\"data\":{\"object\":{\"id\":\"pi_1\",\"metadata\":"
            + metadata + invoice + "}}}";
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class RecordingInvoiceIssuingService : IInvoiceIssuingService
    {
        public string? ConfirmFailureCode { get; init; }

        public List<(string DocumentId, string PaymentMethod)> Confirmations
        { get; } = [];

        public Task<IssueInvoiceResult> IssueInvoiceAsync(
            string documentId,
            bool sendEmail,
            string correlationId,
            CancellationToken cancellationToken)
            => Task.FromResult(new IssueInvoiceResult(
                true, "INVOICE_ISSUED", "Emise."));

        public Task<IssueInvoiceResult> ConfirmPaymentAsync(
            string documentId,
            string correlationId,
            string paymentMethod,
            CancellationToken cancellationToken)
        {
            if (ConfirmFailureCode is not null)
            {
                return Task.FromResult(new IssueInvoiceResult(
                    false, ConfirmFailureCode, "Echec simule."));
            }

            Confirmations.Add((documentId, paymentMethod));
            return Task.FromResult(new IssueInvoiceResult(
                true, "PAYMENT_CONFIRMED", "Payee."));
        }

        public Task<byte[]?> GetCachedInvoicePdfAsync(
            string documentId, CancellationToken cancellationToken)
            => Task.FromResult<byte[]?>(null);

        public Task<byte[]?> EnsureInvoicePdfAsync(
            string documentId, CancellationToken cancellationToken)
            => Task.FromResult<byte[]?>(null);

        public Task<BpceInvoiceRecord?> GetInvoiceRecordAsync(
            string documentId, CancellationToken cancellationToken)
            => Task.FromResult<BpceInvoiceRecord?>(null);
    }
}
