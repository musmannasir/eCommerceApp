using System.ComponentModel.DataAnnotations;
using ECommerceApp.Application.Catalog.Models;

namespace ECommerceApp.Web.Areas.Admin.Models;

public class PromotionFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [StringLength(50), Display(Name = "Coupon code")]
    public string? CouponCode { get; set; }

    [Required, Display(Name = "Discount type")]
    public string DiscountType { get; set; } = "Percentage";

    [Display(Name = "Discount value")]
    public decimal DiscountValue { get; set; }

    [Required, Display(Name = "Applies to")]
    public string ScopeType { get; set; } = "EntireOrder";

    [Display(Name = "Category")]
    public int? ScopeCategoryId { get; set; }

    [Display(Name = "Brand")]
    public int? ScopeBrandId { get; set; }

    [Display(Name = "Product")]
    public int? ScopeProductId { get; set; }

    [Display(Name = "Minimum order amount")]
    public decimal? MinimumOrderAmount { get; set; }

    [Display(Name = "Maximum discount amount")]
    public decimal? MaxDiscountAmount { get; set; }

    [Required, Display(Name = "Starts")]
    [DataType(DataType.DateTime)]
    public DateTime StartsAtUtc { get; set; } = DateTime.UtcNow;

    [Display(Name = "Ends")]
    [DataType(DataType.DateTime)]
    public DateTime? EndsAtUtc { get; set; }

    [Display(Name = "Max total uses")]
    public int? MaxTotalUses { get; set; }

    [Display(Name = "Max uses per customer")]
    public int? MaxUsesPerCustomer { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public IEnumerable<CategoryDto> AvailableCategories { get; set; } = [];
    public IEnumerable<BrandDto> AvailableBrands { get; set; } = [];
    public IEnumerable<ProductPickerItemDto> AvailableProducts { get; set; } = [];
}
