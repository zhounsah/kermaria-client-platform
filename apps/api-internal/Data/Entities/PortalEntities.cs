namespace Kermaria.ApiInternal.Data.Entities;

public abstract class UtcEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("D");
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class Customer : UtcEntity
{
    public string ExternalReference { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public string? BillingEmail { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
}

public sealed class PortalUser : UtcEntity
{
    public string CustomerId { get; set; } = string.Empty;
    public string IdentityProviderSubject { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public string Role { get; set; } = "client_user";
    public DateTime? LastLoginAtUtc { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LastFailedLoginAtUtc { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
}

public sealed class PortalSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("D");
    public string UserId { get; set; } = string.Empty;
    public string SessionTokenHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public DateTime? LastSeenAtUtc { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

public sealed class CustomerService : UtcEntity
{
    public string CustomerId { get; set; } = string.Empty;
    public string ExternalReference { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public string Description { get; set; } = string.Empty;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public string Scope { get; set; } = string.Empty;
    public string CommercialTerms { get; set; } = "Selon devis";
    public string? NextStep { get; set; }
}

public sealed class Invoice : UtcEntity
{
    public string CustomerId { get; set; } = string.Empty;
    public string ExternalReference { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public DateTime IssuedAtUtc { get; set; }
    public DateTime? DueAtUtc { get; set; }
    public string Period { get; set; } = string.Empty;
    public string Currency { get; set; } = "EUR";
    public decimal SubtotalAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? DocumentReference { get; set; }
}

public sealed class SupportRequest : UtcEntity
{
    public string CustomerId { get; set; } = string.Empty;
    public string? CreatedByUserId { get; set; }
    public string? ServiceId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = "normal";
    public string Category { get; set; } = "support";
    public string Status { get; set; } = "open";
    public DateTime? ClosedAtUtc { get; set; }
}

public sealed class ServiceRequest : UtcEntity
{
    public string CustomerId { get; set; } = string.Empty;
    public string? CreatedByUserId { get; set; }
    public string CatalogItemId { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string Timeline { get; set; } = "exploration";
    public string Context { get; set; } = string.Empty;
    public string Status { get; set; } = "received";
}

public sealed class AuditLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString("D");
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string CorrelationId { get; set; } = string.Empty;
    public string? ActorUserId { get; set; }
    public string ActorService { get; set; } = "API-INTERNAL";
    public string? CustomerId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? TargetType { get; set; }
    public string? TargetReference { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public string? ReasonCode { get; set; }
    public string? SourceAddress { get; set; }
    public string? MetadataJson { get; set; }
}

public sealed class AdAction
{
    public string Id { get; set; } = Guid.NewGuid().ToString("D");
    public string? CustomerId { get; set; }
    public string? RequestedByUserId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string TargetReference { get; set; } = string.Empty;
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string Status { get; set; } = "requested";
    public string? ResultCode { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}

public sealed class ServiceCatalogEntry : UtcEntity
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string CommercialTerms { get; set; } = "Selon devis";
    public bool IsActive { get; set; } = true;
}
