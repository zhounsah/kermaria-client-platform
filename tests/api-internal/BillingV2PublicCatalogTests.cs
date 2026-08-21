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
        VerifyCommitmentPaymentOptionsMatchTheCatalog();
        VerifyCommitmentDiscountsAreAppliedByTheEngine();
        VerifyBackupTierFollowsCoveredStorage();
        VerifySharedBackupRequiresSharedStorage();
        VerifyNonPublicTiersAreRefused();
        VerifyAdditionalUsersAreBounded();
        VerifyBaselineSelectionReachesAuthoritativeCheckout();
        VerifyCustomConfigurationIsCheckoutable();
        VerifyCustomConfigurationPricesAcrossCommitments();
        VerifyUpfrontChargesTheWholeTermOnce();
        VerifyUpfrontIsRefusedWithoutEngagement();
        VerifySelectionCanonicalIsStableAndDiscriminating();
        VerifyLegacyOfferKeepsItsOwnIdentity();
        VerifyLaunchFlagsBlockCheckoutWithoutHidingThePrice();
        VerifyQuoteIgnoresAnyAmountSentByTheBrowser();
        VerifyUpfrontLifecycleBoundsTheContract();
        VerifyMonthlyLifecycleKeepsARenewalDate();
        VerifyUpfrontIsWithinTheLaunchScope();
        VerifyQuoteNeverOffersWhatTheRailRefuses();
        VerifyGenericComponentsAreCanonicalAndServerResolved();
        VerifyVpsUpgradeDoesNotRepeatInitialSetup();
        VerifyFulfillmentNeverEquatesManualDeliveryWithProvisioning();
        return Task.CompletedTask;
    }

    private static void VerifyGenericComponentsAreCanonicalAndServerResolved()
    {
        var baseline = Baseline("pack-dossier-securise", "FLEX");
        var generic = baseline with
        {
            Components =
            [
                new BillingV2PublicSelectionComponent("STORAGE-PERSONAL", "32", 1),
                new BillingV2PublicSelectionComponent("VPN-ACCESS", "ESSENTIAL", 1)
            ]
        };
        var resolution = BillingV2PublicSelectionPolicy.Resolve(
            BillingV2PublicCatalogSeed.Snapshot(), generic);
        Ensure(resolution.Resolved, "Selection generique validee cote serveur.");
        Ensure(resolution.Components.Count == 2, "Composants generiques canoniques.");
        Ensure(generic.Canonical().Contains("components", StringComparison.Ordinal),
            "Empreinte des composants distincte du shape legacy.");
    }

    private static void VerifyVpsUpgradeDoesNotRepeatInitialSetup()
    {
        var initialM = new[]
        {
            new BillingV2PriceComponentSnapshot("VPS-CLOUD-M-MONTHLY", "monthly", "initial_subscription", 2990, "EUR", true, 10),
            new BillingV2PriceComponentSnapshot("VPS-CLOUD-M-SETUP", "one_time", "initial_subscription", 2990, "EUR", false, 20)
        };
        var upgradedL = new[]
        {
            new BillingV2PriceComponentSnapshot("VPS-CLOUD-L-MONTHLY", "monthly", "initial_subscription", 4490, "EUR", true, 10),
            new BillingV2PriceComponentSnapshot("VPS-CLOUD-L-SETUP", "one_time", "initial_subscription", 2990, "EUR", false, 20)
        };
        Ensure(BillingV2ComponentizedPricingPolicy.ForInitialCharge(initialM).Count == 2,
            "Le setup M est facture lors de la souscription initiale.");
        var upgradeCharges = BillingV2ComponentizedPricingPolicy.ForSubscriptionChange(upgradedL);
        Ensure(upgradeCharges.Count == 1 && upgradeCharges[0].ServicePriceId == "VPS-CLOUD-L-MONTHLY",
            "M vers L ne refacture jamais le setup initial sans regle explicite.");
        Ensure(BillingV2ComponentizedPricingPolicy.ForRenewal(initialM).Count == 1,
            "Le renouvellement exclut toujours la composante one-time.");
        Ensure(BillingV2SubscriptionChangePolicy.ComponentsForSuccessor(
                BillingV2SubscriptionChangePolicy.Upgrade, upgradedL).Count == 1,
            "Le successeur d'upgrade ne reprend que le MRR sans regle de frais explicite.");
    }

    private static void VerifyFulfillmentNeverEquatesManualDeliveryWithProvisioning()
    {
        Ensure(BillingV2FulfillmentPolicy.InitialStatus("manual_delivery")
               == BillingV2FulfillmentPolicy.Pending,
            "Un service humain regle reste pending jusqu'a livraison reelle.");
        Ensure(BillingV2FulfillmentPolicy.InitialStatus("contractual_acknowledgement")
               == BillingV2FulfillmentPolicy.Fulfilled,
            "Un entitlement contractuel pur peut etre acknowledged.");
        Ensure(BillingV2FulfillmentPolicy.CanTransition(
                BillingV2FulfillmentPolicy.Pending,
                BillingV2FulfillmentPolicy.InProgress),
            "Le fulfillment manuel suit un lifecycle explicite.");
    }

    /// <summary>
    /// Un contrat comptant doit etre borne des sa creation : engagement date,
    /// periode courante egale a la periode payee, et surtout aucune date de
    /// renouvellement. Promettre un renouvellement laisserait croire a un
    /// prelevement automatique qui n'existe pas.
    /// </summary>
    private static void VerifyUpfrontLifecycleBoundsTheContract()
    {
        var anchor = new DateTime(2026, 8, 16, 9, 30, 0, DateTimeKind.Utc);
        var plan = BillingV2SubscriptionLifecyclePolicy.Plan(
            BillingV2PaymentModes.Upfront,
            commitmentMonths: 12,
            anchor);

        Ensure(
            plan.CommitmentEndsAtUtc
                == plan.CommitmentStartedAtUtc.AddMonths(12),
            "Douze mois comptant : la fin d'engagement vaut le debut + 12 mois.");
        Ensure(
            plan.CurrentPeriodStartedAtUtc == plan.CommitmentStartedAtUtc
            && plan.CurrentPeriodEndsAtUtc == plan.CommitmentEndsAtUtc,
            "En comptant, la periode courante EST la periode payee.");
        Ensure(
            plan.RenewsAtUtc is null,
            "Aucun renouvellement automatique ne doit etre promis en comptant.");
        Ensure(
            plan.CommitmentStartedAtUtc < anchor
            && anchor - plan.CommitmentStartedAtUtc < TimeSpan.FromDays(1),
            "L'engagement demarre au jour civil de l'ancre, pas a l'heure UTC.");
    }

    /// <summary>
    /// Le mensuel garde un cycle d'un mois et une date de renouvellement : il
    /// ne doit pas etre borne par le meme mecanisme que le comptant.
    /// </summary>
    private static void VerifyMonthlyLifecycleKeepsARenewalDate()
    {
        var anchor = new DateTime(2026, 8, 16, 9, 30, 0, DateTimeKind.Utc);
        var plan = BillingV2SubscriptionLifecyclePolicy.Plan(
            BillingV2PaymentModes.Monthly,
            commitmentMonths: 12,
            anchor);

        Ensure(
            plan.CommitmentEndsAtUtc
                == plan.CommitmentStartedAtUtc.AddMonths(12),
            "L'engagement de 12 mois est borne meme en reglement mensuel.");
        Ensure(
            plan.CurrentPeriodEndsAtUtc
                == plan.CurrentPeriodStartedAtUtc.AddMonths(1),
            "Le cycle courant d'un mensuel dure un mois.");
        Ensure(
            plan.RenewsAtUtc == plan.CurrentPeriodEndsAtUtc,
            "Un mensuel annonce sa date de renouvellement.");
    }

    private static void VerifyUpfrontIsWithinTheLaunchScope()
    {
        var upfront = BillingV2LaunchScope.EvaluateCheckout(
            "stripe",
            BillingV2PaymentModes.Upfront,
            taxAmountCents: 0);
        Ensure(
            upfront.IsValid,
            "Le comptant doit etre encaissable une fois son cycle de vie pose.");

        var paypal = BillingV2LaunchScope.EvaluateCheckout(
            "paypal",
            BillingV2PaymentModes.Monthly,
            taxAmountCents: 0);
        Ensure(
            !paypal.IsValid,
            "Ouvrir le comptant ne doit rien ouvrir d'autre.");
    }

    /// <summary>
    /// Interface et rail doivent partager la meme autorite. Un mode affiche
    /// comme souscriptible mais refuse au dispatch laissait le client devant
    /// une souscription sans page de paiement.
    /// </summary>
    private static void VerifyQuoteNeverOffersWhatTheRailRefuses()
    {
        foreach (var paymentMode in new[]
                 {
                     BillingV2PaymentModes.Monthly,
                     BillingV2PaymentModes.Upfront
                 })
        {
            var quote = BillingV2PublicQuoteBuilder.Build(
                BillingV2PublicCatalogSeed.Snapshot(),
                Baseline("pack-dossier-securise", "TERM-12") with
                {
                    PaymentMode = paymentMode
                },
                new BillingV2PricingEngine(),
                new BillingV2AuthoritativeCheckoutReadiness(
                    Authorized: true,
                    "BILLING_V2_AUTHORITATIVE_CHECKOUT_LOCALLY_READY"));

            var rail = BillingV2LaunchScope.EvaluateCheckout(
                "stripe",
                paymentMode,
                taxAmountCents: 0);

            Ensure(
                quote.CheckoutAvailable == rail.IsValid,
                $"Devis et rail doivent s'accorder sur {paymentMode}.");
            Ensure(
                quote.CommitmentTotalAfterDiscountCents > 0,
                $"Le prix reste affiche pour {paymentMode}.");
        }
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

    /// <summary>
    /// La matrice exposee doit etre exactement celle de la migration 048 : la
    /// remise depend du couple (duree, mode de reglement), et FLEX n'ouvre pas
    /// le comptant.
    /// </summary>
    private static void VerifyCommitmentPaymentOptionsMatchTheCatalog()
    {
        var catalog = BillingV2PublicCatalogSeed.Snapshot();
        Ensure(catalog.Commitments.Count == 3, "Trois engagements exposes.");

        var expected = new (string Code, string Mode, int BasisPoints)[]
        {
            ("FLEX", BillingV2PaymentModes.Monthly, 0),
            ("TERM-6", BillingV2PaymentModes.Monthly, 1000),
            ("TERM-6", BillingV2PaymentModes.Upfront, 1500),
            ("TERM-12", BillingV2PaymentModes.Monthly, 1500),
            ("TERM-12", BillingV2PaymentModes.Upfront, 2000)
        };

        foreach (var (code, mode, basisPoints) in expected)
        {
            var option = catalog.Commitments
                .Single(item => item.Code == code)
                .Option(mode);
            Ensure(
                option?.DiscountBasisPoints == basisPoints,
                $"Remise {code}/{mode} = {basisPoints} points de base.");
        }

        Ensure(
            catalog.Commitments.Single(item => item.Code == "FLEX")
                .Option(BillingV2PaymentModes.Upfront) is null,
            "Pas de comptant sans engagement.");
        Ensure(
            catalog.CheckoutRoutes.Count == 12,
            "Douze routes legacy mensuelles conservees pour compatibilite.");
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
        // Le libelle est compare accent compris : le repli catalogue est ce
        // que lit un visiteur tant que le schema V2 n'est pas applique, et une
        // divergence avec la migration 048 se verrait en pleine vitrine.
        Ensure(
            backup.Detail == "128 Go protégés",
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

    /// <summary>
    /// Une configuration personnalisee valide est desormais souscriptible : le
    /// checkout ne depend plus de l'existence d'une offre legacy. Aucune route
    /// legacy n'est pour autant inventee.
    /// </summary>
    private static void VerifyCustomConfigurationIsCheckoutable()
    {
        var quote = Quote(
            "pack-dossier-securise",
            "FLEX",
            selection => selection with { RemoteDesktop = true });

        Ensure(!quote.MatchesPresetBaseline, "Configuration personnalisee detectee.");
        Ensure(quote.CheckoutAvailable, "Configuration personnalisee souscriptible.");
        Ensure(
            quote.CheckoutMode == BillingV2PublicCheckoutModes.Native,
            "Souscription V2 native, sans offre legacy.");
        Ensure(
            quote.CheckoutLegacyOfferId is null,
            "Aucune route legacy inventee pour une configuration personnalisee.");
        Ensure(
            quote.MonthlyAfterDiscountCents == 2780,
            "Prix de la configuration personnalisee.");
    }

    /// <summary>
    /// Les deux exemples de reference, sur les trois durees. Le total
    /// contractuel et l'economie affichee proviennent du meme calcul serveur
    /// que le montant preleve.
    /// </summary>
    private static void VerifyCustomConfigurationPricesAcrossCommitments()
    {
        // Dossier securise + stockage 128 + sauvegarde 128 + VPN Essentiel.
        BillingV2PublicSelection Dossier(BillingV2PublicSelection selection)
            => selection with
            {
                StoragePersonalTierCode = "128",
                BackupPersonal = true,
                VpnTierCode = "ESSENTIAL"
            };

        Ensure(
            Quote("pack-dossier-securise", "FLEX", Dossier)
                .MonthlyBeforeDiscountCents == 2180,
            "Composition personnalisee Dossier securise a 21,80 EUR.");
        Ensure(
            Quote("pack-dossier-securise", "TERM-6", Dossier)
                .MonthlyAfterDiscountCents == 1962,
            "Dossier securise personnalise, 6 mois mensuel.");
        Ensure(
            Quote("pack-dossier-securise", "TERM-12", Dossier)
                .MonthlyAfterDiscountCents == 1853,
            "Dossier securise personnalise, 12 mois mensuel.");

        // Bureau Windows + 256 Go + VPN Performance + 2 utilisateurs +
        // Support Plus.
        BillingV2PublicSelection Bureau(BillingV2PublicSelection selection)
            => selection with
            {
                StoragePersonalTierCode = "256",
                BackupPersonal = true,
                VpnTierCode = "PERFORMANCE",
                AdditionalUsers = 2,
                SupportPlus = true
            };

        var bureau = Quote("pack-bureau-windows-distance", "TERM-12", Bureau);
        Ensure(
            bureau.MonthlyBeforeDiscountCents == 6530,
            "Composition personnalisee Bureau Windows a 65,30 EUR.");
        Ensure(
            bureau.MonthlyAfterDiscountCents == 5551,
            "Bureau Windows personnalise, 12 mois mensuel.");
        Ensure(
            bureau.CommitmentTotalAfterDiscountCents == 5551 * 12,
            "Total contractuel du mensuel = douze prelevements.");
        Ensure(bureau.CheckoutAvailable, "Bureau Windows personnalise souscriptible.");
    }

    /// <summary>
    /// Le comptant encaisse la periode entiere en une fois : le total du
    /// aujourd'hui est le montant de l'engagement, pas un mois.
    /// </summary>
    private static void VerifyUpfrontChargesTheWholeTermOnce()
    {
        var expected =
            new (string Preset, string Term, long DueNow, long Monthly, long Saved)[]
            {
                ("pack-dossier-securise", "TERM-6", 6069, 1012, 1071),
                ("pack-dossier-securise", "TERM-12", 11424, 952, 2856)
            };

        foreach (var (preset, term, dueNow, monthly, saved) in expected)
        {
            var quote = Quote(
                preset,
                term,
                selection => selection with
                {
                    PaymentMode = BillingV2PaymentModes.Upfront
                });

            Ensure(
                quote.TotalDueNowCents == dueNow,
                $"Comptant {term} : montant preleve aujourd'hui.");
            Ensure(
                quote.CommitmentTotalAfterDiscountCents == dueNow,
                $"Comptant {term} : le total contractuel est le paiement unique.");
            Ensure(
                quote.MonthlyAfterDiscountCents == monthly,
                $"Comptant {term} : equivalent mensuel affiche.");
            Ensure(
                quote.CommitmentSavingsCents == saved,
                $"Comptant {term} : economie totale.");
            Ensure(quote.CheckoutAvailable, $"Comptant {term} souscriptible.");
            Ensure(
                quote.CheckoutLegacyOfferId is null,
                $"Comptant {term} : aucune offre legacy mensuelle detournee.");
        }

        // Le comptant personnalise doit rester coherent avec le mensuel : le
        // total ne peut pas depasser douze fois le prix de base.
        var bureau = Quote(
            "pack-bureau-windows-distance",
            "TERM-12",
            selection => selection with
            {
                PaymentMode = BillingV2PaymentModes.Upfront,
                StoragePersonalTierCode = "256",
                BackupPersonal = true,
                VpnTierCode = "PERFORMANCE",
                AdditionalUsers = 2,
                SupportPlus = true
            });
        Ensure(
            bureau.TotalDueNowCents == 62688,
            "Bureau Windows personnalise, 12 mois comptant.");
        Ensure(
            bureau.CommitmentSavingsCents == 15672,
            "Economie du Bureau Windows personnalise en comptant.");
    }

    private static void VerifyUpfrontIsRefusedWithoutEngagement()
    {
        var resolution = BillingV2PublicSelectionPolicy.Resolve(
            BillingV2PublicCatalogSeed.Snapshot(),
            Baseline("pack-dossier-securise", "FLEX") with
            {
                PaymentMode = BillingV2PaymentModes.Upfront
            });

        Ensure(!resolution.Resolved, "Comptant refuse sans engagement.");
        Ensure(
            resolution.ReasonCode
                == "BILLING_V2_PUBLIC_PAYMENT_MODE_UNAVAILABLE",
            "Motif explicite de refus du mode de reglement.");
    }

    /// <summary>
    /// L'ancre d'idempotence remplace le `legacy_offer_id`. Elle doit etre
    /// stable pour une meme configuration — deux appels avec la meme cle
    /// retombent sur la meme intention — et differente des qu'un seul choix
    /// change, sinon deux configurations distinctes se factureraient l'une pour
    /// l'autre.
    /// </summary>
    private static void VerifySelectionCanonicalIsStableAndDiscriminating()
    {
        var reference = Baseline("pack-bureau-windows-distance", "TERM-12");
        var identical = Baseline("pack-bureau-windows-distance", "TERM-12");
        Ensure(
            reference.Canonical() == identical.Canonical(),
            "Meme configuration, meme empreinte.");

        var variants = new[]
        {
            reference with { StoragePersonalTierCode = "256" },
            reference with { PaymentMode = BillingV2PaymentModes.Upfront },
            reference with { CommitmentCode = "TERM-6" },
            reference with { AdditionalUsers = 2 },
            reference with { SupportPlus = true },
            reference with { BackupPersonal = !reference.BackupPersonal },
            reference with { RemoteDesktop = !reference.RemoteDesktop },
            reference with { StorageSharedTierCode = "128" },
            reference with { VpnTierCode = "PERFORMANCE" },
            reference with { PresetCode = "pack-pro-association" }
        };
        var fingerprints = variants
            .Select(variant =>
                BillingV2CheckoutSelectionFingerprint.ForSelection(
                    variant.Canonical()))
            .Append(
                BillingV2CheckoutSelectionFingerprint.ForSelection(
                    reference.Canonical()))
            .ToArray();
        Ensure(
            fingerprints.Distinct(StringComparer.Ordinal).Count()
                == fingerprints.Length,
            "Chaque choix distinct produit une empreinte distincte.");

        // Deux appels successifs avec la meme cle client produisent la meme
        // intention : c'est ce qui rend le double clic inoffensif.
        var intent = new BillingV2SubscriptionIntentRequest(
            "customer-1",
            "client-request-1",
            BillingV2CheckoutSelectionFingerprint.ForSelection(
                reference.Canonical()),
            "stripe",
            "test");
        Ensure(
            BillingV2SubscriptionIntentKey.Canonical(intent)
                == BillingV2SubscriptionIntentKey.Canonical(intent),
            "Cle d'intention deterministe.");
    }

    /// <summary>
    /// Le parcours legacy garde une identite propre : son empreinte est
    /// derivee de l'offre et ne peut pas entrer en collision avec celle d'une
    /// configuration native.
    /// </summary>
    private static void VerifyLegacyOfferKeepsItsOwnIdentity()
    {
        var legacy = BillingV2CheckoutSelectionFingerprint.ForLegacyOffer(
            "61000000-0000-0000-0000-000000000114");
        Ensure(
            legacy
                == BillingV2CheckoutSelectionFingerprint.ForLegacyOffer(
                    "61000000-0000-0000-0000-000000000114"),
            "Empreinte legacy stable.");
        Ensure(
            legacy
                != BillingV2CheckoutSelectionFingerprint.ForLegacyOffer(
                    "61000000-0000-0000-0000-000000000112"),
            "Deux offres legacy, deux empreintes.");
        Ensure(
            legacy
                != BillingV2CheckoutSelectionFingerprint.ForSelection(
                    Baseline("pack-bureau-windows-distance", "TERM-12")
                        .Canonical()),
            "Legacy et natif ne partagent jamais une empreinte.");

        // La formule standard payee au mois reste rattachee a son offre
        // legacy : le parcours historique n'est pas casse.
        Ensure(
            Quote("pack-bureau-windows-distance", "TERM-12")
                    .CheckoutLegacyOfferId
                == "61000000-0000-0000-0000-000000000114",
            "Formule standard mensuelle toujours mappee a son offre legacy.");
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
        Ensure(
            selection.PaymentMode == BillingV2PaymentModes.Monthly,
            "Mode de reglement par defaut : mensuel.");

        // Aucune reference de prix fournisseur ne doit pouvoir entrer par la
        // charge utile de souscription : le montant Stripe vient du
        // BillingEvent, jamais d'un `price_id` transmis par le navigateur.
        var checkoutProperties =
            typeof(Contracts.BillingV2AuthoritativeCheckoutPayload)
                .GetProperties();
        Ensure(
            checkoutProperties.All(
                property => !property.Name.Contains(
                    "Stripe",
                    StringComparison.OrdinalIgnoreCase)
                    && !property.Name.Contains(
                        "Price",
                        StringComparison.OrdinalIgnoreCase)
                    && !property.Name.Contains(
                        "Amount",
                        StringComparison.OrdinalIgnoreCase)),
            "Aucun identifiant de prix fournisseur dans la demande de checkout.");
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
