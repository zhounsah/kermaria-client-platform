namespace Kermaria.ApiInternal.Data.Repositories;

/// <summary>
/// Panne d'historisation simulee, reservee aux persistances mock.
/// </summary>
/// <remarks>
/// <para>
/// L'atomicite « mutation + revision » ne se demontre pas en lisant le code :
/// elle se demontre en faisant echouer l'ecriture de la revision et en
/// verifiant que la valeur n'a pas bouge. Cote MariaDB, la garantie vient de
/// la transaction ; cote mock, elle vient du verrou unique. Ce commutateur
/// permet de declencher la seconde moitie du scenario.
/// </para>
/// <para>
/// Il n'existe que dans les depots mock, jamais dans les depots MariaDB : rien
/// dans un environnement persistant ne peut l'armer.
/// </para>
/// </remarks>
public static class MockRevisionFailureSwitch
{
    private static int _armed;

    /// <summary>Arme une panne unique : la prochaine ecriture de revision leve.</summary>
    public static void ArmOnce() => Interlocked.Exchange(ref _armed, 1);

    public static void Disarm() => Interlocked.Exchange(ref _armed, 0);

    /// <summary>
    /// Consomme l'armement et leve le cas echeant. Appele a l'interieur de la
    /// section critique, apres que la mutation a ete preparee : l'ecriture ne
    /// doit alors etre visible de personne.
    /// </summary>
    public static void ThrowIfArmed()
    {
        if (Interlocked.CompareExchange(ref _armed, 0, 1) == 1)
        {
            throw new InvalidOperationException(
                "Ecriture de revision indisponible (panne simulee).");
        }
    }
}
