using System.ComponentModel.DataAnnotations;

namespace ECommerceApp.Web.Models.Addresses;

public class AddressFormViewModel
{
    public int Id { get; set; }

    [StringLength(50), Display(Name = "Label (optional)")]
    public string? Label { get; set; }

    [Required, StringLength(200), Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, StringLength(30), Display(Name = "Phone")]
    public string Phone { get; set; } = string.Empty;

    [Required, StringLength(200), Display(Name = "Address line 1")]
    public string Line1 { get; set; } = string.Empty;

    [StringLength(200), Display(Name = "Address line 2 (optional)")]
    public string? Line2 { get; set; }

    [Required, StringLength(100), Display(Name = "City")]
    public string City { get; set; } = string.Empty;

    [StringLength(10), Display(Name = "Region/state code (optional)")]
    public string? RegionCode { get; set; }

    [Required, StringLength(20), Display(Name = "Postal code")]
    public string PostalCode { get; set; } = string.Empty;

    [Required, StringLength(2, MinimumLength = 2), Display(Name = "Country code")]
    public string CountryCode { get; set; } = string.Empty;

    [Display(Name = "Set as default")]
    public bool IsDefault { get; set; }

    /// <summary>
    /// Where to send the customer after a successful save - e.g. back into
    /// Checkout (Milestone 8.2) when they were sent here for having no saved
    /// addresses yet. Local-only, same open-redirect protection as
    /// AccountController's ReturnUrl.
    /// </summary>
    public string? ReturnUrl { get; set; }
}
