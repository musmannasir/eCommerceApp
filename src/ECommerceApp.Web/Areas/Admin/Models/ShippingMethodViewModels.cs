using System.ComponentModel.DataAnnotations;

namespace ECommerceApp.Web.Areas.Admin.Models;

public class ShippingMethodFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required, StringLength(2, MinimumLength = 2), Display(Name = "Country code")]
    public string CountryCode { get; set; } = string.Empty;

    [StringLength(10), Display(Name = "Region code (optional)")]
    public string? RegionCode { get; set; }

    [Display(Name = "Base rate")]
    public decimal BaseRate { get; set; }

    [Display(Name = "Rate per kg")]
    public decimal RatePerKg { get; set; }

    [Display(Name = "Free shipping threshold (optional)")]
    public decimal? FreeShippingThreshold { get; set; }

    [Display(Name = "Min delivery days (optional)")]
    public int? EstimatedDeliveryDaysMin { get; set; }

    [Display(Name = "Max delivery days (optional)")]
    public int? EstimatedDeliveryDaysMax { get; set; }

    [Display(Name = "Display order")]
    public int DisplayOrder { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}
