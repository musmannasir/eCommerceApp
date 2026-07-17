using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Catalog;

/// <summary>A free-form spec row (e.g. "Screen Size" -> "6.1 inch") shown on the product detail page.</summary>
public class ProductSpecification : AuditableEntity
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }

    public Product Product { get; set; } = null!;
}
