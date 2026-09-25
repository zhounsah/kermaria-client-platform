using System.Text.RegularExpressions;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Codes de l'amorcage de l'identite AD du compte principal.
/// </summary>
public static class PrimaryIdentityBootstrapCodes
{
    public const string Completed = "PRIMARY_IDENTITY_COMPLETED";
    public const string LifecycleMissing = "PRIMARY_IDENTITY_LIFECYCLE_MISSING";
    public const string AwaitingPassword = "PRIMARY_IDENTITY_AWAITING_PASSWORD";
    public const string Failed = "PRIMARY_IDENTITY_FAILED";
    public const string EmailVerificationRequired = "EMAIL_VERIFICATION_REQUIRED";
    public const string NotEligible = "PRIMARY_IDENTITY_NOT_ELIGIBLE";
    public const string SecretMissing = "KOXO_SECRET_MISSING";
    public const string DirectoryNotReady = "AD_IDENTITY_NOT_READY";
    public const string DirectoryDisabled = "AD_WRITES_DISABLED";
    public const string ConfigurationInvalid = "AD_CONFIGURATION_INVALID";
    public const string Conflict = "AD_IDENTITY_CONFLICT";
    public const string CustomerMismatch = "AD_IDENTITY_CUSTOMER_MISMATCH";
    public const string InvalidDirectoryObject = "AD_IDENTITY_INVALID_OBJECT";
    public const string LinkNotConfirmed = "AD_LINK_NOT_CONFIRMED";
    public const string DirectoryCreationFailed = "AD_PROVISIONING_FAILED";
}

/// <summary>
/// Regles pures de l'amorcage de l'identite du compte principal.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="GetExportBlocker"/> est la transcription C# de la branche
/// « compte principal » de <see cref="KoxoExportCandidateQuery"/>. Elle n'est
/// pas decorative : le service la reapplique avant toute action annuaire, et
/// le depot mock s'en sert pour designer ses candidats. Chaque condition a son
/// pendant SQL, verrouille par les tests de forme de la requete.
/// </para>
/// <para>
/// L'ordre va du plus structurel au plus circonstanciel, pour qu'un diagnostic
/// designe la vraie cause plutot que la derniere constatee.
/// </para>
/// </remarks>
public static class PrimaryIdentityBootstrapPolicy
{
    // Domaine (trois sous-autorites) puis RID : forme d'un SID d'utilisateur de
    // domaine. Tout autre SID designe un objet que KoXo ne cree pas.
    private static readonly Regex DomainUserSidPattern =
        new("^S-1-5-21(-\\d{1,10}){4}$", RegexOptions.Compiled);

    private const int MaxSamAccountNameLength = 20;

    /// <summary>
    /// Raison pour laquelle ce compte n'est PAS exportable vers KoXo, ou
    /// <c>null</c> s'il l'est.
    /// </summary>
    public static string? GetExportBlocker(PrimaryIdentityBootstrapRecord record)
    {
        if (!PrimaryIdentityBootstrapStatuses.IsBootstrapping(record.Status))
        {
            return record.Status switch
            {
                PrimaryIdentityBootstrapStatuses.AwaitingPassword =>
                    PrimaryIdentityBootstrapCodes.AwaitingPassword,
                PrimaryIdentityBootstrapStatuses.Completed =>
                    PrimaryIdentityBootstrapCodes.Completed,
                _ => PrimaryIdentityBootstrapCodes.Failed
            };
        }

        return GetIdentityBlocker(record)
            ?? (record.HasUserLink
                // Le lien existe : c'est la branche ordinaire de l'export qui
                // porte desormais ce compte, sans exception ni secret.
                ? PrimaryIdentityBootstrapCodes.Completed
                : null)
            ?? (record.SecretAvailable
                ? null
                : PrimaryIdentityBootstrapCodes.SecretMissing);
    }

    /// <summary>
    /// Conditions communes a l'export et a toute action annuaire (adoption ou
    /// creation) : sans elles, aucune identite ne doit naitre ni etre liee.
    /// </summary>
    public static string? GetIdentityBlocker(PrimaryIdentityBootstrapRecord record)
    {
        if (!record.CustomerActive || !record.PortalUserActive)
        {
            return PrimaryIdentityBootstrapCodes.NotEligible;
        }

        // Une demonstration (essai ou vitrine) a son propre pipeline ; un
        // utilisateur additionnel a son propre cycle. Ni l'un ni l'autre ne
        // passe par l'amorcage du compte principal.
        if (record.IsDemo || record.HasAdditionalUserLifecycle)
        {
            return PrimaryIdentityBootstrapCodes.NotEligible;
        }

        if (!KoxoDirectoryTopology.IsValidUniqueIdentifier(record.KoxoUniqueIdentifier)
            || !string.Equals(
                record.KoxoUniqueIdentifier,
                record.PortalUserKoxoUniqueIdentifier,
                StringComparison.Ordinal))
        {
            return PrimaryIdentityBootstrapCodes.NotEligible;
        }

        if (!record.IdentityComplete)
        {
            return PrimaryIdentityBootstrapCodes.NotEligible;
        }

        if (record.EmailVerificationRequired && !record.EmailVerified)
        {
            return PrimaryIdentityBootstrapCodes.EmailVerificationRequired;
        }

        return null;
    }

    /// <summary>
    /// OU cible KoXo de ce client, celle ou son identite doit se trouver.
    /// </summary>
    public static string ResolveExpectedSecondaryGroup(
        PrimaryIdentityBootstrapRecord record)
        => KoxoDirectoryTopology.ResolveSecondaryGroup(
            isDemo: false,
            record.KoxoGroupReference,
            record.CustomerReference);

    /// <summary>
    /// Verifie strictement l'objet AD retrouve avant toute adoption.
    /// </summary>
    /// <returns>
    /// <c>null</c> si l'objet est adoptable, sinon le code d'echec. Aucun
    /// rapprochement approchant : un objet qui ne satisfait pas une condition
    /// n'est pas « presque le bon », il n'est pas le bon.
    /// </returns>
    public static string? ValidateDirectoryObject(
        AdDirectoryObjectSummary directoryObject,
        PrimaryIdentityBootstrapRecord record)
    {
        if (!string.Equals(directoryObject.ObjectType, "user", StringComparison.OrdinalIgnoreCase)
            || !Guid.TryParse(directoryObject.ObjectGuid, out var objectGuid)
            || objectGuid == Guid.Empty
            || string.IsNullOrWhiteSpace(directoryObject.ObjectSid)
            || !DomainUserSidPattern.IsMatch(directoryObject.ObjectSid)
            || string.IsNullOrWhiteSpace(directoryObject.SamAccountName)
            || directoryObject.SamAccountName.Length > MaxSamAccountNameLength
            || directoryObject.SamAccountName.Any(char.IsWhiteSpace)
            || string.IsNullOrWhiteSpace(directoryObject.DistinguishedName))
        {
            return PrimaryIdentityBootstrapCodes.InvalidDirectoryObject;
        }

        // L'objet porte-t-il la reference d'un AUTRE client ? Le resolveur
        // LDAP laisse ce champ vide ; le mode mock le renseigne.
        if (!string.IsNullOrWhiteSpace(directoryObject.CustomerReference)
            && !string.Equals(
                directoryObject.CustomerReference,
                record.CustomerReference,
                StringComparison.OrdinalIgnoreCase))
        {
            return PrimaryIdentityBootstrapCodes.CustomerMismatch;
        }

        // KoXo range l'identite dans l'OU nommee par GroupeSecondaire. Un
        // objet ailleurs porte peut-etre le bon employeeNumber, mais il n'est
        // pas dans le perimetre de ce client : l'adopter lui donnerait les
        // droits d'un autre.
        var expectedOu = ResolveExpectedSecondaryGroup(record);
        if (!DistinguishedNameContainsOu(
                directoryObject.DistinguishedName,
                expectedOu))
        {
            return PrimaryIdentityBootstrapCodes.CustomerMismatch;
        }

        return null;
    }

    /// <summary>Comparaison exacte d'une composante <c>OU=</c>, jamais par sous-chaine.</summary>
    public static bool DistinguishedNameContainsOu(
        string distinguishedName,
        string organizationalUnit)
    {
        var expected = Services.ActiveDirectory.ActiveDirectoryPathScope
            .EscapeRdnValue(organizationalUnit);
        foreach (var component in SplitDistinguishedName(distinguishedName))
        {
            var separator = component.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = component[..separator].Trim();
            var value = component[(separator + 1)..].Trim();
            if (key.Equals("OU", StringComparison.OrdinalIgnoreCase)
                && value.Equals(expected, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> SplitDistinguishedName(string value)
    {
        var buffer = new System.Text.StringBuilder();
        var escaped = false;
        foreach (var character in value)
        {
            if (escaped)
            {
                buffer.Append(character);
                escaped = false;
                continue;
            }

            if (character == '\\')
            {
                buffer.Append(character);
                escaped = true;
                continue;
            }

            if (character == ',')
            {
                yield return buffer.ToString();
                buffer.Clear();
                continue;
            }

            buffer.Append(character);
        }

        if (buffer.Length > 0)
        {
            yield return buffer.ToString();
        }
    }
}
