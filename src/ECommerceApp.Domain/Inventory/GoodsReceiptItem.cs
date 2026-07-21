using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Inventory;

/// <summary>Immutable line of a <see cref="GoodsReceipt"/> - same non-AuditableEntity reasoning.</summary>
public class GoodsReceiptItem : BaseEntity
{
    public int GoodsReceiptId { get; set; }
    public int PurchaseOrderItemId { get; set; }
    public int QuantityReceived { get; set; }

    /// <summary>True if this line received more than was outstanding on the purchase order item.</summary>
    public bool IsOverride { get; set; }

    public GoodsReceipt GoodsReceipt { get; set; } = null!;
    public PurchaseOrderItem PurchaseOrderItem { get; set; } = null!;
}
