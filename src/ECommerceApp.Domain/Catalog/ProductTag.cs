using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Catalog;

public class ProductTag : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;

    public ICollection<ProductTagMapping> ProductMappings { get; set; } = new List<ProductTagMapping>();
}
