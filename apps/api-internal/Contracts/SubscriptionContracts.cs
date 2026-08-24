using System.Text.Json.Serialization;

namespace Kermaria.ApiInternal.Contracts;

/// <summary>
/// Charge utile du checkout authoritative. `Selection` porte une configuration
/// V2 native — formule ou composants choisis directement. Aucun montant n'y
/// figure — ni prix, ni remise, ni reference de prix fournisseur : tout est
/// recalcule par API-INTERNAL.
/// </summary>
public sealed record BillingV2AuthoritativeCheckoutPayload(
    Services.BillingV2PublicSelectionInput? Selection,
    string? Provider,
    string? IdempotencyKey,
    string? SuccessUrl,
    string? CancelUrl);

public sealed record BillingV2AuthoritativeCheckoutResponse(
    bool Created,
    string SubscriptionId,
    string Provider,
    string Environment,
    string OutboxEventId,
    string IdempotencyKeyHash,
    long TotalDueNowCents,
    string ReasonCode,
    string? ApprovalUrl,
    string CorrelationId);

public sealed record BillingV2ProviderReturnPayload(
    string? Provider,
    string? ProviderCheckoutId,
    string? ProviderSubscriptionId,
    string? RawPayload);

public sealed record SubscriptionSummary(
    string Id,
    string CustomerId,
    string CustomerReference,
    string CustomerName,

    // Identite V2 de la configuration d'origine. Nulle pour une souscription
    // directe : elle n'est rattachee a aucune formule, et forger un
    // identifiant pour combler ce vide reintroduirait une fausse offre.
    [property: JsonPropertyName("presetId")] string? PresetId,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("presetCode")] string? PresetCode,
    string Rail,
    [property: JsonPropertyName("paypalPlanId")] string? PayPalPlanId,
    [property: JsonPropertyName("paypalSubscriptionId")] string? PayPalSubscriptionId,
    [property: JsonPropertyName("stripePriceId")] string? StripePriceId,
    [property: JsonPropertyName("stripeSubscriptionId")] string? StripeSubscriptionId,
    string Status,
    int PriceAmountCents,
    [property: JsonPropertyName("setupFeeAmountCents")] int SetupFeeAmountCents,
    int? TaxRateBasisPoints,
    string FiscalRegime,
    string FiscalMention,
    [property: JsonPropertyName("billingIntervalMonths")] int BillingIntervalMonths,
    [property: JsonPropertyName("commitmentMonths")] int CommitmentMonths,
    [property: JsonPropertyName("paymentMode")] string PaymentMode,
    [property: JsonPropertyName("paidCyclesCount")] int PaidCyclesCount,
    [property: JsonPropertyName("commitmentEndsAt")] string? CommitmentEndsAt,
    [property: JsonPropertyName("cancelRequestedAt")] string? CancelRequestedAt,
    [property: JsonPropertyName("cancelAtTermEnd")] bool CancelAtTermEnd,
    string Currency,
    string? StartedAt,
    string? NextBillingAt,
    string? CancelledAt,
    string CreatedAt,
    string UpdatedAt,
    [property: JsonPropertyName("billingSystem")] string BillingSystem = "billing_v2",

    // Places USER-ADDITIONAL vendues sur cet abonnement, et places
    // effectivement pourvues.
    [property: JsonPropertyName("additionalUserSlotsCount")]
    int AdditionalUserSlotsCount = 0,
    [property: JsonPropertyName("assignedAdditionalUsersCount")]
    int AssignedAdditionalUsersCount = 0);

/// <summary>
/// Personne a installer sur une place USER-ADDITIONAL.
/// </summary>
/// <remarks>
/// Ce que le navigateur n'envoie <b>pas</b> est aussi important que ce qu'il
/// envoie : ni client, ni acteur, ni identifiant d'utilisateur portail. Ces
/// trois valeurs viennent de la session cote serveur ; les accepter du client
/// permettrait d'equiper la place d'un autre client.
/// </remarks>
public sealed record BillingV2AdditionalUserAssignPayload(
    string? Email,
    [property: JsonPropertyName("displayName")] string? DisplayName,
    [property: JsonPropertyName("personalTitle")] string? PersonalTitle,
    [property: JsonPropertyName("givenName")] string? GivenName,
    string? Surname,
    // Jour civil, format ISO `yyyy-MM-dd`. Pas un horodatage : une date de
    // naissance n'a pas de fuseau.
    [property: JsonPropertyName("birthDate")] string? BirthDate,
    string? Initials,
    string? Phone);

public sealed record BillingV2AdditionalUserSetPasswordPayload(
    string? Token,
    string? Password);

public sealed record SubscriptionProvisioningTargetUserSummary(
    string SamAccountName,
    string DisplayName,
    string? UserPrincipalName);

public sealed record SubscriptionProvisioningReconcileRequest(
    IReadOnlyList<string>? TargetUserSamAccountNames);

public sealed record SubscriptionProvisioningActionSummary(
    string Id,
    string ActionType,
    string Status,
    string? ResultCode,
    bool Changed,
    string CorrelationId,
    string TargetReference,
    string RequestedAt,
    string? StartedAt,
    string? CompletedAt);

public sealed record SubscriptionProvisioningSummary(
    string Status,
    IReadOnlyList<string> MappedGroups,
    IReadOnlyList<string> ReconciledGroups,
    IReadOnlyList<SubscriptionProvisioningTargetUserSummary> TargetUsers,
    bool CanRetry,
    string? LastResultCode,
    IReadOnlyList<SubscriptionProvisioningActionSummary> RecentActions);

public sealed record AdminSubscriptionDetail(
    SubscriptionSummary Subscription,
    IReadOnlyList<CommercialDocumentSummary> Documents,
    SubscriptionProvisioningSummary Provisioning);
