using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Catalog;

/// <summary>Plain join record - unlinking a tag from a product removes the row outright, no soft delete.</summary>
public class ProductTagMapping : BaseEntity
{
    public int ProductId { get; set; }
    public int ProductTagId { get; set; }

    public Product Product { get; set; } = null!;
    public ProductTag ProductTag { get; set; } = null!;
}
