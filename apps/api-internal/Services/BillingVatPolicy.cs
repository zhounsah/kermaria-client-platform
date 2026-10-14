namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Regles fiscales fermees utilisees par les devis et les evenements Billing V2.
/// Un montant de catalogue est le montant a payer, TVA comprise lorsqu'elle
/// s'applique. Les autres territoires exigent une qualification humaine.
/// </summary>
public static class BillingVatPolicy
{
    public const string SellerVatNumber = "FR85105111520";
    public const string PolicyVersion = "fr-vat-2026-10-01";
    public const int FrenchStandardRateBasisPoints = 2000;
    public static readonly DateOnly EffectiveDate = new(2026, 10, 1);
    public static readonly DateTime EffectiveFromUtc =
        new(2026, 9, 30, 22, 0, 0, DateTimeKind.Utc);

    public const string FrenchStandard = "fr_standard";
    public const string HistoricalFranchise = "historical_franchise";
    public const string ReverseCharge = "reverse_charge";
    public const string Exempt = "exempt";
    public const string OutsideScope = "outside_scope";
    public const string ReviewRequired = "review_required";

    public static BillingVatDecision Resolve(
        string? customerCountry,
        DateTime servicePeriodStartUtc,
        DateTime servicePeriodEndUtc,
        DateTime? collectedAtUtc = null)
    {
        if (servicePeriodEndUtc <= servicePeriodStartUtc)
        {
            return Review("VAT_SERVICE_PERIOD_INVALID");
        }

        var firstDay = BillingV2BillingCalendar.CivilDate(servicePeriodStartUtc);
        var lastDay = BillingV2BillingCalendar.CivilDate(
            servicePeriodEndUtc.AddTicks(-1));
        if (firstDay < EffectiveDate && lastDay >= EffectiveDate)
        {
            return Review("VAT_TRANSITION_PERIOD_REVIEW_REQUIRED");
        }

        if (lastDay < EffectiveDate)
        {
            // Un encaissement postérieur peut rendre le traitement transitoire
            // litigieux. La date de facture seule ne permet pas de trancher.
            return collectedAtUtc.HasValue
                && BillingV2BillingCalendar.CivilDate(collectedAtUtc.Value)
                    >= EffectiveDate
                ? Review("VAT_TRANSITION_COLLECTION_REVIEW_REQUIRED")
                : new BillingVatDecision(HistoricalFranchise, null, true,
                    "VAT_HISTORICAL_FRANCHISE");
        }

        var country = customerCountry?.Trim();
        if (string.Equals(country, "FR", StringComparison.OrdinalIgnoreCase)
            || string.Equals(country, "France", StringComparison.OrdinalIgnoreCase))
        {
            return new BillingVatDecision(FrenchStandard,
                FrenchStandardRateBasisPoints, true, "VAT_FR_STANDARD");
        }

        return Review("VAT_CUSTOMER_LOCATION_REVIEW_REQUIRED");
    }

    public static BillingVatDecision ExplicitNonTaxable(string treatment)
        => treatment is ReverseCharge or Exempt or OutsideScope
            ? new BillingVatDecision(treatment, null, true, "VAT_EXPLICIT_TREATMENT")
            : Review("VAT_EXPLICIT_TREATMENT_INVALID");

    public static BillingVatAmounts FromInclusive(long totalCents, int rateBasisPoints)
    {
        if (totalCents < 0 || rateBasisPoints is < 0 or > 10000)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCents));
        }

        var net = checked((long)decimal.Round(
            totalCents * 10000m / (10000m + rateBasisPoints),
            0,
            MidpointRounding.AwayFromZero));
        return new BillingVatAmounts(net, checked(totalCents - net), totalCents);
    }

    public static BillingVatAmounts FromExclusive(long netCents, int rateBasisPoints)
    {
        if (netCents < 0 || rateBasisPoints is < 0 or > 10000)
        {
            throw new ArgumentOutOfRangeException(nameof(netCents));
        }

        var tax = checked((long)decimal.Round(
            netCents * rateBasisPoints / 10000m,
            0,
            MidpointRounding.AwayFromZero));
        return new BillingVatAmounts(netCents, tax, checked(netCents + tax));
    }

    public static BillingVatAmounts Split(long totalCents, BillingVatDecision decision)
    {
        if (!decision.AutomaticChargeAllowed)
        {
            throw new InvalidOperationException(decision.ReasonCode);
        }

        return decision.RateBasisPoints.HasValue
            ? FromInclusive(totalCents, decision.RateBasisPoints.Value)
            : new BillingVatAmounts(totalCents, 0, totalCents);
    }

    public static IReadOnlyList<long> AllocateGroupedInclusiveTax(
        IReadOnlyList<long> lineTotalsCents,
        IReadOnlyList<bool> recurringFlags,
        BillingVatDecision decision)
    {
        if (lineTotalsCents.Count != recurringFlags.Count)
        {
            throw new ArgumentException("VAT_GROUP_COUNT_MISMATCH");
        }
        if (!decision.AutomaticChargeAllowed)
        {
            throw new InvalidOperationException(decision.ReasonCode);
        }

        var allocated = new long[lineTotalsCents.Count];
        if (decision.RateBasisPoints is not { } rate)
        {
            return allocated;
        }

        // Le Checkout actuel emet une ligne Stripe recurrente et une ligne
        // ponctuelle. Leur taxe incluse est ventilee sur les lignes locales
        // par plus grands restes, afin que le total fiscal coincide au centime.
        foreach (var recurring in new[] { true, false })
        {
            var indexes = Enumerable.Range(0, lineTotalsCents.Count)
                .Where(index => recurringFlags[index] == recurring)
                .ToArray();
            if (indexes.Length == 0) continue;
            var amounts = indexes.Select(index => lineTotalsCents[index]).ToArray();
            var groupTotal = amounts.Aggregate(0L, (sum, value) => checked(sum + value));
            var targetTax = FromInclusive(groupTotal, rate).TaxAmountCents;
            var shares = BillingV2DiscountAllocator.Allocate(targetTax, amounts);
            for (var position = 0; position < indexes.Length; position++)
            {
                allocated[indexes[position]] = shares[position];
            }
        }

        return allocated;
    }

    /// <summary>
    /// Le devis utilise la meme ventilation de remise et les memes arrondis
    /// ligne par ligne que l'evenement financier du Checkout.
    /// </summary>
    public static BillingVatAmounts ProjectInitialChargeTotals(
        IReadOnlyList<BillingV2PricingItem> items,
        BillingV2PricingResult pricing,
        string paymentMode,
        int commitmentMonths,
        int discountBasisPoints,
        string currency,
        BillingVatDecision decision)
    {
        var sources = items.Select((item, index) =>
            new BillingV2NewSubscriptionPresetItem(
                item.ItemId,
                item.ServiceCode,
                item.TierCode,
                item.ServicePriceCode,
                item.ServiceCode,
                item.TierCode,
                item.ServicePriceCode,
                string.Empty,
                item.Quantity,
                item.AmountCentsSnapshot,
                currency,
                item.BillingCadence,
                item.DiscountEligible)).ToArray();
        var projection = BillingV2BillingEventFactory.BuildInitialCharge(
            new BillingV2BillingEventBuildRequest(
                paymentMode,
                commitmentMonths,
                discountBasisPoints,
                currency,
                sources,
                pricing,
                DateTime.UnixEpoch,
                DateTime.UnixEpoch.AddDays(1),
                "billing_v2.vat_quote_projection",
                decision));
        return new BillingVatAmounts(
            projection.Draft.NetAmountCents,
            projection.Draft.TaxAmountCents,
            projection.Draft.TotalAmountCents);
    }

    public static string Mention(string treatment)
        => treatment switch
        {
            FrenchStandard => "TVA au taux normal de 20 %.",
            HistoricalFranchise => "TVA non applicable, art. 293 B du CGI.",
            ReverseCharge => "Autoliquidation de la TVA par le preneur.",
            Exempt => "Exonération de TVA selon le fondement indiqué sur le document.",
            OutsideScope => "Opération hors du champ de la TVA française.",
            _ => "Traitement fiscal à vérifier avant émission."
        };

    private static BillingVatDecision Review(string reasonCode)
        => new(ReviewRequired, null, false, reasonCode);
}

public sealed record BillingVatDecision(
    string Treatment,
    int? RateBasisPoints,
    bool AutomaticChargeAllowed,
    string ReasonCode);

public sealed record BillingVatAmounts(
    long NetAmountCents,
    long TaxAmountCents,
    long TotalAmountCents);
