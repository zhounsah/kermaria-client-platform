namespace Kermaria.ApiInternal.Services;

public static class FiscalRegimes
{
    public const string FranchiseBase = "franchise_base";
    public const string Standard = "standard";
}

public sealed record FiscalPolicySnapshot(
    string FiscalRegime,
    int? TaxRateBasisPoints,
    string FiscalMention);

public interface IFiscalPolicy
{
    FiscalPolicySnapshot Resolve(int? taxRateBasisPoints);

    int CalculateTaxAmount(int amountCents, int? taxRateBasisPoints);

    int AmountIncludingTax(int amountCents, int? taxRateBasisPoints);
}

public sealed class FiscalPolicy : IFiscalPolicy
{
    public const string FranchiseBaseMention =
        "TVA non applicable, art. 293 B du CGI.";

    public const string StandardMention = "TVA au taux en vigueur.";

    public FiscalPolicySnapshot Resolve(int? taxRateBasisPoints)
        => taxRateBasisPoints is null or <= 0
            ? new FiscalPolicySnapshot(
                FiscalRegimes.FranchiseBase,
                null,
                FranchiseBaseMention)
            : new FiscalPolicySnapshot(
                FiscalRegimes.Standard,
                taxRateBasisPoints,
                StandardMention);

    public int CalculateTaxAmount(int amountCents, int? taxRateBasisPoints)
    {
        var policy = Resolve(taxRateBasisPoints);
        if (policy.FiscalRegime == FiscalRegimes.FranchiseBase)
        {
            return 0;
        }

        return (int)Math.Round(
            amountCents * (policy.TaxRateBasisPoints!.Value / 10000m),
            0,
            MidpointRounding.AwayFromZero);
    }

    public int AmountIncludingTax(int amountCents, int? taxRateBasisPoints)
        => amountCents + CalculateTaxAmount(amountCents, taxRateBasisPoints);
}
