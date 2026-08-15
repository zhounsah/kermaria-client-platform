using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.SmokeTests;

public static class BillingV2PricingTests
{
    public static Task RunAsync()
    {
        VerifyMonthlyDiscountAndOneTimeExclusion();
        VerifyDocumentedPresetRounding();
        VerifyUpfrontPaymentUsesFullCommitmentPeriod();
        VerifyMinimumCommitmentFloor();
        VerifyMonthlyProration();
        VerifyUpfrontProrationNeverRefunds();
        VerifyPriceLocksOverrideDynamicPricing();
        VerifySnapshotsAreUsedAsContractualPrices();
        return Task.CompletedTask;
    }

    private static void VerifyMonthlyDiscountAndOneTimeExclusion()
    {
        var engine = new BillingV2PricingEngine();
        var result = engine.Calculate(new BillingV2PricingRequest(
            [
                Item("base", 690),
                Item("storage", 300),
                Item("backup", 200),
                Item(
                    "init",
                    1290,
                    BillingV2BillingCadences.OneTime,
                    discountEligible: false)
            ],
            DiscountBasisPoints: 1000,
            BillingV2PaymentModes.Monthly,
            CommitmentMonths: 6,
            MinimumCommitmentAmountCents: null,
            PriceLock: null,
            AsOfUtc: Utc(2026, 8, 12)));

        Ensure(result.RecurringSubtotalCents == 1190, "Sous-total recurrent.");
        Ensure(result.RecurringDiscountCents == 119, "Remise mensuelle 10%.");
        Ensure(result.DiscountedRecurringAmountCents == 1071, "Recurrent apres remise.");
        Ensure(result.OneTimeSubtotalCents == 1290, "One-time sans remise.");
        Ensure(result.TotalDueNowCents == 2361, "Total mensuel initial.");
    }

    private static void VerifyDocumentedPresetRounding()
    {
        var engine = new BillingV2PricingEngine();
        var result = engine.Calculate(new BillingV2PricingRequest(
            [Item("dossier", 1190)],
            DiscountBasisPoints: 1500,
            BillingV2PaymentModes.Monthly,
            CommitmentMonths: 12,
            MinimumCommitmentAmountCents: null,
            PriceLock: null,
            AsOfUtc: Utc(2026, 8, 12)));

        Ensure(
            result.PayableRecurringAmountCents == 1012,
            "11,90 EUR avec remise 15% doit arrondir a 10,12 EUR.");
    }

    private static void VerifyUpfrontPaymentUsesFullCommitmentPeriod()
    {
        var engine = new BillingV2PricingEngine();
        var result = engine.Calculate(new BillingV2PricingRequest(
            [
                Item("dossier", 1190),
                Item(
                    "init",
                    1290,
                    BillingV2BillingCadences.OneTime,
                    discountEligible: false)
            ],
            DiscountBasisPoints: 1500,
            BillingV2PaymentModes.Upfront,
            CommitmentMonths: 6,
            MinimumCommitmentAmountCents: null,
            PriceLock: null,
            AsOfUtc: Utc(2026, 8, 12)));

        Ensure(
            result.UpfrontRecurringAmountCents == 6069,
            "6 mois comptant applique 15% sur toute la periode.");
        Ensure(
            result.TotalDueNowCents == 7359,
            "Le paiement comptant ajoute les frais one-time sans remise.");
    }

    private static void VerifyMinimumCommitmentFloor()
    {
        var engine = new BillingV2PricingEngine();
        var minimum = engine.CalculateMinimumCommitmentAmount(
            initialMrrAfterDiscountCents: 10000);
        var result = engine.Calculate(new BillingV2PricingRequest(
            [Item("downgraded-services", 3000)],
            DiscountBasisPoints: 0,
            BillingV2PaymentModes.Monthly,
            CommitmentMonths: 12,
            MinimumCommitmentAmountCents: minimum,
            PriceLock: null,
            AsOfUtc: Utc(2026, 8, 12)));

        Ensure(minimum == 4500, "Plancher contractuel 45%.");
        Ensure(
            result.PayableRecurringAmountCents == 4500,
            "Le mensuel engage facture MAX(services actuels, plancher).");
    }

    private static void VerifyMonthlyProration()
    {
        var engine = new BillingV2PricingEngine();
        var upgrade = engine.CalculateMonthlyProration(
            oldMonthlyAmountCents: 3000,
            newMonthlyAmountCents: 5000,
            periodStartUtc: Utc(2026, 8, 1),
            periodEndUtc: Utc(2026, 8, 31),
            effectiveAtUtc: Utc(2026, 8, 16));
        var downgrade = engine.CalculateMonthlyProration(
            oldMonthlyAmountCents: 5000,
            newMonthlyAmountCents: 3000,
            periodStartUtc: Utc(2026, 8, 1),
            periodEndUtc: Utc(2026, 8, 31),
            effectiveAtUtc: Utc(2026, 8, 16));

        Ensure(upgrade.OldUnusedCreditCents == 1500, "Credit ancien tarif.");
        Ensure(upgrade.NewRemainingChargeCents == 2500, "Charge nouveau tarif.");
        Ensure(upgrade.NetAmountCents == 1000, "Prorata upgrade net.");
        Ensure(downgrade.NetAmountCents == -1000, "Prorata mensuel downgrade.");
    }

    private static void VerifyUpfrontProrationNeverRefunds()
    {
        var engine = new BillingV2PricingEngine();
        var upgrade = engine.CalculateUpfrontUpgradeProration(
            purchasedMonthlyAmountCents: 3000,
            requestedMonthlyAmountCents: 5000,
            periodStartUtc: Utc(2026, 8, 1),
            periodEndUtc: Utc(2026, 8, 31),
            effectiveAtUtc: Utc(2026, 8, 16));
        var reduction = engine.CalculateUpfrontUpgradeProration(
            purchasedMonthlyAmountCents: 5000,
            requestedMonthlyAmountCents: 3000,
            periodStartUtc: Utc(2026, 8, 1),
            periodEndUtc: Utc(2026, 8, 31),
            effectiveAtUtc: Utc(2026, 8, 16));

        Ensure(
            upgrade.ProratedUpgradeAmountCents == 1000,
            "Upfront facture seulement le supplement au prorata.");
        Ensure(
            reduction.ProratedUpgradeAmountCents == 0,
            "Upfront ne genere pas de remboursement automatique.");
    }

    private static void VerifyPriceLocksOverrideDynamicPricing()
    {
        var engine = new BillingV2PricingEngine();
        var monthly = engine.Calculate(new BillingV2PricingRequest(
            [Item("current-v2", 1012)],
            DiscountBasisPoints: 0,
            BillingV2PaymentModes.Monthly,
            CommitmentMonths: 12,
            MinimumCommitmentAmountCents: null,
            PriceLock: new BillingV2PriceLock(
                BillingV2PriceLockTypes.MonthlyRecurring,
                AmountCents: 1190,
                "EUR",
                Utc(2026, 8, 1),
                Utc(2026, 9, 1),
                "active"),
            AsOfUtc: Utc(2026, 8, 12)));
        var prepaid = engine.Calculate(new BillingV2PricingRequest(
            [Item("current-v2", 6069)],
            DiscountBasisPoints: 0,
            BillingV2PaymentModes.Upfront,
            CommitmentMonths: 6,
            MinimumCommitmentAmountCents: null,
            PriceLock: new BillingV2PriceLock(
                BillingV2PriceLockTypes.UpfrontPrepaid,
                AmountCents: 6069,
                "EUR",
                Utc(2026, 8, 1),
                Utc(2027, 2, 1),
                "active"),
            AsOfUtc: Utc(2026, 8, 12)));

        Ensure(
            monthly.PayableRecurringAmountCents == 1190
            && monthly.AppliedPriceLock is not null,
            "Le price lock mensuel preserve le prix contractuel.");
        Ensure(
            prepaid.TotalDueNowCents == 0
            && prepaid.AppliedPriceLock is not null,
            "Le price lock upfront deja paye empeche une nouvelle facture recurrente.");
    }

    private static void VerifySnapshotsAreUsedAsContractualPrices()
    {
        var engine = new BillingV2PricingEngine();
        var result = engine.Calculate(new BillingV2PricingRequest(
            [
                new BillingV2PricingItem(
                    "snapshot",
                    "STORAGE-PERSONAL",
                    "32",
                    "STORAGE-PERSONAL-32-MONTHLY-EUR-V1",
                    AmountCentsSnapshot: 300,
                    Quantity: 2,
                    BillingV2BillingCadences.Monthly,
                    DiscountEligible: true)
            ],
            DiscountBasisPoints: 0,
            BillingV2PaymentModes.Monthly,
            CommitmentMonths: 1,
            MinimumCommitmentAmountCents: null,
            PriceLock: null,
            AsOfUtc: Utc(2026, 8, 12)));

        Ensure(
            result.PayableRecurringAmountCents == 600,
            "Le moteur utilise le snapshot contractuel des items.");
    }

    private static BillingV2PricingItem Item(
        string id,
        long amountCents,
        string cadence = BillingV2BillingCadences.Monthly,
        bool discountEligible = true)
        => new(
            id,
            id.ToUpperInvariant(),
            TierCode: null,
            $"{id.ToUpperInvariant()}-EUR-V1",
            amountCents,
            Quantity: 1,
            cadence,
            discountEligible);

    private static DateTime Utc(int year, int month, int day)
        => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
