namespace Kermaria.ApiInternal.Contracts;

/// <summary>
/// Fiche commerciale creee par un administrateur. Cette intention ne cree ni
/// identite portail, ni mot de passe, ni invitation : l'acces client reste un
/// workflow distinct et explicite.
/// </summary>
public sealed record AdminCustomerCreatePayload(
    string? CustomerType,
    string? DisplayName,
    string? BillingEmail,
    string? Phone,
    string? AddressLine1,
    string? AddressLine2,
    string? PostalCode,
    string? City,
    string? Country);

public sealed record AdminCustomerCreateResponse(
    string CustomerReference,
    string DisplayName,
    string BillingEmail,
    string Status);
