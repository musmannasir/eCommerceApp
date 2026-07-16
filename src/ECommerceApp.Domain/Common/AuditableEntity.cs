namespace ECommerceApp.Domain.Common;

/// <summary>
/// Standard base for recoverable, auditable, concurrency-checked entities
/// (catalog, inventory, order, and similar records). Immutable financial
/// transaction records should NOT derive from this type — see
/// <see cref="ISoftDeletable"/>.
/// </summary>
public abstract class AuditableEntity : BaseEntity, IAuditableEntity, ISoftDeletable, IHasRowVersion
{
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedByUserId { get; set; }
    public bool IsDeleted { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
