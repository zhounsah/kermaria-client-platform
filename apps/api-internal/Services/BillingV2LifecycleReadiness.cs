namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Matrice de readiness du cycle de vie Billing V2 (Phase 3, point 10).
///
/// Elle existe parce que "pret" n'est pas un booleen : une plateforme peut
/// encaisser un premier paiement sans savoir renouveler, ou savoir renouveler
/// sans savoir emettre une facture. Ecraser ces nuances en un seul drapeau,
/// c'est ce qui produit des mises en service prematurees.
///
/// Trois etats, et un seul sens de lecture :
/// - READY     : automatise et verifie par des tests ;
/// - MANUAL    : possible, mais exige une intervention humaine assumee ;
/// - NOT_READY : ne doit pas etre utilise en l'etat.
/// </summary>

/// <summary>
/// Perimetre GELE du lancement Billing V2 (Phase 4, point 1).
///
/// Ce n'est pas de la documentation : c'est une barriere executable. Une
/// capacite hors perimetre doit etre refusee par le code, pas seulement
/// absente d'une liste - sinon elle se retrouve ouverte le jour ou quelqu'un
/// pose le bon drapeau sans savoir qu'elle n'a jamais ete validee.
///
/// Ouvrir une de ces capacites demande de repasser par une validation
/// explicite, pas un simple changement de configuration.
/// </summary>
public static class BillingV2LaunchScope
{
    // --- DANS le perimetre de lancement -------------------------------
    public const bool StripeMonthlyEnabled = true;

    // --- HORS perimetre : refuse en dur --------------------------------

    /// <summary>
    /// Comptant 6/12 mois. Ouvert seulement une fois le cycle de vie
    /// contractuel pose : engagement date en base, droits bornes a la periode
    /// payee, expiration reconnue en fin de terme et aucun renouvellement
    /// automatique. Sans ces quatre garanties, un comptant encaisse laissait
    /// des droits illimites apres son terme.
    /// </summary>
    public const bool UpfrontPaymentEnabled = true;

    public const bool PayPalEnabled = false;
    public const bool SelfServiceUpgradesEnabled = false;
    public const bool SelfServiceDowngradesEnabled = false;
    public const bool CreditLedgerEnabled = false;
    public const bool RefundsEnabled = false;
    public const bool ChargebacksEnabled = false;
    public const bool SelfServiceCancellationEnabled = false;
    public const bool NonZeroTaxEnabled = false;

    /// <summary>
    /// Verdict applique AVANT tout depart vers un provider. Volontairement
    /// place sur le chemin de dispatch et non dans la construction de
    /// l'evenement : le coeur financier continue de savoir calculer un
    /// comptant, on refuse seulement de l'encaisser.
    /// </summary>
    public static BillingV2FinancialDecision EvaluateCheckout(
        string provider,
        string paymentMode,
        long taxAmountCents)
    {
        if (!string.Equals(provider, "stripe", StringComparison.OrdinalIgnoreCase))
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_SCOPE_PROVIDER_OUT_OF_LAUNCH_SCOPE",
                provider);
        }

        if (!UpfrontPaymentEnabled
            && string.Equals(
                paymentMode,
                BillingV2PaymentModes.Upfront,
                StringComparison.Ordinal))
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_SCOPE_UPFRONT_OUT_OF_LAUNCH_SCOPE",
                paymentMode);
        }

        if (!NonZeroTaxEnabled && taxAmountCents != 0)
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_SCOPE_TAX_OUT_OF_LAUNCH_SCOPE",
                taxAmountCents.ToString());
        }

        return BillingV2FinancialDecision.Ok(
            "BILLING_V2_SCOPE_WITHIN_LAUNCH_SCOPE");
    }
}

/// <summary>
/// Fenetre contractuelle d'un abonnement V2, exprimee en SQL pour que toutes
/// les projections de droits appliquent exactement la meme regle.
///
/// Un contrat comptant n'a aucun renouvellement automatique (renews_at NULL) :
/// passe <c>commitment_ends_at</c>, il est expire et ne doit plus rien ouvrir,
/// meme si son statut est reste 'active'. Un contrat mensuel porte une date de
/// renouvellement et n'est donc jamais borne par ce predicat.
///
/// La table doit etre aliasee <c>subscription</c> dans la requete appelante.
/// </summary>
public static class BillingV2ContractWindowSql
{
    public const string SubscriptionStillInForce =
        """
        (
            subscription.renews_at IS NOT NULL
            OR subscription.commitment_ends_at IS NULL
            OR subscription.commitment_ends_at > UTC_TIMESTAMP(6)
        )
        """;
}

public static class BillingV2ReadinessStates
{
    public const string Ready = "READY";
    public const string Manual = "MANUAL";
    public const string NotReady = "NOT_READY";
}

public static class BillingV2ReadinessComponents
{
    public const string InitialCheckout = "initial_checkout";
    public const string StripeSettlement = "stripe_settlement";
    public const string StripeReconciliation = "stripe_reconciliation";
    public const string MonthlyRenewal = "monthly_renewal";
    public const string FailedRenewal = "failed_renewal";
    public const string DocumentIssuance = "document_issuance";
    public const string BpceRecovery = "bpce_recovery";
    public const string Portal = "portal";
    public const string AdProvisioning = "ad_provisioning";
    public const string Cancellation = "cancellation";
    public const string UpgradesDowngrades = "upgrades_downgrades";
    public const string PayPal = "paypal";
    public const string UpfrontPayment = "upfront_payment";
    // Un seul statut "refunds" ne permettrait pas de distinguer le code
    // dormant d'une capacite financiere effectivement activable.
    public const string RefundCoreCode = "refund_core_code";
    public const string RefundSchema = "refund_schema";
    public const string RefundStripe = "refund_stripe";
    public const string RefundDocumentCorrection = "refund_document_correction";
    public const string Refunds = "refunds";
}

public sealed record BillingV2ReadinessComponent(
    string Component,
    string State,
    string ReasonCode,
    string Message);

public sealed record BillingV2LifecycleReadinessInputs(
    bool PersistentSqlAvailable,
    bool FinancialCoreSchemaReady,
    bool RenewalSchemaReady,
    bool AuthoritativeCheckoutEnabled,
    bool ProviderExecutorEnabled,
    bool StripeConfigured,
    bool StripePriceMappingsReady,
    bool ReconciliationWorkerActivatable,
    bool DocumentIssuanceReady,
    bool BpceInvoiceLookupSupported,
    bool ProvisioningEnabled,
    bool PayPalConfigured,
    // Ces preuves ne sont jamais deduites d'un flag runtime. Elles restent
    // fermees tant qu'une validation explicite schema/provider/document ne
    // les a pas apportees.
    bool RefundSchemaReady = false,
    bool RefundStripeOperational = false,
    bool RefundDocumentCorrectionOperational = false);

public static class BillingV2LifecycleReadinessGate
{
    /// <summary>
    /// Composants qui doivent etre READY pour ouvrir un abonnement mensuel
    /// Stripe reel. Les autres peuvent rester MANUAL sans bloquer.
    /// </summary>
    public static readonly IReadOnlySet<string> RequiredForStripeLaunch =
        new HashSet<string>(StringComparer.Ordinal)
        {
            BillingV2ReadinessComponents.InitialCheckout,
            BillingV2ReadinessComponents.StripeSettlement,
            BillingV2ReadinessComponents.StripeReconciliation,
            BillingV2ReadinessComponents.MonthlyRenewal,
            BillingV2ReadinessComponents.FailedRenewal,
            BillingV2ReadinessComponents.DocumentIssuance
        };

    public static IReadOnlyList<BillingV2ReadinessComponent> Evaluate(
        BillingV2LifecycleReadinessInputs inputs)
    {
        var financialCoreReady = inputs.PersistentSqlAvailable
            && inputs.FinancialCoreSchemaReady;
        var railReady = financialCoreReady
            && inputs.AuthoritativeCheckoutEnabled
            && inputs.ProviderExecutorEnabled
            && inputs.StripeConfigured
            && inputs.StripePriceMappingsReady;

        return
        [
            Component(
                BillingV2ReadinessComponents.InitialCheckout,
                railReady,
                "BILLING_V2_READINESS_INITIAL_CHECKOUT",
                "Intention serveur, BillingEvent finalise, PaymentAttempt et montant local font autorite.",
                "Coeur financier, drapeaux Stripe ou mappings de prix incomplets."),
            Component(
                BillingV2ReadinessComponents.StripeSettlement,
                railReady,
                "BILLING_V2_READINESS_STRIPE_SETTLEMENT",
                "Relecture Stripe obligatoire : session, abonnement et invoice verifies avant toute transition.",
                "Le rail Stripe n'est pas complet ; aucun encaissement ne peut etre prouve."),
            // Activable, pas actif : c'est la propriete demandee. Le worker
            // reste OFF tant qu'on ne l'allume pas explicitement.
            Component(
                BillingV2ReadinessComponents.StripeReconciliation,
                railReady && inputs.ReconciliationWorkerActivatable,
                "BILLING_V2_READINESS_STRIPE_RECONCILIATION",
                "Reconciliateur avec bail, backoff et escalade ; activable par BILLING_V2_RECONCILIATION_WORKER_ENABLED.",
                "Le reconciliateur n'est pas activable : un webhook perdu resterait sans filet."),
            Component(
                BillingV2ReadinessComponents.MonthlyRenewal,
                railReady && inputs.RenewalSchemaReady,
                "BILLING_V2_READINESS_MONTHLY_RENEWAL",
                "Cycle identifie par (abonnement, rang), prix contractuels figes, unicite garantie en base.",
                "Schema de renouvellement ou rail Stripe incomplet."),
            Component(
                BillingV2ReadinessComponents.FailedRenewal,
                inputs.RenewalSchemaReady,
                "BILLING_V2_READINESS_FAILED_RENEWAL",
                "Impaye visible via payment_state ; politique de grace V2.0, aucun retrait d'acces automatique.",
                "L'etat de paiement local n'est pas disponible : un impaye passerait inapercu."),
            Component(
                BillingV2ReadinessComponents.DocumentIssuance,
                inputs.DocumentIssuanceReady && inputs.RenewalSchemaReady,
                "BILLING_V2_READINESS_DOCUMENT_ISSUANCE",
                "Un document par cycle, construit depuis les seuls snapshots du BillingEvent, emission retry-safe.",
                "L'emission documentaire n'est pas prete."),
            // MANUAL assume : sans recherche de facture cote BPCE, une reprise
            // apres coupure ne peut pas se conclure toute seule.
            Manual(
                BillingV2ReadinessComponents.BpceRecovery,
                inputs.BpceInvoiceLookupSupported,
                "BILLING_V2_READINESS_BPCE_RECOVERY",
                "Recherche de facture disponible : la reprise apres coupure peut se conclure seule.",
                "L'API BPCE n'expose pas de recherche de facture : un appel indetermine part en revue humaine plutot que de risquer un second numero fiscal."),
            Component(
                BillingV2ReadinessComponents.Portal,
                inputs.PersistentSqlAvailable,
                "BILLING_V2_READINESS_PORTAL",
                "Projections portail disponibles.",
                "Sans SQL persistante, le portail ne peut rien projeter."),
            Manual(
                BillingV2ReadinessComponents.AdProvisioning,
                inputs.ProvisioningEnabled,
                "BILLING_V2_READINESS_AD_PROVISIONING",
                "Provisioning V2 actif a l'activation.",
                "Provisioning V2 non actif : le rattachement AD reste une operation humaine."),
            // Hors perimetre assume des phases 1 a 3.
            Manual(
                BillingV2ReadinessComponents.Cancellation,
                false,
                "BILLING_V2_READINESS_CANCELLATION",
                string.Empty,
                "La resiliation V2 n'est pas automatisee : elle passe par une decision humaine."),
            NotReady(
                BillingV2ReadinessComponents.UpgradesDowngrades,
                "BILLING_V2_READINESS_UPGRADES_DOWNGRADES",
                "Upgrades, downgrades, avoirs et remboursements sont hors perimetre : le Customer Credit Ledger n'existe pas."),
            NotReady(
                BillingV2ReadinessComponents.PayPal,
                "BILLING_V2_READINESS_PAYPAL",
                "PayPal V2 n'est pas raccorde au coeur financier. Cela ne bloque pas Stripe."),
            // Encaissement unique couvrant tout le terme. Le renouvellement en
            // fin de periode reste manuel : rien ne se represente tout seul.
            new BillingV2ReadinessComponent(
                BillingV2ReadinessComponents.UpfrontPayment,
                BillingV2ReadinessStates.Manual,
                "BILLING_V2_READINESS_UPFRONT_PAYMENT",
                "Le comptant 6/12 mois est encaisse en une fois et les droits s'arretent au terme. Le renouvellement est manuel."),
            new BillingV2ReadinessComponent(
                BillingV2ReadinessComponents.RefundCoreCode,
                BillingV2ReadinessStates.Ready,
                "BILLING_V2_READINESS_REFUND_CORE_CODE",
                "Le coeur refund interne, l'outbox, la relecture Stripe et la compensation d'abonnement sont presents ; aucun endpoint client n'est expose."),
            Component(
                BillingV2ReadinessComponents.RefundSchema,
                inputs.PersistentSqlAvailable && inputs.RefundSchemaReady,
                "BILLING_V2_READINESS_REFUND_SCHEMA",
                "Le schema refund 082 et ses dependances transactionnelles sont observes sur MariaDB.",
                "Le schema refund 082 ou ses dependances transactionnelles ne sont pas verifies sur MariaDB."),
            Component(
                BillingV2ReadinessComponents.RefundStripe,
                inputs.RefundStripeOperational,
                "BILLING_V2_READINESS_REFUND_STRIPE",
                "Le flux Stripe test refund, retry idempotent et relecture provider sont prouves.",
                "Le flux refund Stripe test n'est pas encore prouve sur un compte et un PaymentIntent de test reels."),
            Component(
                BillingV2ReadinessComponents.RefundDocumentCorrection,
                inputs.RefundDocumentCorrectionOperational,
                "BILLING_V2_READINESS_REFUND_DOCUMENT_CORRECTION",
                "L'avoir BPCE, sa preuve et sa reprise idempotente sont operationnels.",
                "Aucune primitive BPCE d'avoir/recherche par reference ne permet encore de prouver une correction documentaire idempotente."),
            Component(
                BillingV2ReadinessComponents.Refunds,
                inputs.PersistentSqlAvailable
                    && inputs.RefundSchemaReady
                    && inputs.RefundStripeOperational
                    && inputs.RefundDocumentCorrectionOperational,
                "BILLING_V2_READINESS_REFUNDS",
                "La chaine refund est prouvee de bout en bout et peut etre activee par une decision explicite ; BILLING_V2_REFUNDS_ENABLED reste un verrou d'execution serveur, jamais un droit client.",
                "La capacite refund reste desactivee : elle exige simultanement schema 082 observe, preuve Stripe test reelle et correction documentaire BPCE prouvee. BILLING_V2_REFUNDS_ENABLED ne suffit jamais a les remplacer.")
        ];
    }

    /// <summary>
    /// Un composant NOT_READY ne bloque le lancement Stripe que s'il figure
    /// dans <see cref="RequiredForStripeLaunch"/>. C'est ce qui permet a PayPal
    /// de rester explicitement NOT READY sans empecher Stripe de fonctionner.
    /// </summary>
    public static bool BlocksStripeLaunch(BillingV2ReadinessComponent component)
        => RequiredForStripeLaunch.Contains(component.Component)
           && !string.Equals(
               component.State,
               BillingV2ReadinessStates.Ready,
               StringComparison.Ordinal);

    public static IReadOnlyList<BillingV2ReadinessComponent> StripeLaunchBlockers(
        IReadOnlyList<BillingV2ReadinessComponent> components)
        => components.Where(BlocksStripeLaunch).ToArray();

    private static BillingV2ReadinessComponent Component(
        string name,
        bool ready,
        string reasonCode,
        string readyMessage,
        string notReadyMessage)
        => ready
            ? new BillingV2ReadinessComponent(
                name,
                BillingV2ReadinessStates.Ready,
                reasonCode,
                readyMessage)
            : new BillingV2ReadinessComponent(
                name,
                BillingV2ReadinessStates.NotReady,
                reasonCode,
                notReadyMessage);

    private static BillingV2ReadinessComponent Manual(
        string name,
        bool ready,
        string reasonCode,
        string readyMessage,
        string manualMessage)
        => ready
            ? new BillingV2ReadinessComponent(
                name,
                BillingV2ReadinessStates.Ready,
                reasonCode,
                readyMessage)
            : new BillingV2ReadinessComponent(
                name,
                BillingV2ReadinessStates.Manual,
                reasonCode,
                manualMessage);

    private static BillingV2ReadinessComponent NotReady(
        string name,
        string reasonCode,
        string message)
        => new(name, BillingV2ReadinessStates.NotReady, reasonCode, message);
}
