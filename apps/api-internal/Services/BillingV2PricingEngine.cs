namespace Kermaria.ApiInternal.Services;

public static class BillingV2PaymentModes
{
    public const string Monthly = "monthly";
    public const string Upfront = "upfront";
}

public static class BillingV2BillingCadences
{
    public const string Monthly = "monthly";
    public const string OneTime = "one_time";
}

public static class BillingV2PriceLockTypes
{
    public const string MonthlyRecurring = "monthly_recurring";
    public const string UpfrontPrepaid = "upfront_prepaid";
}

public sealed record BillingV2PricingItem(
    string ItemId,
    string ServiceCode,
    string? TierCode,
    string ServicePriceCode,
    long AmountCentsSnapshot,
    int Quantity,
    string BillingCadence,
    bool DiscountEligible);

public sealed record BillingV2PriceLock(
    string LockType,
    long AmountCents,
    string Currency,
    DateTime EffectiveFromUtc,
    DateTime EffectiveUntilUtc,
    string Status);

public sealed record BillingV2PricingRequest(
    IReadOnlyList<BillingV2PricingItem> Items,
    int DiscountBasisPoints,
    string PaymentMode,
    int CommitmentMonths,
    long? MinimumCommitmentAmountCents,
    BillingV2PriceLock? PriceLock,
    DateTime AsOfUtc);

public sealed record BillingV2PricingResult(
    long DiscountEligibleRecurringSubtotalCents,
    long NonDiscountableRecurringSubtotalCents,
    long RecurringSubtotalCents,
    long RecurringDiscountCents,
    long DiscountedRecurringAmountCents,
    long PayableRecurringAmountCents,
    long OneTimeSubtotalCents,
    long UpfrontRecurringAmountCents,
    long TotalDueNowCents,
    BillingV2PriceLock? AppliedPriceLock);

public sealed record BillingV2ProrationResult(
    long OldUnusedCreditCents,
    long NewRemainingChargeCents,
    long NetAmountCents,
    long TotalPeriodTicks,
    long RemainingPeriodTicks);

public sealed record BillingV2UpfrontProrationResult(
    long PurchasedMonthlyAmountCents,
    long RequestedMonthlyAmountCents,
    long ProratedUpgradeAmountCents,
    long TotalPeriodTicks,
    long RemainingPeriodTicks);

public interface IBillingV2PricingEngine
{
    BillingV2PricingResult Calculate(BillingV2PricingRequest request);

    long CalculateMinimumCommitmentAmount(long initialMrrAfterDiscountCents);

    BillingV2ProrationResult CalculateMonthlyProration(
        long oldMonthlyAmountCents,
        long newMonthlyAmountCents,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        DateTime effectiveAtUtc);

    BillingV2UpfrontProrationResult CalculateUpfrontUpgradeProration(
        long purchasedMonthlyAmountCents,
        long requestedMonthlyAmountCents,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        DateTime effectiveAtUtc);
}

public sealed class BillingV2PricingEngine : IBillingV2PricingEngine
{
    private const int BasisPointDenominator = 10000;
    private const int MinimumCommitmentBasisPoints = 4500;

    public BillingV2PricingResult Calculate(BillingV2PricingRequest request)
    {
        ValidateRequest(request);

        var recurringDiscountEligibleSubtotalCents = 0L;
        var recurringNonDiscountableSubtotalCents = 0L;
        var oneTimeSubtotalCents = 0L;

        foreach (var item in request.Items)
        {
            var lineAmountCents = checked(item.AmountCentsSnapshot * item.Quantity);
            if (string.Equals(
                    item.BillingCadence,
                    BillingV2BillingCadences.Monthly,
                    StringComparison.Ordinal))
            {
                if (item.DiscountEligible)
                {
                    recurringDiscountEligibleSubtotalCents =
                        checked(recurringDiscountEligibleSubtotalCents + lineAmountCents);
                }
                else
                {
                    recurringNonDiscountableSubtotalCents =
                        checked(recurringNonDiscountableSubtotalCents + lineAmountCents);
                }

                continue;
            }

            if (string.Equals(
                    item.BillingCadence,
                    BillingV2BillingCadences.OneTime,
                    StringComparison.Ordinal))
            {
                oneTimeSubtotalCents = checked(oneTimeSubtotalCents + lineAmountCents);
                continue;
            }

            throw new ArgumentException(
                $"Unsupported Billing V2 cadence '{item.BillingCadence}'.",
                nameof(request));
        }

        var discountedRecurringEligibleCents = ApplyDiscount(
            recurringDiscountEligibleSubtotalCents,
            request.DiscountBasisPoints);
        var recurringDiscountCents =
            recurringDiscountEligibleSubtotalCents - discountedRecurringEligibleCents;
        var discountedRecurringAmountCents = checked(
            discountedRecurringEligibleCents + recurringNonDiscountableSubtotalCents);
        var recurringSubtotalCents = checked(
            recurringDiscountEligibleSubtotalCents
            + recurringNonDiscountableSubtotalCents);

        var activeLock = ActivePriceLock(request);
        if (activeLock is not null)
        {
            return ApplyPriceLock(
                request,
                activeLock,
                recurringDiscountEligibleSubtotalCents,
                recurringNonDiscountableSubtotalCents,
                recurringSubtotalCents,
                recurringDiscountCents,
                discountedRecurringAmountCents,
                oneTimeSubtotalCents);
        }

        if (string.Equals(
                request.PaymentMode,
                BillingV2PaymentModes.Upfront,
                StringComparison.Ordinal))
        {
            var upfrontDiscountEligibleCents = checked(
                recurringDiscountEligibleSubtotalCents * request.CommitmentMonths);
            var upfrontNonDiscountableCents = checked(
                recurringNonDiscountableSubtotalCents * request.CommitmentMonths);
            var upfrontRecurringAmountCents = checked(
                ApplyDiscount(
                    upfrontDiscountEligibleCents,
                    request.DiscountBasisPoints)
                + upfrontNonDiscountableCents);

            return new BillingV2PricingResult(
                recurringDiscountEligibleSubtotalCents,
                recurringNonDiscountableSubtotalCents,
                recurringSubtotalCents,
                recurringDiscountCents,
                discountedRecurringAmountCents,
                discountedRecurringAmountCents,
                oneTimeSubtotalCents,
                upfrontRecurringAmountCents,
                checked(upfrontRecurringAmountCents + oneTimeSubtotalCents),
                AppliedPriceLock: null);
        }

        var payableRecurringAmountCents = request.MinimumCommitmentAmountCents.HasValue
            ? Math.Max(
                discountedRecurringAmountCents,
                request.MinimumCommitmentAmountCents.Value)
            : discountedRecurringAmountCents;

        return new BillingV2PricingResult(
            recurringDiscountEligibleSubtotalCents,
            recurringNonDiscountableSubtotalCents,
            recurringSubtotalCents,
            recurringDiscountCents,
            discountedRecurringAmountCents,
            payableRecurringAmountCents,
            oneTimeSubtotalCents,
            UpfrontRecurringAmountCents: 0,
            checked(payableRecurringAmountCents + oneTimeSubtotalCents),
            AppliedPriceLock: null);
    }

    public long CalculateMinimumCommitmentAmount(
        long initialMrrAfterDiscountCents)
    {
        if (initialMrrAfterDiscountCents < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialMrrAfterDiscountCents));
        }

        return MultiplyBasisPoints(
            initialMrrAfterDiscountCents,
            MinimumCommitmentBasisPoints);
    }

    public BillingV2ProrationResult CalculateMonthlyProration(
        long oldMonthlyAmountCents,
        long newMonthlyAmountCents,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        DateTime effectiveAtUtc)
    {
        ValidateCents(oldMonthlyAmountCents, nameof(oldMonthlyAmountCents));
        ValidateCents(newMonthlyAmountCents, nameof(newMonthlyAmountCents));
        var (totalTicks, remainingTicks) = ResolveProrationWindow(
            periodStartUtc,
            periodEndUtc,
            effectiveAtUtc);

        var oldCredit = RoundRatio(
            oldMonthlyAmountCents,
            remainingTicks,
            totalTicks);
        var newCharge = RoundRatio(
            newMonthlyAmountCents,
            remainingTicks,
            totalTicks);

        return new BillingV2ProrationResult(
            oldCredit,
            newCharge,
            checked(newCharge - oldCredit),
            totalTicks,
            remainingTicks);
    }

    public BillingV2UpfrontProrationResult CalculateUpfrontUpgradeProration(
        long purchasedMonthlyAmountCents,
        long requestedMonthlyAmountCents,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        DateTime effectiveAtUtc)
    {
        ValidateCents(
            purchasedMonthlyAmountCents,
            nameof(purchasedMonthlyAmountCents));
        ValidateCents(
            requestedMonthlyAmountCents,
            nameof(requestedMonthlyAmountCents));
        var (totalTicks, remainingTicks) = ResolveProrationWindow(
            periodStartUtc,
            periodEndUtc,
            effectiveAtUtc);
        var monthlyDelta = requestedMonthlyAmountCents
            <= purchasedMonthlyAmountCents
            ? 0
            : checked(requestedMonthlyAmountCents - purchasedMonthlyAmountCents);

        return new BillingV2UpfrontProrationResult(
            purchasedMonthlyAmountCents,
            requestedMonthlyAmountCents,
            RoundRatio(monthlyDelta, remainingTicks, totalTicks),
            totalTicks,
            remainingTicks);
    }

    private static BillingV2PricingResult ApplyPriceLock(
        BillingV2PricingRequest request,
        BillingV2PriceLock lockSnapshot,
        long recurringDiscountEligibleSubtotalCents,
        long recurringNonDiscountableSubtotalCents,
        long recurringSubtotalCents,
        long recurringDiscountCents,
        long discountedRecurringAmountCents,
        long oneTimeSubtotalCents)
    {
        if (string.Equals(
                lockSnapshot.LockType,
                BillingV2PriceLockTypes.UpfrontPrepaid,
                StringComparison.Ordinal))
        {
            return new BillingV2PricingResult(
                recurringDiscountEligibleSubtotalCents,
                recurringNonDiscountableSubtotalCents,
                recurringSubtotalCents,
                recurringDiscountCents,
                discountedRecurringAmountCents,
                PayableRecurringAmountCents: 0,
                oneTimeSubtotalCents,
                UpfrontRecurringAmountCents: 0,
                oneTimeSubtotalCents,
                lockSnapshot);
        }

        if (!string.Equals(
                lockSnapshot.LockType,
                BillingV2PriceLockTypes.MonthlyRecurring,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Unsupported Billing V2 price lock type '{lockSnapshot.LockType}'.",
                nameof(request));
        }

        return new BillingV2PricingResult(
            recurringDiscountEligibleSubtotalCents,
            recurringNonDiscountableSubtotalCents,
            recurringSubtotalCents,
            recurringDiscountCents,
            discountedRecurringAmountCents,
            lockSnapshot.AmountCents,
            oneTimeSubtotalCents,
            UpfrontRecurringAmountCents: 0,
            checked(lockSnapshot.AmountCents + oneTimeSubtotalCents),
            lockSnapshot);
    }

    private static BillingV2PriceLock? ActivePriceLock(
        BillingV2PricingRequest request)
    {
        var priceLock = request.PriceLock;
        if (priceLock is null
            || !string.Equals(
                priceLock.Status,
                "active",
                StringComparison.Ordinal)
            || request.AsOfUtc < priceLock.EffectiveFromUtc
            || request.AsOfUtc >= priceLock.EffectiveUntilUtc)
        {
            return null;
        }

        return priceLock;
    }

    private static long ApplyDiscount(
        long amountCents,
        int discountBasisPoints)
        => MultiplyBasisPoints(
            amountCents,
            BasisPointDenominator - discountBasisPoints);

    private static long MultiplyBasisPoints(
        long amountCents,
        int basisPoints)
        => checked((amountCents * basisPoints + 5000L) / BasisPointDenominator);

    private static long RoundRatio(
        long amountCents,
        long numerator,
        long denominator)
    {
        if (amountCents == 0 || numerator == 0)
        {
            return 0;
        }

        return checked((amountCents * numerator + denominator / 2) / denominator);
    }

    private static (long TotalTicks, long RemainingTicks) ResolveProrationWindow(
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        DateTime effectiveAtUtc)
    {
        if (periodEndUtc <= periodStartUtc)
        {
            throw new ArgumentException(
                "Billing V2 proration period end must be after period start.",
                nameof(periodEndUtc));
        }

        if (effectiveAtUtc < periodStartUtc || effectiveAtUtc > periodEndUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(effectiveAtUtc),
                "Billing V2 proration effective date must be inside the period.");
        }

        return (
            periodEndUtc.Ticks - periodStartUtc.Ticks,
            periodEndUtc.Ticks - effectiveAtUtc.Ticks);
    }

    private static void ValidateRequest(BillingV2PricingRequest request)
    {
        if (request.DiscountBasisPoints is < 0 or > BasisPointDenominator)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Billing V2 discounts must be basis points between 0 and 10000.");
        }

        if (request.CommitmentMonths <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }

        if (request.MinimumCommitmentAmountCents is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }

        if (request.PriceLock is { AmountCents: < 0 })
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }

        if (!string.Equals(
                request.PaymentMode,
                BillingV2PaymentModes.Monthly,
                StringComparison.Ordinal)
            && !string.Equals(
                request.PaymentMode,
                BillingV2PaymentModes.Upfront,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Unsupported Billing V2 payment mode '{request.PaymentMode}'.",
                nameof(request));
        }

        foreach (var item in request.Items)
        {
            ValidateCents(
                item.AmountCentsSnapshot,
                nameof(item.AmountCentsSnapshot));
            if (item.Quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(request));
            }
        }
    }

    private static void ValidateCents(long amountCents, string parameterName)
    {
        if (amountCents < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
