using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Inventory;

/// <summary>
/// Draft -&gt; Submitted -&gt; Approved -&gt; PartiallyReceived/Received, or Cancelled
/// (from Draft/Submitted/Approved only - once any goods have been received, the
/// order can no longer be cancelled outright).
/// </summary>
public class PurchaseOrder : AuditableEntity
{
    public int SupplierId { get; set; }
    public int WarehouseId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string? Notes { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? ApprovedByUserId { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancelledByUserId { get; set; }

    public Supplier Supplier { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
}
