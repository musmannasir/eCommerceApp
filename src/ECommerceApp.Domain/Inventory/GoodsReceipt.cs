using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Inventory;

/// <summary>
/// An immutable receiving event against a <see cref="PurchaseOrder"/> - insert-only,
/// like <see cref="StockMovement"/>/<see cref="StockAdjustment"/>, so it deliberately
/// does not derive from AuditableEntity.
/// </summary>
public class GoodsReceipt : BaseEntity
{
    public int PurchaseOrderId { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public string? ReceivedByUserId { get; set; }
    public string? Notes { get; set; }
    public string? OverrideReason { get; set; }

    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public ICollection<GoodsReceiptItem> Items { get; set; } = new List<GoodsReceiptItem>();
}
