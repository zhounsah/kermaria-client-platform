namespace Kermaria.ApiInternal.Data.Repositories;

/// <summary>
/// Panne simulee de la persistance du mot de passe portail, reservee aux
/// persistances mock.
/// </summary>
/// <remarks>
/// <para>
/// Le scenario a couvrir n'est pas « l'ecriture a echoue » mais « le secret
/// destine a KoXo a-t-il survecu a l'echec ». Tant que le secret est depose et
/// le condensat portail ecrit par deux operations distinctes, une panne entre
/// les deux laisse KoXo appliquer plus tard a l'annuaire un mot de passe que le
/// portail ne connait pas : l'utilisateur ouvre sa session NextCloud, RDS et
/// VPN avec un mot de passe, et le portail avec un autre. Rien ne le signale.
/// </para>
/// <para>
/// Ce commutateur declenche la seconde moitie du scenario : le scelle vient
/// d'etre attache, et l'ecriture du condensat leve. Le test verifie ensuite
/// l'etat reel — aucun secret en attente, condensat inchange — et non un code
/// HTTP.
/// </para>
/// <para>
/// Il n'existe que dans les depots mock, jamais dans les depots MariaDB : rien
/// dans un environnement persistant ne peut l'armer.
/// </para>
/// </remarks>
public static class MockPortalPasswordFailureSwitch
{
    private static int _armed;

    /// <summary>
    /// Arme une panne unique : la prochaine ecriture du condensat portail leve.
    /// </summary>
    public static void ArmOnce() => Interlocked.Exchange(ref _armed, 1);

    public static void Disarm() => Interlocked.Exchange(ref _armed, 0);

    /// <summary>
    /// Consomme l'armement et leve le cas echeant. Appele a l'interieur de la
    /// section critique, apres l'attache du scelle : l'annulation doit alors
    /// remettre les deux ecritures dans leur etat initial.
    /// </summary>
    public static void ThrowIfArmed()
    {
        if (Interlocked.CompareExchange(ref _armed, 0, 1) == 1)
        {
            throw new InvalidOperationException(
                "Ecriture du mot de passe portail indisponible (panne simulee).");
        }
    }
}
