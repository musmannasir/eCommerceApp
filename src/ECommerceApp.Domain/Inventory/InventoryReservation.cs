using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Inventory;

/// <summary>
/// Reserves stock against an <see cref="InventoryItem"/> for a cart or order.
/// Those callers arrive in Milestones 6 and 9, so <see cref="ReferenceType"/>/
/// <see cref="ReferenceId"/> are free-form for now. Unlike <see cref="StockMovement"/>,
/// this entity has a real lifecycle (Active -> Released/Consumed/Expired), which
/// is why it uses the mutable <see cref="AuditableEntity"/> base instead.
/// </summary>
public class InventoryReservation : AuditableEntity
{
    public int InventoryItemId { get; set; }
    public int Quantity { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Active;
    public string? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? ReleasedAtUtc { get; set; }

    public InventoryItem InventoryItem { get; set; } = null!;
}
