using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Catalog;

/// <summary>
/// A purchasable attribute combination of a product (e.g. Color=Red, Size=Large).
/// Price/weight fields override the parent <see cref="Product"/>'s values when set.
/// </summary>
public class ProductVariant : AuditableEntity
{
    public int ProductId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public decimal? CostPrice { get; set; }
    public decimal? SellingPrice { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public decimal? Weight { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Deterministic key from the sorted attribute-value IDs, unique per product - prevents duplicate combinations.</summary>
    public string CombinationKey { get; set; } = string.Empty;

    public Product Product { get; set; } = null!;
    public ICollection<ProductVariantAttributeValue> AttributeValues { get; set; } = new List<ProductVariantAttributeValue>();

    public static string BuildCombinationKey(IEnumerable<int> attributeValueIds) =>
        string.Join(",", attributeValueIds.OrderBy(id => id));
}
