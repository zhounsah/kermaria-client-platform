using Kermaria.ApiInternal.Infrastructure;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Horloge injectable du domaine Billing V2.
///
/// Existe pour une raison precise : un calcul contractuel de periode ne doit
/// jamais dependre directement de <c>DateTime.UtcNow</c>, sinon il est
/// intestable aux bornes (minuit Paris, changement d'heure, fin de mois).
/// </summary>
public interface IBillingV2Clock
{
    DateTime UtcNow { get; }
}

public sealed class SystemBillingV2Clock : IBillingV2Clock
{
    public static SystemBillingV2Clock Instance { get; } = new();

    public DateTime UtcNow => DateTime.UtcNow;
}

/// <summary>
/// Horloge figee, pour les tests de bornes.
/// </summary>
public sealed class FixedBillingV2Clock : IBillingV2Clock
{
    public FixedBillingV2Clock(DateTime utcNow)
        => UtcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);

    public DateTime UtcNow { get; }
}

public sealed record BillingV2ContractPeriod(
    DateOnly CivilStart,
    DateOnly CivilEnd,
    DateTime StartUtc,
    DateTime EndUtc);

/// <summary>
/// Calendrier contractuel Billing V2, en heure civile Europe/Paris.
///
/// Le probleme corrige : un abonnement cree le 16 aout a 00h30 Paris vaut
/// 2026-08-15 22h30 UTC. Prendre <c>.Date</c> sur l'instant UTC datait la
/// periode au 15 aout, alors que la facture, elle, portait le jour Paris (16).
/// Document et facture divergeaient donc d'un jour, quelques heures par nuit,
/// toute l'annee.
///
/// Regle : une date contractuelle ou fiscale est un JOUR CIVIL PARIS, derive
/// d'un instant UTC, jamais un <c>.Date</c> pris sur l'UTC brut.
/// </summary>
public static class BillingV2BillingCalendar
{
    /// <summary>
    /// Jour civil Paris correspondant a un instant UTC.
    /// </summary>
    public static DateOnly CivilDate(DateTime instantUtc)
        => DateOnly.FromDateTime(KermariaTimeZone.ToLocal(instantUtc));

    /// <summary>
    /// Periode contractuelle a partir de l'instant d'ancrage.
    ///
    /// Les bornes sont des jours civils Paris. L'arithmetique mensuelle utilise
    /// <c>AddMonths</c>, qui rabat sur le dernier jour du mois cible : une
    /// ancre au 31 janvier donne une fin au 28/29 fevrier. Ce rabattement est
    /// volontaire et documente ; il ne "remonte" pas au 31 mars ensuite, la
    /// periode suivante etant calculee depuis la meme ancre contractuelle et
    /// non depuis la borne precedente.
    /// </summary>
    public static BillingV2ContractPeriod ResolvePeriod(
        DateTime anchorUtc,
        string paymentMode,
        int commitmentMonths)
    {
        var months = string.Equals(
                paymentMode,
                BillingV2PaymentModes.Upfront,
                StringComparison.Ordinal)
            ? Math.Max(1, commitmentMonths)
            : 1;
        return ResolvePeriodForMonths(anchorUtc, months);
    }

    /// <summary>
    /// Periode du n-ieme cycle, derivee de l'ancre contractuelle et du rang du
    /// cycle. Jamais de l'heure courante : c'est ce qui rend un renouvellement
    /// reproductible et rejouable.
    /// </summary>
    public static BillingV2ContractPeriod ResolveCyclePeriod(
        DateTime anchorUtc,
        int monthsPerCycle,
        int cycleSequence)
    {
        if (monthsPerCycle <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monthsPerCycle));
        }

        if (cycleSequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(cycleSequence));
        }

        var anchorCivil = CivilDate(anchorUtc);
        var start = anchorCivil.AddMonths(monthsPerCycle * (cycleSequence - 1));
        var end = anchorCivil.AddMonths(monthsPerCycle * cycleSequence);
        return Build(start, end);
    }

    private static BillingV2ContractPeriod ResolvePeriodForMonths(
        DateTime anchorUtc,
        int months)
    {
        var start = CivilDate(anchorUtc);
        return Build(start, start.AddMonths(months));
    }

    private static BillingV2ContractPeriod Build(DateOnly start, DateOnly end)
        => new(
            start,
            end,
            ToUtcStartOfCivilDay(start),
            ToUtcStartOfCivilDay(end));

    /// <summary>
    /// Instant UTC correspondant a minuit (heure civile Paris) du jour donne.
    ///
    /// Le 30 mars 2025, 02h00 Paris n'existe pas ; minuit, lui, existe toujours,
    /// ce qui rend cette conversion sure aux deux bascules d'heure.
    /// </summary>
    public static DateTime ToUtcStartOfCivilDay(DateOnly civilDay)
    {
        var localMidnight = DateTime.SpecifyKind(
            civilDay.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(
            localMidnight,
            KermariaTimeZone.TimeZone);
    }
}

public sealed record BillingV2SubscriptionLifecyclePlan(
    DateTime CommitmentStartedAtUtc,
    DateTime CommitmentEndsAtUtc,
    DateTime CurrentPeriodStartedAtUtc,
    DateTime CurrentPeriodEndsAtUtc,
    DateTime? RenewsAtUtc);

/// <summary>
/// Dates contractuelles d'un abonnement V2, derivees de la meme ancre civile
/// que le BillingEvent.
///
/// L'engagement borne le contrat dans les deux modes ; ce qui change, c'est le
/// cycle courant et la promesse de renouvellement :
///
/// - mensuel : cycle d'un mois, <c>renews_at</c> = fin du cycle courant ;
/// - comptant : le cycle courant EST la periode d'engagement deja encaissee, et
///   <c>renews_at</c> reste NULL. Le renouvellement d'un terme prepaye est
///   manuel (MVP) : promettre une date de renouvellement laisserait croire a un
///   prelevement automatique qui n'existe pas.
/// </summary>
public static class BillingV2SubscriptionLifecyclePolicy
{
    /// <summary>
    /// Dates contractuelles d'une souscription.
    /// </summary>
    /// <param name="hasRecurringComponent">
    /// Vrai quand la composition retenue porte au moins une composante
    /// tarifaire recurrente. Un achat purement ponctuel ne se renouvelle
    /// jamais : lui poser un <c>renews_at</c> ferait planifier au moteur de
    /// renouvellement un cycle sans montant a facturer. Cette information vient
    /// des composantes reellement resolues, jamais du `billing_type` du
    /// service, du preset ou du mode de reglement.
    /// </param>
    public static BillingV2SubscriptionLifecyclePlan Plan(
        string paymentMode,
        int commitmentMonths,
        DateTime anchorUtc,
        bool hasRecurringComponent = true)
    {
        var commitment = BillingV2BillingCalendar.ResolveCyclePeriod(
            anchorUtc,
            Math.Max(1, commitmentMonths),
            cycleSequence: 1);
        var current = BillingV2BillingCalendar.ResolvePeriod(
            anchorUtc,
            paymentMode,
            commitmentMonths);
        var upfront = string.Equals(
            paymentMode,
            BillingV2PaymentModes.Upfront,
            StringComparison.Ordinal);

        return new BillingV2SubscriptionLifecyclePlan(
            commitment.StartUtc,
            commitment.EndUtc,
            current.StartUtc,
            current.EndUtc,
            upfront || !hasRecurringComponent ? null : current.EndUtc);
    }
}
