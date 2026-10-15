using System.Security.Cryptography;
using System.Text;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Fenêtre de recette strictement serveur. Elle autorise une seule intention
/// financière, pour un seul utilisateur et une seule sélection. Le flag
/// Checkout général reste éteint ; les webhooks et la réconciliation gardent
/// leur propre chemin de traitement après l'expiration de cette fenêtre.
/// </summary>
public static class BillingV2PrivateVatTestPolicy
{
    public const string LiveTaxRateId = "txr_1UQS5FPeE7Jrh43pSi6OSIyc";
    public const string SelectionCanonical =
        "billing_v2.public_selection.components|-|FLEX|monthly|STORAGE-SHARED/64/1";
    public const long TotalCents = 590;
    public const long TaxCents = 98;
    public const string IdempotencyPrefix = "private-vat-test:";
    public static readonly TimeSpan MinimumSessionRemaining = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan MaximumWindow = TimeSpan.FromHours(2);

    public static string SelectionFingerprint => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(SelectionCanonical)))
        .ToLowerInvariant();

    public static BillingV2PrivateVatTestDecision CheckActor(
        BillingV2RuntimeConfiguration runtime,
        StripeRuntimeConfiguration stripe,
        PortalSessionContext session,
        DateTime nowUtc)
    {
        var configuration = CheckConfiguration(runtime, stripe, nowUtc);
        if (!configuration.Authorized) return configuration;
        if (!string.Equals(session.UserRole, "client_user", StringComparison.Ordinal)
            || !string.Equals(session.UserStatus, "active", StringComparison.Ordinal)
            || !string.Equals(session.UserId, runtime.PrivateVatTestUserId,
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(session.CustomerId, runtime.PrivateVatTestCustomerId,
                StringComparison.OrdinalIgnoreCase)
            || session.ExpiresAtUtc <= nowUtc)
        {
            return Blocked("PRIVATE_VAT_TEST_ACCOUNT_NOT_ALLOWED");
        }
        return configuration;
    }

    public static BillingV2PrivateVatTestDecision CheckSelectionAndAmount(
        BillingV2RuntimeConfiguration runtime,
        StripeRuntimeConfiguration stripe,
        PortalSessionContext session,
        BillingV2AuthoritativeCheckoutRequest request,
        BillingV2AuthoritativeCheckoutComposition composition,
        BillingV2PricingResult pricing,
        string provider,
        DateTime nowUtc)
    {
        var actor = CheckActor(runtime, stripe, session, nowUtc);
        if (!actor.Authorized) return actor;
        var shape = CheckRequestShape(request, provider);
        if (!shape.Authorized) return shape;
        if (!string.Equals(composition.SelectionFingerprint,
                SelectionFingerprint, StringComparison.Ordinal)
            || !string.Equals(composition.PaymentMode,
                BillingV2PaymentModes.Monthly, StringComparison.Ordinal)
            || composition.CommitmentMonths != 1
            || composition.DiscountBasisPoints != 0
            || composition.Items.Count != 1
            || pricing.TotalDueNowCents != TotalCents
            || pricing.PayableRecurringAmountCents != TotalCents
            || pricing.OneTimeSubtotalCents != 0
            || pricing.RecurringDiscountCents != 0)
        {
            return Blocked("PRIVATE_VAT_TEST_SELECTION_OR_AMOUNT_MISMATCH");
        }
        return actor;
    }

    public static BillingV2PrivateVatTestDecision CheckRequestShape(
        BillingV2AuthoritativeCheckoutRequest request,
        string provider)
    {
        if (!string.Equals(provider, "stripe", StringComparison.Ordinal)
            || request.Selection is null
            || request.CompositionOverride is not null
            || request.CartCheckoutLink is not null
            || request.TechnicalRequestId is not null
            || !string.Equals(request.Selection.Canonical(), SelectionCanonical,
                StringComparison.Ordinal))
            return Blocked("PRIVATE_VAT_TEST_SELECTION_OR_AMOUNT_MISMATCH");
        return new BillingV2PrivateVatTestDecision(
            true, "PRIVATE_VAT_TEST_REQUEST_SHAPE_ALLOWED", null, null);
    }

    public static BillingV2PrivateVatTestDecision CheckDispatch(
        BillingV2RuntimeConfiguration runtime,
        StripeRuntimeConfiguration stripe,
        string? actorUserId,
        string? customerId,
        string? idempotencyKey,
        string? selectionFingerprint,
        BillingV2FinalizedBillingEvent billingEvent,
        DateTime nowUtc)
    {
        var configuration = CheckConfiguration(runtime, stripe, nowUtc);
        if (!configuration.Authorized) return configuration;
        if (!string.Equals(actorUserId, runtime.PrivateVatTestUserId,
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(customerId, runtime.PrivateVatTestCustomerId,
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(idempotencyKey, configuration.IdempotencyKey,
                StringComparison.Ordinal)
            || !string.Equals(selectionFingerprint, SelectionFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(billingEvent.CustomerId, customerId,
                StringComparison.OrdinalIgnoreCase)
            || billingEvent.TotalAmountCents != TotalCents
            || billingEvent.RecurringAmountCents != TotalCents
            || billingEvent.OneTimeAmountCents != 0
            || billingEvent.TaxAmountCents != TaxCents
            || billingEvent.LineCount != 1
            || !billingEvent.FrenchTaxable
            || billingEvent.Currency != "EUR"
            || billingEvent.PaymentModeSnapshot != BillingV2PaymentModes.Monthly
            || billingEvent.CommitmentMonthsSnapshot != 1)
        {
            return Blocked("PRIVATE_VAT_TEST_DISPATCH_SCOPE_MISMATCH");
        }
        return configuration;
    }

    public static BillingV2PrivateVatTestDecision CheckGateway(
        BillingV2RuntimeConfiguration runtime,
        StripeRuntimeConfiguration stripe,
        BillingV2StripeCheckoutRequest request,
        DateTime nowUtc)
    {
        if (!runtime.PrivateVatTestEnabled)
        {
            return runtime.AuthoritativeCheckoutEnabled
                ? new BillingV2PrivateVatTestDecision(
                    true, "BILLING_V2_CHECKOUT_OPEN", null, null)
                : Blocked("BILLING_V2_AUTHORITATIVE_CHECKOUT_FLAG_OFF");
        }
        var configuration = CheckConfiguration(runtime, stripe, nowUtc);
        if (!configuration.Authorized) return configuration;
        if (!string.Equals(request.PrivateVatTestId,
                runtime.PrivateVatTestId, StringComparison.OrdinalIgnoreCase)
            || request.ExpiresAtUtc != configuration.ExpiresAtUtc
            || !string.Equals(request.Mode,
                BillingV2StripeModes.Subscription, StringComparison.Ordinal)
            || request.ExpectedAmountCents != TotalCents
            || request.Currency != "EUR"
            || request.Lines.Count != 1
            || !request.Lines[0].Recurring
            || request.Lines[0].UnitAmountCents != TotalCents
            || !string.Equals(request.InclusiveTaxRateId,
                LiveTaxRateId, StringComparison.Ordinal)
            || !request.Metadata.TryGetValue("private_vat_test_id",
                out var metadataPilotId)
            || !string.Equals(metadataPilotId, runtime.PrivateVatTestId,
                StringComparison.OrdinalIgnoreCase)
            || !request.Metadata.TryGetValue("private_vat_test_user_id",
                out var metadataUserId)
            || !string.Equals(metadataUserId, runtime.PrivateVatTestUserId,
                StringComparison.OrdinalIgnoreCase)
            || !request.Metadata.TryGetValue("customer_id",
                out var metadataCustomerId)
            || !string.Equals(metadataCustomerId, runtime.PrivateVatTestCustomerId,
                StringComparison.OrdinalIgnoreCase))
        {
            return Blocked("PRIVATE_VAT_TEST_GATEWAY_SCOPE_MISMATCH");
        }
        return configuration;
    }

    /// <summary>
    /// Le webhook peut arriver après l'expiration de la fenêtre. La preuve
    /// d'une charge déjà réglée reste alors valable pour émettre sa facture,
    /// même si l'ouverture de nouveaux Checkout a été désactivée.
    /// </summary>
    public static BillingV2PrivateVatTestDecision CheckSettledInvoice(
        BillingV2RuntimeConfiguration runtime,
        StripeRuntimeConfiguration stripe,
        BillingV2PrivateVatInvoiceEvidence evidence,
        DocumentForIssuing document)
    {
        if (!Guid.TryParseExact(runtime.PrivateVatTestId, "D", out var pilotId)
            || !Guid.TryParseExact(runtime.PrivateVatTestUserId, "D", out _)
            || !Guid.TryParseExact(runtime.PrivateVatTestCustomerId, "D", out _)
            || runtime.PrivateVatTestStartsAtUtc is not { } start
            || runtime.PrivateVatTestExpiresAtUtc is not { } end
            || end - start <= MinimumSessionRemaining
            || end - start > MaximumWindow
            || stripe.Mode != StripeMode.Live
            || !string.Equals(stripe.FrenchInclusiveVatTaxRateId,
                LiveTaxRateId, StringComparison.Ordinal)
            || !string.Equals(runtime.PrivateVatTestSelectionFingerprint,
                SelectionFingerprint, StringComparison.Ordinal))
            return Blocked("PRIVATE_VAT_TEST_INVOICE_CONFIGURATION_INVALID");

        var line = document.Lines.Count == 1 ? document.Lines[0] : null;
        if (!string.Equals(evidence.ActorUserId,
                runtime.PrivateVatTestUserId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(evidence.CustomerId,
                runtime.PrivateVatTestCustomerId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(document.CustomerId, evidence.CustomerId,
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(evidence.IdempotencyKey,
                IdempotencyPrefix + pilotId.ToString("D"), StringComparison.Ordinal)
            || !string.Equals(evidence.SelectionFingerprint,
                SelectionFingerprint, StringComparison.Ordinal)
            || evidence.EventCreatedAtUtc < start
            || evidence.EventCreatedAtUtc >= end - MinimumSessionRemaining
            || evidence.EventTotalCents != TotalCents
            || evidence.EventTaxCents != TaxCents
            || evidence.SettlementStatus != "settled"
            || document.TotalAmountCents != TotalCents
            || document.Currency != "EUR"
            || document.CustomerCountry is not ("FR" or "France")
            || line is null
            || line.Quantity != 1m
            || line.UnitPriceCents != TotalCents - TaxCents
            || line.TaxRateBasisPoints != BillingVatPolicy.FrenchStandardRateBasisPoints
            || line.TaxTreatment != BillingVatPolicy.FrenchStandard)
            return Blocked("PRIVATE_VAT_TEST_INVOICE_SCOPE_MISMATCH");

        return new BillingV2PrivateVatTestDecision(
            true, "PRIVATE_VAT_TEST_INVOICE_AUTHORIZED", null, end);
    }

    public static BillingV2PrivateVatTestDecision CheckConfiguration(
        BillingV2RuntimeConfiguration runtime,
        StripeRuntimeConfiguration stripe,
        DateTime nowUtc)
    {
        if (!runtime.PrivateVatTestEnabled)
            return Blocked("PRIVATE_VAT_TEST_DISABLED");
        if (runtime.AuthoritativeCheckoutEnabled)
            return Blocked("PRIVATE_VAT_TEST_PUBLIC_CHECKOUT_MUST_STAY_CLOSED");
        if (!Guid.TryParseExact(runtime.PrivateVatTestId, "D", out var pilotId)
            || runtime.FirstRealTestPricingEnabled
            || !Guid.TryParseExact(runtime.PrivateVatTestUserId, "D", out _)
            || !Guid.TryParseExact(runtime.PrivateVatTestCustomerId, "D", out _)
            || !string.Equals(runtime.PrivateVatTestSelectionFingerprint,
                SelectionFingerprint, StringComparison.Ordinal)
            || runtime.PrivateVatTestStartsAtUtc is not { } start
            || runtime.PrivateVatTestExpiresAtUtc is not { } end
            || start.Kind != DateTimeKind.Utc
            || end.Kind != DateTimeKind.Utc
            || end - start <= MinimumSessionRemaining
            || end - start > MaximumWindow
            || stripe.Mode != StripeMode.Live
            || !stripe.IsConfigured
            || !string.Equals(stripe.FrenchInclusiveVatTaxRateId,
                LiveTaxRateId, StringComparison.Ordinal))
        {
            return Blocked("PRIVATE_VAT_TEST_CONFIGURATION_INVALID");
        }
        if (nowUtc.Kind != DateTimeKind.Utc
            || nowUtc < start
            || nowUtc >= end - MinimumSessionRemaining)
        {
            return Blocked("PRIVATE_VAT_TEST_WINDOW_CLOSED");
        }
        return new BillingV2PrivateVatTestDecision(
            true,
            "PRIVATE_VAT_TEST_AUTHORIZED",
            IdempotencyPrefix + pilotId.ToString("D"),
            end);
    }

    private static BillingV2PrivateVatTestDecision Blocked(string reason)
        => new(false, reason, null, null);
}

public sealed record BillingV2PrivateVatTestDecision(
    bool Authorized,
    string ReasonCode,
    string? IdempotencyKey,
    DateTime? ExpiresAtUtc);

public sealed record BillingV2PrivateVatInvoiceEvidence(
    string ActorUserId,
    string CustomerId,
    string IdempotencyKey,
    string SelectionFingerprint,
    long EventTotalCents,
    long EventTaxCents,
    string SettlementStatus,
    DateTime EventCreatedAtUtc);
