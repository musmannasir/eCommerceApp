using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Inventory;

/// <summary>
/// Tracks stock for one purchasable unit in one warehouse. Milestone 2 never
/// requires a <see cref="Product"/> to have variants, so the purchasable unit is
/// either a specific <see cref="ProductVariant"/> (when the product has variants)
/// or the Product itself (when it doesn't, identified by <see cref="ProductId"/>
/// with a null <see cref="ProductVariantId"/>) - never both for the same product
/// in the same warehouse. Enforced by two filtered unique indexes configured in
/// the Infrastructure layer's EF Core configuration for this entity.
/// </summary>
public class InventoryItem : AuditableEntity
{
    public int WarehouseId { get; set; }
    public int ProductId { get; set; }
    public int? ProductVariantId { get; set; }

    public int QuantityOnHand { get; set; }
    public int QuantityReserved { get; set; }
    public int ReorderLevel { get; set; }
    public int ReorderQuantity { get; set; }
    public bool AllowBackorder { get; set; }
    public StockStatus StockStatus { get; set; } = StockStatus.OutOfStock;
    public DateTime LastStockUpdateUtc { get; set; }

    /// <summary>Not persisted - ignored explicitly in the Infrastructure layer's EF Core configuration.</summary>
    public int QuantityAvailable => QuantityOnHand - QuantityReserved;

    public Warehouse Warehouse { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public ProductVariant? ProductVariant { get; set; }
}
