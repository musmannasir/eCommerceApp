using System.ComponentModel.DataAnnotations;

namespace ECommerceApp.Web.Areas.Admin.Models;

public class StoreSettingsViewModel
{
    [Required, StringLength(200), Display(Name = "Store name")]
    public string StoreName { get; set; } = string.Empty;

    [Required, StringLength(10)]
    public string Currency { get; set; } = string.Empty;

    [Required, StringLength(100), Display(Name = "Default country")]
    public string DefaultCountry { get; set; } = string.Empty;

    [Display(Name = "Prices include tax")]
    public bool PricesIncludeTax { get; set; }

    [Range(1, 100), Display(Name = "Recently viewed - max items")]
    public int RecentlyViewedMaxItems { get; set; }

    [Required, StringLength(2), Display(Name = "Default tax country code")]
    public string DefaultTaxCountryCode { get; set; } = string.Empty;

    [StringLength(10), Display(Name = "Default tax region code")]
    public string? DefaultTaxRegionCode { get; set; }

    [Required, StringLength(2), Display(Name = "Default shipping country code")]
    public string DefaultShippingCountryCode { get; set; } = string.Empty;

    [StringLength(10), Display(Name = "Default shipping region code")]
    public string? DefaultShippingRegionCode { get; set; }

    /// <summary>Base64-encoded concurrency token round-tripped via a hidden form field.</summary>
    public string RowVersion { get; set; } = string.Empty;
}
