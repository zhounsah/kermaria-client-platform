using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.Data.Repositories;

public static class DemoAccountRepositoryExtensions
{
    private const int Attempts = 5;

    /// <summary>
    /// Tire un code de groupe <c>CLI-XXXXXX</c> libre.
    /// </summary>
    /// <remarks>
    /// L'espace de tirage (31^6, soit ~887 millions) rend la collision
    /// improbable, mais on verifie tout de meme : sinon elle remonterait en
    /// violation d'index unique au moment de l'insertion, donc en erreur opaque
    /// cote admin. Renvoie <c>null</c> si aucun code libre n'a ete trouve, ce qui
    /// signale une anomalie (index sature ou base incoherente) plutot qu'une
    /// collision ordinaire.
    /// </remarks>
    public static async Task<string?> TryReserveGroupReferenceAsync(
        this IDemoAccountRepository accounts,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var candidate = CustomerReferenceGenerator.Generate();
            if (!await accounts.CustomerReferenceTakenAsync(
                    candidate,
                    cancellationToken))
            {
                return candidate;
            }
        }

        return null;
    }
}
