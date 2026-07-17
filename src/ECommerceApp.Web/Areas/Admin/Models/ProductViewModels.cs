using System.ComponentModel.DataAnnotations;
using ECommerceApp.Application.Catalog.Models;

namespace ECommerceApp.Web.Areas.Admin.Models;

public class ProductFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(300)]
    public string Name { get; set; } = string.Empty;

    [StringLength(350)]
    public string? Slug { get; set; }

    [StringLength(500)]
    public string? ShortDescription { get; set; }

    public string? FullDescription { get; set; }

    [Display(Name = "Brand")]
    public int? BrandId { get; set; }

    [Required, Display(Name = "Category")]
    public int CategoryId { get; set; }

    [Required, StringLength(100), Display(Name = "Base SKU")]
    public string BaseSKU { get; set; } = string.Empty;

    [Range(0, 1000000), Display(Name = "Cost price")]
    public decimal CostPrice { get; set; }

    [Range(0.01, 1000000), Display(Name = "Selling price")]
    public decimal SellingPrice { get; set; }

    [Range(0, 1000000), Display(Name = "Compare-at price")]
    public decimal? CompareAtPrice { get; set; }

    [Required, StringLength(50), Display(Name = "Tax category")]
    public string TaxCategory { get; set; } = "Standard";

    [Display(Name = "Taxable")]
    public bool IsTaxable { get; set; } = true;

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Featured")]
    public bool IsFeatured { get; set; }

    public decimal? Weight { get; set; }
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }

    [StringLength(1000), Display(Name = "Warranty information")]
    public string? WarrantyInformation { get; set; }

    [StringLength(500), Display(Name = "Return eligibility")]
    public string? ReturnEligibility { get; set; }

    [Range(0, int.MaxValue), Display(Name = "Low stock threshold")]
    public int? LowStockThreshold { get; set; }

    [StringLength(500), Display(Name = "Search keywords")]
    public string? SearchKeywords { get; set; }

    [StringLength(200), Display(Name = "Meta title")]
    public string? MetaTitle { get; set; }

    [StringLength(500), Display(Name = "Meta description")]
    public string? MetaDescription { get; set; }

    public IEnumerable<CategoryDto> AvailableCategories { get; set; } = [];
    public IEnumerable<BrandDto> AvailableBrands { get; set; } = [];
}

public class ProductEditViewModel
{
    public ProductFormViewModel Form { get; set; } = new();
    public ProductDto Product { get; set; } = null!;
    public IReadOnlyList<ProductAttributeDto> Attributes { get; set; } = [];
}

public class AddVariantViewModel
{
    [Required]
    public int ProductId { get; set; }

    [Required, StringLength(100)]
    public string SKU { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Barcode { get; set; }

    public decimal? CostPrice { get; set; }
    public decimal? SellingPrice { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public decimal? Weight { get; set; }
    public bool IsActive { get; set; } = true;

    public List<int> AttributeValueIds { get; set; } = [];
}

public class AddSpecificationViewModel
{
    [Required]
    public int ProductId { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(1000)]
    public string Value { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }
}

public class AddTagViewModel
{
    [Required]
    public int ProductId { get; set; }

    [Required, StringLength(100)]
    public string TagName { get; set; } = string.Empty;
}
