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
    bool PayPalConfigured);

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
                "PayPal V2 n'est pas raccorde au coeur financier. Cela ne bloque pas Stripe.")
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
