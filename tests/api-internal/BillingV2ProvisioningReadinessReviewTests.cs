using Kermaria.ApiInternal.Services.Provisioning;

namespace Kermaria.ApiInternal.SmokeTests;

/// <summary>
/// Contrats purs de la preview/review. Les effets SQL sont verifies par le
/// contrat de source WebPortal ; cette suite verrouille que le verdict porte
/// par une preview et celui persiste par une review viennent de la meme policy.
/// </summary>
public static class BillingV2ProvisioningReadinessReviewTests
{
    public static void Run()
    {
        PreviewSuccessAndPersistedReviewShareDecision();
        PreviewFailureAndPersistedReviewShareReasons();
        PersistenceUnavailableRemainsFailClosed();
        Console.WriteLine("Tests preview/review de readiness Billing V2 reussis.");
    }

    private static void PreviewSuccessAndPersistedReviewShareDecision()
    {
        var evaluation = Evaluation(new BillingV2ProvisioningReadinessReviewInputs(
            PersistentSqlAvailable: true,
            CustomerExists: true,
            CustomerIsDemo: false,
            ActiveV2SubscriptionCount: 1,
            UnresolvedRuleCount: 0,
            TargetGroupsResolved: true,
            StorageProviderReady: true,
            StorageTargetsResolved: true,
            AdTargetsResolved: true));
        var preview = evaluation.ToReviewResult(persisted: false);
        var review = evaluation.ToReviewResult(persisted: true);

        Ensure(preview.Ready && review.Ready
            && preview.ReasonCodes.SequenceEqual(review.ReasonCodes)
            && !preview.Persisted && review.Persisted,
            "Une preview et une review persistante doivent partager le meme verdict success.");
    }

    private static void PreviewFailureAndPersistedReviewShareReasons()
    {
        var evaluation = Evaluation(new BillingV2ProvisioningReadinessReviewInputs(
            PersistentSqlAvailable: true,
            CustomerExists: true,
            CustomerIsDemo: false,
            ActiveV2SubscriptionCount: 2,
            UnresolvedRuleCount: 0,
            TargetGroupsResolved: true,
            StorageProviderReady: false,
            StorageTargetsResolved: false,
            AdTargetsResolved: false));
        var preview = evaluation.ToReviewResult(persisted: false);
        var review = evaluation.ToReviewResult(persisted: true);

        Ensure(!preview.Ready && !review.Ready
            && preview.ReasonCodes.SequenceEqual(review.ReasonCodes)
            && preview.ReasonCodes.SequenceEqual(
                [
                    BillingV2ProvisioningReadinessReviewReasons.StorageProviderNotReady,
                    BillingV2ProvisioningReadinessReviewReasons.StorageTargetsUnresolved,
                    BillingV2ProvisioningReadinessReviewReasons.AdTargetsUnresolved
                ])
            && preview.UnresolvedMismatchCount == review.UnresolvedMismatchCount,
            "Une preview failed et une review persistante doivent conserver les memes reason codes.");
    }

    private static void PersistenceUnavailableRemainsFailClosed()
    {
        var preview = BillingV2ProvisioningClientReadinessEvaluation.PersistenceUnavailable
            .ToReviewResult(persisted: false);
        Ensure(!preview.Ready
            && !preview.Persisted
            && preview.ReasonCodes.SequenceEqual(
                [BillingV2ProvisioningReadinessReviewReasons.PersistentSqlUnavailable]),
            "Une preview sans SQL persistant doit rester fail-closed sans reclasser les causes historiques.");
    }

    private static BillingV2ProvisioningClientReadinessEvaluation Evaluation(
        BillingV2ProvisioningReadinessReviewInputs inputs)
    {
        var decision = BillingV2ProvisioningReadinessReviewPolicy.Evaluate(inputs);
        return new(
            decision,
            PersistentSqlAvailable: inputs.PersistentSqlAvailable,
            CustomerExists: inputs.CustomerExists,
            CustomerIsDemo: inputs.CustomerIsDemo,
            ActiveV2SubscriptionCount: inputs.ActiveV2SubscriptionCount,
            [],
            [],
            StorageTargetCount: 0,
            new(inputs.StorageProviderReady, "test"),
            new(Required: !inputs.StorageTargetsResolved, Evaluated: true,
                Resolved: inputs.StorageTargetsResolved, ReasonCode: null),
            new(Required: !inputs.AdTargetsResolved, Evaluated: true,
                Resolved: inputs.AdTargetsResolved, ReasonCode: null));
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
