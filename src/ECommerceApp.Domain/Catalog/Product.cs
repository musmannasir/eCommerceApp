using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Catalog;

/// <summary>
/// TaxCategory and ReturnEligibility are plain strings for now - the structured
/// tax-rate model (Milestone 7) and the structured return-window model
/// (Milestone 13) don't exist yet, so this milestone doesn't pre-build them.
/// </summary>
public class Product : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public string? FullDescription { get; set; }
    public int? BrandId { get; set; }
    public int CategoryId { get; set; }
    public string BaseSKU { get; set; } = string.Empty;
    public decimal CostPrice { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public string TaxCategory { get; set; } = "Standard";
    public bool IsTaxable { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; }
    public bool IsPublished { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public decimal? Weight { get; set; }
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public string? WarrantyInformation { get; set; }
    public string? ReturnEligibility { get; set; }
    public int? LowStockThreshold { get; set; }
    public string? SearchKeywords { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }

    public Brand? Brand { get; set; }
    public Category Category { get; set; } = null!;
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    public ICollection<ProductSpecification> Specifications { get; set; } = new List<ProductSpecification>();
    public ICollection<ProductTagMapping> TagMappings { get; set; } = new List<ProductTagMapping>();
}
