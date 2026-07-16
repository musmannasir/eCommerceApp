namespace ECommerceApp.Domain.Common;

/// <summary>
/// Implemented by entities that track who created/last-updated them and when.
/// The user id is stored as a string snapshot (matching ASP.NET Core Identity's
/// default key type) rather than a foreign key, so the Domain layer stays free
/// of any dependency on the Identity implementation.
/// </summary>
public interface IAuditableEntity
{
    DateTime CreatedAtUtc { get; set; }
    string? CreatedByUserId { get; set; }
    DateTime? UpdatedAtUtc { get; set; }
    string? UpdatedByUserId { get; set; }
}
