using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Catalog;

/// <summary>A global, reusable attribute definition (e.g. "Color", "Size"), shared across products.</summary>
public class ProductAttribute : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public ICollection<ProductAttributeValue> Values { get; set; } = new List<ProductAttributeValue>();
}
