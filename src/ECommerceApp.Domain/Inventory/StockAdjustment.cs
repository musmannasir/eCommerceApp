using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Inventory;

/// <summary>
/// A detailed, immutable record of a manual stock adjustment (who/why), in
/// addition to the generic <see cref="StockMovement"/> ledger row it produces.
/// "With approval where configured" (per the Milestone 3 brief) has no
/// configuration source yet - Store Configuration lands in Milestone 16 - so
/// adjustments apply immediately for any CanManageInventory-authorized user.
/// Adding an approval-status gate later needs no breaking schema change.
/// </summary>
public class StockAdjustment : BaseEntity
{
    public int InventoryItemId { get; set; }
    public int QuantityDelta { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int QuantityOnHandAfter { get; set; }
    public DateTime AdjustedAtUtc { get; set; }
    public string? AdjustedByUserId { get; set; }

    public InventoryItem InventoryItem { get; set; } = null!;
}
