using System.ComponentModel.DataAnnotations;

namespace ECommerceApp.Web.Areas.Admin.Models;

public class TaxRateFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(2, MinimumLength = 2), Display(Name = "Country code")]
    public string CountryCode { get; set; } = string.Empty;

    [StringLength(10), Display(Name = "Region code (optional)")]
    public string? RegionCode { get; set; }

    [Required, StringLength(50), Display(Name = "Tax category")]
    public string TaxCategory { get; set; } = "Standard";

    [Display(Name = "Rate (%)")]
    public decimal RatePercent { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}
