using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Inventory;

/// <summary>
/// Product-level, not variant-level, matching <see cref="SupplierProduct"/>'s
/// granularity - a supplier's sourcing terms are recorded per product, not per
/// variant, so ordering follows the same unit. ProductName/ProductSku are
/// snapshotted at add-time so a PO's history stays accurate even if the
/// product is later renamed or re-SKU'd.
/// </summary>
public class PurchaseOrderItem : AuditableEntity
{
    public int PurchaseOrderId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSku { get; set; } = string.Empty;
    public int QuantityOrdered { get; set; }
    public int QuantityReceived { get; set; }
    public decimal UnitCost { get; set; }

    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
