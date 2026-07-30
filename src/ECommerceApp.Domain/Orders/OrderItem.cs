using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Orders;

/// <summary>
/// One purchased line, snapshotted at order-creation time the same way
/// PurchaseOrderItem snapshots ProductName/ProductSku - so an order's
/// history stays accurate even if the product is later renamed, re-priced,
/// or deactivated. LineTotal is deliberately not a stored column (Quantity *
/// UnitPrice is exact and reproducible, matching PurchaseOrderItem's
/// UnitCost * QuantityOrdered convention of computing rather than storing).
/// </summary>
public class OrderItem : AuditableEntity
{
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public int? ProductVariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? VariantDescription { get; set; }
    public string? ImagePath { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }

    public Order Order { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public ProductVariant? ProductVariant { get; set; }
}
