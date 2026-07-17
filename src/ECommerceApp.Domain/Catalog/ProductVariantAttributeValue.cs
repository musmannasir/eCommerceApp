using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Catalog;

/// <summary>Plain join record - one row per (variant, attribute value) pair making up a variant's combination.</summary>
public class ProductVariantAttributeValue : BaseEntity
{
    public int ProductVariantId { get; set; }
    public int ProductAttributeValueId { get; set; }

    public ProductVariant ProductVariant { get; set; } = null!;
    public ProductAttributeValue ProductAttributeValue { get; set; } = null!;
}
