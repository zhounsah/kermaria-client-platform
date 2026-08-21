namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Seule policy autorisee a transformer un droit regle en etat de fulfillment.
/// Un acknowledge contractuel n'est jamais assimile a la livraison d'un service
/// humain ou d'une ressource technique.
/// </summary>
public static class BillingV2FulfillmentPolicy
{
    public const string Pending = "pending";
    public const string InProgress = "in_progress";
    public const string Fulfilled = "fulfilled";
    public const string Failed = "failed";

    public static string InitialStatus(string fulfillmentMode)
        => fulfillmentMode switch
        {
            "contractual_acknowledgement" => Fulfilled,
            "manual_delivery" or "technical_provisioning" => Pending,
            _ => throw new InvalidOperationException("BILLING_V2_FULFILLMENT_MODE_UNKNOWN")
        };

    public static bool CanTransition(string current, string next)
        => (current, next) switch
        {
            (Pending, InProgress) => true,
            (Pending, Failed) => true,
            (InProgress, Fulfilled) => true,
            (InProgress, Failed) => true,
            (Failed, InProgress) => true,
            _ => false
        };
}
