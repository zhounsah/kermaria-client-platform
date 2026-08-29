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

/// <summary>
/// Une mention fiscale et la date a partir de laquelle elle s'applique.
/// </summary>
public sealed record FiscalMentionVersion(
    string Regime,
    string Mention,
    DateTime EffectiveFromUtc);

/// <summary>
/// Instantane immuable des mentions administrees. Vide par defaut : le code
/// reste alors seul a decider du texte, ce qui est exactement le comportement
/// anterieur a l'administration des mentions.
/// </summary>
public sealed class FiscalMentionSnapshot
{
    public static readonly FiscalMentionSnapshot Empty = new([]);

    private readonly IReadOnlyList<FiscalMentionVersion> _versions;

    public FiscalMentionSnapshot(IReadOnlyList<FiscalMentionVersion> versions)
        => _versions = versions
            .OrderBy(version => version.EffectiveFromUtc)
            .ToArray();

    /// <summary>
    /// Mention en vigueur pour ce regime a la date demandee, ou `null` si
    /// aucune version n'etait encore applicable. La resolution est faite « a la
    /// date » de la ligne de document : une facture emise hier ne change pas
    /// parce que le texte a ete modifie aujourd'hui.
    /// </summary>
    public string? Resolve(string regime, DateTime asOfUtc)
    {
        string? mention = null;
        foreach (var version in _versions)
        {
            if (!string.Equals(version.Regime, regime, StringComparison.Ordinal)) continue;
            if (version.EffectiveFromUtc > asOfUtc) break;
            mention = version.Mention;
        }

        return mention;
    }
}

/// <summary>
/// Porte l'instantane courant des mentions pour les projections synchrones.
/// Il est charge au demarrage et remplace apres chaque mutation ; un
/// instantane absent ou perime ne peut produire que la mention du code, jamais
/// une mention inventee.
/// </summary>
public static class FiscalMentionDirectory
{
    private static FiscalMentionSnapshot _current = FiscalMentionSnapshot.Empty;

    public static FiscalMentionSnapshot Current => Volatile.Read(ref _current);

    public static void Apply(FiscalMentionSnapshot snapshot)
        => Volatile.Write(ref _current, snapshot);

    public static void Reset() => Apply(FiscalMentionSnapshot.Empty);
}

public interface IFiscalPolicy
{
    FiscalPolicySnapshot Resolve(int? taxRateBasisPoints);

    /// <summary>
    /// Meme resolution, mais avec la mention en vigueur a la date donnee. Le
    /// regime et le calcul restent decides par le code : seule la formulation
    /// de la mention est administrable.
    /// </summary>
    FiscalPolicySnapshot Resolve(int? taxRateBasisPoints, DateTime asOfUtc);

    int CalculateTaxAmount(int amountCents, int? taxRateBasisPoints);

    int AmountIncludingTax(int amountCents, int? taxRateBasisPoints);
}

public sealed class FiscalPolicy : IFiscalPolicy
{
    public const string FranchiseBaseMention =
        "TVA non applicable, art. 293 B du CGI.";

    public const string StandardMention = "TVA au taux en vigueur.";

    public static string DefaultMention(string regime)
        => regime == FiscalRegimes.Standard ? StandardMention : FranchiseBaseMention;

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

    public FiscalPolicySnapshot Resolve(int? taxRateBasisPoints, DateTime asOfUtc)
    {
        var snapshot = Resolve(taxRateBasisPoints);
        var administered = FiscalMentionDirectory.Current.Resolve(
            snapshot.FiscalRegime,
            asOfUtc);
        return administered is null
            ? snapshot
            : snapshot with { FiscalMention = administered };
    }

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
