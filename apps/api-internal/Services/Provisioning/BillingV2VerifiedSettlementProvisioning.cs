namespace Kermaria.ApiInternal.Services.Provisioning;

public sealed record BillingV2VerifiedSettlementProvisioningResult(
    string ResultCode,
    ProvisioningExecutionResult? Execution);

/// <summary>
/// Effet post-reglement : jamais une autorite de paiement ou de readiness.
/// Un echec de provisioning ne doit pas annuler un paiement deja verifie.
/// </summary>
public static class BillingV2VerifiedSettlementProvisioning
{
    public static async Task<BillingV2VerifiedSettlementProvisioningResult> TryExecuteAsync(
        bool settlementVerified,
        string subscriptionId,
        Func<string, CancellationToken, Task<bool>> requiresVpsReview,
        Func<string, CancellationToken, Task<ProvisioningExecutionResult?>> reconcile,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!settlementVerified) return new("BILLING_V2_PAYMENT_NOT_VERIFIED", null);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (await requiresVpsReview(subscriptionId, cancellationToken))
                return new("BILLING_V2_VPS_TECHNICAL_REVIEW_REQUIRED", null);
            var result = await reconcile(subscriptionId, cancellationToken);
            return new(result?.ResultCode ?? "BILLING_V2_PROVISIONING_NOT_EXECUTED", result);
        }
        catch (Exception exception) when (
            BillingV2ProviderInboundProvisioningFailurePolicy.ShouldKeepProviderEventProcessed(exception))
        {
            logger.LogWarning(
                "Billing V2 provisioning trigger failed after verified settlement for subscription {SubscriptionId}; exception_type {ExceptionType}. Financial settlement is preserved.",
                subscriptionId, exception.GetType().Name);
            // Une exception peut suivre une ecriture externe partielle :
            // ne pas fabriquer un verdict Changed=false sur cet etat inconnu.
            return new("BILLING_V2_PROVISIONING_TRIGGER_FAILED", null);
        }
    }
}
