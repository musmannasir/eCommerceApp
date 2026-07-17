using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Catalog;

/// <summary>A concrete value for a <see cref="ProductAttribute"/> (e.g. "Red" for "Color").</summary>
public class ProductAttributeValue : AuditableEntity
{
    public int ProductAttributeId { get; set; }
    public string Value { get; set; } = string.Empty;

    public ProductAttribute ProductAttribute { get; set; } = null!;
}
