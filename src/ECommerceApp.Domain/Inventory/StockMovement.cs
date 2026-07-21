using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Inventory;

/// <summary>
/// An immutable ledger entry for every stock change. Never updated or deleted -
/// corrections are made by recording a new, opposite movement, not by editing
/// history. Deliberately does NOT derive from <see cref="AuditableEntity"/> (see
/// its remarks): no soft delete, no UpdatedAt, no RowVersion, since rows are
/// insert-only and never modified after creation.
/// </summary>
public class StockMovement : BaseEntity
{
    public int InventoryItemId { get; set; }
    public StockMovementType MovementType { get; set; }
    public int QuantityChange { get; set; }
    public int QuantityOnHandAfter { get; set; }
    public int QuantityReservedAfter { get; set; }

    /// <summary>Free-form reference to the record that caused this movement (e.g. "StockAdjustment", "PurchaseOrder", "Order") - most of those don't exist as entities yet, so this is a string, not an FK.</summary>
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public string? Reason { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string? CreatedByUserId { get; set; }

    public InventoryItem InventoryItem { get; set; } = null!;
}
