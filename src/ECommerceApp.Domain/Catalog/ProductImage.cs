using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Catalog;

/// <summary>A product-level image when <see cref="ProductVariantId"/> is null, or a variant-specific image otherwise.</summary>
public class ProductImage : AuditableEntity
{
    public int ProductId { get; set; }
    public int? ProductVariantId { get; set; }
    public string Path { get; set; } = string.Empty;
    public string? AltText { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsPrimary { get; set; }

    public Product Product { get; set; } = null!;
    public ProductVariant? ProductVariant { get; set; }
}
