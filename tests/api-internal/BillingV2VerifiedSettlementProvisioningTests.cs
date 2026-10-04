using Kermaria.ApiInternal.Services.Provisioning;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kermaria.ApiInternal.SmokeTests;

public static class BillingV2VerifiedSettlementProvisioningTests
{
    public static async Task RunAsync()
    {
        const string subscription = "verified-subscription";
        var checks = 0;
        var applications = 0;
        var isVps = false;
        Task<bool> Check(string id, CancellationToken token)
        {
            if (id != subscription) throw new InvalidOperationException("Wrong subscription target.");
            checks++;
            return Task.FromResult(isVps);
        }
        var expected = new ProvisioningExecutionResult(true, true, "PROVISIONING_APPLIED", []);
        Task<ProvisioningExecutionResult?> Apply(string id, CancellationToken token)
        {
            if (id != subscription) throw new InvalidOperationException("Wrong provisioning target.");
            applications++;
            return Task.FromResult<ProvisioningExecutionResult?>(expected);
        }
        Task<BillingV2VerifiedSettlementProvisioningResult> Execute(bool verified)
            => BillingV2VerifiedSettlementProvisioning.TryExecuteAsync(
                verified, subscription, Check, Apply, NullLogger.Instance, CancellationToken.None);

        Ensure((await Execute(false)).ResultCode == "BILLING_V2_PAYMENT_NOT_VERIFIED" && checks == 0 && applications == 0,
            "An unverified payment must cause no provisioning or technical lookup.");
        isVps = true;
        Ensure((await Execute(true)).ResultCode == "BILLING_V2_VPS_TECHNICAL_REVIEW_REQUIRED" && applications == 0,
            "A VPS technical workflow must remain outside automatic provisioning.");
        isVps = false;
        Ensure(ReferenceEquals((await Execute(true)).Execution, expected) && applications == 1,
            "Verified eligible settlement must call the existing provisioning engine once.");
        expected = new(false, false, "AD_ACCESS_DENIED", []);
        Ensure(ReferenceEquals((await Execute(true)).Execution, expected),
            "A provisioning refusal must not be converted to success.");
        var failed = await BillingV2VerifiedSettlementProvisioning.TryExecuteAsync(
            true, subscription, Check, (_, _) => throw new IOException("unavailable"),
            NullLogger.Instance, CancellationToken.None);
        Ensure(failed is { Execution: null, ResultCode: "BILLING_V2_PROVISIONING_TRIGGER_FAILED" },
            "A technical failure must remain reportable without undoing settlement.");
        var notExecuted = await BillingV2VerifiedSettlementProvisioning.TryExecuteAsync(
            true, subscription, Check, (_, _) => Task.FromResult<ProvisioningExecutionResult?>(null),
            NullLogger.Instance, CancellationToken.None);
        Ensure(notExecuted is { Execution: null, ResultCode: "BILLING_V2_PROVISIONING_NOT_EXECUTED" }, "An existing readiness gate refusal must remain unexecuted.");
        var technicalLookupFailed = await BillingV2VerifiedSettlementProvisioning.TryExecuteAsync(
            true, subscription, (_, _) => throw new IOException("lookup unavailable"), Apply,
            NullLogger.Instance, CancellationToken.None);
        Ensure(technicalLookupFailed is { Execution: null, ResultCode: "BILLING_V2_PROVISIONING_TRIGGER_FAILED" },
            "An unavailable VPS gate must fail closed before any provisioning.");
        try
        {
            await BillingV2VerifiedSettlementProvisioning.TryExecuteAsync(
                true, subscription, Check, (_, _) => throw new OperationCanceledException(),
                NullLogger.Instance, CancellationToken.None);
            throw new InvalidOperationException("Operation cancellation was swallowed.");
        }
        catch (OperationCanceledException) { }
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try
        {
            await BillingV2VerifiedSettlementProvisioning.TryExecuteAsync(
                true, subscription, Check, Apply, NullLogger.Instance, cancellation.Token);
            throw new InvalidOperationException("Cancellation was swallowed.");
        }
        catch (OperationCanceledException) { }
        Console.WriteLine("Verified settlement provisioning tests passed.");
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
