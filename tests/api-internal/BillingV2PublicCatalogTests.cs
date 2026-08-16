using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.SmokeTests;

/// <summary>
/// Garde-fous de la conception commerciale publique.
///
/// Le premier test est le plus important : il verifie que les quatre formules
/// affichees retombent exactement sur les prix annonces, en repartant de la
/// composition du catalogue. Si quelqu'un modifie un palier ou un prix sans
/// mesurer l'effet commercial, ce test tombe.
/// </summary>
public static class BillingV2PublicCatalogTests
{
    public static Task RunAsync()
    {
        VerifyPresetBaselinePricesMatchPublishedOffers();
        VerifyOnlyMonthlyCommitmentsAreExposed();
        VerifyCommitmentDiscountsAreAppliedByTheEngine();
        VerifyBackupTierFollowsCoveredStorage();
        VerifySharedBackupRequiresSharedStorage();
        VerifyNonPublicTiersAreRefused();
        VerifyAdditionalUsersAreBounded();
        VerifyBaselineSelectionReachesAuthoritativeCheckout();
        VerifyCustomConfigurationIsNotCheckoutable();
        VerifyLaunchFlagsBlockCheckoutWithoutHidingThePrice();
        VerifyQuoteIgnoresAnyAmountSentByTheBrowser();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 11,90 / 15,80 / 36,70 / 48,50 EUR par mois, sans engagement.
    /// </summary>
    private static void VerifyPresetBaselinePricesMatchPublishedOffers()
    {
        var expected = new (string PresetCode, long MonthlyCents)[]
        {
            ("pack-dossier-securise", 1190),
            ("pack-acces-distance", 1580),
            ("pack-bureau-windows-distance", 3670),
            ("pack-pro-association", 4850)
        };

        foreach (var (presetCode, monthlyCents) in expected)
        {
            var quote = Quote(presetCode, "FLEX");
            Ensure(
                quote.MonthlyBeforeDiscountCents == monthlyCents,
                $"Prix affiche de {presetCode} avant remise.");
            Ensure(
                quote.MonthlyAfterDiscountCents == monthlyCents,
                $"Prix affiche de {presetCode} sans engagement.");
            Ensure(
                quote.OneTimeCents == 0,
                $"Aucun frais ponctuel cache dans {presetCode}.");
        }

        // La composition doit venir du catalogue, pas d'un total code en dur :
        // la somme des lignes retombe sur le total du moteur.
        var pro = Quote("pack-pro-association", "FLEX");
        Ensure(
            pro.Lines.Sum(line => line.AmountCents)
                == pro.MonthlyBeforeDiscountCents,
            "Somme des lignes egale au sous-total du moteur.");
        Ensure(pro.Lines.Count == 8, "Composition complete de la formule Pro.");
    }

    private static void VerifyOnlyMonthlyCommitmentsAreExposed()
    {
        var catalog = BillingV2PublicCatalogSeed.Snapshot();
        Ensure(catalog.Commitments.Count == 3, "Trois engagements exposes.");
        Ensure(
            catalog.Commitments.Any(
                item => item.Code == "FLEX" && item.DiscountBasisPoints == 0),
            "Sans engagement a 0 %.");
        Ensure(
            catalog.Commitments.Any(
                item => item.Code == "TERM-6"
                        && item.DiscountBasisPoints == 1000),
            "Six mois a -10 %.");
        Ensure(
            catalog.Commitments.Any(
                item => item.Code == "TERM-12"
                        && item.DiscountBasisPoints == 1500),
            "Douze mois a -15 %.");

        // Les remises "comptant" (1500 / 2000 bp) existent en base mais ne
        // doivent pas apparaitre au lancement.
        Ensure(
            catalog.Commitments.All(
                item => item.DiscountBasisPoints is 0 or 1000 or 1500),
            "Aucune remise upfront exposee.");
        Ensure(
            catalog.CheckoutRoutes.Count == 12,
            "Douze routes de checkout mensuelles.");
    }

    private static void VerifyCommitmentDiscountsAreAppliedByTheEngine()
    {
        var expected = new (string PresetCode, string Commitment, long Cents)[]
        {
            ("pack-dossier-securise", "TERM-6", 1071),
            ("pack-dossier-securise", "TERM-12", 1012),
            ("pack-acces-distance", "TERM-12", 1343),
            ("pack-bureau-windows-distance", "TERM-6", 3303),
            ("pack-bureau-windows-distance", "TERM-12", 3120),
            ("pack-pro-association", "TERM-6", 4365),
            ("pack-pro-association", "TERM-12", 4123)
        };

        foreach (var (presetCode, commitment, cents) in expected)
        {
            var quote = Quote(presetCode, commitment);
            Ensure(
                quote.MonthlyAfterDiscountCents == cents,
                $"Prix remise {presetCode}/{commitment}.");
            Ensure(
                quote.MonthlyDiscountCents
                    == quote.MonthlyBeforeDiscountCents - cents,
                $"Remise affichee coherente {presetCode}/{commitment}.");
            Ensure(
                quote.TotalDueNowCents == cents,
                $"Total du a la souscription {presetCode}/{commitment}.");
        }
    }

    private static void VerifyBackupTierFollowsCoveredStorage()
    {
        var quote = Quote(
            "pack-dossier-securise",
            "FLEX",
            selection => selection with { StoragePersonalTierCode = "128" });

        var backup = quote.Lines.Single(
            line => line.ServiceCode
                == BillingV2PublicCatalogCodes.BackupPersonal);
        Ensure(
            backup.Detail == "128 Go proteges",
            "Le palier de sauvegarde suit la capacite couverte.");
        Ensure(backup.UnitAmountCents == 400, "Prix du palier 128 protege.");
        Ensure(
            quote.MonthlyBeforeDiscountCents == 1790,
            "Total du stockage 128 avec sa sauvegarde.");
    }

    private static void VerifySharedBackupRequiresSharedStorage()
    {
        var resolution = BillingV2PublicSelectionPolicy.Resolve(
            BillingV2PublicCatalogSeed.Snapshot(),
            Baseline("pack-pro-association", "FLEX") with
            {
                StorageSharedTierCode = null,
                BackupShared = true
            });

        Ensure(!resolution.Resolved, "Sauvegarde partagee sans espace partage.");
        Ensure(
            resolution.ReasonCode
                == "BILLING_V2_PUBLIC_SHARED_BACKUP_WITHOUT_STORAGE",
            "Motif explicite de refus.");
    }

    private static void VerifyNonPublicTiersAreRefused()
    {
        var catalog = BillingV2PublicCatalogSeed.Snapshot();

        var storage = BillingV2PublicSelectionPolicy.Resolve(
            catalog,
            Baseline("pack-dossier-securise", "FLEX") with
            {
                StoragePersonalTierCode = "512"
            });
        Ensure(!storage.Resolved, "Palier 512 Go non public.");

        var vpn = BillingV2PublicSelectionPolicy.Resolve(
            catalog,
            Baseline("pack-acces-distance", "FLEX") with
            {
                VpnTierCode = "PRO"
            });
        Ensure(!vpn.Resolved, "VPN Pro non public.");

        var legacy = BillingV2PublicSelectionPolicy.Resolve(
            catalog,
            Baseline("pack-acces-distance", "FLEX") with
            {
                VpnTierCode = "LEGACY"
            });
        Ensure(!legacy.Resolved, "Tier VPN legacy jamais selectionnable.");
    }

    private static void VerifyAdditionalUsersAreBounded()
    {
        var catalog = BillingV2PublicCatalogSeed.Snapshot();
        var tooMany = BillingV2PublicSelectionPolicy.Resolve(
            catalog,
            Baseline("pack-pro-association", "FLEX") with
            {
                AdditionalUsers = 99
            });
        Ensure(!tooMany.Resolved, "Nombre d'utilisateurs plafonne.");

        var quote = Quote(
            "pack-pro-association",
            "FLEX",
            selection => selection with { AdditionalUsers = 3 });
        var line = quote.Lines.Single(
            item => item.ServiceCode
                == BillingV2PublicCatalogCodes.AdditionalUser);
        Ensure(line.Quantity == 3, "Quantite d'utilisateurs supplementaires.");
        Ensure(line.AmountCents == 1170, "Trois utilisateurs a 3,90 EUR.");
    }

    private static void VerifyBaselineSelectionReachesAuthoritativeCheckout()
    {
        var quote = Quote("pack-bureau-windows-distance", "TERM-12");
        Ensure(quote.MatchesPresetBaseline, "Formule standard reconnue.");
        Ensure(quote.CheckoutAvailable, "Checkout autorise pour la formule standard.");
        Ensure(
            quote.CheckoutLegacyOfferId
                == "61000000-0000-0000-0000-000000000114",
            "Route vers l'offre legacy 12 mois mensuelle.");
    }

    private static void VerifyCustomConfigurationIsNotCheckoutable()
    {
        var quote = Quote(
            "pack-dossier-securise",
            "FLEX",
            selection => selection with { RemoteDesktop = true });

        Ensure(!quote.MatchesPresetBaseline, "Configuration personnalisee detectee.");
        Ensure(!quote.CheckoutAvailable, "Pas de checkout sur une configuration hors formule.");
        Ensure(
            quote.CheckoutLegacyOfferId is null,
            "Aucune route legacy inventee pour une configuration personnalisee.");
        Ensure(
            quote.CheckoutReasonCode
                == BillingV2PublicQuoteBuilder.CheckoutCustomConfiguration,
            "Motif explicite d'indisponibilite.");

        // Le prix reste calcule et affichable : le client voit ce que coute sa
        // configuration meme si elle n'est pas encore souscriptible en ligne.
        Ensure(
            quote.MonthlyAfterDiscountCents == 2780,
            "Prix de la configuration personnalisee toujours calcule.");
    }

    private static void VerifyLaunchFlagsBlockCheckoutWithoutHidingThePrice()
    {
        var quote = BillingV2PublicQuoteBuilder.Build(
            BillingV2PublicCatalogSeed.Snapshot(),
            Baseline("pack-dossier-securise", "TERM-12"),
            new BillingV2PricingEngine(),
            new BillingV2AuthoritativeCheckoutReadiness(
                Authorized: false,
                "BILLING_V2_FIRST_REAL_SUBSCRIPTION_NOT_APPROVED"));

        Ensure(quote.MatchesPresetBaseline, "Formule standard reconnue.");
        Ensure(!quote.CheckoutAvailable, "Drapeau de lancement respecte.");
        Ensure(
            quote.CheckoutReasonCode
                == "BILLING_V2_FIRST_REAL_SUBSCRIPTION_NOT_APPROVED",
            "Le motif remonte celui du gate authoritative.");
        Ensure(
            quote.MonthlyAfterDiscountCents == 1012,
            "Le prix reste affiche malgre le gel du lancement.");
    }

    /// <summary>
    /// La selection acceptee du navigateur ne porte que des codes catalogue.
    /// Ce test verrouille le contrat : si quelqu'un ajoute un montant a
    /// l'entree publique, il devra le faire sciemment.
    /// </summary>
    private static void VerifyQuoteIgnoresAnyAmountSentByTheBrowser()
    {
        var properties = typeof(BillingV2PublicSelectionInput).GetProperties();
        Ensure(
            properties.All(
                property => !property.Name.Contains(
                    "Amount",
                    StringComparison.OrdinalIgnoreCase)
                    && !property.Name.Contains(
                        "Cents",
                        StringComparison.OrdinalIgnoreCase)
                    && !property.Name.Contains(
                        "Price",
                        StringComparison.OrdinalIgnoreCase)),
            "Aucun montant accepte depuis le navigateur.");

        var input = new BillingV2PublicSelectionInput
        {
            PresetCode = "pack-dossier-securise",
            StoragePersonalTierCode = "32",
            BackupPersonal = true
        };
        var selection = input.ToSelection();
        Ensure(
            selection.CommitmentCode == "FLEX",
            "Engagement par defaut sans remise.");
    }

    private static BillingV2PublicSelection Baseline(
        string presetCode,
        string commitmentCode)
    {
        var catalog = BillingV2PublicCatalogSeed.Snapshot();
        var preset = catalog.Presets.Single(
            item => item.Code == presetCode);
        return BillingV2PublicSelectionPolicy.Baseline(preset) with
        {
            CommitmentCode = commitmentCode
        };
    }

    private static BillingV2PublicQuote Quote(
        string presetCode,
        string commitmentCode,
        Func<BillingV2PublicSelection, BillingV2PublicSelection>? customize
            = null)
    {
        var selection = Baseline(presetCode, commitmentCode);
        if (customize is not null)
        {
            selection = customize(selection);
        }

        return BillingV2PublicQuoteBuilder.Build(
            BillingV2PublicCatalogSeed.Snapshot(),
            selection,
            new BillingV2PricingEngine(),
            new BillingV2AuthoritativeCheckoutReadiness(
                Authorized: true,
                "BILLING_V2_AUTHORITATIVE_CHECKOUT_LOCALLY_READY"));
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
