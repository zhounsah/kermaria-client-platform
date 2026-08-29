using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.Data.Configuration;

/// <summary>
/// Registre ferme des drapeaux Billing V2.
///
/// Ces drapeaux sont resolus au demarrage depuis la configuration de la machine
/// (<see cref="BillingV2RuntimeConfiguration.Resolve"/>) et injectes en
/// singleton : ils sont donc **tous** `restart_required`. Les rendre dynamiques
/// signifierait pouvoir declencher, depuis une page web, un appel sortant reel
/// chez un prestataire de paiement ou une ecriture d'infrastructure sans qu'un
/// exploitant soit devant la machine. Le Centre de configuration les presente
/// donc en lecture seule, avec le contexte necessaire pour decider — pas comme
/// une rangee d'interrupteurs.
/// </summary>
public static class BillingV2FeatureFlagRegistry
{
    public sealed record Definition(
        string Key,
        string EnvironmentVariable,
        string Label,
        string Description,
        string Risk,
        IReadOnlyList<string> Dependencies);

    public static readonly IReadOnlyList<Definition> Definitions =
    [
        new(
            "new_subscriptions",
            "BILLING_V2_NEW_SUBSCRIPTIONS_ENABLED",
            "Nouvelles souscriptions",
            "Ouvre la creation de souscriptions sur le modele Billing V2. Sans lui, le reste de la chaine est inerte.",
            "high",
            []),
        new(
            "authoritative_checkout",
            "BILLING_V2_AUTHORITATIVE_CHECKOUT_ENABLED",
            "Checkout faisant autorite",
            "Le montant est etabli par API-INTERNAL, jamais par le navigateur ni par le prestataire.",
            "high",
            ["new_subscriptions"]),
        new(
            "first_real_subscription_approved",
            "BILLING_V2_FIRST_REAL_SUBSCRIPTION_APPROVED",
            "Premiere souscription reelle approuvee",
            "Dernier verrou avant de l'argent reel. Il est evalue avant tout appel sortant : tant qu'il est ferme, aucun objet n'est cree chez le prestataire.",
            "critical",
            ["new_subscriptions", "authoritative_checkout", "provider_outbox", "provider_executor"]),
        new(
            "provider_outbox",
            "BILLING_V2_PROVIDER_OUTBOX_ENABLED",
            "File d'intentions prestataire",
            "Enregistre les intentions d'appel prestataire. Ecrit en base, n'appelle rien par lui-meme.",
            "high",
            ["new_subscriptions"]),
        new(
            "provider_executor",
            "BILLING_V2_PROVIDER_EXECUTOR_ENABLED",
            "Executeur prestataire",
            "Consomme la file et appelle reellement le prestataire. C'est lui qui transforme une intention en action externe.",
            "critical",
            ["provider_outbox"]),
        new(
            "provisioning",
            "BILLING_V2_PROVISIONING_ENABLED",
            "Provisioning des services",
            "Applique les quotas de stockage puis les droits AD. Un stockage bloque empeche VPN et RDS du meme utilisateur.",
            "critical",
            []),
        new(
            "reconciliation_worker",
            "BILLING_V2_RECONCILIATION_WORKER_ENABLED",
            "Reconciliateur",
            "Seul composant capable d'appeler le prestataire sans action utilisateur. Il converge l'etat local sur l'etat distant.",
            "critical",
            ["provider_executor"]),
        new(
            "additional_user_provisioning",
            "BILLING_V2_ADDITIONAL_USER_PROVISIONING_ENABLED",
            "Provisioning des utilisateurs supplementaires",
            "Etend le provisioning aux identites additionnelles d'une souscription.",
            "high",
            ["provisioning"]),
        new(
            "generic_selection",
            "BILLING_V2_GENERIC_SELECTION_ENABLED",
            "Selection generique",
            "Autorise une selection catalogue hors preset predefini.",
            "medium",
            ["new_subscriptions"]),
        new(
            "service_fulfillment",
            "BILLING_V2_SERVICE_FULFILLMENT_ENABLED",
            "Livraison des services",
            "Declenche la livraison effective des services souscrits.",
            "high",
            ["provisioning"]),
        new(
            "subscription_changes",
            "BILLING_V2_SUBSCRIPTION_CHANGES_ENABLED",
            "Changements de souscription",
            "Autorise les changements de formule en cours d'abonnement, avec proratisation.",
            "high",
            ["new_subscriptions", "authoritative_checkout"]),
        new(
            "stripe_recurring_mutation",
            "BILLING_V2_STRIPE_RECURRING_MUTATION_ENABLED",
            "Mutation d'abonnement Stripe",
            "Modifie un abonnement existant chez Stripe. Action externe irreversible du point de vue du client.",
            "critical",
            ["subscription_changes", "provider_executor"]),
        new(
            "vps_local_provisioning",
            "BILLING_V2_VPS_LOCAL_PROVISIONING_ENABLED",
            "Provisioning VPS local",
            "Cree des ressources sur l'infrastructure locale.",
            "high",
            ["provisioning"]),
        new(
            "vps_cloud_automation",
            "BILLING_V2_VPS_CLOUD_AUTOMATION_ENABLED",
            "Automatisation VPS cloud",
            "Cree des ressources chez un fournisseur cloud, donc facturables hors de notre controle.",
            "critical",
            ["provisioning"])
    ];

    public static IReadOnlyList<BillingV2FeatureFlagItem> Describe(
        BillingV2RuntimeConfiguration configuration)
        => Definitions
            .Select(definition => new BillingV2FeatureFlagItem(
                definition.Key,
                definition.EnvironmentVariable,
                definition.Label,
                definition.Description,
                IsEnabled(definition.Key, configuration),
                definition.Risk,
                definition.Dependencies,
                definition.Dependencies
                    .Where(dependency => !IsEnabled(dependency, configuration))
                    .ToArray(),
                // Toutes ces valeurs viennent du demarrage : les modifier exige
                // une intervention sur la machine puis un redemarrage du
                // service.
                RestartRequired: true,
                Classification: "restart_required",
                Source: "environment"))
            .ToArray();

    private static bool IsEnabled(string key, BillingV2RuntimeConfiguration configuration)
        => key switch
        {
            "new_subscriptions" => configuration.NewSubscriptionsEnabled,
            "authoritative_checkout" => configuration.AuthoritativeCheckoutEnabled,
            "first_real_subscription_approved" => configuration.FirstRealSubscriptionApproved,
            "provider_outbox" => configuration.ProviderOutboxEnabled,
            "provider_executor" => configuration.ProviderExecutorEnabled,
            "provisioning" => configuration.ProvisioningEnabled,
            "reconciliation_worker" => configuration.ReconciliationWorkerEnabled,
            "additional_user_provisioning" => configuration.AdditionalUserProvisioningEnabled,
            "generic_selection" => configuration.GenericSelectionEnabled,
            "service_fulfillment" => configuration.ServiceFulfillmentEnabled,
            "subscription_changes" => configuration.SubscriptionChangesEnabled,
            "stripe_recurring_mutation" => configuration.StripeRecurringMutationEnabled,
            "vps_local_provisioning" => configuration.VpsLocalProvisioningEnabled,
            "vps_cloud_automation" => configuration.VpsCloudAutomationEnabled,
            _ => false
        };
}
