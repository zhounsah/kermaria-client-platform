using Kermaria.ApiInternal.Data.Repositories;

namespace Kermaria.ApiInternal.Services.Provisioning;

// V0.35 — provisioning « le cas echeant » au reglement d'une commande panier.
//
// Declenche apres le passage `issued -> paid` d'un document dont l'origine est
// `client_cart`, quel que soit le rail (Stripe / PayPal / virement manuel :
// tous convergent sur InvoiceIssuingService.ConfirmPaymentAsync).
//
// Le provisioning du projet est pilote par les abonnements actifs (V0.31 :
// offre recurrente -> groupe AD). Le panier V0.35 est strictement one-shot ;
// aucune offre one-shot du catalogue courant ne mappe un groupe. Ce
// declencheur reconcilie donc l'etat AD du client a partir de ses abonnements
// actifs — inerte pour un panier purement one-shot, mais correctement cable si
// une offre one-shot provisionnable est ajoutee au catalogue plus tard.
public interface ICartProvisioningTrigger
{
    Task OnDocumentPaidAsync(
        string documentId,
        string correlationId,
        CancellationToken cancellationToken);
}

public sealed class CartProvisioningTrigger : ICartProvisioningTrigger
{
    private const string CartOrigin = "client_cart";

    private readonly ICommercialRepository _commercial;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly ISubscriptionProvisioningManager _provisioning;
    private readonly IAuditService _audit;
    private readonly ILogger<CartProvisioningTrigger> _logger;

    public CartProvisioningTrigger(
        ICommercialRepository commercial,
        ISubscriptionRepository subscriptions,
        ISubscriptionProvisioningManager provisioning,
        IAuditService audit,
        ILogger<CartProvisioningTrigger> logger)
    {
        _commercial = commercial;
        _subscriptions = subscriptions;
        _provisioning = provisioning;
        _audit = audit;
        _logger = logger;
    }

    public async Task OnDocumentPaidAsync(
        string documentId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        CartPaidDocumentContext? context;
        try
        {
            context = await _commercial.GetCartPaidDocumentContextAsync(
                documentId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Cart provisioning: unable to read document {DocumentId} context — skipping",
                documentId);
            return;
        }

        if (context is null
            || !string.Equals(context.Origin, CartOrigin, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            var subscriptions = await _subscriptions.GetByCustomerAsync(
                context.CustomerId,
                cancellationToken);
            if (subscriptions.Count == 0)
            {
                _logger.LogInformation(
                    "Cart payment {DocumentId}: no subscription for customer {CustomerId} — one-shot provisioning is a no-op",
                    documentId,
                    context.CustomerId);
                await _audit.RecordAsync(
                    new AuditEvent(
                        correlationId,
                        "cart.provisioning.skipped",
                        "skipped",
                        ReasonCode: "NO_PROVISIONABLE_MAPPING",
                        TargetType: "commercial_document",
                        TargetReference: documentId,
                        CustomerId: context.CustomerId),
                    cancellationToken);
                return;
            }

            // ReconcileAsync reconcilie l'union des groupes de tous les
            // abonnements actifs du client : un seul appel suffit.
            await _provisioning.ReconcileAsync(
                subscriptions[0],
                "cart_payment_reconcile",
                correlationId,
                requestedByUserId: null,
                cancellationToken);
            await _audit.RecordAsync(
                new AuditEvent(
                    correlationId,
                    "cart.provisioning.reconciled",
                    "success",
                    TargetType: "commercial_document",
                    TargetReference: documentId,
                    CustomerId: context.CustomerId),
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Best-effort : le paiement est deja enregistre, on ne le casse pas.
            _logger.LogError(
                ex,
                "Cart provisioning reconcile failed for document {DocumentId} — payment already recorded",
                documentId);
        }
    }
}
